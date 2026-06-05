using System;
using HMUI;
using Zenject;

namespace MultiplayerChat.UI;

public sealed class MicSettingsFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly MicSettingsViewController _micSettingsView = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            showBackButton = true;
            SetTitle("Mic Settings");
            ProvideInitialViewControllers(_micSettingsView);
        }

        if (addedToHierarchy)
            _micSettingsView.MicSettingsApplied += OnApplied;
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
            _micSettingsView.MicSettingsApplied -= OnApplied;
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
