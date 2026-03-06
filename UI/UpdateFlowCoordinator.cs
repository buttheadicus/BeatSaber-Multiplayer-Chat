using System;
using HMUI;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// FlowCoordinator that presents only the update message (no chat input).
/// </summary>
public class UpdateFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly UpdateMessageViewController _updateViewController = null!;

    public void SetMessage(string? message)
    {
        _updateViewController.SetMessage(message);
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            SetTitle("Multiplayer Chat Update");
            showBackButton = true;
        }

        if (addedToHierarchy)
        {
            _updateViewController.CloseClicked += OnClose;
            ProvideInitialViewControllers(_updateViewController);
        }
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
            _updateViewController.CloseClicked -= OnClose;
    }

    private void OnClose(object? sender, EventArgs e) => Dismiss();

    protected override void BackButtonWasPressed(ViewController topViewController)
    {
        Dismiss();
    }

    private void Dismiss()
    {
        BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
    }
}
