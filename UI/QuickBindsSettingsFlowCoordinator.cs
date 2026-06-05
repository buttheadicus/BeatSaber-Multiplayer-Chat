using System;
using HMUI;
using Zenject;

namespace MultiplayerChat.UI;

public sealed class QuickBindsSettingsFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly QuickBindsSettingsViewController _quickBindsView = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            showBackButton = true;
            SetTitle("Quick Binds");
            ProvideInitialViewControllers(_quickBindsView);
        }

        if (addedToHierarchy)
            _quickBindsView.QuickBindsSettingsApplied += OnApplied;
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
            _quickBindsView.QuickBindsSettingsApplied -= OnApplied;
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
