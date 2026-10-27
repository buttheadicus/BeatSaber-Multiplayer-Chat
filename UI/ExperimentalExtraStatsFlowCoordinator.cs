using HMUI;
using Zenject;

namespace MultiplayerChat.UI;

public sealed class ExperimentalExtraStatsFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly ExperimentalExtraStatsViewController _view = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            showBackButton = true;
            SetTitle("Extra Stats");
            ProvideInitialViewControllers(_view);
        }

        if (addedToHierarchy)
            _view.ExtraStatsApplied += OnApplied;
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
            _view.ExtraStatsApplied -= OnApplied;
    }

    private void OnApplied() => Dismiss();

    protected override void BackButtonWasPressed(ViewController topViewController) => Dismiss();

    private void Dismiss()
    {
        if (ParentFlow != null)
            ParentFlow.DismissFlowCoordinator(this);
        else
            BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
    }
}
