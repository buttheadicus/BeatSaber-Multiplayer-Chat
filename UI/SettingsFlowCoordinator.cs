using System;
using HMUI;
using MultiplayerChat.Core;
using MultiplayerChat.Core.Addons;
using Zenject;

namespace MultiplayerChat.UI;

public class MultiplayerChatSettingsFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly SettingsViewController _settingsViewController = null!;
    [Inject] private readonly PlayerSettingsFlowCoordinator _playerSettingsFlowCoordinator = null!;
    [Inject] private readonly MicSettingsFlowCoordinator _micSettingsFlowCoordinator = null!;
    [Inject] private readonly FusedModsSettingsFlowCoordinator _fusedModsSettingsFlowCoordinator = null!;
    [Inject] private readonly PerformanceSettingsFlowCoordinator _performanceSettingsFlowCoordinator = null!;

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
            _settingsViewController.PlayerSettingsClicked += OnPlayerSettingsClicked;
            _settingsViewController.MicSettingsClicked += OnMicSettingsClicked;
            _settingsViewController.FusedModsClicked += OnFusedModsClicked;
            _settingsViewController.AddonsClicked += OnAddonsClicked;
            _settingsViewController.PerformanceClicked += OnPerformanceClicked;
        }
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
        {
            _settingsViewController.ApplyClicked -= OnApply;
            _settingsViewController.PlayerSettingsClicked -= OnPlayerSettingsClicked;
            _settingsViewController.MicSettingsClicked -= OnMicSettingsClicked;
            _settingsViewController.FusedModsClicked -= OnFusedModsClicked;
            _settingsViewController.AddonsClicked -= OnAddonsClicked;
            _settingsViewController.PerformanceClicked -= OnPerformanceClicked;
        }
    }

    private void OnApply(object? sender, EventArgs e) => Dismiss();

    private void OnPlayerSettingsClicked()
    {
        var child = FlowCoordinatorHelper.GetChildFlowCoordinator(this);
        if (child == _playerSettingsFlowCoordinator)
            return;

        _playerSettingsFlowCoordinator.ParentFlow = this;
        PresentFlowCoordinator(_playerSettingsFlowCoordinator);
    }

    private void OnMicSettingsClicked()
    {
        var child = FlowCoordinatorHelper.GetChildFlowCoordinator(this);
        if (child == _micSettingsFlowCoordinator)
            return;

        _micSettingsFlowCoordinator.ParentFlow = this;
        PresentFlowCoordinator(_micSettingsFlowCoordinator);
    }

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
        var addonsFlow = AddonUiBridge.AddonsSettingsFlow;
        if (addonsFlow == null)
        {
            Plugin.Log?.Warn("[MPChat][Addons] Addons settings are not ready yet.");
            return;
        }

        var child = FlowCoordinatorHelper.GetChildFlowCoordinator(this);
        if (child == addonsFlow)
            return;

        addonsFlow.GetType().GetProperty("ParentFlow")?.SetValue(addonsFlow, this);
        PresentFlowCoordinator(addonsFlow);
    }

    private void OnPerformanceClicked()
    {
        var child = FlowCoordinatorHelper.GetChildFlowCoordinator(this);
        if (child == _performanceSettingsFlowCoordinator)
            return;

        _performanceSettingsFlowCoordinator.ParentFlow = this;
        PresentFlowCoordinator(_performanceSettingsFlowCoordinator);
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
