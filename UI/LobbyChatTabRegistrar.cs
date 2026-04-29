using System;
using System.Collections;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.GameplaySetup;
using MultiplayerChat.Core;
using MultiplayerChat.Network;
using MultiplayerChat.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// Waits for GameplaySetup to be ready, then adds the Multiplayer Chat tab.
/// Settings opens in its own FlowCoordinator (separate UI).
/// </summary>
public class LobbyChatTabRegistrar : MonoBehaviour
{
    private const string TabName = "Multiplayer Chat";
    private const int MicBufferSeconds = 90;
    private const float MaxRecordSeconds = 60f;

    private static readonly Color AccentBlue = new(0.32f, 0.58f, 1f, 1f);

    [Inject] private readonly ChatBubbleManager _chatBubbleManager = null!;
    [Inject] private readonly ChatManager _chatManager = null!;
    [Inject] private readonly MultiplayerChatSettingsFlowCoordinator _settingsFlowCoordinator = null!;
    [Inject] private readonly DiContainer _container = null!;
    [Inject] private readonly ChatDMState _dmState = null!;
    [Inject] private readonly ChatIdConfigStore _chatIdConfigStore = null!;
    [Inject] private readonly VoiceSettingsFlowCoordinator _voiceSettingsFlowCoordinator = null!;
    [Inject] private readonly VoiceHotMicManager _voiceHotMicManager = null!;

    [UIComponent("ChatInput")]
    private BeatSaberMarkupLanguage.Components.Settings.StringSetting? _chatInput;

    [UIComponent("RecordButton")]
    private Button? _recordButton;

    [UIComponent("MuteButton")]
    private Button? _muteButton;

    [UIComponent("DMButton")]
    private Button? _dmButton;

    [UIComponent("DeafButton")]
    private Button? _deafButton;

    [UIComponent("VoiceHotMicMuteButton")]
    private Button? _voiceHotMicMuteButton;

    [UIComponent("HearButton")]
    private Button? _hearButton;

    private ColorBlock _lobbyActionDefaultColors;
    private bool _lobbyActionColorsCached;
    private Color _dmButtonLabelDefaultColor = Color.white;
    private bool _lobbyActionLabelColorsCached;

    private bool _tabAdded;
    private bool _isRecording;
    private string? _micDevice;
    private AudioClip? _recordingClip;
    private int _recordingHz;
    private Coroutine? _recordCapCoroutine;
    private float[]? _capturedSamples;
    private int _capturedChannels;
    private int _capturedHz;
    private Coroutine? _previewCoroutine;

