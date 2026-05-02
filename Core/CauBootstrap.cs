using System;
using System.IO;
using MultiplayerChat.Settings;
using UnityEngine;

namespace MultiplayerChat.Core;

/// <summary>
/// Chat Auto Updater: optional exe dropped next to Plugins, deleted every boot when opt-in is on.
/// </summary>
public static class CauBootstrap
{
    public const string CauExeFileName = "Chat.Auto.Updater.CAU.exe";

    /// <summary>Legacy manual install name (removed when clearing).</summary>
    public const string LegacyCauExeFileName = "Chat Auto Updater (CAU).exe";

    /// <summary>
    /// When <see cref="ModFlagsFile.EnableCau"/> is true, delete CAU executables from Plugins before version check.
    /// </summary>
    public static void DeleteCauExeIfEnabled()
    {
        if (!ModFlagsFile.EnableCau)
            return;

        try
        {
            var plugins = GetPluginsDirectory();
            if (plugins == null)
                return;

            TryDelete(Path.Combine(plugins, CauExeFileName));
            TryDelete(Path.Combine(plugins, LegacyCauExeFileName));
        }
        catch (Exception ex)
        {
            Plugin.Log?.Warn($"[MPChat][CAU] Startup delete failed: {ex.Message}");
        }
    }

    public static string? GetPluginsDirectory()
    {
        var gameRoot = Path.GetDirectoryName(Application.dataPath);
        return string.IsNullOrEmpty(gameRoot) ? null : Path.Combine(gameRoot, "Plugins");
    }

    public static string? GetCauExePath()
    {
        var plugins = GetPluginsDirectory();
        return plugins == null ? null : Path.Combine(plugins, CauExeFileName);
    }

    private static void TryDelete(string path)
    {
        if (!File.Exists(path))
            return;
        File.Delete(path);
        Plugin.Log?.Info($"[MPChat][CAU] Deleted {Path.GetFileName(path)}");
    }
}
