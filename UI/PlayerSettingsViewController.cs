using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Settings;
using UnityEngine.UI;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.PlayerSettingsView.bsml")]
public sealed class PlayerSettingsViewController : BSMLAutomaticViewController
{
    private const string LabelChatBubbleSounds = "Chat bubble sounds";

    public event Action? PlayerSettingsApplied;

    [UIComponent("BubbleDuration")]
    private SliderSetting? _bubbleDurationSlider;

    [UIComponent("NameColorInput")]
    private StringSetting? _nameColorInput;

    [UIComponent("ChatBubbleSoundsToggle")]
    private ToggleSetting? _chatBubbleSoundsToggle;

    private string _nameColorHexDraft = "87CEEB";

    [UIValue("BubbleDuration")]
    private float BubbleDuration
    {
        get => ModSettings.BubbleDuration;
        set => ModSettings.BubbleDuration = value;
    }

    [UIValue("NameColorHex")]
    public string NameColorHex
    {
        get => _nameColorHexDraft;
        set => _nameColorHexDraft = NormalizeHexDraft(value);
    }

    [UIValue("ChatBubbleSoundsEnabled")]
    public bool ChatBubbleSoundsEnabled
    {
        get => ModSettings.ChatBubbleSoundsEnabled;
        set => ModSettings.ChatBubbleSoundsEnabled = value;
    }

    [UIAction("#post-parse")]
    private void PostParse()
    {
        LoadNameColorDraftFromSettings();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
        ApplyToggleLabel();
        SyncNameColorInputText();
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        LoadNameColorDraftFromSettings();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
        ApplyToggleLabel();
        SyncNameColorInputText();
    }

    private void LoadNameColorDraftFromSettings() =>
        _nameColorHexDraft = NormalizeHexDraft(ModSettings.NameColor);

    private void SyncNameColorInputText()
    {
        if (_nameColorInput != null)
            _nameColorInput.Text = _nameColorHexDraft;
    }

    private void ApplyToggleLabel()
    {
        if (_chatBubbleSoundsToggle != null)
            _chatBubbleSoundsToggle.Text = LabelChatBubbleSounds;
    }

    [UIAction("ApplyClicked")]
    private void OnApplyClicked()
    {
        if (_bubbleDurationSlider != null)
            ModSettings.BubbleDuration = _bubbleDurationSlider.Value;

        if (_nameColorInput != null)
            _nameColorHexDraft = NormalizeHexDraft(_nameColorInput.Text);

        ModSettings.NameColor = _nameColorHexDraft;

        var tgl = _chatBubbleSoundsToggle?.GetComponentInChildren<Toggle>(true);
        if (tgl != null)
            ModSettings.ChatBubbleSoundsEnabled = tgl.isOn;

        PlayerSettingsApplied?.Invoke();
    }

    private static string NormalizeHexDraft(string? hex)
    {
        var normalized = (hex ?? "").Trim();
        if (normalized.StartsWith("#", StringComparison.Ordinal))
            normalized = normalized.Substring(1);
        if (normalized.Length > 6)
            normalized = normalized.Substring(0, 6);
        if (normalized.Length != 6)
            return "87CEEB";
        return normalized;
    }
}
