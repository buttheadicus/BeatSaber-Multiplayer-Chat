using System;
using System.IO;
using MultiplayerChat;

namespace MultiplayerChat.Core;

/// <summary>
/// When <see cref="MarkerFileName"/> exists in the same directory as this assembly (Beat Saber Plugins folder)
/// and passes <see cref="SlzMarkerProof.TryValidateMarkerContent"/>, optional "SLZ mode" behavior is enabled.
/// Use <c>SlzMarkerTool</c> from the same release to create a valid marker (0.3.1+).
/// </summary>
/// <remarks>
/// Keep fork-specific SLZ behavior out of git: use the gitignored <c>SlzPrivate/</c> folder for extra sources or
/// a local MSBuild props file; marker validation lives in <see cref="SlzMarkerProof"/>.
/// </remarks>
public static class SlzMode
{
    public const string MarkerFileName = "SLZ.dat";

    /// <summary>True after <see cref="Refresh"/> if the marker file is present and passes <see cref="SlzMarkerProof.TryValidateMarkerContent"/>.</summary>
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
                    $"[MPChat] {MarkerFileName} is present but contents are not valid for this mod version. Remove it or recreate with SlzMarkerTool from Multiplayer Chat 0.3.1+.");
            }
        }
        catch
        {
            IsEnabled = false;
        }
    }
}
