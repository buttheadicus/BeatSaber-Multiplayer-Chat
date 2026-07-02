using System;

namespace MultiplayerChat.Contracts;

[Flags]
public enum AddonCapability
{
    None = 0,
    QuickBinds = 1 << 0,
    AvatarColoring = 1 << 1,
    LobbyCustomAvatars = 1 << 3,
}
