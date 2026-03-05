using System;
using MultiplayerChat.Settings;
using BeatSaberMarkupLanguage.FloatingScreen;
using HMUI;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// Creates and manages the moveable floating chat panel (like BeatmapScanner).
/// Only active in multiplayer lobby (MultiplayerLobbyInstaller). Saves position when handle is released.
/// </summary>
public class FloatingChatScreenCreator : IInitializable, IDisposable
{
    private readonly FloatingChatViewController _viewController;

    public static FloatingScreen? Screen { get; private set; }

    public FloatingChatScreenCreator(FloatingChatViewController viewController)
    {
        _viewController = viewController;
    }

    public void Initialize()
    {
        CreateFloatingScreen();
    }

    public void Dispose()
    {
        if (Screen != null)
        {
            Screen.HandleReleased -= OnHandleReleased;
            UnityEngine.Object.Destroy(Screen.gameObject);
            Screen = null;
        }
    }

    private void CreateFloatingScreen()
    {
        var position = ChatPositionSettings.LoadPosition();
        var rotation = ChatPositionSettings.LoadRotation();

        // Super wide for readable chat
        var size = new Vector2(450f, 140f);
        Screen = FloatingScreen.CreateFloatingScreen(size, createHandle: true, position, rotation);

        Screen.SetRootViewController(_viewController, ViewController.AnimationType.None);
        Screen.HandleSide = FloatingScreen.Side.Bottom;
        Screen.HighlightHandle = true;
        Screen.ShowHandle = true;

        // Force screen size (in case HMUI/Screen overwrites it)
        Screen.ScreenSize = size;

        // Force view controller to fill the entire screen - critical for width to take effect
        var vcRect = _viewController.transform as RectTransform;
        if (vcRect != null)
        {
            vcRect.anchorMin = Vector2.zero;
            vcRect.anchorMax = Vector2.one;
            vcRect.offsetMin = Vector2.zero;
            vcRect.offsetMax = Vector2.zero;
        }

        // Reset view controller transform so content displays flat (like BeatmapScanner)
        _viewController.transform.localScale = Vector3.one;
        _viewController.transform.localEulerAngles = Vector3.zero;

        if (Screen.Handle != null)
        {
            Screen.Handle.transform.localScale = Vector3.one * 4f;
            Screen.Handle.transform.localPosition = new Vector3(0f, -10f, 0f);
        }

        Screen.HandleReleased += OnHandleReleased;
        Screen.gameObject.name = "E2EChatFloatingScreen";
        Screen.gameObject.SetActive(true);
    }

    private void OnHandleReleased(object? sender, FloatingScreenHandleEventArgs args)
    {
        if (Screen == null) return;

        // Prevent going below floor (like BeatmapScanner)
        if (Screen.Handle != null && Screen.Handle.transform.position.y < 0)
        {
            Screen.transform.position += new Vector3(0f, -Screen.Handle.transform.position.y + 0.1f, 0f);
        }

        ChatPositionSettings.SavePosition(Screen.transform.position);
        ChatPositionSettings.SaveRotation(Screen.transform.rotation);
    }
}
