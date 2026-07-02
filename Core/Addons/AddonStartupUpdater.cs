using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace MultiplayerChat.Core.Addons;

// Startup update check for installed addons (runs from VersionChecker with the core mod update).
internal static class AddonStartupUpdater
{
    internal static IEnumerator CheckInstallAll(ICollection<string> updatedDisplayNames)
    {
        updatedDisplayNames.Clear();
        MpChatLog.UpdaterInfo("[MPChat][Addons] Checking installed addons for updates...");

        foreach (var definition in AddonReleaseDefinitions.All)
        {
            string? installedDisplayName = null;
            yield return CheckInstallAddon(definition, name => installedDisplayName = name);
            if (!string.IsNullOrEmpty(installedDisplayName))
                updatedDisplayNames.Add(installedDisplayName);
        }
    }

    private static IEnumerator CheckInstallAddon(
        AddonReleaseDefinition definition,
        Action<string> onInstalled)
    {
        onInstalled("");

        var dllPath = Path.Combine(AddonPaths.AddonsRoot, definition.DllFileName);
        if (!File.Exists(dllPath))
        {
            MpChatLog.DebugLine($"[MPChat][Addons] Startup update skipped for {definition.DisplayName}: DLL not installed.");
            yield break;
        }

        MpChatLog.UpdaterInfo($"[MPChat][Addons] Checking {definition.DisplayName} for updates...");

        var buildPath = AddonInstallVersion.GetAddonBuildFilePath(definition.AddonId);
        if (!AddonInstallVersion.TryReadBuildNumber(buildPath, out var localBuild))
            localBuild = 0;

        string releaseJson = "";
        var fetchOk = false;
        var fetchError = "";
        yield return AddonGitHubDownload.FetchReleaseJsonCoroutine(
            definition.ReleasesLatestApi,
            (ok, json) =>
            {
                fetchOk = ok;
                releaseJson = json;
                fetchError = ok ? "" : json;
            });

        if (!fetchOk)
        {
            MpChatLog.UpdaterWarn($"[MPChat][Addons] {definition.DisplayName} update check failed: {fetchError}");
            yield break;
        }

        if (!AddonGitHubRelease.TryGetLatestBuildNumber(
                releaseJson,
                definition.DllFileName,
                out var latestBuild))
        {
            MpChatLog.UpdaterWarn(
                $"[MPChat][Addons] Could not parse {definition.DisplayName} release tag. Ensure the tag is a number and {definition.DllFileName} is attached to the release.");
            yield break;
        }

        if (latestBuild <= localBuild)
        {
            MpChatLog.UpdaterInfo(
                $"[MPChat][Addons] {definition.DisplayName} is up to date (installed build {localBuild}, latest release tag {latestBuild}).");
            if (!File.Exists(buildPath))
                AddonInstallVersion.WriteBuildNumber(buildPath, latestBuild);
            yield break;
        }

        MpChatLog.UpdaterWarn(
            $"[MPChat][Addons] {definition.DisplayName} update available: installed build {localBuild} -> release tag {latestBuild}. Downloading...");

        if (!TryQueueReleaseAssets(definition, releaseJson, out var installs, out var installError))
        {
            MpChatLog.UpdaterWarn($"[MPChat][Addons] {definition.DisplayName} update install failed: {installError}");
            yield break;
        }

        foreach (var install in installs)
        {
            var done = false;
            var success = false;
            var error = "";
            yield return AddonGitHubDownload.DownloadFileCoroutine(
                install.DownloadUrl,
                install.DestPath,
                (ok, message) =>
                {
                    done = true;
                    success = ok;
                    error = message;
                });

            if (!done || !success)
            {
                MpChatLog.UpdaterWarn(
                    $"[MPChat][Addons] Failed to download {install.FileName} for {definition.DisplayName}: {error}");
                yield break;
            }

            if (!string.IsNullOrEmpty(install.BuildFilePath))
                AddonInstallVersion.WriteBuildNumber(install.BuildFilePath!, latestBuild);
        }

        MpChatLog.UpdaterWarn(
            $"[MPChat][Addons] Installed {definition.DisplayName} build {latestBuild}. Restart Beat Saber to load it.");
        onInstalled(definition.DisplayName);
    }

    private static bool TryQueueReleaseAssets(
        AddonReleaseDefinition definition,
        string releaseJson,
        out List<PendingInstall> installs,
        out string error)
    {
        installs = new List<PendingInstall>();
        error = "";

        if (!TryQueueAsset(
                installs,
                releaseJson,
                definition.DllFileName,
                Path.Combine(AddonPaths.AddonsRoot, definition.DllFileName),
                AddonInstallVersion.GetAddonBuildFilePath(definition.AddonId),
                out error))
            return false;

        TryQueueAsset(
            installs,
            releaseJson,
            definition.ManifestFileName,
            Path.Combine(AddonPaths.AddonsRoot, definition.ManifestFileName),
            buildFilePath: null,
            out _);

        return installs.Count > 0;
    }

    private static bool TryQueueAsset(
        List<PendingInstall> installs,
        string releaseJson,
        string fileName,
        string destPath,
        string? buildFilePath,
        out string error)
    {
        error = "";
        if (!AddonGitHubRelease.TryGetAssetDownloadUrl(releaseJson, fileName, out var url))
        {
            error = $"asset {fileName} not found on latest release";
            return false;
        }

        installs.Add(new PendingInstall
        {
            FileName = fileName,
            DestPath = destPath,
            BuildFilePath = buildFilePath,
            DownloadUrl = url
        });
        return true;
    }

    private sealed class PendingInstall
    {
        internal string FileName { get; init; } = string.Empty;

        internal string DestPath { get; init; } = string.Empty;

        internal string? BuildFilePath { get; init; }

        internal string DownloadUrl { get; init; } = string.Empty;
    }
}
