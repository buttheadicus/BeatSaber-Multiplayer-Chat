using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Settings;
using UnityEngine.UI;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.PerformanceSettingsView.bsml")]
public sealed class PerformanceSettingsViewController : BSMLAutomaticViewController
{
    public event Action? PerformanceSettingsApplied;

    private const string LabelLimitIncomingAvatarData = "Limit incoming data during songs";

    [UIComponent("LimitIncomingAvatarDataToggle")] private ToggleSetting? _limitIncomingToggle;

    private bool _draftLimitIncomingAvatarData;

    [UIValue("LimitIncomingAvatarDataDuringSongsDraft")]
    public bool LimitIncomingAvatarDataDuringSongsDraft
    {
        get => _draftLimitIncomingAvatarData;
        set => _draftLimitIncomingAvatarData = value;
    }

    private void ReloadDraftFromDisk()
    {
        _draftLimitIncomingAvatarData = ModSettings.LimitIncomingAvatarDataDuringSongs;
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
        _limitIncomingToggle?.ReceiveValue();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
        ApplyToggleLabel();
    }

    private void ApplyToggleLabel()
    {
        if (_limitIncomingToggle != null)
            _limitIncomingToggle.Text = LabelLimitIncomingAvatarData;
    }

    [UIAction("ApplyClicked")]
    private void OnApplyClicked()
    {
        var tgl = _limitIncomingToggle?.GetComponentInChildren<Toggle>(true);
        if (tgl != null)
            _draftLimitIncomingAvatarData = tgl.isOn;

        ModSettings.LimitIncomingAvatarDataDuringSongs = _draftLimitIncomingAvatarData;
        PerformanceSettingsApplied?.Invoke();
    }
}
