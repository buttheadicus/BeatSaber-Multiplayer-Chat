using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MultiplayerChat.Core;
using MultiplayerChat.Settings;
using HMUI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

namespace MultiplayerChat.UI;

public class ChatBubbleManager : MonoBehaviour, IInitializable, IDisposable
{
    private const int MaxVisibleBubbles = 8;
    private const float BubbleHeight = 36f;

    public static ChatBubbleManager? Instance { get; private set; }

    [Inject] private readonly DiContainer _container = null!;
    [Inject] private readonly ChatManager _chatManager = null!;

    private ChatManager? _messageSubscriptionTarget;

    private readonly List<ChatBubble> _stackedBubbles = new();
    private readonly Dictionary<string, ChatBubble> _ephemeralTypingByUserId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ChatBubble> _ephemeralRecordingByUserId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ChatBubble> _ephemeralLocalByKey = new(StringComparer.Ordinal);
    private Transform? _lobbyHeaderRoot;
    private int _lobbyBannerMissStreak;
    private bool _wasInLobby;
    private bool _isMoveMode;
    private GameObject? _moveHandle;
    private Coroutine? _moveModeHelperCoroutine;

    private float _nextNametagEnsureRealtime = -999f;
    private const float NametagEnsureMinIntervalSec = 2.75f;

    private static readonly string[] LobbyUiScanRoots =
    {
        "MultiplayerLobbyCenterStage", "CenterStage", "LobbySetup", "HostSetup"
    };

    private const float SongLobbyPollSleepSec = 2f;

    public bool IsMoveMode => _isMoveMode;

