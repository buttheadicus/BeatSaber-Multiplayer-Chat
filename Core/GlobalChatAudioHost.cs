using System;
using System.Collections;
using System.Collections.Generic;
using MultiplayerChat;
using MultiplayerChat.Core.Addons;
using MultiplayerChat.Settings;
using MultiplayerChat.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiplayerChat.Core;

public sealed class GlobalChatAudioHost : MonoBehaviour
{
    public static GlobalChatAudioHost? Instance { get; private set; }

    public static bool SongMicCoercionAllowsPttBypass()
    {
        var inst = Instance;
        if (inst == null) return false;
        if (!ModSettings.MuteMicDuringSongPlaying || !inst._songMicLatch) return false;
        return !inst._songMicRestore;
    }

    private const float VoiceActivityHoldoverSec = 0.22f;
    private const float SilenceBeforeUnduckSec = 0.85f;
    private const float DuckSmoothTimeSec = 0.12f;
    private const float SourceRefreshIntervalSec = 2f;

    private float _smoothDuckMul = 1f;
    private float _duckSmoothVel;
    private float _silenceAfterVoiceSec;
    private bool _duckLatched;
    private float _lastVoipReloadRealtime = -999f;
    private const float VoipReloadDebounceSec = 0.45f;
    private bool _lobbyVoipReloadArmed = true;
    private float _nextLobbyHierarchyPollTime;
    private float _lastIncomingVoiceRealtime = -999f;
    private float _incomingVoiceAudiblePollExpiry = -999f;
    private bool _incomingVoiceAudibleCached;
    private const float IncomingVoiceAudiblePollIntervalSec = 0.045f;
    private float _nextSourceRefreshTime;
    private bool _duckVolumesApplied;
    private float _baselineListenerVolume = 1f;

    private struct DuckSourceEntry
    {
        public AudioSource Source;
        public float BaselineVolume;
    }

    private readonly Dictionary<int, DuckSourceEntry> _duckGameSourceBaselines = new(256);

    private readonly HashSet<int> _duckSeenSourceIdsScratch = new(256);

    private readonly List<int> _baselineDeadSweep = new(32);

    private bool _duckHasCachedAudioListener = true;

    private float _nextCachedAudioListenerCheckTime;

    private bool _songMicLatch;
    private bool _songMicRestore;
    private bool _songDeafLatch;
    private bool _songDeafRestore;

    private float _songPolicyNextGameplaySampleRealtime = -999f;
    private bool _songPolicyCachedGameplayLikely;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (Instance == this)
            Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        MpChatLobbyDiagnostics.InvalidateSceneHeuristicCaches();
        MpChatLobbyDiagnostics.LogVoipTransition($"OnSceneLoaded:{scene.name}", $"mode={mode}");
        if (string.Equals(scene.name, "GameCore", System.StringComparison.Ordinal))
        {
            StartCoroutine(ReloadVoipDeferred("[MPChat] VoIP reloaded (GameCore loaded  -  arena / song)"));
            StartCoroutine(DeferredSongVoicePolicySync("GameCore loaded"));
            return;
        }

