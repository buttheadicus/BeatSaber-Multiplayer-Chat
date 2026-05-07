using System;
using System.IO;

namespace MultiplayerChat.Settings;

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

    public static string LearnedIdsFilePath => Path.Combine(RootDirectory, "LearnedIDs.dat");

    public static string ModSettingsFilePath => Path.Combine(RootDirectory, "MultiplayerChat.Settings.json");

    public static string AvatarDataFilePath => Path.Combine(RootDirectory, "AvatarData.dat");

    public static string AvatarDataBackupFilePath => Path.Combine(RootDirectory, "AvatarData.dat.bak");

    public static string AvatarStorageDirectoryPath => Path.Combine(RootDirectory, "Avatar Storage");
}
