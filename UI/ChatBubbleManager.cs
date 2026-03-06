using System;
using System.Collections;
using System.Collections.Generic;
using MultiplayerChat.Core;
using HMUI;
using MultiplayerCore.Models;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// Manages chat bubbles stacked above the lobby header (HOST SETUP / CLIENT SETUP / QUICK PLAY LOBBY).
/// All messages appear in one area; multiple messages stack vertically with newest at bottom.
/// </summary>
public class ChatBubbleManager : MonoBehaviour, IInitializable, IDisposable
{
    private const float DisplayDuration = 15f;
    private const int MaxVisibleBubbles = 8;
    private const float BubbleHeight = 36f;

    [Inject] private readonly DiContainer _container = null!;
    [Inject] private readonly ChatManager _chatManager = null!;
    [Inject] private readonly ModPresenceManager _modPresence = null!;

    private readonly List<ChatBubble> _stackedBubbles = new();
    private Transform? _lobbyHeaderRoot;
    private bool _wasInLobby;

    public void Initialize()
    {
        _chatManager.MessageReceived += OnMessageReceived;
        StartCoroutine(EnsureLobbyHeaderRoot());
    }

    public void Dispose()
    {
        _chatManager.MessageReceived -= OnMessageReceived;
        foreach (var bubble in _stackedBubbles)
        {
            if (bubble != null)
                UnityEngine.Object.Destroy(bubble.gameObject);
        }
        _stackedBubbles.Clear();
        if (_lobbyHeaderRoot != null)
            UnityEngine.Object.Destroy(_lobbyHeaderRoot.gameObject);
    }

    private void OnMessageReceived(object? sender, ChatMessageEventArgs e)
    {
        MultiplayerChat.Plugin.Log?.Info($"[E2EChat] OnMessageReceived: {e.UserName}: {e.Message}");
        if (e.IsSystem)
        {
            ShowStackedBubble("", e.Message);
            return;
        }
        var name = TrimName(e.UserName ?? "", 15);
        var displayName = e.IsDM ? $"{name} (DM)" : name;
        ShowStackedBubble(displayName, e.Message);
    }

    private IEnumerator EnsureLobbyHeaderRoot()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            var inLobby = IsInLobby();
            var inSong = IsInSong();

            if (_wasInLobby && !inLobby)
                ClearChat();
            if (inSong && _stackedBubbles.Count > 0)
                ClearChat();

            _wasInLobby = inLobby;

