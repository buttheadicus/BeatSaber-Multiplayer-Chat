using System;
using System.Collections;
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

// Chat icon on AvatarCaption/BG, left of the MPEX platform icon without changing MPEX name spacing.
public class ChatBubbleAnchor : MonoBehaviour
{
    // MPEX platform icons: 64px assets, 10 PPU, localScale 3.2 on the RectTransform.
    private const float MpexNametagIconPixelSize = 64f;
    private const float MpexNametagIconPixelsPerUnit = 10f;
    private const float MpexNametagIconLocalScale = 3.2f;
    private const float PlatformIconLeftGapPx = 1f;
    private const int NametagSetupWaitFrames = 45;

    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;
    private static Sprite? _chatIconSprite;

    private IConnectedPlayer? _player;
    private ImageView? _bg;
    private CurvedTextMeshPro? _nameText;
    private ImageView? _iconView;
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

    private void OnDestroy()
    {
        if (_subscribedModPresence != null)
        {
            _subscribedModPresence.PresenceUpdated -= OnPresenceUpdated;
            _subscribedModPresence.PlayerWithModAdded -= OnPlayerWithModAdded;
            _subscribedModPresence = null;
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

        CreateNametagIcon();
        UpdateIconVisibility();

        var modPresence = ModPresenceManager.Instance;
        if (modPresence != null)
        {
            _subscribedModPresence = modPresence;
            _subscribedModPresence.PresenceUpdated += OnPresenceUpdated;
            _subscribedModPresence.PlayerWithModAdded += OnPlayerWithModAdded;
        }

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
        if (_iconView == null || _player == null)
            return;

        var modPresence = ModPresenceManager.Instance;
        var visible = modPresence != null && modPresence.HasMod(_player.userId);
        if (_iconView.gameObject.activeSelf == visible)
            return;

        _iconView.gameObject.SetActive(visible);
        if (visible && _bg != null)
            LayoutRebuilder.MarkLayoutForRebuild(_bg.rectTransform);
    }

    private void OnPresenceUpdated(object? sender, EventArgs e) => UpdateIconVisibility();

    private void OnPlayerWithModAdded(object? sender, PlayerWithModEventArgs e)
    {
        if (e?.UserId == _player?.userId)
            UpdateIconVisibility();
    }

    private void CreateNametagIcon()
    {
        if (_chatIconSprite == null)
            _chatIconSprite = LoadChatIconSprite();
        if (_chatIconSprite == null || _bg == null || _nameText == null)
            return;

        if (_bg.transform.Find("MPChatNametagIcon") != null)
            return;

        var sharedLayout = UsesSharedHorizontalLayout();
        if (!sharedLayout)
            EnsureStandaloneHorizontalLayout();

        var iconGo = new GameObject("MPChatNametagIcon");
        iconGo.transform.SetParent(_bg.transform, false);
        iconGo.layer = 5;
        iconGo.AddComponent<CanvasRenderer>();

        _iconView = iconGo.AddComponent<ImageView>();
        _iconView.maskable = true;
        _iconView.fillCenter = true;
        _iconView.preserveAspect = true;
        _iconView.raycastTarget = false;
        _iconView.material = _bg.material;
        _iconView.sprite = _chatIconSprite;

        ApplyNametagIconSizing(iconGo.GetComponent<RectTransform>());
        ApplyNametagLayoutOrder();
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

    private void ApplyNametagIconSizing(RectTransform iconRect)
    {
        if (_bg == null || _chatIconSprite == null)
            return;

        var reference = FindMpexPlayerIconRect(_bg.transform);
        if (reference != null)
        {
            iconRect.localScale = reference.localScale;
            iconRect.sizeDelta = reference.sizeDelta;
            return;
        }

        var spriteWidth = Mathf.Max(1f, _chatIconSprite.texture.width);
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

    // With MPEX: [chat icon][1px gap][platform icon][name spacing][name]. Without MPEX: [chat icon][name].
    private void ApplyNametagLayoutOrder()
    {
        if (_bg == null || _iconView == null || _nameText == null)
            return;

        var mpex = FindMpexPlayerIconRect(_bg.transform);
        var gap = EnsurePlatformIconGapTransform(mpex != null);

        _iconView.transform.SetSiblingIndex(0);

        if (mpex != null && gap != null)
        {
            gap.SetSiblingIndex(1);
            mpex.SetSiblingIndex(2);
        }
        else if (!UsesSharedHorizontalLayout())
            _nameText.transform.SetSiblingIndex(1);

        _nameText.transform.SetSiblingIndex(999);
        if (_bg != null)
            LayoutRebuilder.MarkLayoutForRebuild(_bg.rectTransform);
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

    private static Sprite? LoadChatIconSprite()
    {
        try
        {
            var bytes = ResourceHelpers.GetResource(typeof(ChatBubbleAnchor).Assembly, "MultiplayerChat.Assets.playerhaschat.png");
            if (bytes == null || bytes.Length == 0)
            {
                MultiplayerChat.Plugin.Log?.Warn("[MPChat] Could not find embedded sprite: MultiplayerChat.Assets.playerhaschat.png");
                return null;
            }

            var sprite = Sprites.LoadSpriteRaw(bytes, MpexNametagIconPixelsPerUnit);
            if (sprite == null)
            {
                MultiplayerChat.Plugin.Log?.Warn("[MPChat] Failed to decode embedded chat icon PNG");
                return null;
            }

            MakeNearBlackTransparent(sprite.texture);
            return sprite;
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Failed to load chat icon sprite: {ex.Message}");
            return null;
        }
    }

    // playerhaschat.png ships with an opaque black matte; clear it so only the white glyph shows.
    private static void MakeNearBlackTransparent(Texture2D tex)
    {
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
