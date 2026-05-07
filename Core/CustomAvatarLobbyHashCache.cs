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

            var dict = new Dictionary<string, string>();
            var dir = BeatSaberPaths.CustomAvatarsDirectory;
            if (!Directory.Exists(dir))
            {
                _hashToPath = dict;
                return;
            }

            foreach (var file in Directory.GetFiles(dir, "*.avatar", SearchOption.TopDirectoryOnly))
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

            _hashToPath = dict;
        }
    }
}
