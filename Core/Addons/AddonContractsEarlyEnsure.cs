using System;
using System.IO;
using System.Reflection;

namespace MultiplayerChat.Core.Addons;

// Downloads Contracts before the runtime assembly is loaded.
internal static class AddonContractsEarlyEnsure
{
    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;

        try
        {
            var pluginsDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            TryWriteBootstrapLog("Initialize start; pluginsDir=" + pluginsDir);
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            TryDownloadContractsIfMissing();
        }
        catch (Exception ex)
        {
            TryWriteBootstrapLog("Early bootstrap failed: " + ex);
        }
    }

    private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var requested = new AssemblyName(args.Name);
        if (!string.Equals(requested.Name, "MultiplayerChat.Contracts", StringComparison.Ordinal))
            return null;

        TryDownloadContractsIfMissing();

        var path = AddonPaths.ContractsDllPath;
        return File.Exists(path) ? Assembly.LoadFrom(path) : null;
    }

    private static void TryDownloadContractsIfMissing()
    {
        if (File.Exists(AddonPaths.ContractsDllPath))
            return;

        try
        {
            if (!Directory.Exists(AddonPaths.AddonsRoot))
                Directory.CreateDirectory(AddonPaths.AddonsRoot);
        }
        catch (Exception ex)
        {
            TryWriteBootstrapLog("Could not create addons folder: " + ex.Message);
            return;
        }

        var apiUrl = AddonReleaseDefinitions.AvatarColoring.ReleasesLatestApi;
        if (!AddonGitHubDownload.TryDownloadReleaseJsonSync(apiUrl, out var releaseJson, out var fetchError))
        {
            TryWriteBootstrapLog("Release metadata fetch failed: " + fetchError);
            return;
        }

        if (!AddonGitHubRelease.TryGetAssetDownloadUrl(
                releaseJson,
                AddonReleaseDefinitions.ContractsDllFileName,
                out var downloadUrl))
        {
            TryWriteBootstrapLog("Contracts asset URL missing from release metadata.");
            return;
        }

        if (!AddonGitHubDownload.TryDownloadFileSync(downloadUrl, AddonPaths.ContractsDllPath, out var downloadError))
        {
            TryWriteBootstrapLog("Contracts download failed: " + downloadError);
            return;
        }

        TryWriteBootstrapLog("Contracts downloaded to " + AddonPaths.ContractsDllPath);

        if (AddonGitHubRelease.TryGetReleaseBuildNumber(releaseJson, out var releaseBuild))
            AddonInstallVersion.WriteBuildNumber(AddonInstallVersion.GetContractsBuildFilePath(), releaseBuild);
    }

    private static void TryWriteBootstrapLog(string message)
    {
        try
        {
            var pluginsDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(pluginsDir))
                return;

            var dir = Path.Combine(pluginsDir, "MultiplayerChat");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "early-bootstrap.log"),
                DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine);
        }
        catch
        {
            /* ignore */
        }
    }
}
