using System;
using System.Collections.Generic;
using MultiplayerChat.Core;
using MultiplayerChat.Settings;
using BeatSaberMarkupLanguage.FloatingScreen;
using HMUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// Creates a VR-grabbable floating chat panel (FloatingScreen) with chat UI built in code.
/// Grab the handle at the bottom to move it in 3D space.
/// </summary>
public class FloatingChatPanel : MonoBehaviour, IInitializable, IDisposable
{
    [Inject] private readonly ChatManager _chatManager = null!;

    private const int MaxMessages = 200;
    private const float MessageFontSize = 3.5f;
    private static readonly Color UsernameColor = new(1f, 0.84f, 0f);

    private FloatingScreen? _screen;
    private Transform? _contentRoot;
    private ScrollRect? _scrollRect;
    private readonly List<GameObject> _messageRows = new();

    public void Initialize()
    {
        CreateFloatingScreen();
        MultiplayerChat.Plugin.Log?.Info($"[E2EChat] FloatingChatPanel.Initialize done, _contentRoot={_contentRoot != null}");
        _chatManager.MessageReceived += OnMessageReceived;
    }

    public void Dispose()
    {
        _chatManager.MessageReceived -= OnMessageReceived;
        if (_screen != null)
        {
            _screen.HandleReleased -= OnHandleReleased;
            Destroy(_screen.gameObject);
            _screen = null;
        }
    }

    private void CreateFloatingScreen()
    {
        var position = ChatPositionSettings.LoadPosition();
        var rotation = ChatPositionSettings.LoadRotation();
        var size = new Vector2(120f, 140f);

        _screen = FloatingScreen.CreateFloatingScreen(size, createHandle: true, position, rotation);
        _screen.HandleSide = FloatingScreen.Side.Bottom;
        _screen.HighlightHandle = true;
        _screen.ShowHandle = true;
        _screen.gameObject.name = "E2EChatFloatingScreen";
        _screen.HandleReleased += OnHandleReleased;

        if (_screen.Handle != null)
        {
            _screen.Handle.transform.localScale = Vector3.one * 5f;
            _screen.Handle.transform.localPosition = new Vector3(0f, -12f, 0f);
        }

        BuildChatContent();
        _screen.gameObject.SetActive(true);
    }

    private void BuildChatContent()
    {
        if (_screen == null) return;

        var contentRoot = new GameObject("ChatContent");
        contentRoot.transform.SetParent(_screen.transform, false);
        var contentRect = contentRoot.AddComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        contentRect.localScale = Vector3.one;
        contentRect.localEulerAngles = Vector3.zero;

        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(contentRoot.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(0, 12);
        titleRect.offsetMin = new Vector2(4, -12);
        titleRect.offsetMax = new Vector2(-4, 0);
        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Multiplayer Chat";
        titleText.fontSize = 4;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        var scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(contentRoot.transform, false);
        var scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(4, 4);
        scrollRect.offsetMax = new Vector2(-4, -4);

        var viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollObj.transform, false);
        var viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportObj.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        viewportObj.AddComponent<Mask>().showMaskGraphic = false;

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        _contentRoot = contentObj.transform;
        var contentContentRect = contentObj.AddComponent<RectTransform>();
        contentContentRect.anchorMin = new Vector2(0, 1);
        contentContentRect.anchorMax = new Vector2(1, 1);
        contentContentRect.pivot = new Vector2(0.5f, 1);
        contentContentRect.anchoredPosition = Vector2.zero;
        contentContentRect.sizeDelta = new Vector2(0, 0);

        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2;
        vlg.childForceExpandWidth = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = true;
        vlg.padding = new RectOffset(4, 4, 4, 4);

        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scrollRect = scrollObj.AddComponent<ScrollRect>();
        _scrollRect.content = contentContentRect;
        _scrollRect.viewport = viewportRect;
        _scrollRect.vertical = true;
        _scrollRect.horizontal = false;
    }

    private void OnHandleReleased(object? sender, FloatingScreenHandleEventArgs args)
    {
        if (_screen == null) return;
        if (_screen.Handle != null && _screen.Handle.transform.position.y < 0)
        {
            _screen.transform.position += new Vector3(0f, -_screen.Handle.transform.position.y + 0.1f, 0f);
        }
        ChatPositionSettings.SavePosition(_screen.transform.position);
        ChatPositionSettings.SaveRotation(_screen.transform.rotation);
    }

    private void OnMessageReceived(object? sender, ChatMessageEventArgs e)
    {
        if (_contentRoot == null && _screen != null)
        {
            MultiplayerChat.Plugin.Log?.Warn("[E2EChat] _contentRoot was null, rebuilding chat content");
            ClearExistingChatContent();
            BuildChatContent();
        }
        if (_contentRoot == null) return;
        if (e.IsSystem)
        {
            AddMessage("", e.Message ?? "");
            return;
        }
        var displayName = e.IsDM ? $"{e.UserName} (DM)" : e.UserName;
        AddMessage(displayName, e.Message ?? "");
    }

    private void ClearExistingChatContent()
    {
        if (_screen == null) return;
        for (int i = _screen.transform.childCount - 1; i >= 0; i--)
        {
            var c = _screen.transform.GetChild(i);
            if (c.name == "ChatContent")
            {
                Destroy(c.gameObject);
                break;
            }
        }
    }

    private void AddMessage(string userName, string message)
    {
        if (_contentRoot == null) return;

        MultiplayerChat.Plugin.Log?.Info($"[E2EChat] AddMessage: {userName}: {message}");
        var row = CreateMessageRow(userName, message);
        row.transform.SetParent(_contentRoot, false);
        _messageRows.Add(row);

        while (_messageRows.Count > MaxMessages)
        {
            var old = _messageRows[0];
            _messageRows.RemoveAt(0);
            if (old != null) Destroy(old);
        }

        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 0f;
    }

    private GameObject CreateMessageRow(string userName, string message)
    {
        var go = new GameObject("ChatMessage");
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = new Vector2(0, 20);

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(go.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4, 2);
        textRect.offsetMax = new Vector2(-4, -2);

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        var yellowHex = ColorUtility.ToHtmlStringRGB(UsernameColor);
        tmp.text = string.IsNullOrEmpty(userName)
            ? Escape(message)
            : $"<color=#{yellowHex}>{Escape(userName)}:</color> {Escape(message)}";
        tmp.fontSize = MessageFontSize;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.alignment = TextAlignmentOptions.TopLeft;

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 18;
        le.preferredWidth = 110;

        var csf = go.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        return go;
    }

    private static string Escape(string s) =>
        s.Replace("<", "&lt;").Replace(">", "&gt;");
}
