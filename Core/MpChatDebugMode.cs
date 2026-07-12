using System;
using System.IO;

namespace MultiplayerChat.Core;

// install-local toggle: marker file next to the mod DLL (not AppData). Off by default each fresh copy.
public static class MpChatDebugMode
{
    public const string MarkerFileName = "Debug.dat";

    public static bool IsEnabled { get; private set; }

    public static void Refresh()
    {
        try
        {
            var path = GetMarkerPath();
            IsEnabled = !string.IsNullOrEmpty(path) && File.Exists(path);
        }
        catch
        {
            IsEnabled = false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            var path = GetMarkerPath();
            if (string.IsNullOrEmpty(path))
                return;

            if (enabled)
                File.WriteAllText(path, "1");
            else if (File.Exists(path))
                File.Delete(path);

            IsEnabled = enabled;
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Debug mode marker write failed: " + ex.Message);
        }
    }

    private static string? GetMarkerPath()
    {
        var loc = typeof(MpChatDebugMode).Assembly.Location;
        if (string.IsNullOrEmpty(loc))
            return null;

        var dir = Path.GetDirectoryName(loc);
        return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, MarkerFileName);
    }
}
