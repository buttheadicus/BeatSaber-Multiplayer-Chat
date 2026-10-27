using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.ExperimentalExtraStatsView.bsml")]
public sealed class ExperimentalExtraStatsViewController : BSMLAutomaticViewController
{
    public event Action? ExtraStatsApplied;

    [UIComponent("ExtraStatsToggle")] private ToggleSetting? _extraStatsToggle;
    [UIComponent("ExtraStatsHeightSlider")] private SliderSetting? _extraStatsHeightSlider;

    private bool _draftEnabled;
    private float _draftHeight;

    [UIValue("ExtraStatsEnabledDraft")]
    public bool ExtraStatsEnabledDraft
    {
        get => _draftEnabled;
        set => _draftEnabled = value;
    }

    [UIValue("ExtraStatsHeightMetersDraft")]
    public float ExtraStatsHeightMetersDraft
    {
        get => _draftHeight;
        set => _draftHeight = value;
    }

    private void ReloadDraftFromDisk()
    {
        _draftEnabled = ModSettings.EnableExtraStats;
        _draftHeight = ModSettings.ExtraStatsHeightMeters;
    }

    [UIAction("#post-parse")]
    private void PostParse()
    {
        ReloadDraftFromDisk();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
        ApplyToggleLabel();
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        ReloadDraftFromDisk();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
        ApplyToggleLabel();
        _extraStatsToggle?.ReceiveValue();
        _extraStatsHeightSlider?.ReceiveValue();
    }

    private void ApplyToggleLabel()
    {
        if (_extraStatsToggle != null)
            _extraStatsToggle.Text = "Enable Extra Stats";
    }

    [UIAction("ApplyClicked")]
    private void OnApplyClicked()
    {
        var tgl = _extraStatsToggle?.GetComponentInChildren<Toggle>(true);
        if (tgl != null)
            _draftEnabled = tgl.isOn;

        if (_extraStatsHeightSlider != null)
            _draftHeight = Mathf.Clamp(_extraStatsHeightSlider.Value, 10f, 40f);

        ModSettings.EnableExtraStats = _draftEnabled;
        ModSettings.ExtraStatsHeightMeters = _draftHeight;
        ExtraStatsApplied?.Invoke();
    }
}
