using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Settings;
using TMPro;
using UnityEngine;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.VoiceDuckSettingsView.bsml")]
public class VoiceDuckSettingsViewController : BSMLAutomaticViewController
{
    private const int DuckVolumeStepPercent = 5;

    /// <summary>Fired after duck settings are written to <see cref="ModSettings"/> (parent coordinator may dismiss).</summary>
    public event Action? DuckSettingsApplied;

    [UIComponent("DuckEnabledToggle")] private ToggleSetting? _duckEnabledToggle;
    [UIComponent("DuckVolumeStepLabel")] private TMP_Text? _duckVolumeStepLabel;

    private bool _duckEnabledDraft;
    private int _duckTargetDraftPercent;

    [UIValue("DuckEnabledDraft")]
    public bool DuckEnabledDraft
    {
        get => _duckEnabledDraft;
        set => _duckEnabledDraft = value;
    }

    private void ReloadDraftsFromDisk()
    {
        _duckEnabledDraft = ModSettings.VoiceDuckingEnabled;
        _duckTargetDraftPercent = ModSettings.VoiceDuckTargetPercent;
    }

    [UIAction("#post-parse")]
    private void PostParse()
    {
        ReloadDraftsFromDisk();
        RefreshDuckVolumeLabel();
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        ReloadDraftsFromDisk();
        _duckEnabledToggle?.ReceiveValue();
        RefreshDuckVolumeLabel();
    }

    [UIAction("DuckVolumeDownClicked")]
    private void DuckVolumeDownClicked()
    {
        _duckTargetDraftPercent = Mathf.Clamp(_duckTargetDraftPercent - DuckVolumeStepPercent, 5, 100);
        RefreshDuckVolumeLabel();
    }

    [UIAction("DuckVolumeUpClicked")]
    private void DuckVolumeUpClicked()
    {
        _duckTargetDraftPercent = Mathf.Clamp(_duckTargetDraftPercent + DuckVolumeStepPercent, 5, 100);
        RefreshDuckVolumeLabel();
    }

    [UIAction("ApplyClicked")]
    private void ApplyClicked()
    {
        // ToggleSetting does not always push BSML [UIValue] on user interaction unless UpdateOnChange is enabled — read Unity toggle directly.
        if (_duckEnabledToggle != null && _duckEnabledToggle.Toggle != null)
            _duckEnabledDraft = _duckEnabledToggle.Toggle.isOn;
        ModSettings.VoiceDuckingEnabled = _duckEnabledDraft;
        ModSettings.VoiceDuckTargetPercent = _duckTargetDraftPercent;
        DuckSettingsApplied?.Invoke();
    }

    private void RefreshDuckVolumeLabel()
    {
        if (_duckVolumeStepLabel != null)
            _duckVolumeStepLabel.text = $"{_duckTargetDraftPercent}%";
    }
}
