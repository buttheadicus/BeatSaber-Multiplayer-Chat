using System;
using System.Collections;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.GameplaySetup;
using HMUI;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// Adds a "TEXT CHAT" entry to the MODS tab when lobby loads. Defers AddTab briefly so GameplaySetup is ready.
/// </summary>
public class GameplaySetupTabRegistrar : MonoBehaviour, IDisposable
{
    [Inject] private readonly MainFlowCoordinator _mainFlowCoordinator = null!;
    [Inject] private readonly KeyboardFlowCoordinator _keyboardFlowCoordinator = null!;

    private const string TabName = "Multiplayer Chat";
    private bool _tabAdded;

    private void Start()
    {
        StartCoroutine(AddTabWhenReady());
    }

    private IEnumerator AddTabWhenReady()
    {
        // Give GameplaySetup time to initialize
        for (var i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(0.2f);
            if (TabAddedGlobally) yield break;
            try
            {
                var gs = GameplaySetup.Instance;
                if (gs != null)
                {
                    gs.AddTab(TabName, "MultiplayerChat.UI.LobbyChatTab.bsml", this);
                    _tabAdded = true;
                    TabAddedGlobally = true;
                    yield break;
                }
            }
            catch (Exception)
            {
                // Retry
            }
        }
    }

    private void OnDestroy()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (!_tabAdded) return;
        try
        {
            GameplaySetup.Instance?.RemoveTab(TabName);
            TabAddedGlobally = false;
        }
        catch { /* ignore */ }
    }

    [UIAction("OpenChatClicked")]
    private void OpenChatClicked()
    {
        _mainFlowCoordinator.PresentFlowCoordinator(_keyboardFlowCoordinator);
    }

    internal static bool TabAddedGlobally;
}
