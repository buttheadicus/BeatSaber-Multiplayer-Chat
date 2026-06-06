using System;
using System.Text.RegularExpressions;

namespace MultiplayerChat.Core;

internal static class GitHubReleaseVersion
{
    public const string ModDllAssetFileName = "MultiplayerChat.dll";

    public const string ReleasesApiBase =
        "https://api.github.com/repos/buttheadicus/BeatSaber-Multiplayer-Chat/releases";

    public static string ReleasesUrlPage(int page) =>
        $"{ReleasesApiBase}?per_page=100&page={page}";

    public static bool TryGetLatestBuildNumberFromModDllRelease(string json, out int buildNumber)
    {
        buildNumber = -1;
        try
        {
            var jo = Newtonsoft.Json.Linq.JObject.Parse(json);
            if (jo["assets"] is not Newtonsoft.Json.Linq.JArray assets)
                return false;

            var hasDll = false;
            foreach (var a in assets)
            {
                if (string.Equals(a?["name"]?.ToString(), ModDllAssetFileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    hasDll = true;
                    break;
                }
            }

            if (!hasDll)
                return false;

            var tag = jo["tag_name"]?.ToString()?.Trim() ?? "";
            return TryParseBuildNumberTag(tag, out buildNumber);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryParseBuildNumberTag(string? tag, out int buildNumber)
    {
        buildNumber = -1;
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        var trimmed = tag!.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 1)
            trimmed = trimmed.Substring(1);

        return ModBuildVersion.TryParseBuildNumber(trimmed, out buildNumber);
    }

    public const string CauExeAssetFileName = "Chat.Auto.Updater.CAU.exe";

    public const string CauRepoReleasesLatestApi =
        "https://api.github.com/repos/buttheadicus/Chat-Auto-Updater-CAU-/releases/latest";

    public static bool TryGetCauExeDownloadUrl(string releaseApiJson, out string url)
    {
        url = "";
        try
        {
            var jo = Newtonsoft.Json.Linq.JObject.Parse(releaseApiJson);
            if (jo["assets"] is not Newtonsoft.Json.Linq.JArray assets)
                return false;
            foreach (var a in assets)
            {
                var name = a?["name"]?.ToString();
                if (!string.Equals(name, CauExeAssetFileName, StringComparison.OrdinalIgnoreCase))
                    continue;
                url = a?["browser_download_url"]?.ToString() ?? "";
                return !string.IsNullOrEmpty(url);
            }
        }
        catch
        {
            /* ignore */
        }

        return false;
    }
}
