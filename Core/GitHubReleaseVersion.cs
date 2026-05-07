using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MultiplayerChat.Core;

internal static class GitHubReleaseVersion
{
    public const string ModDllAssetFileName = "MultiplayerChat.dll";

    public const string ReleasesApiBase =
        "https://api.github.com/repos/buttheadicus/BeatSaber-Multiplayer-Chat/releases";

    public static string ReleasesUrlPage(int page) =>
        $"{ReleasesApiBase}?per_page=100&page={page}";

    public static readonly Regex VersionedModZipFileRegex = new(
        @"MultiplayerChat-(\d+)\.(\d+)\.(\d+)\.zip",
        RegexOptions.IgnoreCase);

    public static readonly Regex LooseSemverRegex = new(@"v?(\d+\.\d+\.\d+)", RegexOptions.IgnoreCase);

    public static IEnumerable<string> ExtractReleaseZipUrls(string json)
    {
        foreach (Match m in Regex.Matches(json, @"""browser_download_url""\s*:\s*""(https://[^""]+\.zip)""",
                     RegexOptions.IgnoreCase))
            yield return m.Groups[1].Value;
    }

    public static bool IsModMultiplayerChatZipUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        if (url.IndexOf("/releases/download/", StringComparison.OrdinalIgnoreCase) < 0)
            return false;
        if (url.IndexOf("/archive/", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (url.IndexOf("codeload.github.com", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        var fn = GetLastUrlPathSegment(url);
        if (string.IsNullOrEmpty(fn)) return false;

        if (fn.Equals("CAU.zip", StringComparison.OrdinalIgnoreCase)) return false;
        if (fn.Equals("CAU.exe", StringComparison.OrdinalIgnoreCase)) return false;
        if (fn.Equals("Chat.Auto.Updater.CAU.exe", StringComparison.OrdinalIgnoreCase)) return false;
        if (fn.Equals("Chat Auto Updater (CAU).exe", StringComparison.OrdinalIgnoreCase)) return false;
        if (fn.StartsWith("Source code", StringComparison.OrdinalIgnoreCase)) return false;

        if (fn.Equals("MultiplayerChat.zip", StringComparison.OrdinalIgnoreCase)) return true;
        return VersionedModZipFileRegex.IsMatch(fn);
    }

    public static string GetLastUrlPathSegment(string url)
    {
        try
        {
            var i = url.LastIndexOf('?');
            var path = i >= 0 ? url.Substring(0, i) : url;
            var slash = path.LastIndexOf('/');
            if (slash < 0 || slash >= path.Length - 1) return "";
            return Uri.UnescapeDataString(path.Substring(slash + 1));
        }
        catch
        {
            return "";
        }
    }

    public static bool TryParseVersionFromMultiplayerChatZipUrl(string url, out string version)
    {
        version = "";
        if (!GetLastUrlPathSegment(url).Equals("MultiplayerChat.zip", StringComparison.OrdinalIgnoreCase))
            return false;

        var m = Regex.Match(url, @"/releases/download/([^/]+)/MultiplayerChat\.zip", RegexOptions.IgnoreCase);
        if (!m.Success) return false;

        var tag = m.Groups[1].Value.Trim();
        if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase) && tag.Length > 1)
            tag = tag.Substring(1);
        if (!Regex.IsMatch(tag, @"^\d+\.\d+\.\d+$")) return false;
        version = tag;
        return true;
    }

    public static bool TryGetLatestVersionFromModZips(string json, out string version)
    {
        version = "";
        string? best = null;
        foreach (var url in ExtractReleaseZipUrls(json).Where(IsModMultiplayerChatZipUrl))
        {
            var fn = GetLastUrlPathSegment(url);
            var vm = VersionedModZipFileRegex.Match(fn);
            if (vm.Success)
            {
                var v = $"{vm.Groups[1].Value}.{vm.Groups[2].Value}.{vm.Groups[3].Value}";
                if (best == null || Semver.IsNewer(v, best)) best = v;
                continue;
            }

            if (TryParseVersionFromMultiplayerChatZipUrl(url, out var fromTag))
            {
                if (best == null || Semver.IsNewer(fromTag, best)) best = fromTag;
            }
        }

        if (best == null) return false;
        version = best;
        return true;
    }

    public static bool TryGetLatestVersionFromModDllRelease(string json, out string version)
    {
        version = "";
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
            if (string.IsNullOrEmpty(tag))
                return false;
            if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase) && tag.Length > 1)
                tag = tag.Substring(1);
            if (!Regex.IsMatch(tag, @"^\d+\.\d+\.\d+$"))
                return false;
            version = tag;
            return true;
        }
        catch
        {
            return false;
        }
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

    public static bool TryGetLatestVersionLoose(string json, out string version)
    {
        version = "";
        string? best = null;
        foreach (Match m in LooseSemverRegex.Matches(json))
        {
            var v = m.Groups[1].Value;
            if (best == null || Semver.IsNewer(v, best)) best = v;
        }

        if (best == null) return false;
        version = best;
        return true;
    }
}

internal static class Semver
{
    public static bool IsNewer(string latest, string current)
    {
        try
        {
            var latestParts = latest.Split('.');
            var currentParts = current.Split('.');
            for (var i = 0; i < System.Math.Max(latestParts.Length, currentParts.Length); i++)
            {
                var l = i < latestParts.Length && int.TryParse(latestParts[i], out var lv) ? lv : 0;
                var c = i < currentParts.Length && int.TryParse(currentParts[i], out var cv) ? cv : 0;
                if (l > c) return true;
                if (l < c) return false;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
