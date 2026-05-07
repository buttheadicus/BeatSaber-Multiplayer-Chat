using System;
using BeatSaberMarkupLanguage.MenuButtons;
using HMUI;
using MultiplayerChat.UI;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core;

public class SettingsMenuButton : MonoBehaviour, IInitializable, IDisposable
{
    [Inject] private readonly DiContainer _container = null!;
    [Inject] private readonly MainFlowCoordinator _mainFlowCoordinator = null!;

    private MenuButton? _updateMenuButton;

    public void Initialize()
    {
        _updateMenuButton = new MenuButton("Multiplayer Chat Update", "Check for updates", OnUpdateClicked);
        MenuButtons.Instance.RegisterButton(_updateMenuButton);
    }

    public void Dispose()
    {
        if (_updateMenuButton != null)
        {
            try { MenuButtons.Instance?.UnregisterButton(_updateMenuButton); } catch { }
            _updateMenuButton = null;
        }
    }

    private void OnUpdateClicked()
    {
        var fc = _container.InstantiateComponentOnNewGameObject<UpdateFlowCoordinator>();
        fc.ParentFlow = _mainFlowCoordinator;
        fc.SetMessage(VersionChecker.UpdateMessage);
        _mainFlowCoordinator.PresentFlowCoordinator(fc);
    }
}
