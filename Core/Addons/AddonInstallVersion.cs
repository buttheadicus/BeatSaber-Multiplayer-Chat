using System;
using System.IO;

namespace MultiplayerChat.Core.Addons;

internal static class AddonInstallVersion
{
    internal static string GetAddonBuildFilePath(string addonId) =>
        Path.Combine(AddonPaths.AddonsRoot, $"{addonId}.build");

    internal static string GetContractsBuildFilePath() =>
        Path.Combine(AddonPaths.PluginsDirectory, AddonReleaseDefinitions.ContractsBuildFileName);

    internal static bool TryReadBuildNumber(string path, out int buildNumber)
    {
        buildNumber = -1;
        if (!File.Exists(path))
            return false;

        try
        {
            return ModBuildVersion.TryParseBuildNumber(File.ReadAllText(path), out buildNumber);
        }
        catch
        {
            return false;
        }
    }

    internal static void WriteBuildNumber(string path, int buildNumber)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, buildNumber.ToString());
        }
        catch (Exception ex)
        {
            // Best effort; bootstrap still records install success via the downloaded files.
            _ = ex;
        }
    }
}
