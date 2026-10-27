using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.ExperimentalSettingsView.bsml")]
public sealed class ExperimentalSettingsViewController : BSMLAutomaticViewController
{
    public event Action? ChatSongClicked;
    public event Action? SkinSnatcherClicked;
    public event Action? ExtraStatsClicked;

    [UIAction("#post-parse")]
    private void PostParse() => BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
    }

    [UIAction("ChatSongClicked")]
    private void OnChatSongClicked() => ChatSongClicked?.Invoke();

    [UIAction("SkinSnatcherClicked")]
    private void OnSkinSnatcherClicked() => SkinSnatcherClicked?.Invoke();

    [UIAction("ExtraStatsClicked")]
    private void OnExtraStatsClicked() => ExtraStatsClicked?.Invoke();
}
