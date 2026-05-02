using System;
using HMUI;
using Zenject;

namespace MultiplayerChat.UI;

public class FusedModsSettingsFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly FusedModsSettingsViewController _fusedModsView = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            showBackButton = true;
            SetTitle("Fused Mods");
            ProvideInitialViewControllers(_fusedModsView);
        }

        if (addedToHierarchy)
            _fusedModsView.FusedModsSettingsApplied += OnFusedModsSettingsApplied;
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
            _fusedModsView.FusedModsSettingsApplied -= OnFusedModsSettingsApplied;
    }

    private void OnFusedModsSettingsApplied() => Dismiss();

    protected override void BackButtonWasPressed(ViewController topViewController) => Dismiss();

    private void Dismiss()
    {
        if (ParentFlow != null)
            ParentFlow.DismissFlowCoordinator(this);
        else
            BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
    }
}