            if (_lobbyHeaderRoot != null)
            {
                if (_lobbyHeaderRoot.gameObject != null)
                    _lobbyHeaderRoot.gameObject.SetActive(inLobby);
                else
                    _lobbyHeaderRoot = null;
            }
            if (inLobby)
            {
                if (_lobbyHeaderRoot == null)
                {
                    var root = FindOrCreateLobbyHeaderChatRoot();
                    if (root != null)
                        _lobbyHeaderRoot = root;
                }
                if (_lobbyHeaderRoot != null)
                    EnsureNametagIcons();
            }
        }
    }

    /// <summary>Force clear all chat bubbles (e.g. from user button).</summary>
    public void ForceClearChat()
    {
        ClearChat();
    }

    private void ClearChat()
    {
        foreach (var bubble in _stackedBubbles)
        {
            if (bubble != null && bubble.gameObject != null)
                UnityEngine.Object.Destroy(bubble.gameObject);
        }
        _stackedBubbles.Clear();
        if (_lobbyHeaderRoot != null && _lobbyHeaderRoot.gameObject != null)
        {
            UnityEngine.Object.Destroy(_lobbyHeaderRoot.gameObject);
            _lobbyHeaderRoot = null;
        }
    }

    private void EnsureNametagIcons()
    {
        foreach (var ctrl in UnityEngine.Object.FindObjectsOfType<MultiplayerLobbyAvatarController>())
        {
            if (ctrl == null) continue;
            var cap = ctrl.transform.Find("AvatarCaption") ?? FindRecursive(ctrl.transform, "AvatarCaption")
                ?? FindRecursive(ctrl.transform, "PlayerName") ?? FindRecursive(ctrl.transform, "Name")
                ?? FindNametagByText(ctrl.transform);
            if (cap != null && cap.GetComponent<ChatBubbleAnchor>() == null)
            {
                cap.gameObject.AddComponent<ChatBubbleAnchor>();
            }
        }
    }

    private static Transform? FindNametagByText(Transform root)
    {
        foreach (var tmp in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            if (tmp == null || string.IsNullOrEmpty(tmp.text)) continue;
            var parent = tmp.transform.parent;
            if (parent != null && parent.GetComponent<RectTransform>() != null && parent.GetComponent<ChatBubbleAnchor>() == null)
                return parent;
        }
        foreach (var curved in root.GetComponentsInChildren<HMUI.CurvedTextMeshPro>(true))
        {
            if (curved == null || string.IsNullOrEmpty(curved.text)) continue;
            var parent = curved.transform.parent;
            if (parent != null && parent.GetComponent<RectTransform>() != null && parent.GetComponent<ChatBubbleAnchor>() == null)
                return parent;
        }
        return null;
    }

    private static Transform? FindRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindRecursive(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Creates chat area above the HOST SETUP bar in the lobby's 3D VR UI.
    /// Parents to the game's in-world UI hierarchy. NO screen overlay - VR only.
    /// </summary>
    private Transform? FindOrCreateLobbyHeaderChatRoot()
    {
        var banner = FindHostSetupBannerInLobby();
        if (banner != null)
        {
            var root = CreateChatRootAboveBanner(banner);
            if (root != null)
            {
                MultiplayerChat.Plugin.Log?.Info($"[E2EChat] Chat attached to VR game UI above HOST SETUP: {banner.name}");
                return root;
            }
        }
        MultiplayerChat.Plugin.Log?.Warn("[E2EChat] Could not find HOST SETUP bar in lobby VR UI - chat bubbles disabled");
        return null;
    }

    /// <summary>
    /// Finds the HOST SETUP bar by scanning ALL text components in the scene.
    /// The lobby uses VR World Space / Screen Space Camera canvas - not screen overlay.
    /// </summary>
    private static Transform? FindHostSetupBannerInLobby()
    {
        foreach (var tmp in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>())
        {
            if (tmp == null) continue;
            var text = (tmp.text ?? "").ToUpperInvariant().Trim();
            if (text.Contains("HOST SETUP") || text.Contains("HOSTSETUP") || text.Contains("CLIENT SETUP") ||
                text.Contains("QUICK PLAY LOBBY") || text == "HOST SETUP" || text == "CLIENT SETUP")
            {
                var canvas = tmp.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    return tmp.transform;
            }
        }
        foreach (var curved in UnityEngine.Object.FindObjectsOfType<CurvedTextMeshPro>())
        {
            if (curved == null) continue;
            var text = (curved.text ?? "").ToUpperInvariant().Trim();
            if (text.Contains("HOST SETUP") || text.Contains("HOSTSETUP") || text.Contains("CLIENT SETUP") ||
                text.Contains("QUICK PLAY LOBBY") || text == "HOST SETUP" || text == "CLIENT SETUP")
            {
                var canvas = curved.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    return curved.transform;
            }
        }
        var roots = new[] { "MultiplayerLobbyCenterStage", "CenterStage", "LobbySetup" };
        foreach (var rootName in roots)
        {
            var root = GameObject.Find(rootName);
            if (root == null) continue;
            var found = FindInChildren(root.transform, t =>
            {
                var name = t.name.ToUpperInvariant();
                return name.Contains("HOSTSETUP") || (name.Contains("HOST") && name.Contains("SETUP")) ||
                    name.Contains("HEADER") || name == "TITLE";
            });
            if (found != null)
            {
                var canvas = found.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    return found;
            }
        }
        var titleView = GameObject.Find("TitleViewController");
        if (titleView == null)
        {
            var wrapper = GameObject.Find("Wrapper");
            titleView = wrapper != null ? wrapper.transform.Find("MenuCore/UI/ScreenSystem/TopScreen/TitleViewController")?.gameObject : null;
        }
        if (titleView != null)
        {
            foreach (var tmp in titleView.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (tmp == null) continue;
                var text = (tmp.text ?? "").ToUpperInvariant();
                if (text.Contains("HOST SETUP") || text.Contains("HOSTSETUP") || text.Contains("CLIENT SETUP") ||
                    text.Contains("QUICK PLAY LOBBY"))
                {
                    var c = tmp.GetComponentInParent<Canvas>();
                    if (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay)
                        return tmp.transform;
                }
            }
            var titleCanvas = titleView.GetComponentInParent<Canvas>();
            if (titleCanvas != null && titleCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                return titleView.transform;
        }
        return null;
    }

    private static Transform? FindInChildren(Transform parent, System.Func<Transform, bool> predicate)
    {
        if (predicate(parent)) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindInChildren(parent.GetChild(i), predicate);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Parents chat stack so the bottom of the chat aligns with the top of the HOST SETUP bar.
    /// Chat grows upward from the bar.
    /// </summary>
    private static Transform? CreateChatRootAboveBanner(Transform banner)
    {
        var canvas = banner.GetComponentInParent<Canvas>();
        if (canvas == null) return null;

        var parent = banner.GetComponent<RectTransform>() != null ? banner : banner.parent;
        if (parent == null) return null;

        var rootObj = new GameObject("E2EChatLobbyHeaderStack");
        rootObj.layer = banner.gameObject.layer;
        rootObj.transform.SetParent(parent, false);
        rootObj.transform.SetAsFirstSibling();

        var rootRect = rootObj.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, 0f);
        rootRect.sizeDelta = new Vector2(420f, 320f);

        var vlg = rootObj.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.LowerCenter;
        vlg.childControlHeight = true;
        vlg.childControlWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.spacing = 2f;
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.reverseArrangement = true;

        return rootObj.transform;
    }

    private void ShowStackedBubble(string userName, string message)
    {
        if (!IsInLobby()) return;

        var root = _lobbyHeaderRoot ?? FindOrCreateLobbyHeaderChatRoot();
        if (root == null) return;

        var bubble = CreateStackedBubble(root);
        if (bubble == null) return;

        var trimmed = TrimName(userName ?? "", 15);
        var safeName = string.IsNullOrEmpty(trimmed) ? "" : trimmed.Replace("<", "&lt;").Replace(">", "&gt;");
        var text = string.IsNullOrEmpty(userName)
            ? message
            : $"<color=#87CEEB>{safeName}</color>: {message}";
        bubble.SetText(text);
        bubble.Show(DisplayDuration, isStacked: true);
        bubble.transform.SetAsFirstSibling();
        _stackedBubbles.Insert(0, bubble);

        while (_stackedBubbles.Count > MaxVisibleBubbles)
        {
            var oldest = _stackedBubbles[_stackedBubbles.Count - 1];
            _stackedBubbles.RemoveAt(_stackedBubbles.Count - 1);
            if (oldest != null)
                UnityEngine.Object.Destroy(oldest.gameObject);
        }
    }

    private ChatBubble? CreateStackedBubble(Transform parent)
    {
        var panelObj = new GameObject("E2EChatBubble");
        panelObj.layer = 5;
        panelObj.transform.SetParent(parent, false);

        var rect = panelObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(380f, BubbleHeight);

        var contentSize = panelObj.AddComponent<ContentSizeFitter>();
        contentSize.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentSize.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var layout = panelObj.AddComponent<LayoutElement>();
        layout.minHeight = BubbleHeight;
        layout.preferredWidth = 380f;
        layout.minWidth = 200f;
        layout.flexibleHeight = 0f;

        var textObj = new GameObject("Text");
        textObj.layer = 5;
        textObj.transform.SetParent(panelObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8, 4);
        textRect.offsetMax = new Vector2(-8, -4);

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 14;
        tmp.color = Color.white;
        tmp.richText = true;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 200);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.maxVisibleLines = 6;
        tmp.raycastTarget = false;
        tmp.isOverlay = false;

        return _container.InstantiateComponent<ChatBubble>(panelObj);
    }

    private bool IsInLobby()
    {
        var center = GameObject.Find("MultiplayerLobbyCenterStage");
        if (center != null && center.activeInHierarchy) return true;
        var lobby = GameObject.Find("LobbySetup");
        if (lobby != null && lobby.activeInHierarchy) return true;
        var alt = GameObject.Find("CenterStage");
        if (alt != null && alt.activeInHierarchy) return true;
        return false;
    }

    private static bool IsInSong()
    {
        try
        {
            var scene = SceneManager.GetActiveScene();
            return scene.IsValid() && scene.name == "GameCore";
        }
        catch { return false; }
    }

    private static string TrimName(string name, int maxLen)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (name.Length <= maxLen) return name;
        return name.Substring(0, maxLen) + "...";
    }
}
