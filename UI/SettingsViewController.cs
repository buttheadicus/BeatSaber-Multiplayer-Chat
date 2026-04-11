using System;
using System.Collections;
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
    private const string MicDefaultLabel = "Default (system)";

    public event EventHandler? ApplyClicked;

    [UIComponent("BubbleDuration")]
    private BeatSaberMarkupLanguage.Components.Settings.SliderSetting? _bubbleDurationSlider;

    [UIComponent("NameColorInput")]
    private StringSetting? _nameColorInput;

    [UIComponent("ChatBubbleSoundsToggle")]
    private ToggleSetting? _chatBubbleSoundsToggle;

    [UIComponent("MicInput")]
    private DropDownListSetting? _micInput;

    private readonly List<object> _micOptionObjects = new() { MicDefaultLabel };

    /// <summary>Required by BSML <c>dropdown-list-setting</c> <c>options</c> binding; rebuilt when the view opens.</summary>
    [UIValue("MicOptions")]
    public IList MicOptions => _micOptionObjects;

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

    [UIAction("#post-parse")]
    private void PostParse()
    {
        BuildMicList();
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        BuildMicList();
    }

    private void BuildMicList()
    {
        if (_micInput == null) return;

        _micOptionObjects.Clear();
        _micOptionObjects.Add(MicDefaultLabel);
        foreach (var d in Microphone.devices ?? Array.Empty<string>())
            _micOptionObjects.Add(d);

        _micInput.Values = _micOptionObjects;
        _micInput.UpdateChoices();

        var saved = ModSettings.MicInputDeviceName;
        if (string.IsNullOrEmpty(saved))
            _micInput.Value = MicDefaultLabel;
        else
        {
            var found = false;
            foreach (var d in Microphone.devices ?? Array.Empty<string>())
            {
                if (d != saved) continue;
                _micInput.Value = saved;
                found = true;
                break;
            }

            if (!found)
                _micInput.Value = MicDefaultLabel;
        }

        _micInput.ReceiveValue();
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

        if (_micInput != null)
        {
            var v = _micInput.Value?.ToString() ?? "";
            ModSettings.MicInputDeviceName = v == MicDefaultLabel || string.IsNullOrEmpty(v) ? "" : v;
        }

        ApplyClicked?.Invoke(this, EventArgs.Empty);
    }
}
