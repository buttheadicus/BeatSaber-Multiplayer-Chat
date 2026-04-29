using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using IPALogger = IPA.Logging.Logger;

namespace MultiplayerChat.Core;

/// <summary>
/// Ensures standalone <see cref="TargetDll"/> lives next to <see cref="MultiplayerChat"/>. Downloads the official zip when missing,
/// installs the DLL only, then forces an exit so BSIPA picks up the new assembly on next launch.
/// </summary>
public static class MultiplayerExtensionsBootstrap
{
    public const string TargetDll = "MultiplayerExtensions.dll";

    /// <summary>
    /// <see cref="ReleaseTagUrl"/> ZIP asset (<c>{root}/Plugins/{TargetDll}</c>).
    /// </summary>
    public const string DownloadZipAssetName = "MultiplayerExtensions-1.1.0-bs1.37.5-9b5959b.zip";

    public const string ReleaseTagUrl = "https://github.com/EnderdracheLP/MultiplayerExtensions/releases/tag/v1.1.0";

    private static readonly string DownloadZipUrl =
        "https://github.com/EnderdracheLP/MultiplayerExtensions/releases/download/v1.1.0/"
        + DownloadZipAssetName;

    /// <returns>
    /// <see langword="true"/> → continue loading Multiplayer Chat now.
    /// <see langword="false"/> → <see cref="UnityEngine.Application.Quit"/> scheduled; callers should return immediately from plugin init without binding Zenject.
    /// </returns>
    internal static bool TryContinueAfterEnsuringStandaloneMpex(IPALogger log)
    {
        string? pluginsDir = null;

        try
        {
            pluginsDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }
        catch
        {
            /* ignore */
        }

        if (string.IsNullOrEmpty(pluginsDir))
        {
            log.Warn($"[MPChat][MPEX] Could not resolve plugin directory; skipping auto-install ({TargetDll}).");
            return true;
        }

        var dllPath = Path.Combine(pluginsDir, TargetDll);
        if (File.Exists(dllPath))
            return true;

        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

        log.Warn($"[MPChat][MPEX] {TargetDll} not found — downloading Multiplayer Extensions v1.1.0 for BS 1.37.5–1.40.8…");

        try
        {
            using var handler = new HttpClientHandler();
            using var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromMinutes(2)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MultiplayerChat-MpexBootstrap");

            byte[] zipBytes;

            try
            {
                zipBytes = client.GetByteArrayAsync(DownloadZipUrl).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception exHttp)
            {
                log.Error(
                    $"[MPChat][MPEX] Download failed from {ReleaseTagUrl} — {exHttp.Message}");
                log.Error("[MPChat][MPEX] Install MultiplayerExtensions manually from GitHub, then restart.");
                return true;
            }

            var scratchRoot = Path.Combine(Path.GetTempPath(), "MPChatBootstrapMpex-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(scratchRoot);

            try
            {
                var zipPath = Path.Combine(scratchRoot, DownloadZipAssetName);
                File.WriteAllBytes(zipPath, zipBytes);
                ZipFile.ExtractToDirectory(zipPath, Path.Combine(scratchRoot, "extracted"));

                var extractedDll = FindMultiplayerExtensionsDllRecursive(Path.Combine(scratchRoot, "extracted"));
                if (string.IsNullOrEmpty(extractedDll) || !File.Exists(extractedDll))
                {
                    log.Error($"[MPChat][MPEX] Could not locate {TargetDll} inside the release zip ({DownloadZipAssetName}).");
                    return true;
                }

                File.Copy(extractedDll, dllPath, overwrite: true);
                log.Warn(
                    $"[MPChat][MPEX] Installed {TargetDll} into {pluginsDir}. Beat Saber will close — relaunch once so MultiplayerExtensions loads.");
                ScheduleHardExitSoon();
                return false;
            }
            finally
            {
                TryDeleteScratch(scratchRoot);
            }
        }
        catch (Exception ex)
        {
            log.Error($"[MPChat][MPEX] Auto-install failed: {ex}");
            log.Error($"[MPChat][MPEX] Download manually: {ReleaseTagUrl}");
            return true;
        }
    }

    private static string? FindMultiplayerExtensionsDllRecursive(string root)
    {
        if (!Directory.Exists(root))
            return null;

        try
        {
            foreach (var path in Directory.EnumerateFiles(root, "*.dll", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(path).Equals(TargetDll, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
        }
        catch
        {
            return null;
        }

        return null;
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

    /// <summary>
    /// During BSIPA <c>[Init]</c>, <see cref="UnityEngine.Application.Quit"/> often does nothing and
    /// <see cref="Environment.Exit"/> can be ignored by the Unity/Mono host. Terminating the current process is reliable on Windows.
    /// </summary>
    private static void ScheduleHardExitSoon()
    {
        try
        {
            UnityEngine.Application.Quit();
        }
        catch
        {
            /* ignore */
        }

        var t = new Thread(ForceExitAfterDelay)
        {
            Name = "MultiplayerChat.MpexBootstrap.Exit",
            IsBackground = true
        };
        t.Start();
    }

    private static void ForceExitAfterDelay()
    {
        // Brief delay so IPA / log sinks can flush the message above.
        Thread.Sleep(450);

        try
        {
            UnityEngine.Application.Quit();
        }
        catch
        {
            /* ignore */
        }

        // Unity often ignores Application.Quit during plugin init; Environment.Exit is also unreliable here.
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
