using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Settings;
using UnityEngine.UI;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.AddonsSettingsView.bsml")]
public sealed class AddonsSettingsViewController : BSMLAutomaticViewController
{
    public event Action? AddonsSettingsApplied;

    private const string LabelAvatarColoringExtensions = "Avatar Coloring Extentions";

    [UIComponent("AvatarColoringToggle")] private ToggleSetting? _avatarColoringToggle;

    private bool _draftAvatarColoringExtensions;

    [UIValue("EnableAvatarColoringExtensionsDraft")]
    public bool EnableAvatarColoringExtensionsDraft
    {
        get => _draftAvatarColoringExtensions;
        set => _draftAvatarColoringExtensions = value;
    }

    private void ReloadDraftFromDisk()
    {
        _draftAvatarColoringExtensions = ModSettings.EnableAvatarColoringExtensions;
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
        _avatarColoringToggle?.ReceiveValue();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
        ApplyToggleLabel();
    }

    private void ApplyToggleLabel()
    {
        if (_avatarColoringToggle != null)
            _avatarColoringToggle.Text = LabelAvatarColoringExtensions;
    }

    [UIAction("ApplyClicked")]
    private void OnApplyClicked()
    {
        var tgl = _avatarColoringToggle?.GetComponentInChildren<Toggle>(true);
        if (tgl != null)
            _draftAvatarColoringExtensions = tgl.isOn;

        ModSettings.EnableAvatarColoringExtensions = _draftAvatarColoringExtensions;

        AddonsSettingsApplied?.Invoke();
    }
}
