using HMUI;
using Zenject;

namespace MultiplayerChat.UI;

public sealed class ExperimentalChatSongFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly ExperimentalChatSongViewController _view = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (!firstActivation)
            return;
        showBackButton = true;
        SetTitle("Chat for non chat players");
        ProvideInitialViewControllers(_view);
    }

    protected override void BackButtonWasPressed(ViewController topViewController) => Dismiss();

    private void Dismiss()
    {
        if (ParentFlow != null)
            ParentFlow.DismissFlowCoordinator(this);
        else
            BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
    }
}
