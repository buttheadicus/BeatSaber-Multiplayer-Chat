using MultiplayerChat.AvatarExtras.Assets;
using UnityEngine;

namespace MultiplayerChat.Core;

internal static class EmbeddedResourceHelpers
{
    internal static Sprite? LoadSpriteRaw(byte[] image, float pixelsPerUnit) =>
        Sprites.LoadSpriteRaw(image, pixelsPerUnit);
}
