using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using TMPro;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// Simple view that displays only the update message (large text).
/// </summary>
[ViewDefinition("MultiplayerChat.UI.UpdateMessageView.bsml")]
public class UpdateMessageViewController : BSMLAutomaticViewController
{
    public event EventHandler? CloseClicked;

    [UIComponent("UpdateMessage")]
    private TextMeshProUGUI? _updateMessageText;

    public void SetMessage(string? message)
    {
        if (_updateMessageText != null)
            _updateMessageText.text = message ?? "";
        else
            _pendingMessage = message;
    }

    private string? _pendingMessage;

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        if (_pendingMessage != null && _updateMessageText != null)
        {
            _updateMessageText.text = _pendingMessage;
            _pendingMessage = null;
        }
    }

    [UIAction("CloseClicked")]
    private void OnCloseClicked()
    {
        CloseClicked?.Invoke(this, EventArgs.Empty);
    }
}
