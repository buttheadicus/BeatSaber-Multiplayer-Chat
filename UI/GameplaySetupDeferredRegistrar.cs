using System;
using System.Collections;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.GameplaySetup;
using UnityEngine;

namespace MultiplayerChat.UI;

/// <summary>
/// Waits for GameplaySetup to be ready, then adds the TEXT CHAT entry to the MODS list.
/// Uses BeatSaberUI so it works from both menu and lobby (no injected refs that get destroyed).
/// </summary>
public class GameplaySetupDeferredRegistrar : MonoBehaviour
{
    private const string TabName = "Multiplayer Chat";
    private bool _tabAdded;

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
        StartCoroutine(TryAddTabWhenReady());
    }

    private void OnDestroy()
    {
        if (!_tabAdded)
            return;
        try
        {
            GameplaySetup.Instance?.RemoveTab(TabName);
        }
        catch { /* ignore */ }
    }

    private IEnumerator TryAddTabWhenReady()
    {
        // GameplaySetup is created when song selection or lobby loads - keep retrying until it exists
        while (!_tabAdded)
        {
            yield return new WaitForSeconds(0.5f);
            if (this == null) yield break;
            try
            {
                if (GameplaySetupTabRegistrar.TabAddedGlobally) break;
                var gs = GameplaySetup.Instance;
                if (gs != null)
                {
                    gs.AddTab(TabName, "MultiplayerChat.UI.LobbyChatTab.bsml", this);
                    _tabAdded = true;
                    GameplaySetupTabRegistrar.TabAddedGlobally = true;
                    break;
                }
            }
            catch (Exception)
            {
                // Retry later
            }
        }
    }

    [UIAction("OpenChatClicked")]
    private void OpenChatClicked()
    {
        var fc = BeatSaberMarkupLanguage.BeatSaberUI.CreateFlowCoordinator<KeyboardFlowCoordinator>();
        BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator.PresentFlowCoordinator(fc);
    }
}
