using System;
using MultiplayerChat.Core;
using Newtonsoft.Json.Linq;

namespace MultiplayerChat.Core.Addons;

internal static class AddonGitHubRelease
{
    internal static bool TryGetReleaseBuildNumber(string releaseJson, out int buildNumber)
    {
        buildNumber = -1;
        try
        {
            var jo = JObject.Parse(releaseJson);
            var tag = jo["tag_name"]?.ToString()?.Trim() ?? "";
            return GitHubReleaseVersion.TryParseBuildNumberTag(tag, out buildNumber);
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryGetLatestBuildNumber(string releaseJson, string requiredAssetFileName, out int buildNumber)
    {
        buildNumber = -1;
        try
        {
            var jo = JObject.Parse(releaseJson);
            if (!ReleaseHasAsset(jo, requiredAssetFileName))
                return false;

            var tag = jo["tag_name"]?.ToString()?.Trim() ?? "";
            return GitHubReleaseVersion.TryParseBuildNumberTag(tag, out buildNumber);
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryGetAssetDownloadUrl(string releaseJson, string assetFileName, out string url)
    {
        url = "";
        try
        {
            var jo = JObject.Parse(releaseJson);
            if (jo["assets"] is not JArray assets)
                return false;

            foreach (var asset in assets)
            {
                var name = asset?["name"]?.ToString();
                if (!string.Equals(name, assetFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                url = asset?["browser_download_url"]?.ToString() ?? "";
                return !string.IsNullOrEmpty(url);
            }
        }
        catch
        {
            /* ignore */
        }

        return false;
    }

    private static bool ReleaseHasAsset(JObject release, string assetFileName)
    {
        if (release["assets"] is not JArray assets)
            return false;

        foreach (var asset in assets)
        {
            if (string.Equals(asset?["name"]?.ToString(), assetFileName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
