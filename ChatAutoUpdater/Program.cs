using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChatAutoUpdater;

static class Program
{

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Args: [0] = Beat Saber install path, [1] = Process ID to kill (optional)
        var beatSaberPath = args.Length > 0 ? args[0] : GetBeatSaberPathFromExe();
        var processId = args.Length > 1 && int.TryParse(args[1], out var pid) ? pid : (int?)null;

        // Force close Beat Saber
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
        // CAU lives in Plugins folder; parent of Plugins is Beat Saber root
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
        using var client = new WebClient();
        client.Headers.Add("User-Agent", "MultiplayerChat-CAU");
        var json = await client.DownloadStringTaskAsync("https://api.github.com/repos/buttheadicus/BeatSaber-Multiplayer-Chat/releases/latest");
        var match = Regex.Match(json, @"""browser_download_url""\s*:\s*""(https://[^""]+\.zip)""");
        var zipUrl = match.Success ? match.Groups[1].Value : null;
        if (string.IsNullOrEmpty(zipUrl))
            throw new Exception("No zip asset found in latest release");

        var zipBytes = await client.DownloadDataTaskAsync(zipUrl);
        var tempZip = Path.Combine(Path.GetTempPath(), "MultiplayerChat-Update.zip");
        File.WriteAllBytes(tempZip, zipBytes);

        var extractDir = Path.Combine(Path.GetTempPath(), "MultiplayerChat-Update-Extract");
        if (Directory.Exists(extractDir))
            Directory.Delete(extractDir, true);
        System.IO.Compression.ZipFile.ExtractToDirectory(tempZip, extractDir);

        // Zip structure: extracted folder contains manifest.json and Plugins/ subfolder
        var extractedRoot = extractDir;
        if (Directory.GetFiles(extractDir).Length == 0 && Directory.GetDirectories(extractDir).Length == 1)
            extractedRoot = Directory.GetDirectories(extractDir)[0];

        var pluginsSource = Path.Combine(extractedRoot, "Plugins");
        var pluginsDest = Path.Combine(_beatSaberPath, "Plugins");
        if (!Directory.Exists(pluginsDest))
            Directory.CreateDirectory(pluginsDest);

        if (Directory.Exists(pluginsSource))
        {
            foreach (var file in Directory.GetFiles(pluginsSource))
            {
                var dest = Path.Combine(pluginsDest, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }
        }

        var manifestSource = Path.Combine(extractedRoot, "manifest.json");
        if (File.Exists(manifestSource))
        {
            var manifestDest = Path.Combine(_beatSaberPath, "manifest.json");
            File.Copy(manifestSource, manifestDest, true);
        }

        try { File.Delete(tempZip); } catch { }
        try { Directory.Delete(extractDir, true); } catch { }
    }
}
