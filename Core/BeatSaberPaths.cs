using System.IO;
using IPA.Utilities;

namespace MultiplayerChat.Core;

internal static class BeatSaberPaths
{
    internal static string InstallRoot => UnityGame.InstallPath;

    internal static string CustomAvatarsDirectory => Path.Combine(InstallRoot, "CustomAvatars");
}
