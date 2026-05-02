using System;
using System.IO;

namespace MultiplayerChat.Settings;

/// <summary>
/// Beat Saber persist folder: AppData\LocalLow\Hyperbolic Magnetism\Beat Saber\
/// </summary>
public static class ChatIdFilePaths
{
    private static string? _rootDir;

    public static string RootDirectory
    {
        get
        {
            if (_rootDir != null) return _rootDir;
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _rootDir = Path.Combine(profile, "AppData", "LocalLow", "Hyperbolic Magnetism", "Beat Saber");
            return _rootDir;
        }
    }

    public static string ChatIdFilePath => Path.Combine(RootDirectory, "ChatID.dat");
    public static string ChatIdConfigFilePath => Path.Combine(RootDirectory, "ChatIDConfig.dat");

    /// <summary>DPAPI-encrypted JSON: learned platform user id → others' persistent chat IDs.</summary>
    public static string LearnedIdsFilePath => Path.Combine(RootDirectory, "LearnedIDs.dat");

    /// <summary>Mod UI and voice preferences (same folder as Chat ID, not Unity PlayerPrefs).</summary>
    public static string ModSettingsFilePath => Path.Combine(RootDirectory, "MultiplayerChat.Settings.json");
}
