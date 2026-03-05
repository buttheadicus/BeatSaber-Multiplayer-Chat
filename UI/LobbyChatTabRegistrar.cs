using System;
using System.Collections;
using BeatSaberMarkupLanguage.GameplaySetup;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// Waits for GameplaySetup to be ready, then adds the Multiplayer Chat tab with LobbyChatTabHost.
/// </summary>
public class LobbyChatTabRegistrar : MonoBehaviour
{
    private const string TabName = "Multiplayer Chat";

    [Inject] private readonly LobbyChatTabHost _host = null!;

    private bool _tabAdded;

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
        StartCoroutine(TryAddTabWhenReady());
    }

    private void OnDestroy()
    {
        if (!_tabAdded) return;
        try
        {
            GameplaySetup.Instance?.RemoveTab(TabName);
        }
        catch { /* ignore */ }
    }

    private IEnumerator TryAddTabWhenReady()
    {
        while (!_tabAdded)
        {
            yield return new WaitForSeconds(0.5f);
            if (this == null) yield break;
            try
            {
                var gs = GameplaySetup.Instance;
                if (gs != null)
                {
                    gs.AddTab(TabName, "MultiplayerChat.UI.LobbyChatTab.bsml", _host);
                    _tabAdded = true;
                    break;
                }
            }
            catch (Exception)
            {
                // Retry later
            }
        }
    }
}
