using System;
using System.Collections.Generic;
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

// When lobby custom avatars are enabled, ensure DynamicBone, Custom Avatars, and Final IK are present.
public static class CustomAvatarDependenciesBootstrap
{
    public const string CustomAvatarDll = "CustomAvatar.dll";

    public const string CustomAvatarsDllAlt = "CustomAvatars.dll";

    public const string FinalIkRelativePath = "Libs/FinalIK.dll";

    public const string DynamicBonePluginDll = "Plugins/DynamicBone.dll";

    public const string DynamicBoneLibDll = "Libs/DynamicBone.dll";

    public const string CustomAvatarZipAssetName = "CustomAvatar-v5.4.11+bs.1.40.0.zip";

    public const string CustomAvatarReleaseTagUrl =
        "https://github.com/nicoco007/BeatSaberCustomAvatars/releases/tag/v5.4.11";

    private static readonly string CustomAvatarZipUrl =
        "https://github.com/nicoco007/BeatSaberCustomAvatars/releases/download/v5.4.11/"
        + CustomAvatarZipAssetName;

    private const string BeatModsModsApiBase =
        "https://beatmods.com/api/mods?status=verified&gameName=BeatSaber&platform=steampc&gameVersion=";

    private const string FinalIkModName = "Final IK";

    private const string DynamicBoneModName = "Dynamic Bone";

