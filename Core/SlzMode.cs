using System;
using System.IO;
using MultiplayerChat;

namespace MultiplayerChat.Core;

public static class SlzMode
{
    public const string MarkerFileName = "SLZ.dat";

    public static bool IsEnabled { get; private set; }

    private static bool _warnedInvalidMarker;

    public static void Refresh()
    {
        try
        {
            var loc = typeof(SlzMode).Assembly.Location;
            if (string.IsNullOrEmpty(loc))
            {
                IsEnabled = false;
                return;
            }

            var dir = Path.GetDirectoryName(loc);
            if (string.IsNullOrEmpty(dir))
            {
                IsEnabled = false;
                return;
            }

            var path = Path.Combine(dir, MarkerFileName);
            if (!File.Exists(path))
            {
                IsEnabled = false;
                return;
            }

            string contents;
            try
            {
                contents = File.ReadAllText(path);
            }
            catch
            {
                IsEnabled = false;
                return;
            }

            IsEnabled = SlzMarkerProof.TryValidateMarkerContent(contents);

            if (!IsEnabled && !_warnedInvalidMarker)
            {
                _warnedInvalidMarker = true;
                Plugin.Log?.Warn(
                    $"[MPChat] {MarkerFileName} is present but contents are not valid for this mod version. Remove it or recreate with SlzMarkerTool from Multiplayer Chat 0.3.7+.");
            }
        }
        catch
        {
            IsEnabled = false;
        }
    }
}
