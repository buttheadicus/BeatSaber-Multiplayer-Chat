using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using BeatSaberMarkupLanguage.MenuButtons;
using HMUI;
using MultiplayerChat.UI;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;

namespace MultiplayerChat.Core;

/// <summary>
/// Checks GitHub for newer releases. When an update is available, opens URL and adds a menu tab
/// "Multiplayer Chat Update" that opens the update UI (FlowCoordinator) when clicked.
/// </summary>
public class VersionChecker : MonoBehaviour, IInitializable, IDisposable
{
    private const string ApiUrl = "https://api.github.com/repos/buttheadicus/BeatSaber-Multiplayer-Chat/releases/latest";
    private const string ReleasesUrl = "https://github.com/buttheadicus/BeatSaber-Multiplayer-Chat/releases";

    [Inject] private readonly DiContainer _container = null!;
    [Inject] private readonly MainFlowCoordinator _mainFlowCoordinator = null!;

    private static readonly Regex VersionRegex = new(@"v?(\d+\.\d+\.\d+)", RegexOptions.IgnoreCase);
    private MenuButton? _updateMenuButton;

    public void Initialize()
    {
        StartCoroutine(CheckForUpdates());
    }

    private IEnumerator CheckForUpdates()
    {
        MultiplayerChat.Plugin.Log?.Info("[E2EChat] Version check starting...");
        yield return new WaitForSeconds(0.5f);
        var currentVersion = GetCurrentVersion();
        if (string.IsNullOrEmpty(currentVersion))
        {
            MultiplayerChat.Plugin.Log?.Warn("[E2EChat] Could not read current version from manifest");
            yield break;
        }
        MultiplayerChat.Plugin.Log?.Info($"[E2EChat] Current version: {currentVersion}");

        using var request = UnityWebRequest.Get(ApiUrl);
        request.SetRequestHeader("User-Agent", "MultiplayerChat-Mod");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[E2EChat] Version check failed: {request.error}");
            yield break;
        }

        var latestVersion = ParseVersionFromJson(request.downloadHandler.text);
        if (string.IsNullOrEmpty(latestVersion))
        {
            MultiplayerChat.Plugin.Log?.Warn("[E2EChat] Could not parse version from GitHub response");
            yield break;
        }
        MultiplayerChat.Plugin.Log?.Info($"[E2EChat] Latest GitHub version: {latestVersion}");

        var updateAvailable = IsNewerVersion(latestVersion!, currentVersion!);
        var msg = updateAvailable
            ? "An update to Multiplayer Chat is available! Updating is STRONGLY recommended! We have already opened a tab in your browser to download the latest version."
            : "There is currently no update avalible. Please close this. This will automatically open when there is a update avalible informing you to update this mod.";

        if (updateAvailable)
        {
            Application.OpenURL(ReleasesUrl);
            MultiplayerChat.Plugin.Log?.Info($"[E2EChat] Update available: {currentVersion} -> {latestVersion}");
        }
        else
        {
            MultiplayerChat.Plugin.Log?.Info("[E2EChat] No update needed (up to date or ahead)");
        }

        _updateMenuButton = new MenuButton("Multiplayer Chat Update", "View update message", () =>
        {
            var fc = _container.InstantiateComponentOnNewGameObject<UpdateFlowCoordinator>();
            fc.SetMessage(msg);
            _mainFlowCoordinator.PresentFlowCoordinator(fc);
        });
        MenuButtons.Instance.RegisterButton(_updateMenuButton);

        if (updateAvailable)
        {
            var fc = _container.InstantiateComponentOnNewGameObject<UpdateFlowCoordinator>();
            fc.SetMessage(msg);
            _mainFlowCoordinator.PresentFlowCoordinator(fc);
        }
    }

    public void Dispose()
    {
        if (_updateMenuButton != null)
        {
            try { MenuButtons.Instance?.UnregisterButton(_updateMenuButton); } catch { }
            _updateMenuButton = null;
        }
    }

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
            MultiplayerChat.Plugin.Log?.Warn($"[E2EChat] Failed to read version: {ex.Message}");
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
