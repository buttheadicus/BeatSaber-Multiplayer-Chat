using System;
using System.Collections.Generic;
using System.IO;

namespace MultiplayerChat.Core;

internal static class CustomAvatarLobbyHashCache
{
    private static readonly object Gate = new();

    private static Dictionary<string, string>? _hashToPath;

    internal static void Invalidate() => _hashToPath = null;

    internal static bool TryGetPath(string md5HexUpper, out string fullPath)
    {
        fullPath = "";
        RefreshIfNeeded();
        lock (Gate)
            return _hashToPath != null && _hashToPath.TryGetValue(md5HexUpper.ToUpperInvariant(), out fullPath);
    }

    private static void RefreshIfNeeded()
    {
        lock (Gate)
        {
            if (_hashToPath != null)
                return;

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            IndexDirectory(dict, BeatSaberPaths.CustomAvatarsDirectory, SearchOption.TopDirectoryOnly);
            var cacheDir = CustomAvatarLobbyCachePaths.CacheDirectory;
            if (Directory.Exists(cacheDir))
                IndexDirectory(dict, cacheDir, SearchOption.TopDirectoryOnly);

            _hashToPath = dict;
        }
    }

    private static void IndexDirectory(Dictionary<string, string> dict, string dir, SearchOption search)
    {
        if (!Directory.Exists(dir))
            return;

        foreach (var file in Directory.GetFiles(dir, "*.avatar", search))
        {
            try
            {
                var h = CustomAvatarHashUtil.Md5HexFile(file);
                if (!dict.ContainsKey(h))
                    dict[h] = file;
            }
            catch
            {
                /* skip unreadable */
            }
        }
    }
}
