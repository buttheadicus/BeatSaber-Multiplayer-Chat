using System;
using System.Collections;
using System.IO;
using System.Reflection;
using MultiplayerChat.Core;
using MultiplayerCore.Models;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerChat.UI;

/// <summary>
/// Attached to AvatarCaption (nametag) via SiraUtil LobbyAvatarPlaceRegistration.
/// Adds a chat icon sprite after the player name to indicate they have the E2E Chat mod.
/// Same pattern as MultiplayerExtensions uses for the Steam logo.
/// </summary>
public class ChatBubbleAnchor : MonoBehaviour
{
    private const float IconSize = 14f;
    private const float IconPadding = 4f;

    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;
    private static Sprite? _chatIconSprite;

    private string? _userId;
    private GameObject? _iconObj;

    private void Start()
    {
        StartCoroutine(RegisterWhenReady());
    }

    private void OnDestroy()
    {
        if (ModPresenceManager.Instance != null)
            ModPresenceManager.Instance.PresenceUpdated -= OnPresenceUpdated;
        if (_iconObj != null)
            Destroy(_iconObj);
    }

    private IEnumerator RegisterWhenReady()
    {
        for (var i = 0; i < 5; i++)
        {
            _userId = GetUserIdFromController();
            if (!string.IsNullOrEmpty(_userId))
                break;
            yield return null;
        }

        if (string.IsNullOrEmpty(_userId))
        {
            MultiplayerChat.Plugin.Log?.Warn("[E2EChat] ChatBubbleAnchor: could not get userId from controller/place");
            yield break;
        }

        CreateNametagIcon();
        UpdateIconVisibility();

        var modPresence = ModPresenceManager.Instance;
        if (modPresence != null)
            modPresence.PresenceUpdated += OnPresenceUpdated;
    }

    private void UpdateIconVisibility()
    {
        if (_iconObj == null || _userId == null) return;
        var modPresence = ModPresenceManager.Instance;
        _iconObj.SetActive(modPresence != null && modPresence.HasMod(_userId));
    }

    private void OnPresenceUpdated(object? sender, EventArgs e) => UpdateIconVisibility();

    private void CreateNametagIcon()
    {
        if (_chatIconSprite == null)
            _chatIconSprite = LoadChatIconSprite();
        if (_chatIconSprite == null) return;

        _iconObj = new GameObject("E2EChatNametagIcon");
        _iconObj.transform.SetParent(transform, false);
        _iconObj.transform.SetAsLastSibling();
        _iconObj.layer = 5;

        var rect = _iconObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-IconPadding, 0f);
        rect.sizeDelta = new Vector2(IconSize, IconSize);
        rect.localScale = Vector3.one;

        var img = _iconObj.AddComponent<Image>();
        img.sprite = _chatIconSprite;
        img.color = Color.white;
        img.raycastTarget = false;
        img.preserveAspect = true;
    }

    private static Sprite? LoadChatIconSprite()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetName().Name + ".Assets.playerhaschat.png";
            using var stream = asm.GetManifestResourceStream(name);
            if (stream == null)
            {
                MultiplayerChat.Plugin.Log?.Warn($"[E2EChat] Could not find embedded sprite: {name}");
                return null;
            }
            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);
            var tex = LoadTextureFromPng(bytes);
            if (tex == null)
            {
                MultiplayerChat.Plugin.Log?.Warn("[E2EChat] Failed to load PNG for chat icon");
                return null;
            }
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[E2EChat] Failed to load chat icon sprite: {ex.Message}");
            return null;
        }
    }

    private static Texture2D? LoadTextureFromPng(byte[] bytes)
    {
        try
        {
            return LoadTextureFromPngSystemDrawing(bytes);
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[E2EChat] System.Drawing PNG load failed (e.g. Steam Deck/Linux), using fallback: {ex.Message}");
            return CreatePlaceholderSpriteTexture();
        }
    }

    private static Texture2D? LoadTextureFromPngSystemDrawing(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var bmp = new System.Drawing.Bitmap(ms);
        var tex = new Texture2D(bmp.Width, bmp.Height, TextureFormat.RGBA32, false);
        var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var pixelCount = bmp.Width * bmp.Height;
            var pixels = new Color32[pixelCount];
            int stride = data.Stride;
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    int offset = (bmp.Height - 1 - y) * stride + x * 4;
                    byte b = System.Runtime.InteropServices.Marshal.ReadByte(data.Scan0, offset);
                    byte g = System.Runtime.InteropServices.Marshal.ReadByte(data.Scan0, offset + 1);
                    byte r = System.Runtime.InteropServices.Marshal.ReadByte(data.Scan0, offset + 2);
                    byte a = System.Runtime.InteropServices.Marshal.ReadByte(data.Scan0, offset + 3);
                    pixels[y * bmp.Width + x] = new Color32(r, g, b, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private static Texture2D CreatePlaceholderSpriteTexture()
    {
        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        var c = new Color32(255, 255, 255, 220);
        for (int i = 0; i < 256; i++)
            tex.SetPixel(i % 16, i / 16, c);
        tex.Apply();
        return tex;
    }

    private string? GetUserIdFromController()
    {
        // Try controller first (AvatarCaption is under MultiplayerLobbyAvatarController)
        var controller = GetComponentInParent<MultiplayerLobbyAvatarController>();
        var userId = GetUserIdFromObject(controller);
        if (!string.IsNullOrEmpty(userId)) return userId;

        // Fallback: get from Place (Place contains Controller; AvatarCaption is under both)
        var place = GetComponentInParent<MultiplayerLobbyAvatarPlace>();
        return GetUserIdFromPlace(place);
    }

    private static string? GetUserIdFromObject(object? obj)
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
                    return player.userId;
            }
        }
        foreach (var f in t.GetFields(Flags))
        {
            if (typeof(IConnectedPlayer).IsAssignableFrom(f.FieldType))
            {
                var p = f.GetValue(obj) as IConnectedPlayer;
                if (p != null) return p.userId;
            }
        }
        foreach (var prop in t.GetProperties(Flags))
        {
            if (typeof(IConnectedPlayer).IsAssignableFrom(prop.PropertyType))
            {
                var p = prop.GetValue(obj) as IConnectedPlayer;
                if (p != null) return p.userId;
            }
        }
        return null;
    }

    private static string? GetUserIdFromPlace(MultiplayerLobbyAvatarPlace? place)
    {
        if (place == null) return null;
        var t = place.GetType();
        foreach (var name in new[] { "_connectedPlayer", "_player", "m_ConnectedPlayer", "connectedPlayer" })
        {
            var field = t.GetField(name, Flags);
            if (field != null && typeof(IConnectedPlayer).IsAssignableFrom(field.FieldType))
            {
                var p = field.GetValue(place) as IConnectedPlayer;
                if (p != null) return p.userId;
            }
        }
        foreach (var f in t.GetFields(Flags))
        {
            if (typeof(IConnectedPlayer).IsAssignableFrom(f.FieldType))
            {
                var p = f.GetValue(place) as IConnectedPlayer;
                if (p != null) return p.userId;
            }
        }
        foreach (var prop in t.GetProperties(Flags))
        {
            if (typeof(IConnectedPlayer).IsAssignableFrom(prop.PropertyType))
            {
                var p = prop.GetValue(place) as IConnectedPlayer;
                if (p != null) return p.userId;
            }
        }
        return null;
    }

}