    // BeatMods CDN fallbacks for BS 1.40.x when the API is unreachable.
    private static readonly Dictionary<string, string> BeatModsZipHashFallbacks140 =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { DynamicBoneModName, "4be47b089272a8008880ace535f92bec" },
            { FinalIkModName, "0dc804c710ba91da074c70fbe5785a84" },
        };

    internal static bool SessionDependenciesReady { get; private set; } = true;

    internal static bool IsSessionActive() =>
        MpChatFeatures.LobbyCustomAvatars &&
        ModSettings.EnableLobbyCustomAvatars &&
        SessionDependenciesReady;

    internal static bool TryContinueAfterEnsuringDependencies(IPALogger log)
    {
        if (!MpChatFeatures.LobbyCustomAvatars)
            return true;

        if (!ModSettings.EnableLobbyCustomAvatars)
        {
            SessionDependenciesReady = true;
            return true;
        }

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
            log.Warn("[MPChat][CustomAvatars] Could not resolve plugin directory; lobby custom avatars disabled this session.");
            SessionDependenciesReady = false;
            return true;
        }

        var pluginsPath = pluginsDir!;
        var installRoot = UnityGame.InstallPath;
        var hasCustomAvatarDll = IsCustomAvatarDllPresent(pluginsPath);
        var customAvatarModLoaded = IsCustomAvatarModLoaded();
        var hasDynamicBone = IsDynamicBonePresent(installRoot);
        var hasFinalIk = IsFinalIkPresent(installRoot);

        var needDynamicBone = !hasDynamicBone;
        var needCustomAvatar = !hasCustomAvatarDll;
        var needFinalIk = !hasFinalIk;

        if (!needDynamicBone && !needCustomAvatar && !needFinalIk)
        {
            SessionDependenciesReady = customAvatarModLoaded && hasFinalIk;
            if (SessionDependenciesReady)
                return true;

            LogLoadedDependencyFailures(log, hasCustomAvatarDll, customAvatarModLoaded, hasDynamicBone, hasFinalIk);
            log.Warn(
                "[MPChat][CustomAvatars] Lobby custom avatars are disabled for this session. Chat and voice are unaffected.");
            return true;
        }

        if (TryBatchInstallMissingDependencies(
                log,
                installRoot,
                needDynamicBone,
                needCustomAvatar,
                needFinalIk))
        {
            return false;
        }

        SessionDependenciesReady = false;
        LogLoadedDependencyFailures(log, hasCustomAvatarDll, customAvatarModLoaded, hasDynamicBone, hasFinalIk);
        log.Warn(
            "[MPChat][CustomAvatars] Lobby custom avatars are disabled for this session. Chat and voice are unaffected.");

        return true;
    }

    // Download every missing dependency first, install together, then exit once (no one-at-a-time restarts).
    private static bool TryBatchInstallMissingDependencies(
        IPALogger log,
        string installRoot,
        bool needDynamicBone,
        bool needCustomAvatar,
        bool needFinalIk)
    {
        var pending = new List<PendingDependencyZip>();
        if (needDynamicBone)
            pending.Add(new PendingDependencyZip("DynamicBone", "DynamicBone.zip", DependencySource.BeatMods, DynamicBoneModName));
        if (needCustomAvatar)
            pending.Add(new PendingDependencyZip("Custom Avatars", CustomAvatarZipAssetName, DependencySource.GitHub, null));
        if (needFinalIk)
            pending.Add(new PendingDependencyZip("Final IK", "FinalIK.zip", DependencySource.BeatMods, FinalIkModName));

        if (pending.Count == 0)
            return false;

        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

        log.Warn(
            $"[MPChat][CustomAvatars] Missing {pending.Count} dependencies - downloading all before install...");

        var rawGameVersion = Application.version;
        if (string.IsNullOrWhiteSpace(rawGameVersion))
            rawGameVersion = "1.40.8";

        using var handler = new HttpClientHandler();
        using var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MultiplayerChat-CustomAvatarBootstrap");

        JArray? beatMods = null;
        if (needDynamicBone || needFinalIk)
            TryFetchBeatModsListWithFallbacks(client, rawGameVersion, log, out beatMods);

        foreach (var item in pending)
        {
            log.Warn($"[MPChat][CustomAvatars] Downloading {item.DisplayName}...");
            if (!TryDownloadPendingZip(client, beatMods, item, rawGameVersion, log))
            {
                log.Error(
                    $"[MPChat][CustomAvatars] Batch install aborted ({item.DisplayName} download failed). "
                    + "Beat Saber will stay open. Retry on next launch or install manually.");
                return false;
            }
        }

        log.Warn($"[MPChat][CustomAvatars] Installing {pending.Count} dependencies...");

        var scratchRoot = Path.Combine(Path.GetTempPath(), "MPChatBootstrapCA-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratchRoot);

        try
        {
            foreach (var item in pending)
            {
                if (!TryExtractZipIntoInstallRoot(item.ZipBytes!, scratchRoot, item.ZipFileName, installRoot, log))
                {
                    log.Error(
                        $"[MPChat][CustomAvatars] Batch install aborted ({item.DisplayName} extract failed). "
                        + "Beat Saber will stay open. Retry on next launch or install manually.");
                    return false;
                }
            }
        }
        finally
        {
            TryDeleteScratch(scratchRoot);
        }

        var names = string.Join(", ", pending.ConvertAll(p => p.DisplayName));
        log.Warn(
            $"[MPChat][CustomAvatars] Installed {names}. Beat Saber will close - relaunch once so they load.");
        ScheduleHardExitSoon();
        return true;
    }

    private enum DependencySource
    {
        BeatMods,
        GitHub
    }

    private sealed class PendingDependencyZip
    {
        internal PendingDependencyZip(string displayName, string zipFileName, DependencySource source, string? beatModsModName)
        {
            DisplayName = displayName;
            ZipFileName = zipFileName;
            Source = source;
            BeatModsModName = beatModsModName;
        }

        internal string DisplayName { get; }

        internal string ZipFileName { get; }

        internal DependencySource Source { get; }

        internal string? BeatModsModName { get; }

        internal byte[]? ZipBytes { get; set; }
    }

    internal static string? GetSettingsBlockedMessage()
    {
        if (!MpChatFeatures.LobbyCustomAvatars || !ModSettings.EnableLobbyCustomAvatars)
            return null;

        if (SessionDependenciesReady)
            return null;

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
            return "Lobby custom avatars are unavailable on this install (could not verify dependencies).";

        var pluginsPath = pluginsDir!;
        var installRoot = UnityGame.InstallPath;
        var missing = new List<string>();
        if (!IsDynamicBonePresent(installRoot))
            missing.Add("DynamicBone");

        if (!IsCustomAvatarDllPresent(pluginsPath))
            missing.Add("Custom Avatars");
        else if (!IsCustomAvatarModLoaded())
            missing.Add("Custom Avatars (did not load - restart after installing DynamicBone)");

        if (!IsFinalIkPresent(installRoot))
            missing.Add("Final IK");

        if (missing.Count == 0)
            return "Lobby custom avatars are unavailable on this install.";

        return "Missing on this install (auto-install runs on game start when addon is enabled): "
            + string.Join(", ", missing) + ". Restart after install.";
    }

    internal static bool IsCustomAvatarDllPresent(string pluginsDir) =>
        File.Exists(Path.Combine(pluginsDir, CustomAvatarDll)) ||
        File.Exists(Path.Combine(pluginsDir, CustomAvatarsDllAlt));

    internal static bool IsCustomAvatarModLoaded()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = asm.GetName().Name;
            if (name != null && string.Equals(name, "CustomAvatar", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    internal static bool IsDynamicBonePresent(string installRoot) =>
        File.Exists(Path.Combine(installRoot, DynamicBonePluginDll)) ||
        File.Exists(Path.Combine(installRoot, DynamicBoneLibDll));

    internal static bool IsFinalIkPresent(string installRoot) =>
        File.Exists(Path.Combine(installRoot, FinalIkRelativePath));

    private static void LogLoadedDependencyFailures(
        IPALogger log,
        bool hasCustomAvatarDll,
        bool customAvatarModLoaded,
        bool hasDynamicBone,
        bool hasFinalIk)
    {
        if (!hasCustomAvatarDll)
        {
            log.Error(
                "[MPChat][CustomAvatars] Custom Avatars is not installed on this Beat Saber instance. "
                + $"Release: {CustomAvatarReleaseTagUrl}");
        }
        else if (!customAvatarModLoaded)
        {
            log.Error(
                "[MPChat][CustomAvatars] Custom Avatars did not load (IPA skipped it). "
                + "Install DynamicBone, then restart.");
        }

        if (!hasDynamicBone)
        {
            log.Error(
                "[MPChat][CustomAvatars] DynamicBone is not installed on this Beat Saber instance. "
                + "Custom Avatars requires it.");
        }

        if (!hasFinalIk)
        {
            log.Error(
                "[MPChat][CustomAvatars] Final IK is not installed on this Beat Saber instance.");
        }
    }

    private static string NormalizeGameVersionForBeatMods(string rawVersion)
    {
        if (string.IsNullOrWhiteSpace(rawVersion))
            return "1.40.8";

        var version = rawVersion.Trim();
        var underscore = version.IndexOf('_');
        if (underscore > 0)
            version = version.Substring(0, underscore);

        return version;
    }

    private static List<string> BuildBeatModsVersionCandidates(string rawVersion)
    {
        var candidates = new List<string>();
        var normalized = NormalizeGameVersionForBeatMods(rawVersion);
        if (!string.IsNullOrEmpty(normalized))
            candidates.Add(normalized);

        if (normalized.StartsWith("1.40.", StringComparison.Ordinal))
        {
            foreach (var fallback in new[] { "1.40.8", "1.40.4", "1.40.0" })
            {
                if (!candidates.Contains(fallback))
                    candidates.Add(fallback);
            }
        }

        return candidates;
    }

    private static bool TryFetchBeatModsListWithFallbacks(
        HttpClient client,
        string rawGameVersion,
        IPALogger log,
        out JArray? mods)
    {
        mods = null;
        var normalized = NormalizeGameVersionForBeatMods(rawGameVersion);
        if (!string.Equals(rawGameVersion, normalized, StringComparison.Ordinal))
        {
            log.Debug(
                $"[MPChat][CustomAvatars] BeatMods game version {rawGameVersion} normalized to {normalized}.");
        }

        foreach (var candidate in BuildBeatModsVersionCandidates(rawGameVersion))
        {
            if (TryFetchBeatModsList(client, candidate, log, out mods, logFailures: false))
            {
                if (!string.Equals(candidate, normalized, StringComparison.Ordinal))
                {
                    log.Info(
                        $"[MPChat][CustomAvatars] BeatMods mod list loaded using game version {candidate}.");
                }

                return true;
            }
        }

        log.Warn(
            "[MPChat][CustomAvatars] BeatMods API unavailable; will try known BS 1.40.x zip fallbacks for DynamicBone and Final IK.");
        return false;
    }

    private static bool TryFetchBeatModsList(
        HttpClient client,
        string gameVersion,
        IPALogger log,
        out JArray? mods,
        bool logFailures = true)
    {
        mods = null;

        try
        {
            var listUrl = BeatModsModsApiBase + Uri.EscapeDataString(gameVersion);
            var json = client.GetStringAsync(listUrl).ConfigureAwait(false).GetAwaiter().GetResult();
            mods = JObject.Parse(json)["mods"] as JArray;
            if (mods == null)
            {
                if (logFailures)
                    log.Error("[MPChat][CustomAvatars] BeatMods returned no mod list.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            if (logFailures)
                log.Error($"[MPChat][CustomAvatars] BeatMods list fetch failed: {ex.Message}");
            return false;
        }
    }

    private static bool TryDownloadPendingZip(
        HttpClient client,
        JArray? beatMods,
        PendingDependencyZip item,
        string rawGameVersion,
        IPALogger log)
    {
        try
        {
            byte[] zipBytes;
            if (item.Source == DependencySource.GitHub)
            {
                zipBytes = client.GetByteArrayAsync(CustomAvatarZipUrl).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            else
            {
                if (string.IsNullOrEmpty(item.BeatModsModName))
                {
                    log.Error($"[MPChat][CustomAvatars] BeatMods mod name missing for {item.DisplayName}.");
                    return false;
                }

                var beatModsModName = item.BeatModsModName!;
                string? zipHash = null;
                if (beatMods != null && TryResolveBeatModsZipHash(beatMods, beatModsModName, out var apiHash))
                    zipHash = apiHash;
                else if (TryResolveBeatModsFallbackZipHash(beatModsModName, rawGameVersion, out var fallbackHash))
                {
                    zipHash = fallbackHash;
                    log.Warn(
                        $"[MPChat][CustomAvatars] Using BeatMods CDN fallback for {item.DisplayName}.");
                }

                if (string.IsNullOrEmpty(zipHash))
                {
                    log.Error($"[MPChat][CustomAvatars] Could not resolve download for {item.DisplayName}.");
                    return false;
                }

                var downloadUrl = "https://beatmods.com/cdn/mod/" + zipHash + ".zip";
                zipBytes = client.GetByteArrayAsync(downloadUrl).ConfigureAwait(false).GetAwaiter().GetResult();
            }

            if (zipBytes == null || zipBytes.Length == 0)
            {
                log.Error($"[MPChat][CustomAvatars] {item.DisplayName} download was empty.");
                return false;
            }

            item.ZipBytes = zipBytes;
            return true;
        }
        catch (Exception ex)
        {
            log.Error($"[MPChat][CustomAvatars] {item.DisplayName} download failed: {ex.Message}");
            return false;
        }
    }

    private static bool TryResolveBeatModsZipHash(JArray mods, string modName, out string zipHash)
    {
        zipHash = "";

        foreach (var entry in mods)
        {
            var name = entry?["mod"]?["name"]?.Value<string>();
            if (!string.Equals(name, modName, StringComparison.OrdinalIgnoreCase))
                continue;

            zipHash = entry?["latest"]?["zipHash"]?.Value<string>() ?? "";
            break;
        }

        return !string.IsNullOrEmpty(zipHash);
    }

    private static bool TryResolveBeatModsFallbackZipHash(string modName, string rawGameVersion, out string zipHash)
    {
        zipHash = "";
        var normalized = NormalizeGameVersionForBeatMods(rawGameVersion);
        if (!normalized.StartsWith("1.40", StringComparison.Ordinal))
            return false;

        return BeatModsZipHashFallbacks140.TryGetValue(modName, out zipHash);
    }

    private static bool TryExtractZipIntoInstallRoot(
        byte[] zipBytes,
        string scratchRoot,
        string zipFileName,
        string installRoot,
        IPALogger log)
    {
        try
        {
            var zipPath = Path.Combine(scratchRoot, zipFileName);
            File.WriteAllBytes(zipPath, zipBytes);
            var extractDir = Path.Combine(scratchRoot, Path.GetFileNameWithoutExtension(zipFileName) + "-extracted");
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, recursive: true);

            Directory.CreateDirectory(extractDir);
            ZipFile.ExtractToDirectory(zipPath, extractDir);
            CopyTreeIntoInstallRoot(extractDir, installRoot);
            return true;
        }
        catch (Exception ex)
        {
            log.Error($"[MPChat][CustomAvatars] Failed to extract {zipFileName}: {ex.Message}");
            return false;
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
