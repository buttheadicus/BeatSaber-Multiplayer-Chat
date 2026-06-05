using System;
using HMUI;
using Zenject;

namespace MultiplayerChat.UI;

public sealed class PlayerSettingsFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly PlayerSettingsViewController _playerSettingsView = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            showBackButton = true;
            SetTitle("Player Settings");
            ProvideInitialViewControllers(_playerSettingsView);
        }

        if (addedToHierarchy)
            _playerSettingsView.PlayerSettingsApplied += OnApplied;
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
            _playerSettingsView.PlayerSettingsApplied -= OnApplied;
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
