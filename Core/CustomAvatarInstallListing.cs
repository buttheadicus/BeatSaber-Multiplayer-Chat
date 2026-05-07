using System;
using System.Collections.Generic;
using System.IO;

namespace MultiplayerChat.Core;

internal static class CustomAvatarInstallListing
{
    internal const string NoneLabel = "(none)";

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
