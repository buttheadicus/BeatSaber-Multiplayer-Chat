using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.ExperimentalWarningView.bsml")]
public sealed class ExperimentalWarningViewController : BSMLAutomaticViewController
{
    public event Action? Accepted;

    [UIAction("#post-parse")]
    private void PostParse() => BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
    }

    [UIAction("AcceptClicked")]
    private void OnAcceptClicked() => Accepted?.Invoke();
}