    private Button? _lobbyChatStringButton;
    private TMP_InputField? _lobbyChatTmp;
    private bool _lobbyTypingBroadcastToOthers;
    private bool _lobbyModalKeyboardEnterSubscribed;
    private Coroutine? _lobbyTypingModalWatchCoroutine;
    private bool _recordingPresenceBroadcastToOthers;

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
        _dmState.DMTargetChanged += OnDmTargetChanged;
        _chatIdConfigStore.MutedStateChanged += OnMutedConfigChanged;
        VoiceChatRuntimeState.Changed += OnVoiceStateChanged;
        StartCoroutine(TryAddTabWhenReady());
    }

    private void OnDestroy()
    {
        UnbindLobbyChatTypingListeners();
        _dmState.DMTargetChanged -= OnDmTargetChanged;
        _chatIdConfigStore.MutedStateChanged -= OnMutedConfigChanged;
        VoiceChatRuntimeState.Changed -= OnVoiceStateChanged;

        if (_isRecording)
            StopRecordingInternal();
        if (_previewCoroutine != null)
        {
            StopCoroutine(_previewCoroutine);
            _previewCoroutine = null;
        }

        if (!_tabAdded) return;
        try
        {
            GameplaySetup.Instance?.RemoveTab(TabName);
        }
        catch { /* ignore */ }
    }

    private IEnumerator TryAddTabWhenReady()
    {
        while (!_tabAdded)
        {
            yield return new WaitForSeconds(0.5f);
            if (this == null) yield break;
            try
            {
                var gs = GameplaySetup.Instance;
                if (gs != null)
                {
                    gs.AddTab(TabName, "MultiplayerChat.UI.LobbyChatTab.bsml", this);
                    _tabAdded = true;
                    UpdateLobbyActionButtonHighlights();
                    StartCoroutine(BindLobbyTypingListenersNextFrame());
                    break;
                }
            }
            catch (Exception ex)
            {
                MultiplayerChat.Plugin.Log?.Error($"[MPChat] Failed to add tab: {ex}");
            }
        }
    }

    [UIAction("SettingsClicked")]
    private void SettingsClicked()
    {
        var mainFlow = BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator;
        var topFlow = FlowCoordinatorHelper.GetTopFlowCoordinator(mainFlow);
        _settingsFlowCoordinator.ParentFlow = topFlow;
        topFlow.PresentFlowCoordinator(_settingsFlowCoordinator);
    }

    [UIAction("VoiceSettingsClicked")]
    private void VoiceSettingsClicked()
    {
        var mainFlow = BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator;
        var topFlow = FlowCoordinatorHelper.GetTopFlowCoordinator(mainFlow);
        _voiceSettingsFlowCoordinator.ParentFlow = topFlow;
        topFlow.PresentFlowCoordinator(_voiceSettingsFlowCoordinator);
    }

    [UIAction("DeafClicked")]
    private void DeafClicked()
    {
        VoiceChatRuntimeState.SetDeaf(!VoiceChatRuntimeState.IsDeaf);
        _chatManager.SendVoiceDeafenStateNotify(VoiceChatRuntimeState.IsDeaf);
        if (VoiceChatRuntimeState.IsDeaf)
            _chatManager.PostSystemMessageRich(
                "<color=#CCCCCC>You have went deaf, you cannot hear anybody, not even voice messages.</color>");
        else
            _chatManager.PostSystemMessageRich("<color=#CCCCCC>You have went undeaf, you can hear all voices.</color>");
        UpdateLobbyActionButtonHighlights();
    }

    [UIAction("VoiceHotMicMuteClicked")]
    private void VoiceHotMicMuteClicked()
    {
        VoiceChatRuntimeState.SetHotMicMuted(!VoiceChatRuntimeState.IsHotMicMuted);
        if (VoiceChatRuntimeState.IsHotMicMuted)
            _chatManager.PostSystemMessageRich("<color=#CCCCCC>You have muted your microphone.</color>");
        else
            _chatManager.PostSystemMessageRich("<color=#CCCCCC>You have unmuted your microphone.</color>");
        UpdateLobbyActionButtonHighlights();
    }

    [UIAction("PlayerVolumeClicked")]
    private void PlayerVolumeClicked()
    {
        OpenMuteOrDmGrid(PlayerListViewController.Mode.Volume);
    }

    [UIAction("HearClicked")]
    private void HearClicked()
    {
        OpenMuteOrDmGrid(PlayerListViewController.Mode.Listen);
    }

    [UIAction("SubmitClicked")]
    private void SubmitClicked()
    {
        var text = _chatInput?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text))
        {
            ClearLobbyTypingBroadcastIfNeeded();
            return;
        }

        ClearLobbyTypingBroadcastIfNeeded();

        if (!_chatManager.SendMessage(text))
            return;
        _chatInput!.Text = "";
        StartCoroutine(DeferredRefreshLobbyActionHighlights());
    }

    [UIAction("MuteClicked")]
    private void MuteClicked()
    {
        OpenMuteOrDmGrid(PlayerListViewController.Mode.Mute);
    }

    private void OpenMuteOrDmGrid(PlayerListViewController.Mode mode)
    {
        var mainFlow = BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator;
        var topFlow = FlowCoordinatorHelper.GetTopFlowCoordinator(mainFlow);
        // MonoBehaviour FlowCoordinators must be resolved (bound FromNewComponentOnNewGameObject), not DiContainer.Instantiate<T>().
        var fc = _container.Resolve<PlayerListFlowCoordinator>();
        // Reuse one coordinator: dismiss if still presented so Present does not stack UIs.
        if (fc.ParentFlow != null)
        {
            try
            {
                fc.ParentFlow.DismissFlowCoordinator(fc);
            }
            catch (Exception ex)
            {
                MultiplayerChat.Plugin.Log?.Debug($"[MPChat] Dismiss player list before open: {ex.Message}");
            }
        }
        fc.Present(topFlow, mode);
    }

    [UIAction("DMClicked")]
    private void DMClicked()
    {
        // Foolproof: always snap DM button to default (black) first; tab OnEnable re-applies blue if still DMing.
        var wasDm = _dmState.IsInDMMode;
        ForceDmButtonNeutralVisual();

        if (wasDm)
        {
            var stoppedTarget = _dmState.DMTargetUserId;
            var stoppedChatId = _dmState.DMTargetChatId;
            var peerName = _dmState.DMTargetUserName;
            _dmState.ClearDMTarget();
            if (!string.IsNullOrEmpty(stoppedTarget))
            {
                if (!string.IsNullOrEmpty(peerName))
                    _chatManager.PostSystemMessageRich(
                        "You are no longer DM'ing " + ChatManager.SystemLineWithColoredPlayerName(peerName!, "", null) + ".");
                else
                    _chatManager.PostSystemMessageRich(
                        "<color=#CCCCCC>You are no longer DM'ing that player.</color>");
                _chatManager.SendDmStoppedNotify(stoppedTarget!, stoppedChatId);
            }

            UpdateLobbyActionButtonHighlights();
            return;
        }

        OpenMuteOrDmGrid(PlayerListViewController.Mode.DM);
    }

    private void OnDmTargetChanged(object? sender, EventArgs e) => StartCoroutine(DeferredRefreshLobbyActionHighlights());

    private void OnMutedConfigChanged() => UpdateLobbyActionButtonHighlights();

    private void OnVoiceStateChanged() => UpdateLobbyActionButtonHighlights();

    private void OnEnable()
    {
        if (_tabAdded)
            StartCoroutine(RefreshLobbyUiWhenTabShown());
    }

    private void OnDisable()
    {
        ClearLobbyTypingBroadcastIfNeeded();
    }

    /// <summary>GameplaySetup often deselects this tab when opening Mute/DM/Settings; when user comes back, re-sync DM (and mute) from state.</summary>
    private IEnumerator RefreshLobbyUiWhenTabShown()
    {
        yield return null;
        yield return null;
        yield return null;
        UpdateLobbyActionButtonHighlights();
    }

    private IEnumerator DeferredRefreshLobbyActionHighlights()
    {
        yield return null;
        yield return null;
        UpdateLobbyActionButtonHighlights();
    }

    private void ForceDmButtonNeutralVisual()
    {
        CacheLobbyActionDefaultColorsOnce();
        CacheLobbyActionLabelColorsOnce();
        if (!_lobbyActionColorsCached || _dmButton == null) return;
        ApplyHighlight(_dmButton, false);
        ApplyLobbyActionButtonLabelColor(_dmButton, false, _dmButtonLabelDefaultColor);
    }

    private void CacheLobbyActionDefaultColorsOnce()
    {
        if (_lobbyActionColorsCached) return;
        var src = _dmButton != null ? _dmButton : _muteButton;
        if (src == null) return;
        _lobbyActionDefaultColors = src.colors;
        _lobbyActionColorsCached = true;
    }

    private void CacheLobbyActionLabelColorsOnce()
    {
        if (_lobbyActionLabelColorsCached) return;
        if (_dmButton != null)
        {
            var t = _dmButton.GetComponentInChildren<TMP_Text>();
            if (t != null)
                _dmButtonLabelDefaultColor = t.color;
        }
        _lobbyActionLabelColorsCached = true;
    }

    private void UpdateLobbyActionButtonHighlights()
    {
        CacheLobbyActionDefaultColorsOnce();
        CacheLobbyActionLabelColorsOnce();
        if (!_lobbyActionColorsCached) return;

        var dmOn = _dmState.IsInDMMode;
        var muteOn = _chatIdConfigStore.HasAnyMutedEntry();
        var hotMicMuteOn = VoiceChatRuntimeState.IsHotMicMuted;
        var hearOn = VoiceChatRuntimeState.IsListenFilterActive || VoiceChatRuntimeState.IsTalkToActive;

        ApplyHighlight(_dmButton, dmOn);
        ApplyHighlight(_muteButton, muteOn);
        ApplyHighlight(_voiceHotMicMuteButton, hotMicMuteOn);
        ApplyHighlight(_hearButton, hearOn);
        ApplyLobbyActionButtonLabelColor(_dmButton, dmOn, _dmButtonLabelDefaultColor);
        ApplyLobbyActionButtonLabelColor(_voiceHotMicMuteButton, hotMicMuteOn, _dmButtonLabelDefaultColor);
        ApplyLobbyActionButtonLabelColor(_hearButton, hearOn, _dmButtonLabelDefaultColor);

        if (_deafButton != null)
        {
            var tm = _deafButton.GetComponentInChildren<TMP_Text>();
            if (tm != null)
                tm.text = VoiceChatRuntimeState.IsDeaf ? "Undeaf" : "Deaf";
        }

        if (_voiceHotMicMuteButton != null)
        {
            var tm = _voiceHotMicMuteButton.GetComponentInChildren<TMP_Text>();
            if (tm != null)
                tm.text = VoiceChatRuntimeState.IsHotMicMuted ? "Unmute mic" : "Mute mic";
        }
    }

    private static void ApplyLobbyActionButtonLabelColor(Button? btn, bool active, Color defaultLabelColor)
    {
        if (btn == null) return;
        var t = btn.GetComponentInChildren<TMP_Text>();
        if (t == null) return;
        t.color = active ? Color.white : defaultLabelColor;
    }

    private void ApplyHighlight(Button? btn, bool active)
    {
        if (btn == null) return;
        var c = btn.colors;
        if (active)
        {
            c.normalColor = AccentBlue;
            c.highlightedColor = AccentBlue;
            c.selectedColor = AccentBlue;
            c.pressedColor = AccentBlue;
        }
        else
        {
            c.normalColor = _lobbyActionDefaultColors.normalColor;
            c.highlightedColor = _lobbyActionDefaultColors.highlightedColor;
            c.selectedColor = _lobbyActionDefaultColors.selectedColor;
            c.pressedColor = _lobbyActionDefaultColors.pressedColor;
            c.disabledColor = _lobbyActionDefaultColors.disabledColor;
        }

        btn.colors = c;
    }

    private IEnumerator BindLobbyTypingListenersNextFrame()
    {
        yield return null;
        TryBindLobbyChatTypingListeners();
    }

    private void TryBindLobbyChatTypingListeners()
    {
        UnbindLobbyChatTypingListeners();
        if (_chatInput == null) return;
        _lobbyChatStringButton = _chatInput.GetComponentInChildren<Button>(true);
        if (_lobbyChatStringButton != null)
            _lobbyChatStringButton.onClick.AddListener(OnLobbyChatBarPressed);

        _lobbyChatTmp = _chatInput.GetComponentInChildren<TMP_InputField>(true);
        if (_lobbyChatTmp != null)
        {
            _lobbyChatTmp.onSubmit.AddListener(OnLobbyChatSubmitOrEnter);
            _lobbyChatTmp.onEndEdit.AddListener(OnLobbyChatEndEdit);
            _lobbyChatTmp.onDeselect.AddListener(OnLobbyChatDeselect);
        }

        TryBindLobbyModalKeyboardTypingHooks();
    }

    private void UnbindLobbyChatTypingListeners()
    {
        if (_lobbyTypingModalWatchCoroutine != null)
        {
            StopCoroutine(_lobbyTypingModalWatchCoroutine);
            _lobbyTypingModalWatchCoroutine = null;
        }

        TryUnbindLobbyModalKeyboardTypingHooks();

        if (_lobbyChatStringButton != null)
        {
            _lobbyChatStringButton.onClick.RemoveListener(OnLobbyChatBarPressed);
            _lobbyChatStringButton = null;
        }

        if (_lobbyChatTmp != null)
        {
            _lobbyChatTmp.onSubmit.RemoveListener(OnLobbyChatSubmitOrEnter);
            _lobbyChatTmp.onEndEdit.RemoveListener(OnLobbyChatEndEdit);
            _lobbyChatTmp.onDeselect.RemoveListener(OnLobbyChatDeselect);
            _lobbyChatTmp = null;
        }

        ClearLobbyTypingBroadcastIfNeeded();
    }

    private void TryBindLobbyModalKeyboardTypingHooks()
    {
        TryUnbindLobbyModalKeyboardTypingHooks();
        if (_chatInput?.ModalKeyboard?.Keyboard == null) return;
        _chatInput.ModalKeyboard.Keyboard.EnterPressed += OnLobbyModalKeyboardEnterPressed;
        _lobbyModalKeyboardEnterSubscribed = true;
    }

    private void TryUnbindLobbyModalKeyboardTypingHooks()
    {
        if (!_lobbyModalKeyboardEnterSubscribed)
            return;
        if (_chatInput?.ModalKeyboard?.Keyboard != null)
            _chatInput.ModalKeyboard.Keyboard.EnterPressed -= OnLobbyModalKeyboardEnterPressed;
        _lobbyModalKeyboardEnterSubscribed = false;
    }

    private void OnLobbyModalKeyboardEnterPressed(string _) => ClearLobbyTypingBroadcastIfNeeded();

    private IEnumerator WatchLobbyTypingModalUntilDismissed()
    {
        yield return null;
        var mk = _chatInput?.ModalKeyboard;
        var go = mk?.ModalView != null ? mk.ModalView.gameObject : null;
        if (go == null)
        {
            _lobbyTypingModalWatchCoroutine = null;
            yield break;
        }

        // Wait for the modal to appear (BSML opens it on the next frame); avoid clearing typing if there is no modal UI.
        var showDeadline = Time.realtimeSinceStartup + 2f;
        while (_lobbyTypingBroadcastToOthers && !go.activeInHierarchy && Time.realtimeSinceStartup < showDeadline)
            yield return null;

        if (!_lobbyTypingBroadcastToOthers || !go.activeInHierarchy)
        {
            _lobbyTypingModalWatchCoroutine = null;
            yield break;
        }

        while (_lobbyTypingBroadcastToOthers && go.activeInHierarchy)
            yield return null;

        ClearLobbyTypingBroadcastIfNeeded();
        _lobbyTypingModalWatchCoroutine = null;
    }

    private void OnLobbyChatBarPressed()
    {
        if (_lobbyTypingBroadcastToOthers) return;
        _chatManager.BroadcastChatActivity(ChatActivityPacket.TypingStart);
        _lobbyTypingBroadcastToOthers = true;
        if (_lobbyTypingModalWatchCoroutine != null)
        {
            StopCoroutine(_lobbyTypingModalWatchCoroutine);
            _lobbyTypingModalWatchCoroutine = null;
        }

        _lobbyTypingModalWatchCoroutine = StartCoroutine(WatchLobbyTypingModalUntilDismissed());
    }

    private void OnLobbyChatSubmitOrEnter(string _)
    {
        ClearLobbyTypingBroadcastIfNeeded();
    }

    private void OnLobbyChatEndEdit(string _)
    {
        ClearLobbyTypingBroadcastIfNeeded();
    }

    private void OnLobbyChatDeselect(string _)
    {
        ClearLobbyTypingBroadcastIfNeeded();
    }

    private void ClearLobbyTypingBroadcastIfNeeded()
    {
        if (!_lobbyTypingBroadcastToOthers) return;
        _chatManager.BroadcastChatActivity(ChatActivityPacket.TypingStop);
        _lobbyTypingBroadcastToOthers = false;
    }

    [UIAction("ForceClearClicked")]
    private void ForceClearClicked()
    {
        _chatBubbleManager.ForceClearChat();
    }

    [UIAction("ForceEndVoiceClicked")]
    private void ForceEndVoiceClicked()
    {
        _chatManager.ForceStopVoicePlayback();
    }

    [UIAction("ForceResetVoipClicked")]
    private void ForceResetVoipClicked()
    {
        GlobalChatAudioHost.ForceResetVoipFromUi("[MPChat] FORCE RESET VOIP (lobby tab button)");
        _chatBubbleManager.RebindToActiveChatManager();
    }

    [UIAction("RecordClicked")]
    private void RecordClicked()
    {
        if (_isRecording)
            StopRecordingInternal();
        else
            StartRecordingInternal();
    }

    [UIAction("PlayVoiceClicked")]
    private void PlayVoiceClicked()
    {
        if (_capturedSamples == null || _capturedSamples.Length == 0)
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Nothing recorded  -  use Record, then Stop.");
            return;
        }

        var frames = _capturedSamples.Length / _capturedChannels;
        var clip = AudioClip.Create("MPChatVoicePreview", frames, _capturedChannels, _capturedHz, false);
        clip.SetData(_capturedSamples, 0);
        if (_previewCoroutine != null)
            StopCoroutine(_previewCoroutine);
        _previewCoroutine = StartCoroutine(PlayPreviewAndCleanup(clip));
    }

    [UIAction("SendVoiceClicked")]
    private void SendVoiceClicked()
    {
        if (_capturedSamples == null || _capturedSamples.Length == 0)
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Nothing to send  -  record a voice message first.");
            return;
        }

        var blob = VoiceMessageCodec.EncodeFromFloatSamples(_capturedSamples, _capturedChannels, _capturedHz);
        if (blob == null)
            return;

        if (!_chatManager.SendVoiceMessage(blob))
            return;

        _capturedSamples = null;
        _capturedChannels = 0;
        _capturedHz = 0;
    }

    private void StartRecordingInternal()
    {
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] No microphone found. Check Windows sound input settings.");
            return;
        }

        if (_isRecording)
            return;

        _micDevice = ResolveMicDeviceNameForRecording();
        Microphone.GetDeviceCaps(_micDevice, out var minFreq, out var maxFreq);
        var hz = 44100;
        if (maxFreq > 0)
            hz = Mathf.Clamp(44100, minFreq, maxFreq);
        else if (minFreq > 0)
            hz = minFreq;

        _recordingHz = hz;
        _recordingClip = Microphone.Start(_micDevice, false, MicBufferSeconds, hz);
        if (_recordingClip == null)
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Microphone.Start failed.");
            return;
        }

        _isRecording = true;
        _chatManager.BroadcastChatActivity(ChatActivityPacket.RecordingVoiceStart);
        _recordingPresenceBroadcastToOthers = true;
        if (_recordCapCoroutine != null)
            StopCoroutine(_recordCapCoroutine);
        _recordCapCoroutine = StartCoroutine(RecordingCapCoroutine());
        UpdateRecordButtonLabel();
    }

    /// <summary>Null means use the system default input device (Unity).</summary>
    private static string? ResolveMicDeviceNameForRecording()
    {
        var name = ModSettings.MicInputDeviceName;
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var d in Microphone.devices ?? Array.Empty<string>())
        {
            if (d == name) return name;
        }

        return null;
    }

    private void StopRecordingInternal()
    {
        if (!_isRecording && _recordingClip == null)
            return;

        if (_recordingPresenceBroadcastToOthers)
        {
            _chatManager.BroadcastChatActivity(ChatActivityPacket.RecordingVoiceStop);
            _recordingPresenceBroadcastToOthers = false;
        }

        if (_recordCapCoroutine != null)
        {
            StopCoroutine(_recordCapCoroutine);
            _recordCapCoroutine = null;
        }

        var clip = _recordingClip;
        _recordingClip = null;
        _isRecording = false;

        int sampleCount;
        if (clip == null)
        {
            if (Microphone.IsRecording(_micDevice))
                Microphone.End(_micDevice);
            UpdateRecordButtonLabel();
            _voiceHotMicManager.ForceReloadMicrophone();
            return;
        }

        if (Microphone.IsRecording(_micDevice))
        {
            sampleCount = Microphone.GetPosition(_micDevice);
            Microphone.End(_micDevice);
        }
        else
            sampleCount = clip.samples;

        if (sampleCount <= 0)
        {
            Destroy(clip);
            UpdateRecordButtonLabel();
            _voiceHotMicManager.ForceReloadMicrophone();
            return;
        }

        var channels = clip.channels;
        var totalFloats = sampleCount * channels;
        var data = new float[totalFloats];
        clip.GetData(data, 0);
        Destroy(clip);

        _capturedSamples = data;
        _capturedChannels = channels;
        _capturedHz = _recordingHz;
        UpdateRecordButtonLabel();

        // Voice message recording uses Microphone.Start/End on the same device as hot mic; release so hot mic can restart.
        _voiceHotMicManager.ForceReloadMicrophone();
    }

    private IEnumerator RecordingCapCoroutine()
    {
        yield return new WaitForSeconds(MaxRecordSeconds);
        if (_isRecording)
            StopRecordingInternal();
        _recordCapCoroutine = null;
    }

    private IEnumerator PlayPreviewAndCleanup(AudioClip clip)
    {
        var go = new GameObject("MPChatVoicePreviewPlayer");
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = 1f;
        src.spatialBlend = 0f;
        src.bypassEffects = true;
        src.bypassListenerEffects = true;
        src.bypassReverbZones = true;
        src.Play();
        yield return new WaitForSeconds(clip.length + 0.08f);
        Destroy(clip);
        Destroy(go);
        _previewCoroutine = null;
    }

    private void UpdateRecordButtonLabel()
    {
        if (_recordButton == null) return;
        var text = _recordButton.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = _isRecording ? "Stop" : "Record";
    }
}
