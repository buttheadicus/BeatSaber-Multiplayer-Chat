using System;
using System.Collections.Generic;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerChat.UI;

/// <summary>
/// Settings view for Multiplayer Chat. Name color and bubble duration (move chat UI hidden for now).
/// </summary>
[ViewDefinition("MultiplayerChat.UI.SettingsView.bsml")]
public class SettingsViewController : BSMLAutomaticViewController
{
    public event EventHandler? ApplyClicked;

    [UIComponent("BubbleDuration")]
    private BeatSaberMarkupLanguage.Components.Settings.SliderSetting? _bubbleDurationSlider;

    [UIComponent("NameColorInput")]
    private StringSetting? _nameColorInput;

    [UIComponent("ChatBubbleSoundsToggle")]
    private ToggleSetting? _chatBubbleSoundsToggle;

    [UIComponent("AvatarExtensionsToggle")]
    private ToggleSetting? _avatarExtensionsToggle;

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

    [UIValue("EnableAvatarExtensions")]
    public bool EnableAvatarExtensions
    {
        get => ModSettings.EnableAvatarExtensions;
        set => ModSettings.EnableAvatarExtensions = value;
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

        var extTgl = _avatarExtensionsToggle?.GetComponentInChildren<Toggle>(true);
        if (extTgl != null)
            ModSettings.EnableAvatarExtensions = extTgl.isOn;

        ApplyClicked?.Invoke(this, EventArgs.Empty);
    }
}
