using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChatAutoUpdater;

static class Program
{
    public const string DllName = "MultiplayerChat.dll";
    public const string PdbName = "MultiplayerChat.pdb";

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Args: [0] = Beat Saber install path, [1] = Process ID to kill (optional)
        var beatSaberPath = args.Length > 0 ? args[0] : GetBeatSaberPathFromExe();
        var processId = args.Length > 1 && int.TryParse(args[1], out var pid) ? pid : (int?)null;

        if (processId.HasValue)
        {
            try
            {
                var proc = Process.GetProcessById(processId.Value);
                proc.Kill();
                proc.WaitForExit(5000);
            }
            catch { /* ignore */ }
        }
        else
        {
            foreach (var proc in Process.GetProcessesByName("Beat Saber"))
            {
                try { proc.Kill(); proc.WaitForExit(3000); } catch { }
            }
        }

        Application.Run(new UpdaterForm(beatSaberPath));
    }

    private static string GetBeatSaberPathFromExe()
    {
        var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var pluginsDir = Path.GetDirectoryName(exePath);
        var beatSaberRoot = pluginsDir != null ? Path.GetDirectoryName(pluginsDir) : null;
        return beatSaberRoot ?? Environment.CurrentDirectory;
    }
}

public class UpdaterForm : Form
{
    private readonly string _beatSaberPath;
    private const string ReleasesUrl = "https://github.com/buttheadicus/BeatSaber-Multiplayer-Chat/releases/latest";
    private const string LatestReleaseApi = "https://api.github.com/repos/buttheadicus/BeatSaber-Multiplayer-Chat/releases/latest";

    public UpdaterForm(string beatSaberPath)
    {
        _beatSaberPath = beatSaberPath ?? Environment.CurrentDirectory;
        Text = "Multiplayer Chat Updater";
        Size = new System.Drawing.Size(400, 180);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var label = new Label
        {
            Text = "Multiplayer Chat has an update.",
            AutoSize = true,
            Location = new System.Drawing.Point(20, 20),
            Font = new System.Drawing.Font("Segoe UI", 12F)
        };

        var versionInfoBtn = new Button
        {
            Text = "Version Info",
            Location = new System.Drawing.Point(20, 70),
            Size = new System.Drawing.Size(160, 35)
        };
        versionInfoBtn.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(ReleasesUrl) { UseShellExecute = true }); } catch { }
        };

        var installBtn = new Button
        {
            Text = "Install",
            Location = new System.Drawing.Point(200, 70),
            Size = new System.Drawing.Size(160, 35)
        };
        installBtn.Click += async (_, _) =>
        {
            installBtn.Enabled = false;
            installBtn.Text = "Installing...";
            try
            {
                await DownloadAndInstallAsync();
                MessageBox.Show("Update installed successfully! You can now launch Beat Saber.", "Success", MessageBoxButtons.OK);
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Install failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                installBtn.Enabled = true;
                installBtn.Text = "Install";
            }
        };

        Controls.Add(label);
        Controls.Add(versionInfoBtn);
        Controls.Add(installBtn);
    }

    private async Task DownloadAndInstallAsync()
    {
        // GitHub API + releases require TLS 1.2+
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

        var tempZip = Path.Combine(Path.GetTempPath(), "MultiplayerChat-Update-" + Guid.NewGuid().ToString("N") + ".zip");
        var extractDir = Path.Combine(Path.GetTempPath(), "MultiplayerChat-Update-Extract-" + Guid.NewGuid().ToString("N"));

        try
        {
            using var client = new WebClient();
            client.Headers.Set("User-Agent", "MultiplayerChat-CAU");
            client.Headers.Set("Accept", "application/vnd.github.v3+json");

            var json = await client.DownloadStringTaskAsync(LatestReleaseApi);
            var zipUrl = PickReleaseZipUrl(json);
            if (string.IsNullOrEmpty(zipUrl))
                throw new Exception("No suitable .zip release asset found on GitHub (check repo releases).");

            var zipBytes = await client.DownloadDataTaskAsync(zipUrl);
            File.WriteAllBytes(tempZip, zipBytes);

            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, true);
            ZipFile.ExtractToDirectory(tempZip, extractDir);

            var dllSourcePath = FindFileRecursive(extractDir, Program.DllName);
            if (string.IsNullOrEmpty(dllSourcePath))
                throw new Exception($"{Program.DllName} not found inside the release zip. Release layout may have changed.");

            var sourceDir = Path.GetDirectoryName(dllSourcePath);
            if (string.IsNullOrEmpty(sourceDir))
                throw new Exception("Invalid path to extracted DLL.");

            var pluginsDest = Path.Combine(_beatSaberPath, "Plugins");
            if (!Directory.Exists(pluginsDest))
                Directory.CreateDirectory(pluginsDest);

            // 1) Remove current plugin binaries (unlocked: game was killed)
            foreach (var name in new[] { Program.DllName, Program.PdbName })
            {
                var existing = Path.Combine(pluginsDest, name);
                if (File.Exists(existing))
                {
                    try { File.Delete(existing); }
                    catch (Exception ex)
                    {
                        throw new Exception($"Could not delete {name}: {ex.Message}. Close Beat Saber / file handles and retry.");
                    }
                }
            }

            // 2) Copy new DLL + PDB from the folder that contained the DLL in the archive
            var destDll = Path.Combine(pluginsDest, Program.DllName);
            File.Copy(dllSourcePath, destDll, false);

            var pdbSource = Path.Combine(sourceDir, Program.PdbName);
            if (File.Exists(pdbSource))
            {
                var destPdb = Path.Combine(pluginsDest, Program.PdbName);
                File.Copy(pdbSource, destPdb, false);
            }
        }
        finally
        {
            TryDelete(tempZip);
            TryDeleteDir(extractDir);
        }
    }

    /// <summary>GitHub JSON has many browser_download_url fields; pick the mod release .zip, not API zipballs.</summary>
    private static string PickReleaseZipUrl(string json)
    {
        var matches = Regex.Matches(json, @"""browser_download_url""\s*:\s*""(https://[^""]+\.zip)""", RegexOptions.IgnoreCase);
        var candidates = new List<string>();
        foreach (Match m in matches)
        {
            if (m.Success)
                candidates.Add(m.Groups[1].Value);
        }

        if (candidates.Count == 0)
            return string.Empty;

        // Prefer GitHub "releases/download" assets (uploaded release zips)
        var releaseDownloads = candidates
            .Where(u => u.IndexOf("/releases/download/", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        IEnumerable<string> pool = releaseDownloads.Count > 0 ? releaseDownloads : candidates;

        // Prefer filename containing MultiplayerChat (mod zip)
        var named = pool.FirstOrDefault(u => u.IndexOf("MultiplayerChat", StringComparison.OrdinalIgnoreCase) >= 0);
        if (named != null)
            return named;

        // Prefer BeatSaber / repo name in URL
        named = pool.FirstOrDefault(u => u.IndexOf("BeatSaber", StringComparison.OrdinalIgnoreCase) >= 0
            || u.IndexOf("Multiplayer-Chat", StringComparison.OrdinalIgnoreCase) >= 0);
        if (named != null)
            return named;

        return pool.FirstOrDefault() ?? string.Empty;
    }

    private static string FindFileRecursive(string root, string fileName)
    {
        try
        {
            foreach (var path in Directory.GetFiles(root, fileName, SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(path);
                if (string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
        }
        catch { /* ignore inaccessible dirs */ }

        return string.Empty;
    }

    private static void TryDelete(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { /* ignore */ }
    }

    private static void TryDeleteDir(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { /* ignore */ }
    }
}
