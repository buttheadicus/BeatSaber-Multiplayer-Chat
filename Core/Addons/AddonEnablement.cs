using MultiplayerChat.Contracts;
using MultiplayerChat.Settings;

namespace MultiplayerChat.Core.Addons;

internal static class AddonEnablement
{
    internal static bool IsUnwired(string addonId) => false;

    internal static bool IsEnabled(string addonId)
    {
        return addonId switch
        {
            AddonIds.QuickBinds => ModSettings.EnableQuickBinds,
            AddonIds.AvatarColoring => ModSettings.EnableAvatarColoringExtensions,
            AddonIds.CustomAvatars => ModSettings.EnableLobbyCustomAvatars,
            AddonIds.Spectaplay => ModSettings.EnableSpectaplay,
            _ => true
        };
    }

    internal static AddonCapability CapabilityFor(string addonId) =>
        addonId switch
        {
            AddonIds.QuickBinds => AddonCapability.QuickBinds,
            AddonIds.AvatarColoring => AddonCapability.AvatarColoring,
            AddonIds.CustomAvatars => AddonCapability.LobbyCustomAvatars,
            AddonIds.Spectaplay => AddonCapability.Spectaplay,
            _ => AddonCapability.None
        };

    internal static string DisplayNameFor(string addonId) =>
        addonId switch
        {
            AddonIds.QuickBinds => "Quick Binds",
            AddonIds.AvatarColoring => "Avatar Coloring Extensions",
            AddonIds.CustomAvatars => "Custom Avatars",
            AddonIds.Spectaplay => "Spectaplay",
            _ => addonId
        };
}
