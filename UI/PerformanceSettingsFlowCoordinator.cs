using System;
using HMUI;
using Zenject;

namespace MultiplayerChat.UI;

public sealed class PerformanceSettingsFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly PerformanceSettingsViewController _performanceView = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            showBackButton = true;
            SetTitle("Performance");
            ProvideInitialViewControllers(_performanceView);
        }

        if (addedToHierarchy)
            _performanceView.PerformanceSettingsApplied += OnApplied;
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
            _performanceView.PerformanceSettingsApplied -= OnApplied;
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
