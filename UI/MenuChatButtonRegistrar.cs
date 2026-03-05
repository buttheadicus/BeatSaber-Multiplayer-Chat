using System;
using BeatSaberMarkupLanguage.MenuButtons;
using HMUI;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// Registers a "TEXT CHAT" MenuButton in the main menu left panel.
/// Per BSMG wiki: presents FlowCoordinator when clicked via MainFlowCoordinator.
/// </summary>
public class MenuChatButtonRegistrar : IInitializable, IDisposable
{
    [Inject] private readonly MainFlowCoordinator _mainFlowCoordinator = null!;
    [Inject] private readonly KeyboardFlowCoordinator _keyboardFlowCoordinator = null!;

    private MenuButton? _menuButton;

    public void Initialize()
    {
        _menuButton = new MenuButton(
            "TEXT CHAT",
            "Open text chat (E2E encrypted). Join a multiplayer lobby to send messages.",
            OnMenuButtonClicked);
        MenuButtons.Instance.RegisterButton(_menuButton);
    }

    public void Dispose()
    {
        if (_menuButton != null)
        {
            MenuButtons.Instance.UnregisterButton(_menuButton);
            _menuButton = null;
        }
    }

    private void OnMenuButtonClicked()
    {
        _mainFlowCoordinator.PresentFlowCoordinator(_keyboardFlowCoordinator);
    }
}
