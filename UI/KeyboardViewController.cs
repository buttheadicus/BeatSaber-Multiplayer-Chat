using System;
using MultiplayerChat.Core;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// ViewController with keyboard input for chat. Presented via FlowCoordinator.
/// </summary>
[ViewDefinition("MultiplayerChat.UI.KeyboardView.bsml")]
public class KeyboardViewController : BSMLAutomaticViewController
{
    public event EventHandler<string>? TextSubmitted;
    public event EventHandler? Cancelled;

    [InjectOptional] private readonly ChatManager? _chatManager;

    [UIComponent("ChatInput")]
    private StringSetting? _chatInput;

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
