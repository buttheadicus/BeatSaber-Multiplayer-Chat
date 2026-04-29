using System;
using System.IO;

namespace MultiplayerChat.Core;

/// <summary>
/// When <see cref="MarkerFileName"/> exists in the same directory as this assembly (Beat Saber Plugins folder),
/// optional "SLZ mode" behavior is enabled. The companion <c>SlzMarker</c> tool creates that file.
/// </summary>
/// <remarks>
/// Keep fork-specific SLZ behavior out of git: use the gitignored <c>SlzPrivate/</c> folder for extra sources or
/// a local MSBuild props file; this file only performs the marker-file check shipped with the mod.
/// </remarks>
public static class SlzMode
{
    public const string MarkerFileName = "SLZ.dat";

    /// <summary>True after <see cref="Refresh"/> if the marker file is present next to the mod DLL.</summary>
    public static bool IsEnabled { get; private set; }

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

            IsEnabled = File.Exists(Path.Combine(dir, MarkerFileName));
        }
        catch
        {
            IsEnabled = false;
        }
    }
}
