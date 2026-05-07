using System;
using HMUI;
using Zenject;

namespace MultiplayerChat.UI;

public sealed class AddonsSettingsFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly AddonsSettingsViewController _addonsView = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            showBackButton = true;
            SetTitle("Addons");
            ProvideInitialViewControllers(_addonsView);
        }

        if (addedToHierarchy)
            _addonsView.AddonsSettingsApplied += OnAddonsSettingsApplied;
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
            _addonsView.AddonsSettingsApplied -= OnAddonsSettingsApplied;
    }

    private void OnAddonsSettingsApplied() => Dismiss();

    protected override void BackButtonWasPressed(ViewController topViewController) => Dismiss();

    private void Dismiss()
    {
        if (ParentFlow != null)
            ParentFlow.DismissFlowCoordinator(this);
        else
            BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
    }
}
