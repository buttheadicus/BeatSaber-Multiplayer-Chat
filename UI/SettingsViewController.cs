using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Core;
using MultiplayerChat.Settings;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// Settings view for Multiplayer Chat. Name color and other options.
/// </summary>
[ViewDefinition("MultiplayerChat.UI.SettingsView.bsml")]
public class SettingsViewController : BSMLAutomaticViewController
{
    public event EventHandler? ApplyClicked;

    [UIComponent("BubbleDuration")]
    private BeatSaberMarkupLanguage.Components.Settings.SliderSetting? _bubbleDurationSlider;

    [UIComponent("NameColorInput")]
    private BeatSaberMarkupLanguage.Components.Settings.StringSetting? _nameColorInput;

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
        ApplyClicked?.Invoke(this, EventArgs.Empty);
    }
}
