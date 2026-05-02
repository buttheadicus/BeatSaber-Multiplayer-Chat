using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Settings;
using UnityEngine.UI;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.FusedModsSettingsView.bsml")]
public class FusedModsSettingsViewController : BSMLAutomaticViewController
{
    /// <summary>Fired after fused-mod toggles are written to <see cref="ModSettings"/>.</summary>
    public event Action? FusedModsSettingsApplied;

    [UIComponent("AvatarExtensionsToggle")] private ToggleSetting? _avatarExtensionsToggle;

    private bool _avatarExtrasDraft;

    /// <summary>Unchanged copy from main settings (Avatar Extras row).</summary>
    private const string LabelAvatarExtras =
        "Enable Avatar Extras (may affect performance; restart required, not finalized yet)";

    [UIValue("EnableAvatarExtensionsDraft")]
    public bool EnableAvatarExtensionsDraft
    {
        get => _avatarExtrasDraft;
        set => _avatarExtrasDraft = value;
    }

    private void ReloadDraftFromDisk()
    {
        _avatarExtrasDraft = ModSettings.EnableAvatarExtensions;
    }

    [UIAction("#post-parse")]
    private void PostParse()
    {
        ReloadDraftFromDisk();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
        ApplyAvatarExtrasToggleLabel();
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        ReloadDraftFromDisk();
        _avatarExtensionsToggle?.ReceiveValue();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
        ApplyAvatarExtrasToggleLabel();
    }

    private void ApplyAvatarExtrasToggleLabel()
    {
        if (_avatarExtensionsToggle != null)
            _avatarExtensionsToggle.Text = LabelAvatarExtras;
    }

    [UIAction("ApplyClicked")]
    private void OnApplyClicked()
    {
        if (_avatarExtensionsToggle != null && _avatarExtensionsToggle.Toggle != null)
            _avatarExtrasDraft = _avatarExtensionsToggle.Toggle.isOn;
        ModSettings.EnableAvatarExtensions = _avatarExtrasDraft;
        FusedModsSettingsApplied?.Invoke();
    }
}
