using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using MultiplayerChat.Settings;
using MultiplayerChat.UI;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;

namespace MultiplayerChat.Core;

/// <summary>
/// Checks Beat Saber Multiplayer Chat on GitHub for newer releases. Optional CAU path fetches
/// <see cref="GitHubReleaseVersion.CauExeAssetFileName"/> from the CAU repo's latest release when enabled.
/// </summary>
public class VersionChecker : MonoBehaviour, IInitializable, IDisposable
{
    private const string ApiUrl = "https://api.github.com/repos/buttheadicus/BeatSaber-Multiplayer-Chat/releases/latest";

    [Inject] private readonly DiContainer _container = null!;
    [Inject] private readonly MainFlowCoordinator _mainFlowCoordinator = null!;

    /// <summary>Update message for display in the Multiplayer Chat Update menu tab. Set after version check completes.</summary>
    public static string UpdateMessage { get; private set; } = "Checking for updates...";

    public void Initialize()
    {
        StartCoroutine(CheckForUpdates());
    }

    private IEnumerator CheckForUpdates()
    {
        MultiplayerChat.Plugin.Log?.Info("[MPChat] Version check starting...");
        yield return new WaitForSeconds(0.5f);
        var currentVersion = GetCurrentVersion();
        if (string.IsNullOrEmpty(currentVersion))
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Could not read current version from manifest");
            UpdateMessage = "Could not read this mod version.";
            yield break;
        }

        MultiplayerChat.Plugin.Log?.Info($"[MPChat] Current version: {currentVersion}");

