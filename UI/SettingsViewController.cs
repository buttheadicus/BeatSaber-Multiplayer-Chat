using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Core;
using MultiplayerChat.Settings;
using UnityEngine.UI;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.SettingsView.bsml")]
public class SettingsViewController : BSMLAutomaticViewController
{
    private const string LabelEnableCau = "Enable 'Chat Auto Updater' (CAU)";
    private const string LabelDebugLogging = "Debug mode (very verbose; will lag; install only)";

    public event EventHandler? ApplyClicked;

    public event Action? PlayerSettingsClicked;

    public event Action? MicSettingsClicked;

    public event Action? FusedModsClicked;

    public event Action? AddonsClicked;

    public event Action? PerformanceClicked;

    [UIComponent("EnableCauToggle")]
    private ToggleSetting? _enableCauToggle;

    [UIComponent("DebugLoggingToggle")]
    private ToggleSetting? _debugLoggingToggle;

    [UIValue("EnableCau")]
    public bool EnableCau
    {
        get => ModSettings.EnableCau;
        set => ModSettings.EnableCau = value;
    }

    [UIValue("DebugLogging")]
    public bool DebugLogging
    {
        get => ModSettings.DebugLogging;
        set => ModSettings.DebugLogging = value;
    }

    [UIAction("PlayerSettingsClicked")]
    private void OnPlayerSettingsClicked() => PlayerSettingsClicked?.Invoke();

    [UIAction("MicSettingsClicked")]
    private void OnMicSettingsClicked() => MicSettingsClicked?.Invoke();

    [UIAction("FusedModsClicked")]
    private void OnFusedModsClicked() => FusedModsClicked?.Invoke();

    [UIAction("AddonsClicked")]
    private void OnAddonsClicked() => AddonsClicked?.Invoke();

    [UIAction("PerformanceClicked")]
    private void OnPerformanceClicked() => PerformanceClicked?.Invoke();

    [UIAction("#post-parse")]
    private void PostParse()
    {
        MpChatDebugMode.Refresh();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
        ApplyToggleLabels();
        _debugLoggingToggle?.ReceiveValue();
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        MpChatDebugMode.Refresh();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
        ApplyToggleLabels();
        _debugLoggingToggle?.ReceiveValue();
    }

    private void ApplyToggleLabels()
    {
        if (_enableCauToggle != null)
            _enableCauToggle.Text = LabelEnableCau;
        if (_debugLoggingToggle != null)
            _debugLoggingToggle.Text = LabelDebugLogging;
    }

    [UIAction("ApplyClicked")]
    private void OnApplyClicked()
    {
        var cauTgl = _enableCauToggle?.GetComponentInChildren<Toggle>(true);
        if (cauTgl != null)
            ModSettings.EnableCau = cauTgl.isOn;

        var debugTgl = _debugLoggingToggle?.GetComponentInChildren<Toggle>(true);
        if (debugTgl != null)
            ModSettings.DebugLogging = debugTgl.isOn;

        ApplyClicked?.Invoke(this, EventArgs.Empty);
    }
}
