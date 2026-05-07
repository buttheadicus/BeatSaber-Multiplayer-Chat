using System;
using System.Collections;
using System.Collections.Generic;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Settings;
using UnityEngine;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.VoiceSettingsView.bsml")]
public class VoiceSettingsViewController : BSMLAutomaticViewController
{
    private const string MicDefaultLabel = "Windows Default";

    public event EventHandler? ApplyClicked;

    public event Action? ConfigureLowerVolumeWhenSpeakingClicked;

    [UIComponent("MicInput")] private DropDownListSetting? _micInput;

    [UIComponent("PttBindDropdown")] private DropDownListSetting? _pttDropdown;

    [UIComponent("PushToTalkToggle")] private ToggleSetting? _pushToTalkToggle;

    [UIComponent("MuteMicDuringSongToggle")] private ToggleSetting? _muteMicDuringSongToggle;

    [UIComponent("DeafDuringSongToggle")] private ToggleSetting? _deafDuringSongToggle;

    private readonly List<object> _micOptionObjects = new() { MicDefaultLabel };
    private readonly List<object> _pttOptionObjects = new() { "Primary", "Secondary", "Trigger", "Grip" };

    [UIValue("MicOptions")]
    public IList MicOptions => _micOptionObjects;

    [UIValue("PttOptions")]
    public IList PttOptions => _pttOptionObjects;

    [UIValue("PushToTalk")]
    public bool PushToTalk
    {
        get => ModSettings.PushToTalkEnabled;
        set => ModSettings.PushToTalkEnabled = value;
    }

    [UIValue("MuteMicDuringSong")]
    public bool MuteMicDuringSong
    {
        get => ModSettings.MuteMicDuringSongPlaying;
        set => ModSettings.MuteMicDuringSongPlaying = value;
    }

    [UIValue("DeafDuringSong")]
    public bool DeafDuringSong
    {
        get => ModSettings.DeafDuringSongPlaying;
        set => ModSettings.DeafDuringSongPlaying = value;
    }

    [UIAction("#post-parse")]
    private void PostParse()
    {
        BuildMicList();
        BuildPttDropdown();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        BuildMicList();
        BuildPttDropdown();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
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

    private void BuildPttDropdown()
    {
        if (_pttDropdown == null) return;
        _pttDropdown.Values = _pttOptionObjects;
        _pttDropdown.UpdateChoices();
        var idx = ModSettings.PttBindingIndex;
        if (idx >= 0 && idx < _pttOptionObjects.Count)
            _pttDropdown.Value = _pttOptionObjects[idx];
        _pttDropdown.ReceiveValue();
    }

    [UIAction("ConfigureLowerVolumeWhenSpeakingClicked")]
    private void OnConfigureLowerVolumeWhenSpeakingClicked() => ConfigureLowerVolumeWhenSpeakingClicked?.Invoke();

    [UIAction("ApplyClicked")]
    private void OnApplyClicked()
    {
        if (_micInput != null)
        {
            var v = _micInput.Value?.ToString() ?? "";
            ModSettings.MicInputDeviceName = v == MicDefaultLabel || string.IsNullOrEmpty(v) ? "" : v;
        }

        if (_pttDropdown != null)
        {
            var s = _pttDropdown.Value?.ToString() ?? "Primary";
            ModSettings.PttBindingIndex = s switch
            {
                "Secondary" => 1,
                "Trigger" => 2,
                "Grip" => 3,
                _ => 0
            };
        }

        // BSML toggles do not always sync [UIValue] back to PlayerPrefs until Apply; persist explicitly from components.
        if (_pushToTalkToggle != null)
            ModSettings.PushToTalkEnabled = _pushToTalkToggle.Value;

        if (_muteMicDuringSongToggle != null)
            ModSettings.MuteMicDuringSongPlaying = _muteMicDuringSongToggle.Value;

        if (_deafDuringSongToggle != null)
            ModSettings.DeafDuringSongPlaying = _deafDuringSongToggle.Value;

        ApplyClicked?.Invoke(this, EventArgs.Empty);
    }
}
