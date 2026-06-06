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

// Lobby stacked chat bubbles plus ephemeral typing/recording lines; anchors under multiplayer lobby UI (prefers a strip above TitleViewController).
public class ChatBubbleManager : MonoBehaviour, IInitializable, IDisposable
{
    private const int MaxVisibleBubbles = 8;
    private const float BubbleHeight = 36f;

    public static ChatBubbleManager? Instance { get; private set; }

    [Inject] private readonly DiContainer _container = null!;
    [Inject(Optional = true)] private readonly ChatManager? _chatManager;

    private ChatManager? _messageSubscriptionTarget;

    private readonly List<ChatBubble> _stackedBubbles = new();

    private const float AvatarTransferStatusBubbleSeconds = 5f;
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
    private const float NametagEnsureMinIntervalSec = 4.5f;

    private ChatBubble? _timedHeaderNoticeBubble;
    private Coroutine? _timedHeaderNoticeCoroutine;

    public const string UpdateAvailableHeaderMessage =
        "There is an update for MultiplayerChat! I've already opened a link in your browser to download and install it!";

    private static readonly string[] LobbyUiScanRoots =
    {
        "MultiplayerLobbyCenterStage", "CenterStage", "LobbySetup", "HostSetup"
    };

    private const float SongLobbyPollSleepSec = 2f;

    private const float MainMenuIdlePollSleepSec = 4f;

    private const float MainMenuTitleBarPollSleepSec = 4f;

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
        if (!this)
            return;