        using var request = UnityWebRequest.Get(ApiUrl);
        request.SetRequestHeader("User-Agent", "MultiplayerChat-Mod");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Version check failed: {request.error}");
            UpdateMessage = "Could not reach GitHub to check for updates.";
            yield break;
        }

        var json = request.downloadHandler.text;
        var latestVersion = ParseVersionFromJson(json);
        if (string.IsNullOrEmpty(latestVersion))
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Could not parse version from GitHub response");
            UpdateMessage = "Could not parse the latest release version.";
            yield break;
        }

        MultiplayerChat.Plugin.Log?.Info($"[MPChat] Latest GitHub version: {latestVersion}");

        var updateAvailable = IsNewerVersion(latestVersion!, currentVersion!);
        UpdateMessage = updateAvailable
            ? "An update to Multiplayer Chat is available."
            : "Multiplayer Chat is up to date.";

        if (!updateAvailable)
        {
            MultiplayerChat.Plugin.Log?.Info("[MPChat] No update needed (up to date or ahead)");
            yield break;
        }

        MultiplayerChat.Plugin.Log?.Info($"[MPChat] Update available: {currentVersion} -> {latestVersion}");

        if (!ModSettings.EnableCau)
        {
            PresentUpdateFlowCoordinator();
            yield break;
        }

        using (var cauReleaseReq = UnityWebRequest.Get(GitHubReleaseVersion.CauRepoReleasesLatestApi))
        {
            cauReleaseReq.SetRequestHeader("User-Agent", "MultiplayerChat-Mod");
            yield return cauReleaseReq.SendWebRequest();

            if (cauReleaseReq.result != UnityWebRequest.Result.Success)
            {
                MultiplayerChat.Plugin.Log?.Warn(
                    $"[MPChat][CAU] Could not fetch CAU release: {cauReleaseReq.error}");
                UpdateMessage =
                    "An update is available. Enable CAU is on, but the CAU updater release could not be reached.";
                PresentUpdateFlowCoordinator();
                yield break;
            }

            var cauReleaseJson = cauReleaseReq.downloadHandler.text;
            if (!GitHubReleaseVersion.TryGetCauExeDownloadUrl(cauReleaseJson, out var cauUrl))
            {
                MultiplayerChat.Plugin.Log?.Warn(
                    $"[MPChat][CAU] CAU repo latest release has no {GitHubReleaseVersion.CauExeAssetFileName} asset.");
                UpdateMessage =
                    $"An update is available. Enable CAU is on, but {GitHubReleaseVersion.CauExeAssetFileName} was not found on the CAU repo's latest release.";
                PresentUpdateFlowCoordinator();
                yield break;
            }

            var maybeDest = CauBootstrap.GetCauExePath();
            if (string.IsNullOrEmpty(maybeDest))
            {
                MultiplayerChat.Plugin.Log?.Warn("[MPChat][CAU] Could not resolve Plugins path.");
                PresentUpdateFlowCoordinator();
                yield break;
            }

            string destPath = maybeDest!;
            yield return DownloadToFileCoroutine(cauUrl, destPath);

            if (!File.Exists(destPath))
            {
                MultiplayerChat.Plugin.Log?.Warn("[MPChat][CAU] Download did not produce the exe.");
                UpdateMessage = "An update is available. CAU download failed.";
                PresentUpdateFlowCoordinator();
                yield break;
            }

            LaunchCauAndQuit(destPath);
        }

        yield break;
    }

    private static IEnumerator DownloadToFileCoroutine(string url, string destPath)
    {
        var tmp = destPath + ".download.tmp";
        try
        {
            if (File.Exists(tmp))
                File.Delete(tmp);
        }
        catch
        {
            /* ignore */
        }

        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("User-Agent", "MultiplayerChat-Mod");
        req.downloadHandler = new DownloadHandlerBuffer();
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat][CAU] Download failed: {req.error}");
            yield break;
        }

        var data = req.downloadHandler.data;
        if (data == null || data.Length == 0)
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat][CAU] Download empty.");
            yield break;
        }

        try
        {
            var dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(tmp, data);
            if (File.Exists(destPath))
                File.Delete(destPath);
            File.Move(tmp, destPath);
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat][CAU] Write/move failed: {ex.Message}");
            try
            {
                if (File.Exists(tmp))
                    File.Delete(tmp);
            }
            catch
            {
                /* ignore */
            }
        }
    }

    private void LaunchCauAndQuit(string cauPath)
    {
        try
        {
            var gameRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(gameRoot))
            {
                MultiplayerChat.Plugin.Log?.Warn("[MPChat][CAU] Could not get Beat Saber path.");
                PresentUpdateFlowCoordinator();
                return;
            }

            var processId = Process.GetCurrentProcess().Id;
            Process.Start(new ProcessStartInfo
            {
                FileName = cauPath,
                Arguments = $"\"{gameRoot}\" {processId}",
                UseShellExecute = true
            });
            Application.Quit();
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat][CAU] Failed to launch: {ex.Message}");
            PresentUpdateFlowCoordinator();
        }
    }

    private void PresentUpdateFlowCoordinator()
    {
        try
        {
            var fc = _container.InstantiateComponentOnNewGameObject<UpdateFlowCoordinator>();
            fc.ParentFlow = _mainFlowCoordinator;
            fc.SetMessage(UpdateMessage);
            _mainFlowCoordinator.PresentFlowCoordinator(fc);
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Failed to present update tab: {ex.Message}");
        }
    }

    public void Dispose()
    {
    }

    private static string? GetCurrentVersion()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var resourceName = "MultiplayerChat.manifest.json";
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null) return null;

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var match = Regex.Match(json, @"""version""\s*:\s*""([^""]+)""");
            return match.Success ? match.Groups[1].Value : null;
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Failed to read version: {ex.Message}");
            return null;
        }
    }

    private static string? ParseVersionFromJson(string json)
    {
        if (GitHubReleaseVersion.TryGetLatestVersionFromModDllRelease(json, out var fromDll))
            return fromDll;
        if (GitHubReleaseVersion.TryGetLatestVersionFromModZips(json, out var fromZips))
            return fromZips;
        if (GitHubReleaseVersion.TryGetLatestVersionLoose(json, out var loose))
            return loose;
        return null;
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        try
        {
            var latestParts = latest.Split('.');
            var currentParts = current.Split('.');
            for (var i = 0; i < Math.Max(latestParts.Length, currentParts.Length); i++)
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
