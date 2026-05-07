using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.SettingsView.bsml")]
public class SettingsViewController : BSMLAutomaticViewController
{
    private const string LabelChatBubbleSounds = "Chat bubble sounds";
    private const string LabelEnableCau = "Enable CAU";
    private const string LabelDebugLogging = "Debug mode (verbose logs)";

    public event EventHandler? ApplyClicked;

    public event Action? FusedModsClicked;

    public event Action? AddonsClicked;

    [UIComponent("BubbleDuration")]
    private BeatSaberMarkupLanguage.Components.Settings.SliderSetting? _bubbleDurationSlider;

    [UIComponent("NameColorInput")]
    private StringSetting? _nameColorInput;

    [UIComponent("ChatBubbleSoundsToggle")]
    private ToggleSetting? _chatBubbleSoundsToggle;

    [UIComponent("EnableCauToggle")]
    private ToggleSetting? _enableCauToggle;

    [UIComponent("DebugLoggingToggle")]
    private ToggleSetting? _debugLoggingToggle;

    [UIValue("BubbleDuration")]
    private float BubbleDuration
    {
        get => ModSettings.BubbleDuration;
        set => ModSettings.BubbleDuration = value;
    }

    [UIValue("NameColor")]
    private string NameColor
    {
        get => ModSettings.NameColor;
        set
        {
            var hex = (value ?? "").Trim();
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length > 6) hex = hex.Substring(0, 6);
            ModSettings.NameColor = hex;
        }
    }

    [UIValue("ChatBubbleSoundsEnabled")]
    public bool ChatBubbleSoundsEnabled
    {
        get => ModSettings.ChatBubbleSoundsEnabled;
        set => ModSettings.ChatBubbleSoundsEnabled = value;
    }

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

    [UIAction("FusedModsClicked")]
    private void OnFusedModsClicked() => FusedModsClicked?.Invoke();

    [UIAction("AddonsClicked")]
    private void OnAddonsClicked() => AddonsClicked?.Invoke();

    [UIAction("#post-parse")]
    private void PostParse()
    {
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
        ApplyMainSettingsToggleLabels();
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        if (_nameColorInput != null)
        {
            var hex = ModSettings.NameColor ?? "";
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            _nameColorInput.Text = hex;
        }

        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
        ApplyMainSettingsToggleLabels();
    }

    private void ApplyMainSettingsToggleLabels()
    {
        if (_chatBubbleSoundsToggle != null)
            _chatBubbleSoundsToggle.Text = LabelChatBubbleSounds;
        if (_enableCauToggle != null)
            _enableCauToggle.Text = LabelEnableCau;
        if (_debugLoggingToggle != null)
            _debugLoggingToggle.Text = LabelDebugLogging;
    }

    [UIAction("ApplyClicked")]
    private void OnApplyClicked()
    {
        if (_bubbleDurationSlider != null)
            ModSettings.BubbleDuration = _bubbleDurationSlider.Value;
        if (_nameColorInput != null)
        {
            var hex = (_nameColorInput.Text ?? "").Trim();
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length > 6) hex = hex.Substring(0, 6);
            ModSettings.NameColor = hex;
        }
        var tgl = _chatBubbleSoundsToggle?.GetComponentInChildren<Toggle>(true);
        if (tgl != null)
            ModSettings.ChatBubbleSoundsEnabled = tgl.isOn;

        var cauTgl = _enableCauToggle?.GetComponentInChildren<Toggle>(true);
        if (cauTgl != null)
            ModSettings.EnableCau = cauTgl.isOn;

        var debugTgl = _debugLoggingToggle?.GetComponentInChildren<Toggle>(true);
        if (debugTgl != null)
            ModSettings.DebugLogging = debugTgl.isOn;

        ApplyClicked?.Invoke(this, EventArgs.Empty);
    }
}
