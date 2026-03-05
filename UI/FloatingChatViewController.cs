using System;
using System.Collections.Generic;
using MultiplayerChat.Core;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// ViewController for the moveable floating chat panel. Chat log only (no input).
/// Input/Send stay on LobbyChatTab.
/// </summary>
[ViewDefinition("MultiplayerChat.UI.FloatingChatView.bsml")]
public class FloatingChatViewController : BSMLAutomaticViewController
{
    [Inject] private readonly ChatManager _chatManager = null!;

    [UIObject("ChatLogContent")]
    private GameObject? _chatLogContent;

    private readonly List<GameObject> _messageRows = new();
    private const int MaxMessages = 200;
    private const float MessageFontSize = 3f;

    // Chatroom styling: yellow usernames, white message text
    private static readonly Color UsernameColor = new(1f, 0.84f, 0f); // #FFD700

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        if (firstActivation)
        {
            _chatManager.MessageReceived += OnMessageReceived;
        }
        // Force content flat every activation - FloatingScreen can inherit rotation
        EnsureFlatTransform();
        EnsureContentWidth();
    }

    private void EnsureContentWidth()
    {
        if (_chatLogContent == null) return;
        var le = _chatLogContent.GetComponent<LayoutElement>();
        if (le == null) le = _chatLogContent.AddComponent<LayoutElement>();
        le.preferredWidth = 400f;
        le.minWidth = 400f;
        var vlg = _chatLogContent.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.spacing = 2f;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
        }
    }

    private void EnsureFlatTransform()
    {
        transform.localScale = Vector3.one;
        transform.localEulerAngles = Vector3.zero;
        foreach (Transform child in transform)
        {
            child.localEulerAngles = Vector3.zero;
        }
        // Force this view to fill parent (FloatingScreen) so width/height take effect
        if (transform is RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
        if (removedFromHierarchy)
        {
            _chatManager.MessageReceived -= OnMessageReceived;
        }
    }

    private void OnMessageReceived(object? sender, ChatMessageEventArgs e)
    {
        if (_chatLogContent == null) return;
        if (e.IsSystem)
        {
            AddMessageToLog("", e.Message);
            return;
        }
        var displayName = e.IsDM ? $"{e.UserName} (DM)" : e.UserName;
        AddMessageToLog(displayName, e.Message);
    }

    private void AddMessageToLog(string userName, string message)
    {
        if (_chatLogContent == null) return;

        var row = CreateMessageRow(userName, message);
        row.transform.SetParent(_chatLogContent.transform, false);
        _messageRows.Add(row);

        while (_messageRows.Count > MaxMessages)
        {
            var old = _messageRows[0];
            _messageRows.RemoveAt(0);
            if (old != null)
                Destroy(old);
        }

        ScrollToBottom();
    }

    /// <summary>
    /// Creates a chatroom-style row (emulating BeatSaberPlus ChatMessageWidget).
    /// Explicit width, top-left anchored text, word wrap.
    /// </summary>
    private GameObject CreateMessageRow(string userName, string message)
    {
        const float leftRightMargins = 4f;
        const float topDownMargins = 1f;
        var contentWidth = GetContentWidth();

        var go = new GameObject("ChatMessage");
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(leftRightMargins, 0);
        rect.offsetMax = new Vector2(-leftRightMargins, 0);
        rect.sizeDelta = new Vector2(0, 24);

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(go.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.localPosition = Vector3.zero;
        textRect.sizeDelta = new Vector2(Mathf.Max(100f, contentWidth - (2 * leftRightMargins)), 100f);

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        var yellowHex = ColorUtility.ToHtmlStringRGB(UsernameColor);
        tmp.text = string.IsNullOrEmpty(userName)
            ? EscapeRichText(message)
            : $"<color=#{yellowHex}>{EscapeRichText(userName)}:</color> {EscapeRichText(message)}";
        tmp.fontSize = MessageFontSize;
        tmp.color = Color.white;
        tmp.outlineColor = Color.white;
        tmp.outlineWidth = 0.15f;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.margin = new Vector4(leftRightMargins, topDownMargins, leftRightMargins, topDownMargins);
        tmp.alignment = TextAlignmentOptions.TopLeft;

        var layout = go.AddComponent<LayoutElement>();
        layout.minHeight = 20;
        layout.preferredWidth = 400f;

        var contentSizeFitter = go.AddComponent<ContentSizeFitter>();
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        return go;
    }

    private float GetContentWidth()
    {
        var scrollRect = _chatLogContent?.GetComponentInParent<ScrollRect>();
        if (scrollRect?.viewport != null)
        {
            var w = scrollRect.viewport.rect.width;
            if (w > 10f) return w;
        }
        if (_chatLogContent != null && _chatLogContent.transform.parent is RectTransform parentRect)
        {
            var w = parentRect.rect.width;
            if (w > 10f) return w;
        }
        return 400f;
    }

    private static string EscapeRichText(string s)
    {
        return s.Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private void ScrollToBottom()
    {
        var scrollRect = _chatLogContent?.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    /// <summary>
    /// Clears the chat log (e.g. when leaving lobby).
    /// </summary>
    public void ClearLog()
    {
        if (_chatLogContent == null) return;
        foreach (var row in _messageRows)
        {
            if (row != null)
                Destroy(row);
        }
        _messageRows.Clear();
    }
}
