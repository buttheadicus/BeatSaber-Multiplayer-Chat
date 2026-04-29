using UnityEngine;

namespace MultiplayerChat.AvatarExtras.Networking;

/// <summary>
/// If this color is stored for a part, that part uses the bundled rainbow material instead of tint.
/// </summary>
public static class Magic
{
    public static readonly Color MagicRainbowColor = new(0, 1, 0, .5f);
}
