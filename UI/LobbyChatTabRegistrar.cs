using System;
using System.Collections;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.GameplaySetup;
using MultiplayerChat.Core;
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

    [UIComponent("ChatInput")]
    private BeatSaberMarkupLanguage.Components.Settings.StringSetting? _chatInput;

    [UIComponent("RecordButton")]
    private Button? _recordButton;

    [UIComponent("MuteButton")]
    private Button? _muteButton;

    [UIComponent("DMButton")]
    private Button? _dmButton;

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

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
        _dmState.DMTargetChanged += OnDmTargetChanged;
        _chatIdConfigStore.MutedStateChanged += OnMutedConfigChanged;
        StartCoroutine(TryAddTabWhenReady());
    }

    private void OnDestroy()
    {
        _dmState.DMTargetChanged -= OnDmTargetChanged;
        _chatIdConfigStore.MutedStateChanged -= OnMutedConfigChanged;

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

    [UIAction("SubmitClicked")]
    private void SubmitClicked()
    {
        var text = _chatInput?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;

        var cm = ChatManager.Instance;
        if (cm != null)
        {
            if (!cm.SendMessage(text))
                return;
            _chatInput!.Text = "";
            StartCoroutine(DeferredRefreshLobbyActionHighlights());
        }
        else
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] ChatManager.Instance is null - not in multiplayer lobby?");
        }
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
            _dmState.ClearDMTarget();
            if (!string.IsNullOrEmpty(stoppedTarget))
                _chatManager.SendDmStoppedNotify(stoppedTarget!, stoppedChatId);
            UpdateLobbyActionButtonHighlights();
            return;
        }

        OpenMuteOrDmGrid(PlayerListViewController.Mode.DM);
    }

    private void OnDmTargetChanged(object? sender, EventArgs e) => StartCoroutine(DeferredRefreshLobbyActionHighlights());

    private void OnMutedConfigChanged() => UpdateLobbyActionButtonHighlights();

    private void OnEnable()
    {
        if (_tabAdded)
            StartCoroutine(RefreshLobbyUiWhenTabShown());
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

        ApplyHighlight(_dmButton, dmOn);
        ApplyHighlight(_muteButton, muteOn);
        ApplyLobbyActionButtonLabelColor(_dmButton, dmOn, _dmButtonLabelDefaultColor);
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
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Nothing recorded — use Record, then Stop.");
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
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Nothing to send — record a voice message first.");
            return;
        }

        var blob = VoiceMessageCodec.EncodeFromFloatSamples(_capturedSamples, _capturedChannels, _capturedHz);
        if (blob == null)
            return;

        var cm = ChatManager.Instance;
        if (cm == null)
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] ChatManager.Instance is null — not in multiplayer lobby?");
            return;
        }

        if (!cm.SendVoiceMessage(blob))
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

        _micDevice = null;
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
        if (_recordCapCoroutine != null)
            StopCoroutine(_recordCapCoroutine);
        _recordCapCoroutine = StartCoroutine(RecordingCapCoroutine());
        UpdateRecordButtonLabel();
    }

    private void StopRecordingInternal()
    {
        if (!_isRecording && _recordingClip == null)
            return;

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
