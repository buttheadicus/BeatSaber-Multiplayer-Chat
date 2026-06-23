using System;
using MultiplayerChat.AvatarExtras.Assets;
using MultiplayerChat.Core;
using UnityEngine;

namespace MultiplayerChat.UI;

internal static class NametagStatusSprites
{
    private const float PixelsPerUnit = 10f;

    private static Sprite? _unmuted;
    private static Sprite? _muted;
    private static Sprite? _talking;
    private static Sprite? _playerMuted;
    private static Sprite? _undeafened;
    private static Sprite? _deafened;
    private static Sprite? _cannotHearYou;

    internal static bool EnsureLoaded()
    {
        _unmuted ??= Load("MultiplayerChat.Assets.unmuted.png", "unmuted");
        _muted ??= Load("MultiplayerChat.Assets.muted.png", "muted");
        _talking ??= Load("MultiplayerChat.Assets.talking.png", "talking");
        _playerMuted ??= Load("MultiplayerChat.Assets.playermuted.png", "playermuted");
        _undeafened ??= Load("MultiplayerChat.Assets.undeafened.png", "undeafened");
        _deafened ??= Load("MultiplayerChat.Assets.deafened.png", "deafened");
        _cannotHearYou ??= Load("MultiplayerChat.Assets.cannothearyou.png", "cannothearyou");
        return _unmuted != null && _muted != null && _talking != null && _playerMuted != null &&
               _undeafened != null && _deafened != null && _cannotHearYou != null;
    }

    internal static Sprite? ForMic(NametagMicIconState state) =>
        state switch
        {
            NametagMicIconState.Muted => _muted,
            NametagMicIconState.Talking => _talking,
            NametagMicIconState.PlayerMuted => _playerMuted,
            _ => _unmuted
        };

    internal static Sprite? ForHeadphone(NametagHeadphoneIconState state) =>
        state switch
        {
            NametagHeadphoneIconState.Deafened => _deafened,
            NametagHeadphoneIconState.CannotHearYou => _cannotHearYou,
            _ => _undeafened
        };

    private static Sprite? Load(string resourceName, string label)
    {
        try
        {
            var bytes = ResourceHelpers.GetResource(typeof(NametagStatusSprites).Assembly, resourceName);
            if (bytes == null || bytes.Length == 0)
            {
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Could not find embedded sprite: {resourceName}");
                return null;
            }

            var sprite = EmbeddedResourceHelpers.LoadSpriteRaw(bytes, PixelsPerUnit);
            if (sprite == null)
            {
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Failed to decode embedded {label} icon PNG");
                return null;
            }

            return sprite;
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Failed to load {label} icon sprite: {ex.Message}");
            return null;
        }
    }
}