    public void Initialize()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RebindToActiveChatManager();
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(ChatSoundEffects.LoadClipsRoutine());
        StartCoroutine(EnsureLobbyHeaderRoot());
    }

    public void RebindToActiveChatManager()
    {
        var live = ChatManager.Instance ?? _chatManager;
        if (live == null) return;
        if (_messageSubscriptionTarget == live)
            return;
        var prev = _messageSubscriptionTarget?.GetHashCode().ToString() ?? "null";
        MpChatLobbyDiagnostics.LogVoipTransition("ChatBubbleManager:Rebind",
            $"prevSub={prev} nextSub={live.GetHashCode()} lobby={MpChatLobbyDiagnostics.LobbyHierarchyLooksLikeMultiplayerLobby()} resultsLike={MpChatLobbyDiagnostics.ResultsLikeUiVisible()}");
        if (_messageSubscriptionTarget != null)
            _messageSubscriptionTarget.MessageReceived -= OnMessageReceived;
        _messageSubscriptionTarget = live;
        _messageSubscriptionTarget.MessageReceived += OnMessageReceived;
    }

    public void Dispose()
    {
        // Zenject disposes lobby-scoped bindings when leaving MP UI; we migrate to DontDestroyOnLoad in Initialize.
        if (ReferenceEquals(Instance, this) && gameObject.scene.IsValid() &&
            gameObject.scene.name.IndexOf("DontDestroy", StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
        _isMoveMode = false;
        RemoveMoveHandle();
        if (_messageSubscriptionTarget != null)
            _messageSubscriptionTarget.MessageReceived -= OnMessageReceived;
        _messageSubscriptionTarget = null;
        ClearEphemeralPresenceMaps();
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

    private bool _lastPollInLobby;

    private IEnumerator EnsureLobbyHeaderRoot()
    {
        _lastPollInLobby = false;
        while (true)
        {
            var quickBannerRetry = _lobbyHeaderRoot == null && _lastPollInLobby && _lobbyBannerMissStreak < 40;
            yield return new WaitForSeconds(quickBannerRetry ? 0.12f : 0.5f);

            var inSong = IsInSong();
            if (inSong)
            {
                if (_stackedBubbles.Count > 0)
                    ClearChat();
                _wasInLobby = false;
                _lastPollInLobby = false;
                yield return new WaitForSeconds(SongLobbyPollSleepSec);
                continue;
            }

            var inLobby = IsInLobby();
            _lastPollInLobby = inLobby;

            if (_wasInLobby && !inLobby)
                ClearChat();

            if (inLobby && !_wasInLobby)
            {
                _nextNametagEnsureRealtime = -999f;
                RebindToActiveChatManager();
            }

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
                    {
                        _lobbyHeaderRoot = root;
                        _lobbyBannerMissStreak = 0;
                    }
                    else
                        _lobbyBannerMissStreak++;
                }

                if (_lobbyHeaderRoot != null)
                {
                    EnsureNametagIcons();
                    ApplyPlacementMode();
                }
            }
            else
                _lobbyBannerMissStreak = 0;
        }
    }

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

    public void ResetToDefaultPosition()
    {
        if (_lobbyHeaderRoot == null) return;
        var rt = _lobbyHeaderRoot.GetComponent<RectTransform>();
        if (rt != null)
        {
            var pos = DefaultAnchoredPositionForCurrentLobbyRoot();
            rt.anchoredPosition = pos;
            ModSettings.LobbyChatPosition = pos;
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
            rt.anchoredPosition = DefaultAnchoredPositionForCurrentLobbyRoot();
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

    public void ForceClearChat()
    {
        ClearEphemeralPresenceMaps();
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
        ClearEphemeralPresenceMaps();
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
        var now = Time.realtimeSinceStartup;
        if (now < _nextNametagEnsureRealtime)
            return;
        _nextNametagEnsureRealtime = now + NametagEnsureMinIntervalSec;

        var hitAnyRoot = false;
        foreach (var rootName in LobbyUiScanRoots)
        {
            var go = GameObject.Find(rootName);
            if (go == null) continue;
            hitAnyRoot = true;
            foreach (var ctrl in go.GetComponentsInChildren<MultiplayerLobbyAvatarController>(true))
                TryAttachNametagAnchor(ctrl);
        }

        if (!hitAnyRoot)
        {
            foreach (var ctrl in UnityEngine.Object.FindObjectsOfType<MultiplayerLobbyAvatarController>())
                TryAttachNametagAnchor(ctrl);
        }
    }

    private static void TryAttachNametagAnchor(MultiplayerLobbyAvatarController ctrl)
    {
        if (ctrl == null) return;
        var cap = ctrl.transform.Find("AvatarCaption") ?? FindRecursive(ctrl.transform, "AvatarCaption")
            ?? FindRecursive(ctrl.transform, "PlayerName") ?? FindRecursive(ctrl.transform, "Name")
            ?? FindNametagByText(ctrl.transform);
        if (cap != null && cap.GetComponent<ChatBubbleAnchor>() == null)
            cap.gameObject.AddComponent<ChatBubbleAnchor>();
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

    private Transform? FindOrCreateLobbyHeaderChatRoot()
    {
        var titleAnchored = CreateLobbyChatRootAboveTitleBar();
        if (titleAnchored != null)
            return titleAnchored;

        var startAnchored = CreateLobbyChatRootAboveStartButtonRow();
        if (startAnchored != null)
            return startAnchored;

        var banner = FindHostSetupBannerInLobby(allowOverlay: false) ?? FindHostSetupBannerInLobby(allowOverlay: true);
        if (banner != null)
        {
            var root = CreateChatRootAboveBanner(banner);
            if (root != null)
            {
                MultiplayerChat.Plugin.Log?.Info($"[MPChat] Chat attached to lobby UI via header text: {banner.name}");
                return root;
            }
        }

        MultiplayerChat.Plugin.Log?.Warn("[MPChat] Could not anchor lobby chat (title bar, START row, or header bar not found)");
        return null;
    }

    // Inserts a strip immediately BEFORE TitleViewController so vertical layouts stack it above the title row:
    // bottom edge of the chat block sits just above the top edge of the title bar.
    private static Transform? CreateLobbyChatRootAboveTitleBar()
    {
        var titleGo = GameObject.Find("Wrapper/MenuCore/UI/ScreenSystem/TopScreen/TitleViewController")
                      ?? GameObject.Find("TitleViewController");
        if (titleGo == null)
            return null;

        var titleView = titleGo.transform;
        var parent = titleView.parent;
        if (parent == null)
            return null;

        if (parent.GetComponentInParent<Canvas>() == null)
            return null;

        var rootObj = new GameObject("MPChatLobbyHeaderStack");
        rootObj.layer = titleView.gameObject.layer;

        rootObj.transform.SetParent(parent, false);
        rootObj.transform.SetSiblingIndex(titleView.GetSiblingIndex());

        var rootRect = rootObj.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.sizeDelta = new Vector2(0f, 320f);
        rootRect.anchoredPosition = ModSettings.CustomPlacement
            ? ModSettings.LobbyChatPosition
            : DefaultChatPosition + TitleBarLobbyChatAnchoredPositionOffset;

        rootObj.AddComponent<MpChatTitleBarAnchoredChatRoot>();

        var le = rootObj.AddComponent<LayoutElement>();
        le.preferredHeight = 320f;
        le.minHeight = 96f;
        le.flexibleHeight = 0f;

        EnsureCanvasRaycaster(rootObj.transform);

        ApplyLobbyBubbleStackLayout(rootObj);

        MultiplayerChat.Plugin.Log?.Info(
            $"[MPChat] Chat strip above title bar (before {titleView.name} in layout; parent={parent.name}; anchoredYOffset+= {TitleBarLobbyChatAnchoredPositionOffset.y})");
        return rootObj.transform;
    }

    private static Transform? CreateLobbyChatRootAboveStartButtonRow()
    {
        var start = LobbyUiStartButtonLocator.FindStartButtonTransform();
        if (start == null)
            return null;

        var buttonRow = start.parent;
        if (buttonRow == null)
            return null;

        var attachParent = buttonRow.parent;
        if (attachParent == null)
            return null;

        // START often sits inside a horizontal strip; prefer inserting above that strip inside a vertical panel.
        if (attachParent.GetComponent<HorizontalLayoutGroup>() != null &&
            attachParent.GetComponent<VerticalLayoutGroup>() == null &&
            attachParent.parent != null)
        {
            buttonRow = attachParent;
            attachParent = attachParent.parent;
            if (attachParent == null)
                return null;
        }

        if (attachParent.GetComponentInParent<Canvas>() == null)
            return null;

        var insertIndex = buttonRow.GetSiblingIndex();

        var rootObj = new GameObject("MPChatLobbyHeaderStack");
        rootObj.layer = start.gameObject.layer;
        rootObj.transform.SetParent(attachParent, false);
        rootObj.transform.SetSiblingIndex(insertIndex);

        EnsureCanvasRaycaster(attachParent);

        var rootRect = rootObj.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.sizeDelta = new Vector2(0f, 320f);
        rootRect.anchoredPosition = ModSettings.CustomPlacement ? ModSettings.LobbyChatPosition : DefaultChatPosition;

        var le = rootObj.AddComponent<LayoutElement>();
        le.preferredHeight = 320f;
        le.minHeight = 96f;
        le.flexibleHeight = 0f;

        ApplyLobbyBubbleStackLayout(rootObj);

        MultiplayerChat.Plugin.Log?.Info($"[MPChat] Chat attached above START row (parent={attachParent.name})");
        return rootObj.transform;
    }

    private static void ApplyLobbyBubbleStackLayout(GameObject rootObj)
    {
        var vlg = rootObj.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.LowerCenter;
        vlg.childControlHeight = true;
        vlg.childControlWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.spacing = 2f;
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.reverseArrangement = true;
    }

    private static Transform? FindHostSetupBannerInLobby(bool allowOverlay)
    {
        bool AcceptCanvas(Canvas? canvas) =>
            canvas != null && (allowOverlay || canvas.renderMode != RenderMode.ScreenSpaceOverlay);

        foreach (var rootName in LobbyUiScanRoots)
        {
            var rootGo = GameObject.Find(rootName);
            if (rootGo == null) continue;
            var hit = ScanLobbyBannerTextUnderHierarchy(rootGo.transform, AcceptCanvas);
            if (hit != null) return hit;

            var found = FindInChildren(rootGo.transform, t =>
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
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;
                var text = (tmp.text ?? "").ToUpperInvariant();
                if (BannerTextLooksLikeLobbyHeader(text))
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

        foreach (var tmp in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>())
        {
            if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;
            var text = (tmp.text ?? "").ToUpperInvariant().Trim();
            if (BannerTextLooksLikeLobbyHeader(text))
            {
                var canvas = tmp.GetComponentInParent<Canvas>();
                if (AcceptCanvas(canvas))
                    return tmp.transform;
            }
        }

        foreach (var curved in UnityEngine.Object.FindObjectsOfType<CurvedTextMeshPro>())
        {
            if (curved == null || !curved.gameObject.activeInHierarchy) continue;
            var text = (curved.text ?? "").ToUpperInvariant().Trim();
            if (BannerTextLooksLikeLobbyHeader(text))
            {
                var canvas = curved.GetComponentInParent<Canvas>();
                if (AcceptCanvas(canvas))
                    return curved.transform;
            }
        }

        return null;
    }

    private static Transform? ScanLobbyBannerTextUnderHierarchy(Transform root, Func<Canvas?, bool> acceptCanvas)
    {
        foreach (var tmp in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;
            var text = (tmp.text ?? "").ToUpperInvariant().Trim();
            if (!BannerTextLooksLikeLobbyHeader(text)) continue;
            var canvas = tmp.GetComponentInParent<Canvas>();
            if (acceptCanvas(canvas))
                return tmp.transform;
        }

        foreach (var curved in root.GetComponentsInChildren<CurvedTextMeshPro>(true))
        {
            if (curved == null || !curved.gameObject.activeInHierarchy) continue;
            var text = (curved.text ?? "").ToUpperInvariant().Trim();
            if (!BannerTextLooksLikeLobbyHeader(text)) continue;
            var canvas = curved.GetComponentInParent<Canvas>();
            if (acceptCanvas(canvas))
                return curved.transform;
        }

        return null;
    }

    private static bool BannerTextLooksLikeLobbyHeader(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("HOST SETUP") || text.Contains("HOSTSETUP") || text.Contains("CLIENT SETUP") ||
               text.Contains("QUICK PLAY LOBBY") || text.Contains("SERVER SETUP") ||
               text.Contains("MULTIPLAYER LOBBY") || text.Contains("PRIVATE LOBBY") ||
               text.Contains("ROOM CODE") || text.Contains("INVITE CODE") || text.Contains("BEAT TOGETHER") ||
               text == "HOST SETUP" || text == "CLIENT SETUP";
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

    private static readonly Vector2 DefaultChatPosition = Vector2.zero;

    // Extra anchoredPosition applied only when the lobby chat strip is parented above TitleViewController.
    // Increase Y to shift the strip toward the top of the screen (away from the lobby floor); decrease if it moves the wrong way.
    private static readonly Vector2 TitleBarLobbyChatAnchoredPositionOffset = new(0f, 310f);

    private Vector2 DefaultAnchoredPositionForCurrentLobbyRoot()
    {
        if (_lobbyHeaderRoot != null && _lobbyHeaderRoot.GetComponent<MpChatTitleBarAnchoredChatRoot>() != null)
            return DefaultChatPosition + TitleBarLobbyChatAnchoredPositionOffset;
        return DefaultChatPosition;
    }

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

        ApplyLobbyBubbleStackLayout(rootObj);

        return rootObj.transform;
    }

    public void SetEphemeralTypingLine(string senderUserId, bool visible, string richText)
    {
        SetEphemeralLine(_ephemeralTypingByUserId, senderUserId, visible, richText);
    }

    public void SetEphemeralRecordingVoiceLine(string senderUserId, bool visible, string richText)
    {
        SetEphemeralLine(_ephemeralRecordingByUserId, senderUserId, visible, richText);
    }

    private const string LocalPttEphemeralKey = "__local_ptt__";

    public void SetLocalPushToTalkOpen(bool visible)
    {
        SetEphemeralLine(_ephemeralLocalByKey, LocalPttEphemeralKey, visible,
            "<color=#CCCCCC>Push To Talk is open.</color>");
    }

    private void ClearEphemeralPresenceMaps()
    {
        foreach (var kv in _ephemeralTypingByUserId.ToList())
        {
            if (kv.Value == null) continue;
            RemoveBubbleFromStack(kv.Value);
            kv.Value.DismissEphemeral();
        }

        _ephemeralTypingByUserId.Clear();
        foreach (var kv in _ephemeralRecordingByUserId.ToList())
        {
            if (kv.Value == null) continue;
            RemoveBubbleFromStack(kv.Value);
            kv.Value.DismissEphemeral();
        }

        _ephemeralRecordingByUserId.Clear();
        foreach (var kv in _ephemeralLocalByKey.ToList())
        {
            if (kv.Value == null) continue;
            RemoveBubbleFromStack(kv.Value);
            kv.Value.DismissEphemeral();
        }

        _ephemeralLocalByKey.Clear();
    }

    private void RemoveBubbleFromStack(ChatBubble bubble)
    {
        if (bubble == null) return;
        _stackedBubbles.Remove(bubble);
    }

    private void SetEphemeralLine(Dictionary<string, ChatBubble> map, string senderUserId, bool visible, string richText)
    {
        if (string.IsNullOrEmpty(senderUserId)) return;
        if (!visible)
        {
            if (map.TryGetValue(senderUserId, out var old) && old != null)
            {
                RemoveBubbleFromStack(old);
                old.DismissEphemeral();
                map.Remove(senderUserId);
            }

            if (_lobbyHeaderRoot != null)
            {
                var rt = _lobbyHeaderRoot.GetComponent<RectTransform>();
                if (rt != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }

            return;
        }

        if (!IsInLobby()) return;

        if (_lobbyHeaderRoot == null)
        {
            var newRoot = FindOrCreateLobbyHeaderChatRoot();
            if (newRoot == null) return;
            _lobbyHeaderRoot = newRoot;
            if (_isMoveMode)
                EnsureMoveHandle();
        }

        if (map.TryGetValue(senderUserId, out var existing) && existing != null && existing.gameObject != null)
        {
            existing.SetText(richText);
            var rt0 = _lobbyHeaderRoot.GetComponent<RectTransform>();
            if (rt0 != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt0);
            return;
        }

        var bubble = CreateStackedBubble(_lobbyHeaderRoot);
        if (bubble == null) return;
        bubble.SetText(richText);
        bubble.ShowStackedPersistent();
        bubble.transform.SetAsFirstSibling();
        _stackedBubbles.Insert(0, bubble);
        map[senderUserId] = bubble;

        var rt1 = _lobbyHeaderRoot.GetComponent<RectTransform>();
        if (rt1 != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt1);

        while (_stackedBubbles.Count > MaxVisibleBubbles)
        {
            var oldest = _stackedBubbles[_stackedBubbles.Count - 1];
            _stackedBubbles.RemoveAt(_stackedBubbles.Count - 1);
            if (oldest == null) continue;
            RemoveFromEphemeralMaps(oldest);
            UnityEngine.Object.Destroy(oldest.gameObject);
        }
    }

    private void RemoveFromEphemeralMaps(ChatBubble bubble)
    {
        foreach (var kv in _ephemeralTypingByUserId.ToList())
        {
            if (kv.Value == bubble)
                _ephemeralTypingByUserId.Remove(kv.Key);
        }

        foreach (var kv in _ephemeralRecordingByUserId.ToList())
        {
            if (kv.Value == bubble)
                _ephemeralRecordingByUserId.Remove(kv.Key);
        }

        foreach (var kv in _ephemeralLocalByKey.ToList())
        {
            if (kv.Value == bubble)
                _ephemeralLocalByKey.Remove(kv.Key);
        }
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
            var hex = nameColorHex!.Trim();
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
            {
                RemoveFromEphemeralMaps(oldest);
                UnityEngine.Object.Destroy(oldest.gameObject);
            }
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

    private static bool IsInSong() => MpChatLobbyDiagnostics.SongGameplayLikelyActive();

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        MpChatLobbyDiagnostics.InvalidateSceneHeuristicCaches();
        RebindToActiveChatManager();
        _lobbyBannerMissStreak = 0;
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

internal sealed class MpChatTitleBarAnchoredChatRoot : MonoBehaviour
{
}
