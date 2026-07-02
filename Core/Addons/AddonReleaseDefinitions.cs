using System.Collections.Generic;

namespace MultiplayerChat.Core.Addons;

internal sealed class AddonReleaseDefinition
{
    internal string AddonId { get; init; } = string.Empty;

    internal string DisplayName { get; init; } = string.Empty;

    internal string DllFileName { get; init; } = string.Empty;

    internal string ManifestFileName { get; init; } = string.Empty;

    internal string ReleasesLatestApi { get; init; } = string.Empty;

    internal bool IncludesContractsDll { get; init; }
}

internal static class AddonReleaseDefinitions
{
    internal const string ContractsDllFileName = "MultiplayerChat.Contracts.dll";

    internal const string ContractsBuildFileName = "MultiplayerChat.Contracts.build";

    internal static AddonReleaseDefinition AvatarColoring { get; } = new()
    {
        AddonId = "avatarColoring",
        DisplayName = "Avatar Coloring Extensions",
        DllFileName = "MultiplayerChat.Addon.AvatarColoring.dll",
        ManifestFileName = "MultiplayerChat.Addon.AvatarColoring.addon.json",
        ReleasesLatestApi =
            "https://api.github.com/repos/buttheadicus/MPC-addon-AvatarColoringExtentions/releases/latest",
        IncludesContractsDll = true
    };

    internal static AddonReleaseDefinition CustomAvatars { get; } = new()
    {
        AddonId = "customAvatars",
        DisplayName = "Custom Multiplayer Avatars",
        DllFileName = "MultiplayerChat.Addon.CustomAvatars.dll",
        ManifestFileName = "MultiplayerChat.Addon.CustomAvatars.addon.json",
        ReleasesLatestApi =
            "https://api.github.com/repos/buttheadicus/MPC-addon-MultiplayerCustomAvatars/releases/latest"
    };

    internal static AddonReleaseDefinition QuickBinds { get; } = new()
    {
        AddonId = "quickBinds",
        DisplayName = "Quick Binds",
        DllFileName = "MultiplayerChat.Addon.QuickBinds.dll",
        ManifestFileName = "MultiplayerChat.Addon.QuickBinds.addon.json",
        ReleasesLatestApi = "https://api.github.com/repos/buttheadicus/MPC-addon-QuickBinds/releases/latest"
    };

    internal static IReadOnlyList<AddonReleaseDefinition> All { get; } =
        new[] { QuickBinds, AvatarColoring, CustomAvatars };
}
