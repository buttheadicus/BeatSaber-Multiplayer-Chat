using System;
using System.Collections;
using System.Collections.Generic;
using MultiplayerChat.Core;
using MultiplayerChat.Settings;
using HMUI;
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
    private const int MaxVisibleBubbles = 8;
    private const float BubbleHeight = 36f;

    /// <summary>Active lobby instance (BSML settings VC often does not get this via Zenject injection).</summary>
    public static ChatBubbleManager? Instance { get; private set; }

    [Inject] private readonly DiContainer _container = null!;
    [Inject] private readonly ChatManager _chatManager = null!;
    [Inject] private readonly ModPresenceManager _modPresence = null!;

    private readonly List<ChatBubble> _stackedBubbles = new();
    private Transform? _lobbyHeaderRoot;
    private bool _wasInLobby;
    private bool _isMoveMode;
    private GameObject? _moveHandle;
    private Coroutine? _moveModeHelperCoroutine;

    public bool IsMoveMode => _isMoveMode;

    public void Initialize()
    {
        Instance = this;
        _chatManager.MessageReceived += OnMessageReceived;
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(ChatSoundEffects.LoadClipsRoutine());
        StartCoroutine(EnsureLobbyHeaderRoot());
    }

    public void Dispose()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
        _isMoveMode = false;
        RemoveMoveHandle();
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
        MultiplayerChat.Plugin.Log?.Info($"[MPChat] OnMessageReceived: {e.UserName}: {e.Message}");
        if (e.IsSystem)
        {
            ShowStackedBubble("", e.Message);
            return;
        }
        var name = TrimName(e.UserName ?? "", 30);
        var msg = e.Message ?? "";
        if (e.IsDM)
            msg = $"(DM) {msg}";
        ShowStackedBubble(name, msg, e.NameColor);
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
            // GameCore is often additively loaded; IsInSong checks all loaded scenes.
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
                {
                    EnsureNametagIcons();
                    ApplyPlacementMode();
                }
            }
        }
    }

    /// <summary>When custom placement is on: enter move mode to show handle for dragging.</summary>
    public void EnterMoveMode()
    {
        if (!ModSettings.CustomPlacement) return;
        if (_isMoveMode) return;
        _isMoveMode = true;
        if (_lobbyHeaderRoot == null)
        {
            var root = FindOrCreateLobbyHeaderChatRoot();
            if (root != null)
                _lobbyHeaderRoot = root;
        }
        EnsureMoveHandle();
        if (_moveHandle == null)
            _moveModeHelperCoroutine = StartCoroutine(RetryEnterMoveMode());
        else
            _moveModeHelperCoroutine = StartCoroutine(MoveModeHelperMessages());
    }

    private IEnumerator RetryEnterMoveMode()
    {
        yield return null;
        for (int i = 0; i < 12 && _isMoveMode; i++)
        {
            if (!_isMoveMode) yield break;
            if (_lobbyHeaderRoot == null)
            {
                var root = FindOrCreateLobbyHeaderChatRoot();
                if (root != null)
                    _lobbyHeaderRoot = root;
            }
            if (_lobbyHeaderRoot != null)
            {
                EnsureMoveHandle();
                if (_moveHandle != null)
                {
                    _moveModeHelperCoroutine = StartCoroutine(MoveModeHelperMessages());
                    yield break;
                }
            }
            yield return new WaitForSeconds(0.25f);
        }
        _moveModeHelperCoroutine = StartCoroutine(MoveModeHelperMessages());
    }

    /// <summary>Exit move mode and save position.</summary>
    public void ExitMoveMode()
    {
        if (!_isMoveMode) return;
        _isMoveMode = false;
        if (_moveModeHelperCoroutine != null)
        {
            StopCoroutine(_moveModeHelperCoroutine);
            _moveModeHelperCoroutine = null;
        }
        RemoveMoveHandle();
        if (_lobbyHeaderRoot != null)
        {
            var rt = _lobbyHeaderRoot.GetComponent<RectTransform>();
            if (rt != null)
                ModSettings.LobbyChatPosition = rt.anchoredPosition;
        }
    }

    /// <summary>Reset chat to default position (above HOST SETUP).</summary>
    public void ResetToDefaultPosition()
    {
        if (_lobbyHeaderRoot == null) return;
        var rt = _lobbyHeaderRoot.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = DefaultChatPosition;
            ModSettings.LobbyChatPosition = DefaultChatPosition;
        }
    }

    private void ApplyPlacementMode()
    {
        if (_lobbyHeaderRoot == null) return;
        var rt = _lobbyHeaderRoot.GetComponent<RectTransform>();
        if (rt == null) return;

        if (ModSettings.CustomPlacement)
        {
            rt.anchoredPosition = ModSettings.LobbyChatPosition;
            if (_isMoveMode)
                EnsureMoveHandle();
            else
                RemoveMoveHandle();
        }
        else
        {
            _isMoveMode = false;
            RemoveMoveHandle();
            if (_moveModeHelperCoroutine != null)
            {
                StopCoroutine(_moveModeHelperCoroutine);
                _moveModeHelperCoroutine = null;
            }
            rt.anchoredPosition = DefaultChatPosition;
        }
    }

    private IEnumerator MoveModeHelperMessages()
    {
        while (_isMoveMode)
        {
            yield return new WaitForSeconds(1f);
            if (!_isMoveMode) yield break;
            ShowStackedBubble("", "chat message (to help you see where you are moving the chat)", nameColorHex: null);
        }
    }

    private void EnsureMoveHandle()
    {
        if (_moveHandle != null || _lobbyHeaderRoot == null) return;
        var handleObj = new GameObject("MPChatMoveHandle");
        handleObj.layer = 5; // UI layer for raycast targeting
        handleObj.transform.SetParent(_lobbyHeaderRoot, false);
        handleObj.transform.SetAsLastSibling();

        var rect = handleObj.AddComponent<RectTransform>();
        // Centered grab handle over the chat stack (ignores vertical layout, like draggable affordances in list UIs).
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        const float h = 120f;
        rect.sizeDelta = new Vector2(h, h);

        var layout = handleObj.AddComponent<LayoutElement>();
        layout.minWidth = h;
        layout.minHeight = h;
        layout.preferredWidth = h;
        layout.preferredHeight = h;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;
        layout.ignoreLayout = true;

        var img = handleObj.AddComponent<Image>();
        img.color = new Color(0.4f, 0.7f, 1f, 0.92f);
        img.raycastTarget = true;
        img.sprite = BeatSaberMarkupLanguage.Utilities.ImageResources.BlankSprite;

        var overlay = handleObj.AddComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = 32000;
        handleObj.AddComponent<GraphicRaycaster>();

        handleObj.AddComponent<ChatMoveHandle>();
        _moveHandle = handleObj;
        handleObj.SetActive(true);

        EnsureCanvasRaycaster(handleObj.transform);
        MultiplayerChat.Plugin.Log?.Info("[MPChat] Move handle created on chat root (nested canvas, on top)");

        var rootRect = _lobbyHeaderRoot.GetComponent<RectTransform>();
        if (rootRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
    }

    private void RemoveMoveHandle()
    {
        if (_moveHandle != null)
        {
            UnityEngine.Object.Destroy(_moveHandle);
            _moveHandle = null;
        }
    }

    /// <summary>Force clear all chat bubbles (e.g. from user button). Keeps root to avoid layout corruption.</summary>
    public void ForceClearChat()
    {
        foreach (var bubble in _stackedBubbles)
        {
            if (bubble != null && bubble.gameObject != null)
                UnityEngine.Object.Destroy(bubble.gameObject);
        }
        _stackedBubbles.Clear();
        if (_lobbyHeaderRoot != null && _lobbyHeaderRoot.GetComponent<RectTransform>() != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_lobbyHeaderRoot.GetComponent<RectTransform>());
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
        var banner = FindHostSetupBannerInLobby(allowOverlay: false) ?? FindHostSetupBannerInLobby(allowOverlay: true);
        if (banner != null)
        {
            var root = CreateChatRootAboveBanner(banner);
            if (root != null)
            {
                MultiplayerChat.Plugin.Log?.Info($"[MPChat] Chat attached to lobby UI above HOST SETUP: {banner.name}");
                return root;
            }
        }
        MultiplayerChat.Plugin.Log?.Warn("[MPChat] Could not find HOST SETUP bar in lobby UI - chat bubbles disabled");
        return null;
    }

    /// <summary>
    /// Finds the HOST SETUP bar by scanning text in the scene. Prefers world-space / camera canvases;
    /// when <paramref name="allowOverlay"/> is true, Screen Space Overlay matches are allowed (move handle / chat root).
    /// </summary>
    private static Transform? FindHostSetupBannerInLobby(bool allowOverlay)
    {
        bool AcceptCanvas(Canvas? canvas) =>
            canvas != null && (allowOverlay || canvas.renderMode != RenderMode.ScreenSpaceOverlay);

        foreach (var tmp in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>())
        {
            if (tmp == null) continue;
            var text = (tmp.text ?? "").ToUpperInvariant().Trim();
            if (text.Contains("HOST SETUP") || text.Contains("HOSTSETUP") || text.Contains("CLIENT SETUP") ||
                text.Contains("QUICK PLAY LOBBY") || text == "HOST SETUP" || text == "CLIENT SETUP")
            {
                var canvas = tmp.GetComponentInParent<Canvas>();
                if (AcceptCanvas(canvas))
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
                if (AcceptCanvas(canvas))
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
                if (AcceptCanvas(canvas))
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
                    if (AcceptCanvas(c))
                        return tmp.transform;
                }
            }
            var titleCanvas = titleView.GetComponentInParent<Canvas>();
            if (AcceptCanvas(titleCanvas))
                return titleView.transform;
        }
        return null;
    }

    private static void EnsureCanvasRaycaster(Transform parent)
    {
        var canvas = parent.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
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
    private static readonly Vector2 DefaultChatPosition = Vector2.zero;

    private static Transform? CreateChatRootAboveBanner(Transform banner)
    {
        var canvas = banner.GetComponentInParent<Canvas>();
        if (canvas == null) return null;

        var parent = banner.GetComponent<RectTransform>() != null ? banner : banner.parent;
        if (parent == null) return null;

        var rootObj = new GameObject("MPChatLobbyHeaderStack");
        rootObj.layer = banner.gameObject.layer;
        rootObj.transform.SetParent(parent, false);
        rootObj.transform.SetAsFirstSibling();

        EnsureCanvasRaycaster(parent);

        var rootRect = rootObj.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = ModSettings.CustomPlacement ? ModSettings.LobbyChatPosition : DefaultChatPosition;
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

    private void ShowStackedBubble(string userName, string message, string? nameColorHex = null)
    {
        if (!IsInLobby()) return;

        if (_lobbyHeaderRoot == null)
        {
            var newRoot = FindOrCreateLobbyHeaderChatRoot();
            if (newRoot == null) return;
            _lobbyHeaderRoot = newRoot;
            if (_isMoveMode)
                EnsureMoveHandle();
        }

        var bubble = CreateStackedBubble(_lobbyHeaderRoot);
        if (bubble == null) return;

        var trimmed = TrimName(userName ?? "", 30);
        var safeName = string.IsNullOrEmpty(trimmed) ? "" : trimmed.Replace("<", "&lt;").Replace(">", "&gt;");
        string text;
        if (string.IsNullOrEmpty(userName))
        {
            text = message;
        }
        else if (!string.IsNullOrEmpty(nameColorHex))
        {
            var hex = nameColorHex.Trim();
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length > 6) hex = hex.Substring(0, 6);
            if (hex.Length != 6) hex = "87CEEB";
            text = $"<color=#{hex}>{safeName}</color>: {message}";
        }
        else
        {
            text = $"{safeName}: {message}";
        }
        bubble.SetText(text);
        bubble.Show(ModSettings.BubbleDuration, isStacked: true);
        bubble.transform.SetAsFirstSibling();
        _stackedBubbles.Insert(0, bubble);

        if (_lobbyHeaderRoot != null && _lobbyHeaderRoot.GetComponent<RectTransform>() != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_lobbyHeaderRoot.GetComponent<RectTransform>());

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
        var panelObj = new GameObject("MPChatBubble");
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

    /// <summary>True when the gameplay scene is loaded (often additive; active scene may still be Menu).</summary>
    private static bool IsInSong()
    {
        try
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && s.name == "GameCore")
                    return true;
            }
            return false;
        }
        catch { return false; }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "GameCore" || _stackedBubbles.Count == 0)
            return;
        ClearChat();
    }

    private static string TrimName(string name, int maxLen)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (name.Length <= maxLen) return name;
        return name.Substring(0, maxLen) + "...";
    }
}
