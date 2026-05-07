using System;
using MultiplayerChat.Core;
using MultiplayerChat.Network;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace MultiplayerChat.UI;

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

    private Button? _keyboardChatStringButton;
    private TMP_InputField? _chatTmp;
    private bool _typingBroadcastToOthers;

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

        TryBindTypingField();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        UnbindTypingField();
        base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
    }

    private void TryBindTypingField()
    {
        UnbindTypingField();
        if (_chatInput == null) return;
        _keyboardChatStringButton = _chatInput.GetComponentInChildren<Button>(true);
        if (_keyboardChatStringButton != null)
            _keyboardChatStringButton.onClick.AddListener(OnKeyboardChatBarPressed);

        _chatTmp = _chatInput.GetComponentInChildren<TMP_InputField>(true);
        if (_chatTmp != null)
        {
            _chatTmp.onSubmit.AddListener(OnKeyboardChatSubmitOrEnter);
            _chatTmp.onEndEdit.AddListener(OnKeyboardChatEndEdit);
            _chatTmp.onDeselect.AddListener(OnKeyboardChatDeselect);
        }
    }

    private void UnbindTypingField()
    {
        if (_keyboardChatStringButton != null)
        {
            _keyboardChatStringButton.onClick.RemoveListener(OnKeyboardChatBarPressed);
            _keyboardChatStringButton = null;
        }

        if (_chatTmp != null)
        {
            _chatTmp.onSubmit.RemoveListener(OnKeyboardChatSubmitOrEnter);
            _chatTmp.onEndEdit.RemoveListener(OnKeyboardChatEndEdit);
            _chatTmp.onDeselect.RemoveListener(OnKeyboardChatDeselect);
            _chatTmp = null;
        }

        ClearTypingBroadcastIfNeeded();
    }

    private void OnKeyboardChatBarPressed()
    {
        if (_typingBroadcastToOthers) return;
        _chatManager?.BroadcastChatActivity(ChatActivityPacket.TypingStart);
        _typingBroadcastToOthers = true;
    }

    private void OnKeyboardChatSubmitOrEnter(string _)
    {
        ClearTypingBroadcastIfNeeded();
    }

    private void OnKeyboardChatEndEdit(string _)
    {
        ClearTypingBroadcastIfNeeded();
    }

    private void OnKeyboardChatDeselect(string _)
    {
        ClearTypingBroadcastIfNeeded();
    }

    public void FlushTypingPresenceToPeers()
    {
        ClearTypingBroadcastIfNeeded();
    }

    private void ClearTypingBroadcastIfNeeded()
    {
        if (!_typingBroadcastToOthers) return;
        _chatManager?.BroadcastChatActivity(ChatActivityPacket.TypingStop);
        _typingBroadcastToOthers = false;
    }

    [UIAction("SubmitClicked")]
    private void SubmitClicked()
    {
        var text = _chatInput?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text))
        {
            ClearTypingBroadcastIfNeeded();
            Cancelled?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_chatManager == null || !_chatManager.SendMessage(text))
            return;

        ClearTypingBroadcastIfNeeded();
        TextSubmitted?.Invoke(this, text);
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    [UIAction("CancelClicked")]
    private void CancelClicked()
    {
        ClearTypingBroadcastIfNeeded();
        Cancelled?.Invoke(this, EventArgs.Empty);
    }
}
