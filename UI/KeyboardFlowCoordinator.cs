using System;
using HMUI;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// FlowCoordinator that presents the keyboard chat view.
/// Per BSMG wiki: present via MainFlowCoordinator when MenuButton is clicked.
/// </summary>
public class KeyboardFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly KeyboardViewController _keyboardViewController = null!;

    public void SetUpdateMessage(string? message)
    {
        _keyboardViewController.SetUpdateMessage(message);
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            SetTitle("TEXT CHAT");
            showBackButton = true;
        }

        if (addedToHierarchy)
        {
            _keyboardViewController.TextSubmitted += OnSubmitted;
            _keyboardViewController.Cancelled += OnCancelled;
            ProvideInitialViewControllers(_keyboardViewController);
        }
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
        {
            _keyboardViewController.TextSubmitted -= OnSubmitted;
            _keyboardViewController.Cancelled -= OnCancelled;
        }
    }

    private void OnSubmitted(object? sender, string e) => Dismiss();
    private void OnCancelled(object? sender, EventArgs e) => Dismiss();

    private void Dismiss()
    {
        BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
    }

    protected override void BackButtonWasPressed(ViewController topViewController)
    {
        Dismiss();
    }
}
