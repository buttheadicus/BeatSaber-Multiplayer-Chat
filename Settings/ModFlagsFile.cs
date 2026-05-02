using System;
using System.IO;
using Newtonsoft.Json;

namespace MultiplayerChat.Settings;

/// <summary>
/// Small JSON flags next to Chat ID files under Beat Saber LocalLow (see <see cref="ChatIdFilePaths"/>).
/// Kept separate from PlayerPrefs so CAU opt-in lives with other persistent identity paths.
/// </summary>
public static class ModFlagsFile
{
    private static readonly object Gate = new();

    private const string FileName = "MultiplayerChat.ModFlags.json";

    private static string FilePath => Path.Combine(ChatIdFilePaths.RootDirectory, FileName);

    private sealed class FlagsDto
    {
        /// <summary>When true, startup deletes bundled CAU exe and version check may download and run it.</summary>
        public bool enableCau;
    }

    /// <summary>Off unless the player explicitly enabled it in settings (missing file = false).</summary>
    public static bool EnableCau
    {
        get
        {
            lock (Gate)
            {
                try
                {
                    var path = FilePath;
                    if (!File.Exists(path))
                        return false;
                    var json = File.ReadAllText(path);
                    var dto = JsonConvert.DeserializeObject<FlagsDto>(json);
                    return dto?.enableCau == true;
                }
                catch (Exception ex)
                {
                    MultiplayerChat.Plugin.Log?.Warn($"[MPChat] ModFlags read failed: {ex.Message}");
                    return false;
                }
            }
        }
        set
        {
            lock (Gate)
            {
                try
                {
                    Directory.CreateDirectory(ChatIdFilePaths.RootDirectory);
                    var dto = new FlagsDto { enableCau = value };
                    File.WriteAllText(FilePath, JsonConvert.SerializeObject(dto, Formatting.Indented));
                }
                catch (Exception ex)
                {
                    MultiplayerChat.Plugin.Log?.Warn($"[MPChat] ModFlags write failed: {ex.Message}");
                }
            }
        }
    }
}
