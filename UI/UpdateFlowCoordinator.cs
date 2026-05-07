using System;
using HMUI;
using Zenject;

namespace MultiplayerChat.UI;

public class UpdateFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly UpdateMessageViewController _updateViewController = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }

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
            ProvideInitialViewControllers(_updateViewController);
        }
        if (addedToHierarchy)
            _updateViewController.CloseClicked += OnClose;
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
        if (ParentFlow != null)
            ParentFlow.DismissFlowCoordinator(this);
        else
            BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
    }
}
