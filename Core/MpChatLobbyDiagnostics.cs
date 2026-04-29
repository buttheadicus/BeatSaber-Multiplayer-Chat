using System.Text;
using MultiplayerChat;
using MultiplayerChat.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiplayerChat.Core;

/// <summary>Verbose, throttled logging for arena → lobby / results UI transitions.</summary>
public static class MpChatLobbyDiagnostics
{
    /// <summary>
    /// Default off: verbose paths walk every <c>TMP_Text</c>, repeatedly call GameObject.Find and FindObjectOfType — destroys frame time.
    /// Set <c>true</c> only temporarily when debugging transitions.
    /// </summary>
    /// Setting <see langword="false"/> skips expensive scene/TMP walks and avoids unreachable-code warnings in dev builds when logging is compiled out.
    public static readonly bool DetailedVoipSnapshots = false;

    /// <summary>
    /// Extra <c>[MPChat][VoIP]</c> lines during reload (TryRunVoipReload, pipeline context, mic force-reload). Default off — reduces log I/O and string work in lobby.
    /// </summary>
    public static bool VerboseVoipReloadLogs;

    private static float _lastFullSnapshotRealtime = -999f;
    private const float FullSnapshotCooldownSec = 1.25f;

    public static void LogVoipTransition(string tag, string? detail = null)
    {
        if (!DetailedVoipSnapshots)
            return;

        var sb = new StringBuilder(256);
        sb.Append("[MPChat][Diag][VoIP] ").Append(tag);
        sb.Append(" activeScene=").Append(SceneManager.GetActiveScene().name);
        sb.Append(" sceneCount=").Append(SceneManager.sceneCount);
        sb.Append(" anyGameCore=").Append(AnyGameCoreLoaded());
        sb.Append(" songGameplay=").Append(SongGameplayLikelyActive());
        sb.Append(" resultsLike=").Append(ResultsLikeUiVisible());
        sb.Append(" lobbyHeuristic=").Append(LobbyHierarchyLooksLikeMultiplayerLobby());
        sb.Append(" chatMgr=").Append(ChatManager.Instance != null ? ChatManager.Instance.GetHashCode().ToString() : "null");
        sb.Append(" bubbles=").Append(ChatBubbleManager.Instance != null ? ChatBubbleManager.Instance.GetHashCode().ToString() : "null");
        sb.Append(" hotMicMgr=").Append(VoiceHotMicManager.Instance != null ? VoiceHotMicManager.Instance.GetHashCode().ToString() : "null");
        if (!string.IsNullOrEmpty(detail))
            sb.Append(" | ").Append(detail);
        Plugin.Log?.Info(sb.ToString());
    }

    public static void LogFullUiSnapshotThrottled(string reason)
    {
        if (!DetailedVoipSnapshots)
            return;

        var now = Time.realtimeSinceStartup;
        if (now - _lastFullSnapshotRealtime < FullSnapshotCooldownSec)
            return;
        _lastFullSnapshotRealtime = now;

        var sb = new StringBuilder(512);
        sb.Append("[MPChat][Diag][UI] snapshot reason=").Append(reason);
        sb.Append(" active=").Append(SceneManager.GetActiveScene().name);
        sb.Append(" loaded=[");
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (i > 0) sb.Append(',');
            sb.Append(s.name).Append(s.isLoaded ? "" : "(!loaded)");
        }

        sb.Append("] gameplaySetup=");
        try
        {
            var gs = BeatSaberMarkupLanguage.GameplaySetup.GameplaySetup.Instance;
            sb.Append(gs != null ? "present" : "null");
        }
        catch
        {
            sb.Append("error");
        }

