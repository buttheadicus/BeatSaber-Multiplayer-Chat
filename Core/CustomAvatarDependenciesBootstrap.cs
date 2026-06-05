using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using IPA.Utilities;
using MultiplayerChat.Settings;
using Newtonsoft.Json.Linq;
using UnityEngine;
using IPALogger = IPA.Logging.Logger;

namespace MultiplayerChat.Core;

// When lobby custom avatars are enabled, ensure nicoco007 Custom Avatars and Final IK are present (like MPEX bootstrap).
public static class CustomAvatarDependenciesBootstrap
{
    public const string CustomAvatarDll = "CustomAvatar.dll";

    public const string CustomAvatarsDllAlt = "CustomAvatars.dll";

    public const string FinalIkRelativePath = "Libs/FinalIK.dll";

    public const string CustomAvatarZipAssetName = "CustomAvatar-v5.4.11+bs.1.40.0.zip";

    public const string CustomAvatarReleaseTagUrl =
        "https://github.com/nicoco007/BeatSaberCustomAvatars/releases/tag/v5.4.11";

    private static readonly string CustomAvatarZipUrl =
        "https://github.com/nicoco007/BeatSaberCustomAvatars/releases/download/v5.4.11/"
        + CustomAvatarZipAssetName;

    private const string BeatModsModsApiBase =
        "https://beatmods.com/api/mods?status=verified&gameName=BeatSaber&platform=steampc&gameVersion=";

    private const string FinalIkModName = "Final IK";

    internal static bool TryContinueAfterEnsuringDependencies(IPALogger log)
    {
        if (!MpChatFeatures.LobbyCustomAvatars)
            return true;

        if (!ModSettings.EnableLobbyCustomAvatars)
            return true;

        string? pluginsDir;
        try
        {
            pluginsDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }
        catch
        {
            pluginsDir = null;
        }

        if (string.IsNullOrEmpty(pluginsDir))
        {
            log.Warn("[MPChat][CustomAvatars] Could not resolve plugin directory; skipping dependency auto-install.");
            return true;
        }

        var installRoot = UnityGame.InstallPath;
        var needCustomAvatar = !IsCustomAvatarPresent(pluginsDir!);
        var needFinalIk = !IsFinalIkPresent(installRoot);

        if (!needCustomAvatar && !needFinalIk)
            return true;

        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

        var installedAny = false;

        if (needCustomAvatar)
        {
            log.Warn(
                "[MPChat][CustomAvatars] Custom Avatars not found  -  downloading v5.4.11 for Beat Saber 1.40.0-1.40.8...");
            if (TryInstallCustomAvatarFromGitHub(log, installRoot))
                installedAny = true;
            else
                log.Error($"[MPChat][CustomAvatars] Install Custom Avatars manually: {CustomAvatarReleaseTagUrl}");
        }

        if (needFinalIk)
        {
            log.Warn("[MPChat][CustomAvatars] Final IK not found  -  downloading from BeatMods...");
            if (TryInstallFinalIkFromBeatMods(log, installRoot))
                installedAny = true;
            else
                log.Error("[MPChat][CustomAvatars] Install Final IK from ModAssistant or BeatMods, then restart.");
        }

        if (!installedAny)
            return true;

        log.Warn(
            "[MPChat][CustomAvatars] Installed missing dependencies. Beat Saber will close  -  relaunch once so they load.");
        ScheduleHardExitSoon();
        return false;
    }

    internal static bool IsCustomAvatarPresent(string pluginsDir) =>
        File.Exists(Path.Combine(pluginsDir, CustomAvatarDll)) ||
        File.Exists(Path.Combine(pluginsDir, CustomAvatarsDllAlt));

    internal static bool IsFinalIkPresent(string installRoot) =>
        File.Exists(Path.Combine(installRoot, FinalIkRelativePath));

    private static bool TryInstallCustomAvatarFromGitHub(IPALogger log, string installRoot)
    {
        try
        {
            using var handler = new HttpClientHandler();
            using var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromMinutes(3)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MultiplayerChat-CustomAvatarBootstrap");

            var zipBytes = client.GetByteArrayAsync(CustomAvatarZipUrl).ConfigureAwait(false).GetAwaiter().GetResult();
            return ExtractZipIntoInstallRoot(zipBytes, installRoot, log, CustomAvatarZipAssetName);
        }
        catch (Exception ex)
        {
            log.Error($"[MPChat][CustomAvatars] Custom Avatars download failed: {ex.Message}");
            return false;
        }
    }

