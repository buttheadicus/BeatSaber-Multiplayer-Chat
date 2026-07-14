using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HMUI;
using MultiplayerChat.AvatarExtras.Assets;
using MultiplayerChat.Core;
using MultiplayerCore.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace MultiplayerChat.UI;

// nametag row: [custom avatars?][chat][MPEX][name]. Mod users also get mic/headphone icons centered above the row.
public class ChatBubbleAnchor : MonoBehaviour
{
    // MPEX platform icons: 64px assets, 10 PPU, localScale 3.2 on the RectTransform.
    private const float MpexNametagIconPixelSize = 64f;
    private const float MpexNametagIconPixelsPerUnit = 10f;
    private const float MpexNametagIconLocalScale = 3.2f;
    private const float PlatformIconLeftGapPx = 1f;
    private const float StatusIconGapPx = 2f;
    private const float StatusIconExtraLiftLocal = 0.24f;
    private const float SlzCaptionFontSize = 1.55f;
    private const float SlzCaptionLiftAboveStatusLocal = 0.18f;
    private const string SlzDeveloperCaption =
        "yes, i (the developer) can hear you. i cannot speak to you though, the bot has control of the game... mostly.";
    private const int NametagSetupWaitFrames = 45;

    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;
    private static readonly List<ChatBubbleAnchor> ActiveAnchors = new(16);
    private static Sprite? _chatIconSprite;
    private static Sprite? _customAvatarsIconSprite;

    private IConnectedPlayer? _player;
    private ImageView? _bg;
    private CurvedTextMeshPro? _nameText;
    private ImageView? _iconView;
    private ImageView? _customAvatarsIconView;
    private Transform? _statusRow;
    private ImageView? _micStatusView;
    private ImageView? _headphoneStatusView;
    private CurvedTextMeshPro? _slzCaptionText;
    private bool _registeredForStatusTick;
    private bool _subscribedLocalVoiceState;
    private NametagMicIconState _lastMicState = (NametagMicIconState)(-1);
    private NametagHeadphoneIconState _lastHeadphoneState = (NametagHeadphoneIconState)(-1);
    private ModPresenceManager? _subscribedModPresence;

    private void Awake()
    {
        MpChatLobbyAvatarZenject.TryInject(this);
    }

    [Inject]
    private void Construct(IConnectedPlayer player)
    {
        _player = player;
    }

    private void Start()
    {
        StartCoroutine(SetupWhenReady());
    }

    internal static int ActiveStatusAnchorCount => ActiveAnchors.Count;

    private void OnDestroy()
    {
        if (_registeredForStatusTick)
            ActiveAnchors.Remove(this);
        if (_subscribedModPresence != null)
        {
            _subscribedModPresence.PresenceUpdated -= OnPresenceUpdated;
            _subscribedModPresence.PlayerWithModAdded -= OnPlayerWithModAdded;
            _subscribedModPresence = null;
        }

        if (_subscribedLocalVoiceState)
        {
            VoiceChatRuntimeState.Changed -= OnLocalVoiceRuntimeStateChanged;
            _subscribedLocalVoiceState = false;
        }
    }

    internal static void TickAllStatusIcons()
    {
        if (!MpChatLobbyDiagnostics.NametagVoiceLobbySyncActive())
            return;

        for (var i = ActiveAnchors.Count - 1; i >= 0; i--)
        {
            var anchor = ActiveAnchors[i];
            if (!anchor)
            {
                ActiveAnchors.RemoveAt(i);
                continue;
            }

            anchor.RefreshStatusIconsIfNeeded();
        }
    }