        // Lobby often reuses Menu / other scene names; detect the same objects presence uses.
        StartCoroutine(ReloadVoipIfMultiplayerLobbyDeferred($"[MPChat] VoIP reloaded (scene loaded: {scene.name})"));
    }

    private void OnSceneUnloaded(Scene scene)
    {
        MpChatLobbyDiagnostics.LogVoipTransition($"OnSceneUnloaded:{scene.name}", null);
        if (!string.Equals(scene.name, "GameCore", System.StringComparison.Ordinal))
            return;
        StartCoroutine(ReloadVoipDeferred("[MPChat] VoIP reloaded (GameCore unloaded)"));
        StartCoroutine(DeferredSongVoicePolicySync("GameCore unloaded"));
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        MpChatLobbyDiagnostics.InvalidateSceneHeuristicCaches();
        MpChatLobbyDiagnostics.LogVoipTransition("OnActiveSceneChanged",
            $"old={oldScene.name} new={newScene.name}");
        if (string.Equals(oldScene.name, "GameCore", System.StringComparison.Ordinal))
        {
            StartCoroutine(ReloadVoipDeferred("[MPChat] VoIP reloaded (left GameCore  -  lobby / menu)"));
            StartCoroutine(DeferredSongVoicePolicySync("active scene left GameCore"));
        }

        if (!string.Equals(newScene.name, "GameCore", System.StringComparison.Ordinal))
            StartCoroutine(ReloadVoipIfMultiplayerLobbyDeferred("[MPChat] VoIP reloaded (active scene  -  multiplayer lobby)"));
    }

    private static bool HierarchyLooksLikeMultiplayerLobby() =>
        MpChatLobbyDiagnostics.MultiplayerLobbyReturnContextActive();

    private IEnumerator ReloadVoipIfMultiplayerLobbyDeferred(string logLine)
    {
        yield return null;
        yield return new WaitForSecondsRealtime(0.35f);
        if (!HierarchyLooksLikeMultiplayerLobby())
            yield break;
        if (string.Equals(SceneManager.GetActiveScene().name, "GameCore", System.StringComparison.Ordinal))
            yield break;
        TryRunVoipReload(logLine, bypassDebounce: false);
        // Debounced reload skips work inside TryRunVoipReload; still sync mute/deaf-during-song for lobby return.
        ApplySongVoicePolicy();
    }

    private IEnumerator ReloadVoipDeferred(string logLine)
    {
        yield return null;
        yield return null;
        yield return new WaitForSecondsRealtime(0.25f);
        TryRunVoipReload(logLine, bypassDebounce: false);
        // Debounced skip leaves ApplySongVoicePolicy uncalled inside TryRunVoipReload; always re-evaluate after arena/lobby transition wait.
        ApplySongVoicePolicy();
    }

    private IEnumerator DeferredSongVoicePolicySync(string reason)
    {
        yield return null;
        ApplySongVoicePolicy();
        if (MpChatVerboseDebug.IsOn)
            Plugin.Log?.Info(
                $"[MPChat][SongVoice] deferred sync ({reason}): songGameplay={MpChatLobbyDiagnostics.SongGameplayLikelyActive()} anyGameCore={MpChatLobbyDiagnostics.AnyGameCoreLoaded()} micLatch={_songMicLatch} deafLatch={_songDeafLatch}");
    }

    public static void ForceResetVoipFromUi(string logLine)
    {
        if (Instance != null)
            Instance.TryRunVoipReload(logLine, bypassDebounce: true);
    }

    private void TryRunVoipReload(string logLine, bool bypassDebounce = false)
    {
        if (!bypassDebounce && Time.realtimeSinceStartup - _lastVoipReloadRealtime < VoipReloadDebounceSec)
            return;

        _lastVoipReloadRealtime = Time.realtimeSinceStartup;
        var cm = ChatManager.Instance;
        var vm = VoiceHotMicManager.Instance;
        if (MpChatVerboseDebug.IsOn)
            Plugin.Log?.Info(
                $"[MPChat][VoIP] TryRunVoipReload begin: {logLine} activeScene={SceneManager.GetActiveScene().name} chatMgrNull={cm == null} voiceHotMicMgrNull={vm == null}");
        ReleaseDuckingIfNeeded();
        MpChatLobbyDiagnostics.LogVoipTransition("TryRunVoipReload:before", logLine);
        cm?.LogVoipReloadContext("TryRunVoipReload (before pipeline)");
        cm?.ForceFullVoipReset();
        vm?.ForceReloadMicrophone();
        VoiceHotMicManager.OnVoipPipelineReloaded();
        AddonCustomAvatarsBridge.OnVoipPipelineReloaded();
        ChatBubbleManager.Instance?.RebindToActiveChatManager();
        cm?.LogVoipReloadContext("TryRunVoipReload (after pipeline)");
        MpChatLobbyDiagnostics.LogVoipTransition("TryRunVoipReload:after", logLine);
        MpChatLobbyDiagnostics.LogFullUiSnapshotThrottled(logLine);
        if (MpChatVerboseDebug.IsOn)
            Plugin.Log?.Info($"[MPChat][VoIP] TryRunVoipReload end: {logLine}");

        // Arena entry: GameCore is loaded and this reload just ran  -  apply mute/deaf-during-song in lockstep with the new ChatManager / mic pipeline.
        ApplySongVoicePolicy();
    }

    private void ApplySongVoicePolicy()
    {
        // SongGameplayLikelyActive can call FindObjectOfType repeatedly  -  throttle; song-edge timing error is negligible.
        var nowRlPolicy = Time.realtimeSinceStartup;
        if (nowRlPolicy >= _songPolicyNextGameplaySampleRealtime)
        {
            // In menu / lobby, SongGameplayLikelyActive may fall back to FindObjectOfType  -  sample less often than in GameCore.
            var interval = MpChatLobbyDiagnostics.AnyGameCoreLoaded() ? 0.08f : 0.28f;
            _songPolicyNextGameplaySampleRealtime = nowRlPolicy + interval;
            _songPolicyCachedGameplayLikely = MpChatLobbyDiagnostics.SongGameplayLikelyActive();
        }

        var inSong = _songPolicyCachedGameplayLikely;

        if (ModSettings.MuteMicDuringSongPlaying)
        {
            if (inSong && !_songMicLatch)
            {
                _songMicRestore = VoiceChatRuntimeState.IsHotMicMuted;
                if (!VoiceChatRuntimeState.IsHotMicMuted)
                    VoiceChatRuntimeState.SetHotMicMuted(true);
                _songMicLatch = true;
                Plugin.Log?.Info($"[MPChat][SongVoice] Auto mic mute ON (inSong={inSong})");
            }
            else if (!inSong && _songMicLatch)
            {
                VoiceChatRuntimeState.SetHotMicMuted(_songMicRestore);
                _songMicLatch = false;
                Plugin.Log?.Info("[MPChat][SongVoice] Auto mic mute OFF (restored)");
            }
        }
        else if (_songMicLatch)
        {
            VoiceChatRuntimeState.SetHotMicMuted(_songMicRestore);
            _songMicLatch = false;
        }

        if (ModSettings.DeafDuringSongPlaying)
        {
            if (inSong && !_songDeafLatch)
            {
                _songDeafRestore = VoiceChatRuntimeState.IsDeaf;
                if (!VoiceChatRuntimeState.IsDeaf)
                    VoiceChatRuntimeState.SetDeaf(true);
                _songDeafLatch = true;
                Plugin.Log?.Info($"[MPChat][SongVoice] Auto deaf ON (inSong={inSong})");
            }
            else if (!inSong && _songDeafLatch)
            {
                if (!_songDeafRestore)
                    VoiceChatRuntimeState.SetDeaf(false);
                _songDeafLatch = false;
                Plugin.Log?.Info("[MPChat][SongVoice] Auto deaf OFF (restored)");
            }
        }
        else if (_songDeafLatch)
        {
            if (!_songDeafRestore)
                VoiceChatRuntimeState.SetDeaf(false);
            _songDeafLatch = false;
        }
    }

    public static void NotifyIncomingVoiceActivity()
    {
        if (Instance != null)
        {
            Instance._lastIncomingVoiceRealtime = Time.realtimeSinceStartup;
            Instance._incomingVoiceAudiblePollExpiry = -999f;
        }
    }

    private void LateUpdate()
    {
        ApplySongVoicePolicy();
        VoiceHotMicManager.EnsureActiveLobbyHostAfterArena();
        AddonCustomAvatarsBridge.EnsureActiveLobbyHostAfterArena();
        PollLobbyHierarchyForVoipReload();

        if (!ModSettings.VoiceDuckingEnabled)
        {
            ReleaseDuckingIfNeeded();
            return;
        }

        // FindObjectOfType<AudioListener> every LateUpdate wasted ms; assume present and re-check periodically.
        var nowRl = Time.realtimeSinceStartup;
        if (nowRl >= _nextCachedAudioListenerCheckTime)
        {
            _nextCachedAudioListenerCheckTime = nowRl + 0.75f;
            _duckHasCachedAudioListener = UnityEngine.Object.FindObjectOfType<AudioListener>() != null;
        }

        if (!_duckHasCachedAudioListener)
            return;

        var dt = Time.unscaledDeltaTime;
        var audible = IsIncomingVoiceAudibleNow();

        // Hot mic / voice messages arrive in bursts with gaps; a multi-second “continuous audible” gate
        // prevented ducking from ever engaging. Latch on any audible activity; release after a short silence tail.
        if (audible)
        {
            _duckLatched = true;
            _silenceAfterVoiceSec = 0f;
        }
        else if (_duckLatched)
        {
            _silenceAfterVoiceSec += dt;
            if (_silenceAfterVoiceSec >= SilenceBeforeUnduckSec)
                _duckLatched = false;
        }

        var wantDuck = _duckLatched && (audible || _silenceAfterVoiceSec < SilenceBeforeUnduckSec);
        var duckTarget = Mathf.Clamp01(ModSettings.VoiceDuckTargetPercent / 100f);
        var targetMul = wantDuck ? duckTarget : 1f;
        _smoothDuckMul = Mathf.SmoothDamp(_smoothDuckMul, targetMul, ref _duckSmoothVel, DuckSmoothTimeSec, Mathf.Infinity, dt);

        if (!wantDuck && _smoothDuckMul > 0.999f)
        {
            ReleaseAppliedDuckVolumes();
            return;
        }

        if (wantDuck && !_duckVolumesApplied)
            RebuildDuckSourceSnapshot();

        MaintainAndApplyDuckedGameAudio();
    }

    private void ReleaseDuckingIfNeeded()
    {
        ReleaseAppliedDuckVolumes();
        _smoothDuckMul = 1f;
        _duckSmoothVel = 0f;
        _duckLatched = false;
        _silenceAfterVoiceSec = 0f;
    }

    private void ReleaseAppliedDuckVolumes()
    {
        if (!_duckVolumesApplied)
            return;

        AudioListener.volume = _baselineListenerVolume;
        foreach (var kv in _duckGameSourceBaselines)
        {
            var e = kv.Value;
            if (e.Source == null || !e.Source)
                continue;
            e.Source.volume = e.BaselineVolume;
        }

        _duckGameSourceBaselines.Clear();
        _duckVolumesApplied = false;
        _nextSourceRefreshTime = 0f;
    }

    private static bool IsModChatOrProtectedUiSound(AudioSource s)
    {
        if (s == null || s.gameObject == null)
            return false;
        for (var t = s.transform; t != null; t = t.parent)
        {
            var n = t.name;
            if (n == "MPChatVoicePlayback" || n == "MPChatHotMicPlayer" || n == "MPChatUISound" ||
                n == "MPChatVoicePreviewPlayer")
                return true;
        }

        return false;
    }

    private static IEnumerable<AudioSource> EnumerateSceneHierarchyAudioSources()
    {
        foreach (var s in Resources.FindObjectsOfTypeAll<AudioSource>())
        {
            if (s == null)
                continue;
            var go = s.gameObject;
            if (!go.scene.IsValid())
                continue;
            if ((go.hideFlags & HideFlags.HideAndDontSave) != 0)
                continue;
            yield return s;
        }
    }

    private void RebuildDuckSourceSnapshot()
    {
        _duckGameSourceBaselines.Clear();
        _baselineListenerVolume = Mathf.Clamp(AudioListener.volume, 0.02f, 2f);

        foreach (var s in EnumerateSceneHierarchyAudioSources())
        {
            if (s == null || !s.gameObject.activeInHierarchy)
                continue;
            if (IsModChatOrProtectedUiSound(s))
                continue;
            var id = s.GetInstanceID();
            if (!_duckGameSourceBaselines.ContainsKey(id))
                _duckGameSourceBaselines[id] = new DuckSourceEntry { Source = s, BaselineVolume = s.volume };
        }

        _duckVolumesApplied = true;
        _nextSourceRefreshTime = Time.realtimeSinceStartup + SourceRefreshIntervalSec;
    }

    private void MaintainAndApplyDuckedGameAudio()
    {
        if (!_duckVolumesApplied)
            return;

        var mul = Mathf.Clamp(_smoothDuckMul, 0.02f, 1f);
        AudioListener.volume = _baselineListenerVolume;

        _baselineDeadSweep.Clear();
        foreach (var kv in _duckGameSourceBaselines)
        {
            var id = kv.Key;
            var e = kv.Value;
            if (e.Source == null || !e.Source)
            {
                _baselineDeadSweep.Add(id);
                continue;
            }

            e.Source.volume = Mathf.Clamp(e.BaselineVolume * mul, 0f, 3f);
        }

        foreach (var id in _baselineDeadSweep)
            _duckGameSourceBaselines.Remove(id);

        var doPeriodicRefresh = Time.realtimeSinceStartup >= _nextSourceRefreshTime;
        if (!doPeriodicRefresh)
            return;

        _nextSourceRefreshTime = Time.realtimeSinceStartup + SourceRefreshIntervalSec;
        _duckSeenSourceIdsScratch.Clear();

        foreach (var s in EnumerateSceneHierarchyAudioSources())
        {
            if (s == null || !s.gameObject.activeInHierarchy)
                continue;
            if (IsModChatOrProtectedUiSound(s))
                continue;

            var id = s.GetInstanceID();
            _duckSeenSourceIdsScratch.Add(id);
            if (!_duckGameSourceBaselines.ContainsKey(id))
                _duckGameSourceBaselines[id] = new DuckSourceEntry { Source = s, BaselineVolume = s.volume };
        }

        _baselineDeadSweep.Clear();
        foreach (var kv in _duckGameSourceBaselines)
        {
            if (!_duckSeenSourceIdsScratch.Contains(kv.Key))
                _baselineDeadSweep.Add(kv.Key);
        }

        foreach (var id in _baselineDeadSweep)
            _duckGameSourceBaselines.Remove(id);
    }

    private bool IsIncomingVoiceAudibleNow()
    {
        var now = Time.realtimeSinceStartup;
        if (now - _lastIncomingVoiceRealtime < VoiceActivityHoldoverSec)
            return true;

        var cm = ChatManager.Instance;
        if (cm == null)
            return false;

        if (now >= _incomingVoiceAudiblePollExpiry)
        {
            _incomingVoiceAudibleCached = cm.IsIncomingVoiceAudible();
            _incomingVoiceAudiblePollExpiry = now + IncomingVoiceAudiblePollIntervalSec;
        }

        return _incomingVoiceAudibleCached;
    }

    private void PollLobbyHierarchyForVoipReload()
    {
        if (Time.realtimeSinceStartup < _nextLobbyHierarchyPollTime)
            return;
        var pollInterval = MpChatLobbyDiagnostics.ActiveSceneIsMainMenuWithoutGameCore() ? 2.5f : 0.5f;
        _nextLobbyHierarchyPollTime = Time.realtimeSinceStartup + pollInterval;

        var inGameCore = string.Equals(SceneManager.GetActiveScene().name, "GameCore", System.StringComparison.Ordinal);
        if (inGameCore)
        {
            _lobbyVoipReloadArmed = true;
            return;
        }

        var lobbyUi = HierarchyLooksLikeMultiplayerLobby();
        if (lobbyUi && _lobbyVoipReloadArmed)
        {
            _lobbyVoipReloadArmed = false;
            MpChatLobbyDiagnostics.LogVoipTransition("PollLobby:reload_armed", null);
            TryRunVoipReload("[MPChat] VoIP reloaded (multiplayer lobby hierarchy active)");
            ApplySongVoicePolicy();
        }
        else if (!lobbyUi)
            _lobbyVoipReloadArmed = true;
    }
}