        sb.Append(" resultsLike=").Append(ResultsLikeUiVisible());
        Plugin.Log?.Info(sb.ToString());
    }

    public static bool AnyGameCoreLoaded()
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && string.Equals(s.name, "GameCore", System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>Clears TTL caches for lobby/song heuristics — call on scene transitions so mute policy / UI react immediately.</summary>
    public static void InvalidateSceneHeuristicCaches()
    {
        _lobbyHeuristicCacheTime = -999f;
        _songGameplayCacheTime = -999f;
    }

    private static float _lobbyHeuristicCacheTime = -999f;
    private static bool _lobbyHeuristicCached;
    /// <summary>Shared across poll cadences so ChatBubbleManager + VoIP reload do not each GameObject.Find four times the same frame.</summary>
    private const float LobbyHeuristicCacheTtlSec = 0.35f;

    private static float _songGameplayCacheTime = -999f;
    private static bool _songGameplayCached;
    /// <summary>Many systems queried SongGameplayLikelyActive twice per tick — cache avoids paired hitches ~0.5s apart (felt ~1s).</summary>
    private const float SongGameplayCacheTtlSec = 0.35f;

    /// <summary>
    /// True during active beatmap / arena gameplay. Uses scene name, audio sync, and spawn controller — MP arena may omit <c>GameCore</c> or audio sync until late.
    /// Result is TTL-cached (~300ms); call <see cref="InvalidateSceneHeuristicCaches"/> after scene transitions if you need an immediate refresh.
    /// </summary>
    public static bool SongGameplayLikelyActive()
    {
        var now = Time.realtimeSinceStartup;
        if (now - _songGameplayCacheTime < SongGameplayCacheTtlSec)
            return _songGameplayCached;
        _songGameplayCached = SongGameplayLikelyActiveUncached();
        _songGameplayCacheTime = now;
        return _songGameplayCached;
    }

    private static bool SongGameplayLikelyActiveUncached()
    {
        if (AnyGameCoreLoaded())
            return true;
        // Multiplayer lobby UI: not beatmap gameplay — avoids repeated FindObjectOfType scans on policy ticks.
        if (LobbyHierarchyLooksLikeMultiplayerLobbyUncached())
            return false;
        try
        {
            if (Object.FindObjectOfType<AudioTimeSyncController>() != null)
                return true;
            if (Object.FindObjectOfType<BeatmapObjectSpawnController>() != null)
                return true;
            if (Object.FindObjectOfType<SongController>() != null)
                return true;
            if (Object.FindObjectOfType<StandardLevelGameplayManager>() != null)
                return true;
        }
        catch
        {
            /* ignore */
        }

        return false;
    }

    /// <summary>Heuristic: post-song results / level end screens often contain these strings.</summary>
    public static bool ResultsLikeUiVisible()
    {
        try
        {
            foreach (var tmp in Object.FindObjectsOfType<TMPro.TMP_Text>())
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;
                var t = (tmp.text ?? "").ToUpperInvariant();
                if (t.Contains("RESULT") || t.Contains("CONTINUE") || t.Contains("LEVEL COMPLETE") ||
                    t.Contains("LEVEL FAILED") || t.Contains("CLEARED") || t.Contains("SCORE"))
                    return true;
            }
        }
        catch { /* ignore */ }

        return false;
    }

    public static bool LobbyHierarchyLooksLikeMultiplayerLobby()
    {
        var now = Time.realtimeSinceStartup;
        if (now - _lobbyHeuristicCacheTime < LobbyHeuristicCacheTtlSec)
            return _lobbyHeuristicCached;
        _lobbyHeuristicCached = LobbyHierarchyLooksLikeMultiplayerLobbyUncached();
        _lobbyHeuristicCacheTime = now;
        return _lobbyHeuristicCached;
    }

    private static bool LobbyHierarchyLooksLikeMultiplayerLobbyUncached()
    {
        var center = GameObject.Find("MultiplayerLobbyCenterStage");
        if (center != null && center.activeInHierarchy)
            return true;
        var lobby = GameObject.Find("LobbySetup");
        if (lobby != null && lobby.activeInHierarchy)
            return true;
        var alt = GameObject.Find("CenterStage");
        if (alt != null && alt.activeInHierarchy)
            return true;
        var host = GameObject.Find("HostSetup");
        return host != null && host.activeInHierarchy;
    }
}
