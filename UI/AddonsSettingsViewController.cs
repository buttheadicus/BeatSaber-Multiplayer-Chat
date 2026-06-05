using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.AddonsSettingsView.bsml")]
public sealed class AddonsSettingsViewController : BSMLAutomaticViewController
{
    public event Action? AddonsSettingsApplied;

    public event Action? CustomAvatarsClicked;

    public event Action? QuickBindsClicked;

    public event Action? AvatarColoringClicked;

    [UIAction("#post-parse")]
    private void PostParse() => BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
    }

    [UIAction("AvatarColoringClicked")]
    private void OnAvatarColoringClicked() => AvatarColoringClicked?.Invoke();

    [UIAction("CustomAvatarsClicked")]
    private void OnCustomAvatarsClicked() => CustomAvatarsClicked?.Invoke();

    [UIAction("QuickBindsClicked")]
    private void OnQuickBindsClicked() => QuickBindsClicked?.Invoke();

    [UIAction("ApplyClicked")]
    private void OnApplyClicked() => AddonsSettingsApplied?.Invoke();
}
