using System;
using HMUI;
using MultiplayerChat.Core;
using Zenject;

namespace MultiplayerChat.UI;

public class MultiplayerChatSettingsFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly SettingsViewController _settingsViewController = null!;
    [Inject] private readonly FusedModsSettingsFlowCoordinator _fusedModsSettingsFlowCoordinator = null!;
    [Inject] private readonly AddonsSettingsFlowCoordinator _addonsSettingsFlowCoordinator = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            showBackButton = true;
            SetTitle("Multiplayer Chat Settings");
            ProvideInitialViewControllers(_settingsViewController);
        }
        if (addedToHierarchy)
        {
            _settingsViewController.ApplyClicked += OnApply;
            _settingsViewController.FusedModsClicked += OnFusedModsClicked;
            _settingsViewController.AddonsClicked += OnAddonsClicked;
        }
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
        {
            _settingsViewController.ApplyClicked -= OnApply;
            _settingsViewController.FusedModsClicked -= OnFusedModsClicked;
            _settingsViewController.AddonsClicked -= OnAddonsClicked;
        }
        // Do not destroy - instance is reused to prevent overlap when reopening
    }

    private void OnApply(object? sender, EventArgs e) => Dismiss();

    private void OnFusedModsClicked()
    {
        var child = FlowCoordinatorHelper.GetChildFlowCoordinator(this);
        if (child == _fusedModsSettingsFlowCoordinator)
            return;

        _fusedModsSettingsFlowCoordinator.ParentFlow = this;
        PresentFlowCoordinator(_fusedModsSettingsFlowCoordinator);
    }

    private void OnAddonsClicked()
    {
        var child = FlowCoordinatorHelper.GetChildFlowCoordinator(this);
        if (child == _addonsSettingsFlowCoordinator)
            return;

        _addonsSettingsFlowCoordinator.ParentFlow = this;
        PresentFlowCoordinator(_addonsSettingsFlowCoordinator);
    }

    protected override void BackButtonWasPressed(ViewController topViewController)
    {
        Dismiss();
    }

    private void Dismiss()
    {
        if (ParentFlow != null)
            ParentFlow.DismissFlowCoordinator(this);
        else
            BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
    }
}
