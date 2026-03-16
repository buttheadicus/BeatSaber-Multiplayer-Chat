using System;
using HMUI;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// FlowCoordinator that presents the Multiplayer Chat settings view.
/// Renamed from SettingsFlowCoordinator to avoid Zenject conflict with Beat Saber's type.
/// </summary>
public class MultiplayerChatSettingsFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly SettingsViewController _settingsViewController = null!;

    /// <summary>Parent to dismiss from; set before PresentFlowCoordinator when presenting from lobby.</summary>
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
            _settingsViewController.ApplyClicked += OnApply;
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
            _settingsViewController.ApplyClicked -= OnApply;
        // Do not destroy - instance is reused to prevent overlap when reopening
    }

    private void OnApply(object? sender, EventArgs e) => Dismiss();

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
