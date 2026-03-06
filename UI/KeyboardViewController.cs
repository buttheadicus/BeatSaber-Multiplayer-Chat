using System;
using MultiplayerChat.Core;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using TMPro;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// ViewController with keyboard input for chat. Presented via FlowCoordinator.
/// Can display an update message at the top when opened from version check.
/// </summary>
[ViewDefinition("MultiplayerChat.UI.KeyboardView.bsml")]
public class KeyboardViewController : BSMLAutomaticViewController
{
    public event EventHandler<string>? TextSubmitted;
    public event EventHandler? Cancelled;

    [InjectOptional] private readonly ChatManager? _chatManager;

    [UIComponent("ChatInput")]
    private StringSetting? _chatInput;

    [UIComponent("UpdateMessage")]
    private TextMeshProUGUI? _updateMessageText;

    public void SetUpdateMessage(string? message)
    {
        if (_updateMessageText != null)
            _updateMessageText.text = message ?? "";
        else
            _pendingUpdateMessage = message;
    }

    private string? _pendingUpdateMessage;

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        if (_pendingUpdateMessage != null && _updateMessageText != null)
        {
            _updateMessageText.text = _pendingUpdateMessage;
            _pendingUpdateMessage = null;
        }
    }

    [UIAction("SubmitClicked")]
    private void SubmitClicked()
    {
        var text = _chatInput?.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(text))
        {
            TextSubmitted?.Invoke(this, text);
            _chatManager?.SendMessage(text);
        }
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    [UIAction("CancelClicked")]
    private void CancelClicked()
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }
}
