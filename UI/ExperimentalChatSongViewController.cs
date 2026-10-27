using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Core;
using TMPro;

// this will 100% break your UI if you use it, have fun

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.ExperimentalChatSongView.bsml")]
public sealed class ExperimentalChatSongViewController : BSMLAutomaticViewController
{
    [UIComponent("ChatSongMessage")] private StringSetting? _chatSongInput;
    [UIComponent("ChatSongStatus")] private TMP_Text? _chatSongStatus;

    private string _chatSongDraft = "";

    [UIValue("ChatSongDraft")]
    public string ChatSongDraft
    {
        get => _chatSongDraft;
        set => _chatSongDraft = value ?? "";
    }

    [UIAction("#post-parse")]
    private void PostParse() => BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
        if (_chatSongStatus != null)
            _chatSongStatus.text = "";
    }

    [UIAction("CreateChatSongClicked")]
    private void OnCreateChatSongClicked()
    {
        if (_chatSongInput != null)
            _chatSongDraft = _chatSongInput.Text ?? _chatSongDraft;

        ChatSongFactory.TryCreate(_chatSongDraft, out var status);
        if (_chatSongStatus != null)
            _chatSongStatus.text = status;
    }
}
