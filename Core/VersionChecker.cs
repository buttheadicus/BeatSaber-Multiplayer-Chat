using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using HMUI;
using MultiplayerChat.UI;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;

namespace MultiplayerChat.Core;

/// <summary>
/// Checks GitHub for newer releases. Update message is shown in the Multiplayer Chat Update menu tab.
/// Auto-opens the update tab when an update is detected.
/// </summary>
public class VersionChecker : MonoBehaviour, IInitializable, IDisposable
{
    private const string ApiUrl = "https://api.github.com/repos/buttheadicus/BeatSaber-Multiplayer-Chat/releases/latest";
    private const string ReleasesUrl = "https://github.com/buttheadicus/BeatSaber-Multiplayer-Chat/releases";

    private static readonly Regex VersionRegex = new(@"v?(\d+\.\d+\.\d+)", RegexOptions.IgnoreCase);

    [Inject] private readonly DiContainer _container = null!;
    [Inject] private readonly MainFlowCoordinator _mainFlowCoordinator = null!;

    /// <summary>Update message for display in Settings. Set after version check completes.</summary>
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
            yield break;
        }
        MultiplayerChat.Plugin.Log?.Info($"[MPChat] Current version: {currentVersion}");

        using var request = UnityWebRequest.Get(ApiUrl);
        request.SetRequestHeader("User-Agent", "MultiplayerChat-Mod");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Version check failed: {request.error}");
            yield break;
        }

        var latestVersion = ParseVersionFromJson(request.downloadHandler.text);
        if (string.IsNullOrEmpty(latestVersion))
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Could not parse version from GitHub response");
            yield break;
        }
        MultiplayerChat.Plugin.Log?.Info($"[MPChat] Latest GitHub version: {latestVersion}");

        var updateAvailable = IsNewerVersion(latestVersion!, currentVersion!);
        var msg = updateAvailable
            ? "An update to Multiplayer Chat is available! Updating is STRONGLY recommended! We have already opened a tab in your browser to download the latest version."
            : "There is currently no update avalible. Please close this. This will automatically open when there is a update avalible informing you to update this mod.";

        UpdateMessage = msg;

        if (updateAvailable)
        {
            MultiplayerChat.Plugin.Log?.Info($"[MPChat] Update available: {currentVersion} -> {latestVersion}");
            LaunchChatAutoUpdater();
        }
        else
        {
            MultiplayerChat.Plugin.Log?.Info("[MPChat] No update needed (up to date or ahead)");
        }
    }

    private void LaunchChatAutoUpdater()
    {
        try
        {
            var gameRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(gameRoot))
            {
                MultiplayerChat.Plugin.Log?.Warn("[MPChat] Could not get Beat Saber path");
                Application.OpenURL(ReleasesUrl);
                PresentUpdateFlowCoordinator();
                return;
            }
            var cauPath = Path.Combine(gameRoot, "Plugins", "Chat Auto Updater (CAU).exe");
            if (!File.Exists(cauPath))
            {
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat] CAU not found at {cauPath}");
                Application.OpenURL(ReleasesUrl);
                PresentUpdateFlowCoordinator();
                return;
            }
            var processId = Process.GetCurrentProcess().Id;
            var startInfo = new ProcessStartInfo
            {
                FileName = cauPath,
                Arguments = $"\"{gameRoot}\" {processId}",
                UseShellExecute = true
            };
            Process.Start(startInfo);
            Application.Quit();
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Failed to launch CAU: {ex.Message}");
            Application.OpenURL(ReleasesUrl);
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

    public void Dispose() { }

    private static string? GetCurrentVersion()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var resourceName = "MultiplayerChat.manifest.json";
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null) return null;

            using var reader = new System.IO.StreamReader(stream);
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
        // Find all versions in the JSON (from name, tag_name, asset names) and return the highest
        var matches = VersionRegex.Matches(json);
        string? maxVersion = null;
        foreach (Match m in matches)
        {
            var v = m.Groups[1].Value;
            if (string.IsNullOrEmpty(maxVersion) || IsNewerVersion(v, maxVersion!))
                maxVersion = v;
        }
        return maxVersion;
    }

    private static string? ExtractVersion(string s)
    {
        var m = VersionRegex.Match(s);
        return m.Success ? m.Groups[1].Value : null;
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