    private static bool TryInstallFinalIkFromBeatMods(IPALogger log, string installRoot)
    {
        try
        {
            var gameVersion = Application.version;
            if (string.IsNullOrWhiteSpace(gameVersion))
                gameVersion = "1.40.8";

            using var handler = new HttpClientHandler();
            using var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromMinutes(3)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MultiplayerChat-CustomAvatarBootstrap");

            var listUrl = BeatModsModsApiBase + Uri.EscapeDataString(gameVersion);
            var json = client.GetStringAsync(listUrl).ConfigureAwait(false).GetAwaiter().GetResult();
            var root = JObject.Parse(json);
            var mods = root["mods"] as JArray;
            if (mods == null)
            {
                log.Error("[MPChat][CustomAvatars] BeatMods returned no mod list.");
                return false;
            }

            string? zipHash = null;
            foreach (var entry in mods)
            {
                var name = entry?["mod"]?["name"]?.Value<string>();
                if (!string.Equals(name, FinalIkModName, StringComparison.OrdinalIgnoreCase))
                    continue;

                zipHash = entry?["latest"]?["zipHash"]?.Value<string>();
                break;
            }

            if (string.IsNullOrEmpty(zipHash))
            {
                log.Error($"[MPChat][CustomAvatars] Could not find {FinalIkModName} on BeatMods for game version {gameVersion}.");
                return false;
            }

            var downloadUrl = "https://beatmods.com/cdn/mod/" + zipHash + ".zip";
            var zipBytes = client.GetByteArrayAsync(downloadUrl).ConfigureAwait(false).GetAwaiter().GetResult();
            return ExtractZipIntoInstallRoot(zipBytes, installRoot, log, "FinalIK.zip");
        }
        catch (Exception ex)
        {
            log.Error($"[MPChat][CustomAvatars] Final IK download failed: {ex.Message}");
            return false;
        }
    }

    private static bool ExtractZipIntoInstallRoot(byte[] zipBytes, string installRoot, IPALogger log, string label)
    {
        var scratchRoot = Path.Combine(Path.GetTempPath(), "MPChatBootstrapCA-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratchRoot);

        try
        {
            var zipPath = Path.Combine(scratchRoot, label);
            File.WriteAllBytes(zipPath, zipBytes);
            var extractDir = Path.Combine(scratchRoot, "extracted");
            ZipFile.ExtractToDirectory(zipPath, extractDir);
            CopyTreeIntoInstallRoot(extractDir, installRoot);
            return true;
        }
        catch (Exception ex)
        {
            log.Error($"[MPChat][CustomAvatars] Failed to extract {label}: {ex.Message}");
            return false;
        }
        finally
        {
            TryDeleteScratch(scratchRoot);
        }
    }

    private static void CopyTreeIntoInstallRoot(string extractedRoot, string installRoot)
    {
        foreach (var path in Directory.EnumerateFiles(extractedRoot, "*", SearchOption.AllDirectories))
        {
            var rel = path.Substring(extractedRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var dest = Path.Combine(installRoot, rel);
            var destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);
            File.Copy(path, dest, overwrite: true);
        }
    }

    private static void TryDeleteScratch(string scratchRoot)
    {
        try
        {
            if (Directory.Exists(scratchRoot))
                Directory.Delete(scratchRoot, recursive: true);
        }
        catch
        {
            /* best effort */
        }
    }

    private static void ScheduleHardExitSoon()
    {
        try
        {
            Application.Quit();
        }
        catch
        {
            /* ignore */
        }

        var t = new Thread(ForceExitAfterDelay)
        {
            Name = "MultiplayerChat.CustomAvatarBootstrap.Exit",
            IsBackground = true
        };
        t.Start();
    }

    private static void ForceExitAfterDelay()
    {
        Thread.Sleep(450);

        try
        {
            Application.Quit();
        }
        catch
        {
            /* ignore */
        }

        try
        {
            Process.GetCurrentProcess().Kill();
        }
        catch
        {
            try
            {
                Environment.Exit(0);
            }
            catch
            {
                /* ignored */
            }
        }
    }
}
