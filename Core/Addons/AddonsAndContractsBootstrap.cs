using System;
using System.Collections.Generic;
using System.IO;
using IPALogger = IPA.Logging.Logger;

namespace MultiplayerChat.Core.Addons;

// downloads official addon DLLs and MultiplayerChat.Contracts.dll from GitHub releases on first install.
public static class AddonsAndContractsBootstrap
{
    private sealed class PendingAsset
    {
        internal string FileName { get; init; } = string.Empty;

        internal string DestPath { get; init; } = string.Empty;

        internal string? BuildFilePath { get; init; }
    }

    private sealed class PendingRelease
    {
        internal string ApiUrl { get; init; } = string.Empty;

        internal string DisplayLabel { get; init; } = string.Empty;

        internal List<PendingAsset> Assets { get; } = new();
    }

    public static bool TryContinueAfterEnsuringInstalled(IPALogger log)
    {
        try
        {
            return TryContinueAfterEnsuringInstalledCore(log);
        }
        catch (Exception ex)
        {
            log.Error($"[MPChat][Addons] Bootstrap failed: {ex}");
            return true;
        }
    }

    private static bool TryContinueAfterEnsuringInstalledCore(IPALogger log)
    {
        try
        {
            if (!Directory.Exists(AddonPaths.AddonsRoot))
                Directory.CreateDirectory(AddonPaths.AddonsRoot);
        }
        catch (Exception ex)
        {
            log.Warn($"[MPChat][Addons] Could not create addons folder: {ex.Message}");
            return true;
        }

        log.Info(
            $"[MPChat][Addons] Bootstrap scan: plugins={AddonPaths.PluginsDirectory}, contracts exists={File.Exists(AddonPaths.ContractsDllPath)}, addons root={AddonPaths.AddonsRoot}");

        var pendingReleases = new List<PendingRelease>();
        QueueMissingInstalls(pendingReleases, log);

        if (pendingReleases.Count == 0)
        {
            log.Info("[MPChat][Addons] Bootstrap: nothing missing.");
            return true;
        }

        log.Warn($"[MPChat][Addons] Installing {CountAssets(pendingReleases)} file(s) from GitHub releases...");

        foreach (var release in pendingReleases)
        {
            if (!AddonGitHubDownload.TryDownloadReleaseJsonSync(release.ApiUrl, out var releaseJson, out var fetchError))
            {
                log.Error(
                    $"[MPChat][Addons] Could not read {release.DisplayLabel} release metadata: {fetchError}");
                log.Error("[MPChat][Addons] Addon install aborted. Beat Saber will stay open; retry on next launch.");
                return true;
            }

            if (!AddonGitHubRelease.TryGetReleaseBuildNumber(releaseJson, out var releaseBuild))
            {
                log.Error(
                    $"[MPChat][Addons] Could not parse build number for {release.DisplayLabel} (tag must be a simple number).");
                return true;
            }

            foreach (var asset in release.Assets)
            {
                if (!AddonGitHubRelease.TryGetAssetDownloadUrl(releaseJson, asset.FileName, out var url))
                {
                    log.Error($"[MPChat][Addons] Release asset missing: {asset.FileName}");
                    return true;
                }

                log.Warn($"[MPChat][Addons] Downloading {asset.FileName}...");
                if (!AddonGitHubDownload.TryDownloadFileSync(url, asset.DestPath, out var downloadError))
                {
                    log.Error($"[MPChat][Addons] Download failed for {asset.FileName}: {downloadError}");
                    return true;
                }

                if (!string.IsNullOrEmpty(asset.BuildFilePath))
                    AddonInstallVersion.WriteBuildNumber(asset.BuildFilePath!, releaseBuild);
            }
        }

        log.Warn("[MPChat][Addons] Addon install finished. Beat Saber will close - relaunch once so addons load.");
        MpChatBootstrapExit.ScheduleHardExitSoon("MultiplayerChat.AddonsBootstrap.Exit");
        return false;
    }

