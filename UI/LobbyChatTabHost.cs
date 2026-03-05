using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using MultiplayerChat.Core;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// Host for the Multiplayer Chat GameplaySetup tab. Handles chat input, send, mute, DM, and settings.
/// Messages display in chat bubbles only (no ChatLog).
/// </summary>
public class LobbyChatTabHost : IInitializable, IDisposable
{
    [Inject] private readonly ChatManager _chatManager = null!;

    [UIComponent("ChatInput")]
    private StringSetting? _chatInput;

    public void Initialize() { }

    public void Dispose() { }

    [UIAction("SubmitClicked")]
    private void SubmitClicked()
    {
        var text = _chatInput?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;

        var cm = Core.ChatManager.Instance;
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
    private void MuteClicked()
    {
        // Coming Soon - beta placeholder
    }

    [UIAction("DMClicked")]
    private void DMClicked()
    {
        // Coming Soon - beta placeholder
    }

    [UIAction("SettingsClicked")]
    private void SettingsClicked()
    {
        // Placeholder - do nothing for now
    }
}
