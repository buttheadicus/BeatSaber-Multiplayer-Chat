using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Contracts;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.AddonsSettingsView.bsml")]
public sealed class AddonsSettingsViewController : BSMLAutomaticViewController
{
    public event Action<string>? AddonClicked;

    [UIAction("#post-parse")]
    private void PostParse() => BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
    }

    [UIAction("QuickBindsClicked")]
    private void OnQuickBindsClicked() => AddonClicked?.Invoke(AddonIds.QuickBinds);

    [UIAction("AvatarColoringClicked")]
    private void OnAvatarColoringClicked() => AddonClicked?.Invoke(AddonIds.AvatarColoring);

    [UIAction("CustomAvatarsClicked")]
    private void OnCustomAvatarsClicked() => AddonClicked?.Invoke(AddonIds.CustomAvatars);
}