    private static void QueueMissingInstalls(List<PendingRelease> pendingReleases, IPALogger log)
    {
        if (!File.Exists(AddonPaths.ContractsDllPath))
        {
            log.Warn("[MPChat][Addons] MultiplayerChat.Contracts.dll missing; fetching from Avatar Coloring release.");
            QueueAvatarColoringRelease(
                pendingReleases,
                includeContracts: true,
                includeAddonDll: true,
                includeManifest: true);
        }

        foreach (var definition in AddonReleaseDefinitions.All)
        {
            var dllPath = Path.Combine(AddonPaths.AddonsRoot, definition.DllFileName);
            if (File.Exists(dllPath))
                continue;

            log.Warn($"[MPChat][Addons] {definition.DisplayName} is missing; fetching from GitHub.");
            if (definition.IncludesContractsDll)
            {
                QueueAvatarColoringRelease(
                    pendingReleases,
                    includeContracts: false,
                    includeAddonDll: true,
                    includeManifest: true);
            }
            else
            {
                QueueAddonRelease(pendingReleases, definition, includeManifest: true);
            }
        }
    }

    private static void QueueAvatarColoringRelease(
        List<PendingRelease> pendingReleases,
        bool includeContracts,
        bool includeAddonDll,
        bool includeManifest)
    {
        var definition = AddonReleaseDefinitions.AvatarColoring;
        var release = GetOrCreateRelease(pendingReleases, definition.ReleasesLatestApi, definition.DisplayName);

        if (includeContracts)
        {
            AddAsset(
                release,
                AddonReleaseDefinitions.ContractsDllFileName,
                AddonPaths.ContractsDllPath,
                AddonInstallVersion.GetContractsBuildFilePath());
        }

        if (includeAddonDll)
        {
            AddAsset(
                release,
                definition.DllFileName,
                Path.Combine(AddonPaths.AddonsRoot, definition.DllFileName),
                AddonInstallVersion.GetAddonBuildFilePath(definition.AddonId));
        }

        if (includeManifest)
        {
            AddAsset(
                release,
                definition.ManifestFileName,
                Path.Combine(AddonPaths.AddonsRoot, definition.ManifestFileName),
                buildFilePath: null);
        }
    }

    private static void QueueAddonRelease(
        List<PendingRelease> pendingReleases,
        AddonReleaseDefinition definition,
        bool includeManifest)
    {
        var release = GetOrCreateRelease(pendingReleases, definition.ReleasesLatestApi, definition.DisplayName);
        AddAsset(
            release,
            definition.DllFileName,
            Path.Combine(AddonPaths.AddonsRoot, definition.DllFileName),
            AddonInstallVersion.GetAddonBuildFilePath(definition.AddonId));

        if (includeManifest)
        {
            AddAsset(
                release,
                definition.ManifestFileName,
                Path.Combine(AddonPaths.AddonsRoot, definition.ManifestFileName),
                buildFilePath: null);
        }
    }

    private static PendingRelease GetOrCreateRelease(
        List<PendingRelease> pendingReleases,
        string apiUrl,
        string displayLabel)
    {
        foreach (var existing in pendingReleases)
        {
            if (string.Equals(existing.ApiUrl, apiUrl, StringComparison.Ordinal))
                return existing;
        }

        var release = new PendingRelease
        {
            ApiUrl = apiUrl,
            DisplayLabel = displayLabel
        };
        pendingReleases.Add(release);
        return release;
    }

    private static void AddAsset(
        PendingRelease release,
        string fileName,
        string destPath,
        string? buildFilePath)
    {
        foreach (var existing in release.Assets)
        {
            if (string.Equals(existing.DestPath, destPath, StringComparison.OrdinalIgnoreCase))
                return;
        }

        release.Assets.Add(new PendingAsset
        {
            FileName = fileName,
            DestPath = destPath,
            BuildFilePath = buildFilePath
        });
    }

    private static int CountAssets(List<PendingRelease> pendingReleases)
    {
        var count = 0;
        foreach (var release in pendingReleases)
            count += release.Assets.Count;
        return count;
    }
}
