using System;
using System.Collections.Generic;
using System.IO;

namespace MultiplayerChat.Core;

internal static class CustomAvatarInstallListing
{
    internal const string DefaultBeatSaberAvatarLabel = "Default Beat Saber Avatar";

    // wire sentinel: receivers restore the stock multiplayer lobby rig.
    internal const string VanillaDescriptorHash = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";

    internal static bool IsDefaultBeatSaberAvatarLabel(string? label) =>
        !string.IsNullOrEmpty(label) &&
        (string.Equals(label, DefaultBeatSaberAvatarLabel, StringComparison.Ordinal) ||
         string.Equals(label, "(none)", StringComparison.Ordinal));

    internal static bool IsVanillaDescriptorHash(string? hash) =>
        string.Equals(hash?.Trim(), VanillaDescriptorHash, StringComparison.OrdinalIgnoreCase);

    internal static List<string> ListRelativeAvatarFilenames()
    {
        var list = new List<string>();
        var dir = BeatSaberPaths.CustomAvatarsDirectory;
        try
        {
            if (!Directory.Exists(dir))
                return list;

            foreach (var file in Directory.GetFiles(dir, "*.avatar", SearchOption.TopDirectoryOnly))
                list.Add(Path.GetFileName(file));
            list.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            /* ignore */
        }

        return list;
    }
}