        if (ReferenceEquals(Instance, this))
        {
            try
            {
                var host = gameObject;
                if (host != null)
                {
                    var scene = host.scene;
                    if (scene.IsValid() &&
                        scene.name.IndexOf("DontDestroy", StringComparison.OrdinalIgnoreCase) >= 0)
                        return;
                }
            }
            catch (MissingReferenceException)
            {
                return;
            }
        }

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
            if (bubble == null)
                continue;
            try
            {
                var bubbleGo = bubble.gameObject;
                if (bubbleGo != null)
                    UnityEngine.Object.Destroy(bubbleGo);
            }
            catch (MissingReferenceException)
            {
                /* destroyed with scene */
            }
        }

        _stackedBubbles.Clear();
        if (_lobbyHeaderRoot != null)
        {
            try
            {
                var headerGo = _lobbyHeaderRoot.gameObject;
                _lobbyHeaderRoot = null;
                if (headerGo != null)
                    UnityEngine.Object.Destroy(headerGo);
            }
            catch (MissingReferenceException)
            {
                _lobbyHeaderRoot = null;
            }
        }
    }

    private void OnMessageReceived(object? sender, ChatMessageEventArgs e)
    {
        if (e.IsSystem)
        {
            var systemText = e.Message ?? "";
            if (IsAvatarTransferStatusMessage(systemText))
            {
                TryShowStackedBubble("", systemText, durationSeconds: AvatarTransferStatusBubbleSeconds);
                return;
            }

            ShowStackedBubble("", systemText);
            return;
        }
        var name = TrimName(e.UserName ?? "", 30);
        var msg = e.Message ?? "";
        if (e.IsDM)
            msg = $"(DM) {msg}";
        ShowStackedBubble(name, msg, e.NameColor);
    }

    private bool _lastPollInLobby;

    // Polls lobby vs song; finds/creates header root, clears bubbles when leaving lobby, keeps nametag icons wired while in lobby.
    private IEnumerator EnsureLobbyHeaderRoot()
    {
        _lastPollInLobby = false;
        while (true)
        {
            var quickBannerRetry = _lobbyHeaderRoot == null && _lastPollInLobby && _lobbyBannerMissStreak < 40;
            var inSong = IsInSong();
            var inLobby = IsInLobby();
            var showTitleBarChat = ShouldShowTitleBarChat();
            var pollDelay = ResolveLobbyHeaderPollDelay(quickBannerRetry, inLobby, inSong, showTitleBarChat);
            yield return new WaitForSeconds(pollDelay);

            inSong = IsInSong();
            if (inSong)
            {
                if (_stackedBubbles.Count > 0)
                    ClearChat();
                _wasInLobby = false;
                _lastPollInLobby = false;
                yield return new WaitForSeconds(SongLobbyPollSleepSec);
                continue;
            }

            inLobby = IsInLobby();
            showTitleBarChat = ShouldShowTitleBarChat();
            _lastPollInLobby = inLobby;

            if (_wasInLobby && !inLobby && !showTitleBarChat)
                ClearChat();

            if (inLobby && !_wasInLobby)
            {
                _nextNametagEnsureRealtime = -999f;
                RebindToActiveChatManager();
                ModPresenceManager.Instance?.RefreshAfterLobbyReturn();
                MpCustomAvatarSyncManager.PollDeferredAvatarUpdates();
            }

            _wasInLobby = inLobby;

            if (_lobbyHeaderRoot != null)
            {
                if (_lobbyHeaderRoot.gameObject != null)
                    _lobbyHeaderRoot.gameObject.SetActive(showTitleBarChat);
                else
                    _lobbyHeaderRoot = null;
            }

            if (showTitleBarChat)
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
                else if (!IsMainMenuTitleBarPollContext(inLobby))
                    TryUpgradeLobbyHeaderRootToTitleBarPreferred();

                if (_lobbyHeaderRoot != null)
                {
                    if (inLobby && Time.realtimeSinceStartup >= _nextNametagEnsureRealtime)
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
            ApplyDefaultLobbyHeaderAnchoredPosition(rt);
        }
    }

    public void ShowTimedHeaderSystemMessage(string message, float durationSeconds = 30f)
    {
        if (string.IsNullOrEmpty(message))
            return;

        StopTimedHeaderNotice();
        if (!TryShowStackedBubble("", message))
            return;

        if (_stackedBubbles.Count > 0)
            _timedHeaderNoticeBubble = _stackedBubbles[0];
        _timedHeaderNoticeCoroutine = StartCoroutine(ClearTimedHeaderNoticeAfter(durationSeconds));
    }

    public void ShowUpdateAvailableNoticeTest() =>
        ShowTimedHeaderSystemMessage(UpdateAvailableHeaderMessage, 30f);

    private void StopTimedHeaderNotice()
    {
        if (_timedHeaderNoticeCoroutine != null)
        {
            StopCoroutine(_timedHeaderNoticeCoroutine);
            _timedHeaderNoticeCoroutine = null;
        }

        if (_timedHeaderNoticeBubble != null)
        {
            RemoveBubbleFromStack(_timedHeaderNoticeBubble);
            if (_timedHeaderNoticeBubble.gameObject != null)
                UnityEngine.Object.Destroy(_timedHeaderNoticeBubble.gameObject);
            _timedHeaderNoticeBubble = null;
        }
    }

    private IEnumerator ClearTimedHeaderNoticeAfter(float durationSeconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0.5f, durationSeconds));
        StopTimedHeaderNotice();
    }

    private void ApplyDefaultLobbyHeaderAnchoredPosition(RectTransform rt)
    {
        if (_lobbyHeaderRoot != null && _lobbyHeaderRoot.GetComponent<MpChatTitleBarAnchoredChatRoot>() != null)
            MpChatTitleBarAnchoredChatRoot.ApplyAnchoredPosition(rt, ModSettings.CustomPlacement, ModSettings.LobbyChatPosition);
        else
            rt.anchoredPosition = DefaultAnchoredPositionForCurrentLobbyRoot();
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

    private void TryUpgradeLobbyHeaderRootToTitleBarPreferred()
    {
        if (_lobbyHeaderRoot == null || _lobbyHeaderRoot.gameObject == null)
            return;

        if (_lobbyHeaderRoot.GetComponent<MpChatTitleBarAnchoredChatRoot>() != null)
            return;

        var titleAnchored = CreateLobbyChatRootAboveTitleBar();
        if (titleAnchored == null)
            return;

        var bubblesToMove = _lobbyHeaderRoot.GetComponentsInChildren<ChatBubble>(true);
        foreach (var bubble in bubblesToMove)
        {
            if (bubble != null && bubble.gameObject != null)
                bubble.transform.SetParent(titleAnchored, false);
        }

        UnityEngine.Object.Destroy(_lobbyHeaderRoot.gameObject);
        _lobbyHeaderRoot = titleAnchored;
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
    private static Transform? FindTitleViewControllerTransformForChatAnchor()
    {
        var direct = GameObject.Find("Wrapper/MenuCore/UI/ScreenSystem/TopScreen/TitleViewController")
                     ?? GameObject.Find("TitleViewController");
        if (direct != null)
            return direct.transform;

        foreach (var rootName in new[] { "Wrapper", "MenuCore" })
        {
            var rootGo = GameObject.Find(rootName);
            if (rootGo == null)
                continue;
            foreach (var tr in rootGo.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name != "TitleViewController")
                    continue;
                if (tr.GetComponentInParent<Canvas>() == null)
                    continue;
                return tr;
            }
        }

        return null;
    }

    private static Transform? CreateLobbyChatRootAboveTitleBar()
    {
        var titleView = TryGetCachedTitleViewForChatAnchor();
        if (titleView == null)
            return null;
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
        rootObj.AddComponent<MpChatTitleBarAnchoredChatRoot>();

        var le = rootObj.AddComponent<LayoutElement>();
        le.preferredHeight = 320f;
        le.minHeight = 96f;
        le.flexibleHeight = 0f;
        le.ignoreLayout = true;

        EnsureCanvasRaycaster(rootObj.transform);

        ApplyLobbyBubbleStackLayout(rootObj);
        MpChatTitleBarAnchoredChatRoot.ApplyAnchoredPosition(rootRect, ModSettings.CustomPlacement, ModSettings.LobbyChatPosition);

        MpChatLog.DebugLine(
            $"[MPChat] Chat strip above title bar (before {titleView.name}; parent={parent.name})");
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

        var titleTf = TryGetCachedTitleViewForChatAnchor();
        var titleView = titleTf != null ? titleTf.gameObject : null;

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

    private Vector2 DefaultAnchoredPositionForCurrentLobbyRoot()
    {
        if (_lobbyHeaderRoot != null && _lobbyHeaderRoot.GetComponent<MpChatTitleBarAnchoredChatRoot>() != null)
            return MpChatTitleBarAnchoredChatRoot.DefaultAnchoredOffset;
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

    private static bool IsAvatarTransferStatusMessage(string message) =>
        message.IndexOf("Downloading ", StringComparison.Ordinal) >= 0 &&
        message.IndexOf("avatar. Please wait.", StringComparison.Ordinal) >= 0;

    private bool TryShowStackedBubble(
        string userName,
        string message,
        string? nameColorHex = null,
        float? durationSeconds = null)
    {
        if (!ShouldShowTitleBarChat())
            return false;

        if (_lobbyHeaderRoot == null)
        {
            var newRoot = FindOrCreateLobbyHeaderChatRoot();
            if (newRoot == null)
                return false;
            _lobbyHeaderRoot = newRoot;
            if (_isMoveMode)
                EnsureMoveHandle();
        }

        var bubble = CreateStackedBubble(_lobbyHeaderRoot);
        if (bubble == null)
            return false;

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
        bubble.Show(durationSeconds ?? ModSettings.BubbleDuration, isStacked: true);
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

        return true;
    }

    private void ShowStackedBubble(string userName, string message, string? nameColorHex = null) =>
        TryShowStackedBubble(userName, message, nameColorHex);

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
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = 14;
        tmp.color = Color.white;
        tmp.richText = true;
        if (tmp.font != null)
        {
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color32(0, 0, 0, 200);
        }
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.maxVisibleLines = 6;
        tmp.raycastTarget = false;
        tmp.isOverlay = false;

        return _container.InstantiateComponent<ChatBubble>(panelObj);
    }

    private static float _deepLobbyLayoutScanTime = -999f;

    private static bool _deepLobbyLayoutScanHit;

    private static Transform? _cachedTitleViewForChatAnchor;

    private static float ResolveLobbyHeaderPollDelay(
        bool quickBannerRetry,
        bool inLobby,
        bool inSong,
        bool showTitleBarChat)
    {
        if (quickBannerRetry)
            return 0.12f;
        if (inSong || inLobby)
            return 0.5f;
        if (showTitleBarChat && IsMainMenuTitleBarPollContext(inLobby))
            return MainMenuTitleBarPollSleepSec;
        if (showTitleBarChat)
            return 0.5f;
        return MainMenuIdlePollSleepSec;
    }

    private static bool IsMainMenuTitleBarPollContext(bool inLobby) =>
        !inLobby && MpChatLobbyDiagnostics.ActiveSceneIsMainMenuWithoutGameCore();

    private bool ShouldShowTitleBarChat()
    {
        if (MpChatLobbyDiagnostics.SongGameplayLikelyActive())
            return false;
        if (IsInLobby())
            return true;
        if (_lobbyHeaderRoot != null && _lobbyHeaderRoot.gameObject != null)
            return true;
        return TryGetCachedTitleViewForChatAnchor() != null;
    }

    private static void InvalidateTitleViewChatAnchorCache() =>
        _cachedTitleViewForChatAnchor = null;

    private static Transform? TryGetCachedTitleViewForChatAnchor()
    {
        if (_cachedTitleViewForChatAnchor != null)
        {
            if (_cachedTitleViewForChatAnchor)
                return _cachedTitleViewForChatAnchor;
            _cachedTitleViewForChatAnchor = null;
        }

        _cachedTitleViewForChatAnchor = FindTitleViewControllerTransformForChatAnchor();
        return _cachedTitleViewForChatAnchor;
    }

    private bool IsInLobby()
    {
        if (MpChatLobbyDiagnostics.LobbyHierarchyLooksLikeMultiplayerLobby())
            return true;

        // Arena to lobby: MP chrome can stay inactive under results while GameObject.Find misses title rows.
        if (MpChatLobbyDiagnostics.SongGameplayLikelyActive())
            return false;
        if (!MpChatLobbyDiagnostics.ResultsLikeUiVisible())
            return false;

        return MultiplayerLobbyLayoutExistsIncludingInactiveCached();
    }

    private static bool MultiplayerLobbyLayoutExistsIncludingInactiveCached()
    {
        var now = Time.realtimeSinceStartup;
        if (now - _deepLobbyLayoutScanTime < 1f)
            return _deepLobbyLayoutScanHit;
        _deepLobbyLayoutScanTime = now;
        _deepLobbyLayoutScanHit = false;

        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!DescendantHasInactiveMpLobbyChromeName(root.transform))
                    continue;
                _deepLobbyLayoutScanHit = true;
                return true;
            }
        }

        return false;
    }

    private static bool DescendantHasInactiveMpLobbyChromeName(Transform root)
    {
        var stack = new Stack<Transform>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            var n = t.gameObject.name;
            if (n == "MultiplayerLobbyCenterStage" || n == "LobbySetup" || n == "HostSetup")
                return true;
            for (var c = 0; c < t.childCount; c++)
                stack.Push(t.GetChild(c));
        }

        return false;
    }

    private static bool IsInSong() => MpChatLobbyDiagnostics.SongGameplayLikelyActive();

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        MpChatLobbyDiagnostics.InvalidateSceneHeuristicCaches();
        InvalidateTitleViewChatAnchorCache();
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

// Title-bar chat strip: ignore parent layout groups and keep a stable anchored offset above TitleViewController.
internal sealed class MpChatTitleBarAnchoredChatRoot : MonoBehaviour
{
    internal static readonly Vector2 DefaultAnchoredOffset = new(0f, 310f);

    private int _stabilizeFramesLeft;

    private void OnEnable()
    {
        _stabilizeFramesLeft = 12;
        StabilizeNow();
    }

    private void LateUpdate()
    {
        if (_stabilizeFramesLeft <= 0)
            return;
        _stabilizeFramesLeft--;
        StabilizeNow();
    }

    private void StabilizeNow()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null)
            return;
        if (TryGetComponent<LayoutElement>(out var le))
            le.ignoreLayout = true;
        ApplyAnchoredPosition(rt, ModSettings.CustomPlacement, ModSettings.LobbyChatPosition);
    }

    internal static void ApplyAnchoredPosition(RectTransform rt, bool customPlacement, Vector2 customPosition)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = customPlacement ? customPosition : DefaultAnchoredOffset;
    }
}
