using System;
using System.Collections;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.GameplaySetup;
using MultiplayerChat.Core;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// Waits for GameplaySetup to be ready, then adds the Multiplayer Chat tab.
/// Settings opens in its own FlowCoordinator (separate UI).
/// </summary>
public class LobbyChatTabRegistrar : MonoBehaviour
{
    private const string TabName = "Multiplayer Chat";

    [Inject] private readonly ChatBubbleManager _chatBubbleManager = null!;
    [Inject] private readonly MultiplayerChatSettingsFlowCoordinator _settingsFlowCoordinator = null!;

    [UIComponent("ChatInput")]
    private BeatSaberMarkupLanguage.Components.Settings.StringSetting? _chatInput;

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
                    gs.AddTab(TabName, "MultiplayerChat.UI.LobbyChatTab.bsml", this);
                    _tabAdded = true;
                    break;
                }
            }
            catch (Exception ex)
            {
                MultiplayerChat.Plugin.Log?.Error($"[E2EChat] Failed to add tab: {ex}");
            }
        }
    }

    [UIAction("SettingsClicked")]
    private void SettingsClicked()
    {
        var mainFlow = BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator;
        var topFlow = FlowCoordinatorHelper.GetTopFlowCoordinator(mainFlow);
        _settingsFlowCoordinator.ParentFlow = topFlow;
        topFlow.PresentFlowCoordinator(_settingsFlowCoordinator);
    }

    [UIAction("SubmitClicked")]
    private void SubmitClicked()
    {
        var text = _chatInput?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;

        var cm = ChatManager.Instance;
        if (cm != null)
        {
            cm.SendMessage(text);
            _chatInput!.Text = "";
        }
        else
        {
            MultiplayerChat.Plugin.Log?.Warn("[E2EChat] ChatManager.Instance is null - not in multiplayer lobby?");
        }
    }

    [UIAction("MuteClicked")]
    private void MuteClicked() { }

    [UIAction("DMClicked")]
    private void DMClicked() { }

    [UIAction("ForceClearClicked")]
    private void ForceClearClicked()
    {
        _chatBubbleManager.ForceClearChat();
    }
}