    private IEnumerator SetupWhenReady()
    {
        for (var i = 0; i < 120; i++)
        {
            if (_player == null || string.IsNullOrEmpty(_player.userId))
            {
                MpChatLobbyAvatarZenject.TryInject(this);
                _player ??= ResolvePlayerFallback();
            }

            if (_player != null && !string.IsNullOrEmpty(_player.userId))
                break;
            yield return null;
        }

        if (_player == null || string.IsNullOrEmpty(_player.userId))
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] ChatBubbleAnchor: could not get userId from controller/place");
            yield break;
        }

        if (!EnsureNameTagRefs())
            yield break;

        yield return WaitForNametagLayoutStable();

        CreateNametagIcons();
        UpdateIconVisibility();

        var modPresence = ModPresenceManager.Instance;
        if (modPresence != null)
        {
            _subscribedModPresence = modPresence;
            _subscribedModPresence.PresenceUpdated += OnPresenceUpdated;
            _subscribedModPresence.PlayerWithModAdded += OnPlayerWithModAdded;
        }

        EnsureLocalVoiceStateSubscription();

        // MPEX re-parents its platform icon to sibling 0 when platform data arrives; keep order stable (lightweight retries).
        for (var i = 0; i < 4; i++)
        {
            ApplyNametagLayoutOrder();
            yield return new WaitForSeconds(0.2f);
        }
    }

    private IEnumerator WaitForNametagLayoutStable()
    {
        var hasMpexTag = HasMpexAvatarNameTag();
        for (var i = 0; i < NametagSetupWaitFrames; i++)
        {
            if (_bg == null || _nameText == null)
            {
                yield return null;
                continue;
            }

            if (hasMpexTag)
            {
                if (HasMpexPlayerIcon(_bg.transform))
                    yield break;
            }
            else if (IsExternalNametagLayoutReady())
            {
                yield break;
            }

            yield return null;
        }
    }

    private bool HasMpexAvatarNameTag()
    {
        foreach (var mb in GetComponents<MonoBehaviour>())
        {
            var typeName = mb?.GetType().FullName;
            if (typeName != null && typeName.EndsWith(".MpexAvatarNameTag", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private bool EnsureNameTagRefs()
    {
        _bg = transform.Find("BG")?.GetComponent<ImageView>();
        _nameText = transform.Find("Name")?.GetComponent<CurvedTextMeshPro>();

        if (_nameText == null)
            _nameText = GetComponentInChildren<CurvedTextMeshPro>(true);
        if (_bg == null && _nameText != null)
            _bg = _nameText.transform.parent?.GetComponent<ImageView>();

        if (_bg == null || _nameText == null)
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] ChatBubbleAnchor: AvatarCaption missing BG or Name child");
            return false;
        }

        return true;
    }

    private static bool IsExternalNametagLayoutReady(Transform captionRoot, Transform bg, Transform nameTransform)
    {
        if (HasMpexPlayerIcon(bg))
            return true;

        foreach (var mb in captionRoot.GetComponents<MonoBehaviour>())
        {
            var typeName = mb?.GetType().FullName;
            if (typeName != null && typeName.EndsWith(".MpexAvatarNameTag", StringComparison.Ordinal))
                return true;
        }

        if (bg.GetComponent<HorizontalLayoutGroup>() != null && nameTransform.parent == bg)
            return true;

        return nameTransform.parent == captionRoot;
    }

    private bool IsExternalNametagLayoutReady() =>
        _bg != null && _nameText != null &&
        IsExternalNametagLayoutReady(transform, _bg.transform, _nameText.transform);

    private static bool HasMpexPlayerIcon(Transform bg)
    {
        for (var i = 0; i < bg.childCount; i++)
        {
            if (bg.GetChild(i).name.StartsWith("MpexPlayerIcon(", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private void UpdateIconVisibility()
    {
        if (_player == null)
            return;

        var modPresence = ModPresenceManager.Instance;
        if (modPresence == null)
            return;

        var showChat = modPresence.HasMod(_player.userId);
        var showCustomAvatars = modPresence.HasLobbyCustomAvatars(_player.userId);
        var layoutDirty = false;

        if (_iconView != null && _iconView.gameObject.activeSelf != showChat)
        {
            _iconView.gameObject.SetActive(showChat);
            layoutDirty = true;
        }

        if (_customAvatarsIconView != null && _customAvatarsIconView.gameObject.activeSelf != showCustomAvatars)
        {
            _customAvatarsIconView.gameObject.SetActive(showCustomAvatars);
            layoutDirty = true;
        }

        var customGap = _bg != null ? _bg.transform.Find("MPChatNametagGapCustomToChat") : null;
        if (customGap != null && customGap.gameObject.activeSelf != showCustomAvatars)
        {
            customGap.gameObject.SetActive(showCustomAvatars);
            layoutDirty = true;
        }

        var showVoiceStatusIcons = showChat && MpChatLobbyDiagnostics.NametagVoiceLobbySyncActive();
        if (showVoiceStatusIcons)
            EnsureStatusIconsRowIfNeeded();

        if (_statusRow != null)
        {
            if (_statusRow.gameObject.activeSelf != showVoiceStatusIcons)
                _statusRow.gameObject.SetActive(showVoiceStatusIcons);
            if (showVoiceStatusIcons)
                PositionStatusIconsAboveBg();
        }

        RefreshSlzCaption(showChat && modPresence.IsSlzCompanionClient(_player.userId));

        if (layoutDirty && _bg != null)
        {
            ApplyNametagLayoutOrder();
            LayoutRebuilder.MarkLayoutForRebuild(_bg.rectTransform);
        }

        if (showVoiceStatusIcons && _statusRow != null)
        {
            EnsureRegisteredForStatusTick();
            RefreshStatusIconsIfNeeded(force: true);
        }
    }

    private void EnsureRegisteredForStatusTick()
    {
        if (!MpChatLobbyDiagnostics.NametagVoiceLobbySyncActive())
            return;
        if (_registeredForStatusTick)
            return;
        ActiveAnchors.Add(this);
        _registeredForStatusTick = true;
        EnsureLocalVoiceStateSubscription();
        NametagVoiceStatusTicker.EnsureRunning();
    }

    private void EnsureStatusIconsRowIfNeeded()
    {
        if (!MpChatLobbyDiagnostics.NametagVoiceLobbySyncActive())
            return;
        if (_statusRow != null || _bg == null || _player == null)
            return;

        var modPresence = ModPresenceManager.Instance;
        if (modPresence == null || !modPresence.HasMod(_player.userId))
            return;

        CreateStatusIconsRow();
    }

    private void RefreshStatusIconsIfNeeded(bool force = false)
    {
        if (!MpChatLobbyDiagnostics.NametagVoiceLobbySyncActive())
            return;
        if (!this || _player == null || _micStatusView == null || _headphoneStatusView == null)
            return;
        if (_statusRow == null || !_statusRow.gameObject.activeInHierarchy)
            return;

        var modPresence = ModPresenceManager.Instance;
        if (modPresence == null || !modPresence.HasMod(_player.userId))
            return;

        var localUserId = ChatManager.Instance?.LocalUserId;
        var mutedByLocal = ChatManager.Instance?.IsUserMutedByLocal(_player.userId) == true;
        NametagVoiceStatusRegistry.ResolveIconStates(
            _player.userId,
            localUserId,
            mutedByLocal,
            out var mic,
            out var headphone);

        if (!force && mic == _lastMicState && headphone == _lastHeadphoneState)
            return;

        _lastMicState = mic;
        _lastHeadphoneState = headphone;

        var micSprite = NametagStatusSprites.ForMic(mic);
        if (micSprite != null && _micStatusView.sprite != micSprite)
            _micStatusView.sprite = micSprite;

        var hpSprite = NametagStatusSprites.ForHeadphone(headphone);
        if (hpSprite != null && _headphoneStatusView.sprite != hpSprite)
            _headphoneStatusView.sprite = hpSprite;
    }

    private void EnsureLocalVoiceStateSubscription()
    {
        if (_subscribedLocalVoiceState || _player == null || string.IsNullOrEmpty(_player.userId))
            return;

        var localId = ChatManager.Instance?.LocalUserId;
        if (string.IsNullOrEmpty(localId) || !string.Equals(_player.userId, localId, StringComparison.Ordinal))
            return;

        VoiceChatRuntimeState.Changed += OnLocalVoiceRuntimeStateChanged;
        _subscribedLocalVoiceState = true;
    }

    private void OnLocalVoiceRuntimeStateChanged() => RefreshStatusIconsIfNeeded(force: true);

    private void OnPresenceUpdated(object? sender, EventArgs e) => UpdateIconVisibility();

    private void OnPlayerWithModAdded(object? sender, PlayerWithModEventArgs e)
    {
        if (e?.UserId == _player?.userId)
            UpdateIconVisibility();
    }

    private void CreateNametagIcons()
    {
        if (_chatIconSprite == null)
            _chatIconSprite = LoadEmbeddedNametagIconSprite("MultiplayerChat.Assets.playerhaschat.png", "chat");
        if (_customAvatarsIconSprite == null)
            _customAvatarsIconSprite = LoadEmbeddedNametagIconSprite("MultiplayerChat.Assets.playerhasavatars.png", "custom avatars");
        if (_chatIconSprite == null || _bg == null || _nameText == null)
            return;

        var sharedLayout = UsesSharedHorizontalLayout();
        if (!sharedLayout)
            EnsureStandaloneHorizontalLayout();

        if (_bg.transform.Find("MPChatNametagIcon") == null)
        {
            _iconView = CreateNametagIconView("MPChatNametagIcon", _chatIconSprite);
        }
        else
        {
            _iconView = _bg.transform.Find("MPChatNametagIcon")?.GetComponent<ImageView>();
        }

        if (_customAvatarsIconSprite != null && _bg.transform.Find("MPChatNametagCustomAvatarsIcon") == null)
        {
            _customAvatarsIconView = CreateNametagIconView("MPChatNametagCustomAvatarsIcon", _customAvatarsIconSprite);
        }
        else
        {
            _customAvatarsIconView = _bg.transform.Find("MPChatNametagCustomAvatarsIcon")?.GetComponent<ImageView>();
        }

        EnsureCustomToChatGapTransform();
        ApplyNametagLayoutOrder();
    }

    private void CreateStatusIconsRow()
    {
        if (_bg == null || !NametagStatusSprites.EnsureLoaded())
            return;

        var existing = transform.Find("MPChatNametagStatusRow");
        if (existing != null)
        {
            _statusRow = existing;
            _micStatusView = existing.Find("MPChatNametagMicStatus")?.GetComponent<ImageView>();
            _headphoneStatusView = existing.Find("MPChatNametagHeadphoneStatus")?.GetComponent<ImageView>();
            PositionStatusIconsAboveBg();
            return;
        }

        var rowGo = new GameObject("MPChatNametagStatusRow");
        rowGo.layer = 5;
        rowGo.transform.SetParent(transform, false);
        _statusRow = rowGo.transform;

        _micStatusView = CreateStatusIconView(rowGo.transform, "MPChatNametagMicStatus",
            NametagStatusSprites.ForMic(NametagMicIconState.Unmuted));
        _headphoneStatusView = CreateStatusIconView(rowGo.transform, "MPChatNametagHeadphoneStatus",
            NametagStatusSprites.ForHeadphone(NametagHeadphoneIconState.Undeafened));

        PositionStatusIconsAboveBg();
        rowGo.SetActive(false);
    }

    private ImageView CreateStatusIconView(Transform parent, string objectName, Sprite? sprite)
    {
        var iconGo = new GameObject(objectName);
        iconGo.transform.SetParent(parent, false);
        iconGo.layer = 5;
        iconGo.AddComponent<CanvasRenderer>();

        var iconView = iconGo.AddComponent<ImageView>();
        iconView.maskable = true;
        iconView.fillCenter = true;
        iconView.preserveAspect = true;
        iconView.raycastTarget = false;
        if (_bg != null && _bg.material != null)
            iconView.material = _bg.material;
        if (sprite != null)
        {
            iconView.sprite = sprite;
            ApplyNametagIconSizing(iconGo.GetComponent<RectTransform>(), sprite);
        }

        return iconView;
    }

    private float ResolveStatusIconXOffset()
    {
        var scale = MpexNametagIconLocalScale;
        if (_bg != null)
        {
            var reference = FindMpexPlayerIconRect(_bg.transform);
            if (reference != null)
                scale = reference.localScale.x;
        }

        var iconWidth = MpexNametagIconPixelSize / MpexNametagIconPixelsPerUnit * scale;
        var gap = StatusIconGapPx / MpexNametagIconPixelsPerUnit * scale;
        return iconWidth * 0.5f + gap * 0.5f;
    }

    private void PositionStatusIconsAboveBg()
    {
        if (_statusRow == null || _bg == null)
            return;

        var bgTransform = _bg.transform;
        var y = bgTransform.localPosition.y;
        var bgRect = _bg.rectTransform;
        if (bgRect != null)
        {
            var h = bgRect.rect.height;
            if (h <= 1f)
                h = bgRect.sizeDelta.y;
            if (h > 1f)
                y += h * 0.5f * Mathf.Abs(bgTransform.localScale.y);
        }

        var scale = MpexNametagIconLocalScale;
        var reference = FindMpexPlayerIconRect(_bg.transform);
        if (reference != null)
            scale = reference.localScale.x;
        var iconHeight = MpexNametagIconPixelSize / MpexNametagIconPixelsPerUnit * scale;
        y += StatusIconExtraLiftLocal + iconHeight * 0.35f;
        _statusRow.localPosition = new Vector3(0f, y, bgTransform.localPosition.z);

        var xOffset = ResolveStatusIconXOffset();
        if (_micStatusView != null)
            _micStatusView.transform.localPosition = new Vector3(-xOffset, 0f, 0f);
        if (_headphoneStatusView != null)
            _headphoneStatusView.transform.localPosition = new Vector3(xOffset, 0f, 0f);

        PositionSlzCaptionAboveStatus();
    }

    private void RefreshSlzCaption(bool show)
    {
        if (!show)
        {
            if (_slzCaptionText != null && _slzCaptionText.gameObject.activeSelf)
                _slzCaptionText.gameObject.SetActive(false);
            return;
        }

        EnsureSlzCaption();
        if (_slzCaptionText == null)
            return;

        if (!_slzCaptionText.gameObject.activeSelf)
            _slzCaptionText.gameObject.SetActive(true);
        PositionSlzCaptionAboveStatus();
    }

    private void EnsureSlzCaption()
    {
        if (_slzCaptionText != null || _nameText == null)
            return;

        var existing = transform.Find("MPChatNametagSlzCaption");
        if (existing != null)
        {
            _slzCaptionText = existing.GetComponent<CurvedTextMeshPro>();
            if (_slzCaptionText != null)
                return;
        }

        var go = new GameObject("MPChatNametagSlzCaption");
        go.layer = 5;
        go.transform.SetParent(transform, false);

        var tmp = go.AddComponent<CurvedTextMeshPro>();
        tmp.font = _nameText.font;
        tmp.fontSharedMaterial = _nameText.fontSharedMaterial;
        tmp.text = SlzDeveloperCaption;
        tmp.fontSize = Math.Min(SlzCaptionFontSize, Mathf.Max(1.1f, _nameText.fontSize * 0.42f));
        tmp.color = new Color(0.92f, 0.92f, 0.95f, 0.92f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.richText = false;
        tmp.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(28f, 4f);
        }

        _slzCaptionText = tmp;
        go.SetActive(false);
    }

    private void PositionSlzCaptionAboveStatus()
    {
        if (_slzCaptionText == null)
            return;

        var y = 0f;
        var z = 0f;
        if (_statusRow != null && _statusRow.gameObject.activeInHierarchy)
        {
            y = _statusRow.localPosition.y + SlzCaptionLiftAboveStatusLocal;
            z = _statusRow.localPosition.z;
        }
        else if (_bg != null)
        {
            y = _bg.transform.localPosition.y + 0.55f;
            z = _bg.transform.localPosition.z;
        }

        _slzCaptionText.transform.localPosition = new Vector3(0f, y, z);
    }

    private ImageView CreateNametagIconView(string objectName, Sprite sprite)
    {
        var iconGo = new GameObject(objectName);
        iconGo.transform.SetParent(_bg!.transform, false);
        iconGo.layer = 5;
        iconGo.AddComponent<CanvasRenderer>();

        var iconView = iconGo.AddComponent<ImageView>();
        iconView.maskable = true;
        iconView.fillCenter = true;
        iconView.preserveAspect = true;
        iconView.raycastTarget = false;
        iconView.material = _bg!.material;
        iconView.sprite = sprite;

        ApplyNametagIconSizing(iconGo.GetComponent<RectTransform>(), sprite);
        return iconView;
    }

    private static RectTransform? FindMpexPlayerIconRect(Transform bg)
    {
        for (var i = 0; i < bg.childCount; i++)
        {
            var child = bg.GetChild(i);
            if (child.name.StartsWith("MpexPlayerIcon(", StringComparison.Ordinal))
                return child as RectTransform;
        }

        return null;
    }

    private void ApplyNametagIconSizing(RectTransform iconRect, Sprite sprite)
    {
        if (_bg == null)
            return;

        var reference = FindMpexPlayerIconRect(_bg.transform);
        if (reference != null)
        {
            iconRect.localScale = reference.localScale;
            iconRect.sizeDelta = reference.sizeDelta;
            return;
        }

        var spriteWidth = Mathf.Max(1f, sprite.texture.width);
        var scale = MpexNametagIconLocalScale * (MpexNametagIconPixelSize / spriteWidth);
        iconRect.localScale = new Vector3(scale, scale, scale);
    }

    private bool UsesSharedHorizontalLayout()
    {
        if (_bg == null || _nameText == null)
            return false;

        if (HasMpexPlayerIcon(_bg.transform))
            return true;

        return _bg.GetComponent<HorizontalLayoutGroup>() != null &&
               _nameText.transform.parent == _bg.transform;
    }

    // with MPEX: [custom avatars][1px][chat icon][1px gap][platform icon][name spacing][name]. Without MPEX: [custom avatars?][chat icon][name].
    private void ApplyNametagLayoutOrder()
    {
        if (_bg == null || _iconView == null || _nameText == null)
            return;

        var mpex = FindMpexPlayerIconRect(_bg.transform);
        var platformGap = EnsurePlatformIconGapTransform(mpex != null);
        var customGap = EnsureCustomToChatGapTransform();
        var sibling = 0;

        if (_customAvatarsIconView != null)
        {
            _customAvatarsIconView.transform.SetSiblingIndex(sibling++);
            if (customGap != null)
                customGap.SetSiblingIndex(sibling++);
        }

        _iconView.transform.SetSiblingIndex(sibling++);

        if (mpex != null && platformGap != null)
        {
            platformGap.SetSiblingIndex(sibling++);
            mpex.SetSiblingIndex(sibling++);
        }
        else if (!UsesSharedHorizontalLayout())
            _nameText.transform.SetSiblingIndex(sibling);

        _nameText.transform.SetSiblingIndex(999);
        LayoutRebuilder.MarkLayoutForRebuild(_bg.rectTransform);
        if (_statusRow != null && _statusRow.gameObject.activeSelf)
            PositionStatusIconsAboveBg();
    }

    private Transform? EnsureCustomToChatGapTransform()
    {
        if (_bg == null)
            return null;

        var gap = _bg.transform.Find("MPChatNametagGapCustomToChat");
        if (gap == null)
        {
            var gapGo = new GameObject("MPChatNametagGapCustomToChat");
            gapGo.transform.SetParent(_bg.transform, false);
            gapGo.layer = 5;
            gap = gapGo.transform;
            var le = gapGo.AddComponent<LayoutElement>();
            le.minWidth = PlatformIconLeftGapPx;
            le.preferredWidth = PlatformIconLeftGapPx;
            le.flexibleWidth = 0f;
        }
        else if (gap.TryGetComponent<LayoutElement>(out var le))
        {
            le.minWidth = PlatformIconLeftGapPx;
            le.preferredWidth = PlatformIconLeftGapPx;
        }

        return gap;
    }

    private Transform? EnsurePlatformIconGapTransform(bool required)
    {
        if (_bg == null)
            return null;

        var gap = _bg.transform.Find("MPChatNametagGap");
        if (!required)
        {
            if (gap != null)
                gap.gameObject.SetActive(false);
            return null;
        }

        if (gap == null)
        {
            var gapGo = new GameObject("MPChatNametagGap");
            gapGo.transform.SetParent(_bg.transform, false);
            gapGo.layer = 5;
            gap = gapGo.transform;
            var le = gapGo.AddComponent<LayoutElement>();
            le.minWidth = PlatformIconLeftGapPx;
            le.preferredWidth = PlatformIconLeftGapPx;
            le.flexibleWidth = 0f;
        }
        else
        {
            gap.gameObject.SetActive(true);
            if (gap.TryGetComponent<LayoutElement>(out var le))
            {
                le.minWidth = PlatformIconLeftGapPx;
                le.preferredWidth = PlatformIconLeftGapPx;
            }
        }

        return gap;
    }

    private void EnsureStandaloneHorizontalLayout()
    {
        if (_bg == null || _nameText == null)
            return;

        if (!_bg.TryGetComponent<HorizontalLayoutGroup>(out var layout))
        {
            layout = _bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childScaleWidth = false;
            layout.childScaleHeight = false;
            layout.spacing = 4f;
        }

        if (_nameText.transform.parent != _bg.transform)
            _nameText.transform.SetParent(_bg.transform, false);
    }

    private static Sprite? LoadEmbeddedNametagIconSprite(string resourceName, string label)
    {
        try
        {
            var bytes = ResourceHelpers.GetResource(typeof(ChatBubbleAnchor).Assembly, resourceName);
            if (bytes == null || bytes.Length == 0)
            {
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Could not find embedded sprite: {resourceName}");
                return null;
            }

            var sprite = Sprites.LoadSpriteRaw(bytes, MpexNametagIconPixelsPerUnit);
            if (sprite == null)
            {
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Failed to decode embedded {label} icon PNG");
                return null;
            }

            MakeNearBlackTransparent(sprite.texture);
            return sprite;
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Failed to load {label} icon sprite: {ex.Message}");
            return null;
        }
    }

    // playerhaschat.png ships with an opaque black matte; clear it so only the white glyph shows.
    internal static void MakeNearBlackTransparent(Texture2D tex)
    {
        try
        {
            if (!tex.isReadable)
                return;

            var pixels = tex.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                if (p.r < 12 && p.g < 12 && p.b < 12)
                    pixels[i] = new Color32(0, 0, 0, 0);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Nametag sprite matte clear failed: {ex.Message}");
        }
    }

    private IConnectedPlayer? ResolvePlayerFallback()
    {
        var controller = GetComponentInParent<MultiplayerLobbyAvatarController>();
        var player = GetPlayerFromObject(controller);
        if (player != null) return player;

        var place = GetComponentInParent<MultiplayerLobbyAvatarPlace>();
        player = GetPlayerFromObject(place);
        if (player != null) return player;

        var facade = GetComponentInParent<MultiplayerConnectedPlayerFacade>();
        return GetPlayerFromObject(facade);
    }

    private static IConnectedPlayer? GetPlayerFromObject(object? obj)
    {
        if (obj == null) return null;
        var t = obj.GetType();
        foreach (var name in new[] { "_connectedPlayer", "_player", "m_ConnectedPlayer", "connectedPlayer", "_playerData" })
        {
            var field = t.GetField(name, Flags);
            if (field != null)
            {
                var val = field.GetValue(obj);
                if (val is IConnectedPlayer player)
                    return player;
            }
        }
        foreach (var f in t.GetFields(Flags))
        {
            if (typeof(IConnectedPlayer).IsAssignableFrom(f.FieldType))
            {
                var p = f.GetValue(obj) as IConnectedPlayer;
                if (p != null) return p;
            }
        }
        foreach (var prop in t.GetProperties(Flags))
        {
            if (typeof(IConnectedPlayer).IsAssignableFrom(prop.PropertyType))
            {
                var p = prop.GetValue(obj) as IConnectedPlayer;
                if (p != null) return p;
            }
        }
        return null;
    }
}
