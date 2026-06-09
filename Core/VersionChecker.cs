using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using MultiplayerChat.Settings;
using MultiplayerChat.UI;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Zenject;

namespace MultiplayerChat.Core;

public class VersionChecker : MonoBehaviour, IInitializable, IDisposable
{
    private const string ApiUrl = "https://api.github.com/repos/buttheadicus/BeatSaber-Multiplayer-Chat/releases/latest";
    private const string ReleasePageUrl = "https://github.com/buttheadicus/BeatSaber-Multiplayer-Chat/releases/latest";
    private const float MainMenuUpdateNoticeDelaySec = 2f;
    private const float MaxWaitForMainMenuSec = 120f;

    public static VersionChecker? Instance { get; private set; }

    public static string UpdateMessage { get; private set; } = "Checking for updates...";

    public void Initialize()
    {
        Instance = this;
        StartCoroutine(CheckForUpdates());
    }

    private IEnumerator CheckForUpdates()
    {
        MpChatLog.DebugLine("[MPChat] Version check starting...");
        yield return new WaitForSeconds(2.5f);
        if (!ModBuildVersion.TryGetEmbeddedBuildNumber(out var currentBuild))
        {
            UpdateMessage = "Could not read this mod build number.";
            yield break;
        }

        using var request = UnityWebRequest.Get(ApiUrl);
        request.SetRequestHeader("User-Agent", "MultiplayerChat-Mod");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            UpdateMessage = "Could not reach GitHub to check for updates.";
            yield break;
        }

        var json = request.downloadHandler.text;
        if (!GitHubReleaseVersion.TryGetLatestBuildNumberFromModDllRelease(json, out var latestBuild))
        {
            UpdateMessage = "Could not parse the latest release build number.";
            yield break;
        }

        MpChatLog.DebugLine($"[MPChat] Version check: local build {currentBuild}, latest release build {latestBuild}.");
        var updateAvailable = latestBuild > currentBuild;
        UpdateMessage = updateAvailable
            ? ChatBubbleManager.UpdateAvailableHeaderMessage
            : "Multiplayer Chat is up to date.";

        if (!updateAvailable)
            yield break;

        if (!ModSettings.EnableCau)
        {
            yield return ShowUpdateNoticeWhenMainMenuReady(openReleasePage: true);
            yield break;
        }

        using (var cauReleaseReq = UnityWebRequest.Get(GitHubReleaseVersion.CauRepoReleasesLatestApi))
        {
            cauReleaseReq.SetRequestHeader("User-Agent", "MultiplayerChat-Mod");
            yield return cauReleaseReq.SendWebRequest();

            if (cauReleaseReq.result != UnityWebRequest.Result.Success)
            {
                UpdateMessage =
                    "An update is available. Enable CAU is on, but the CAU updater release could not be reached.";
                yield return ShowUpdateNoticeWhenMainMenuReady(openReleasePage: false);
                yield break;
            }

            var cauReleaseJson = cauReleaseReq.downloadHandler.text;
            if (!GitHubReleaseVersion.TryGetCauExeDownloadUrl(cauReleaseJson, out var cauUrl))
            {
                UpdateMessage =
                    $"An update is available. Enable CAU is on, but {GitHubReleaseVersion.CauExeAssetFileName} was not found on the CAU repo's latest release.";
                yield return ShowUpdateNoticeWhenMainMenuReady(openReleasePage: false);
                yield break;
            }

            var maybeDest = CauBootstrap.GetCauExePath();
            if (string.IsNullOrEmpty(maybeDest))
            {
                yield return ShowUpdateNoticeWhenMainMenuReady(openReleasePage: false);
                yield break;
            }

            string destPath = maybeDest!;
            yield return DownloadToFileCoroutine(cauUrl, destPath);

            if (!File.Exists(destPath))
            {
                UpdateMessage = "An update is available. CAU download failed.";
                yield return ShowUpdateNoticeWhenMainMenuReady(openReleasePage: false);
                yield break;
            }

            LaunchCauAndQuit(destPath);
        }

        yield break;
    }

    private IEnumerator ShowUpdateNoticeWhenMainMenuReady(bool openReleasePage, string? message = null)
    {
        yield return WaitForMainMenuReadyThenDelay(MainMenuUpdateNoticeDelaySec);
        if (!IsMainMenuSceneActive())
            yield break;

        yield return PresentTitleBarNoticeRoutine(openReleasePage, message);
    }

    private static IEnumerator WaitForMainMenuReadyThenDelay(float delayAfterMenu)
    {
        var deadline = Time.realtimeSinceStartup + MaxWaitForMainMenuSec;
        while (Time.realtimeSinceStartup < deadline && !IsMainMenuSceneActive())
            yield return null;

        if (!IsMainMenuSceneActive())
        {
            MpChatLog.Warn("[MPChat] Update notice skipped: main menu scene not active.");
            yield break;
        }

        yield return new WaitForSeconds(delayAfterMenu);
    }

    private static bool IsMainMenuSceneActive()
    {
        var scene = SceneManager.GetActiveScene();
        return scene.IsValid() && scene.name == "MainMenu";
    }

    private static IEnumerator PresentTitleBarNoticeRoutine(bool openReleasePage, string? message)
    {
        var text = message ?? UpdateMessage;
        if (openReleasePage || text == ChatBubbleManager.UpdateAvailableHeaderMessage)
            OpenReleasePage();

        for (var i = 0; i < 48 && ChatBubbleManager.Instance == null; i++)
            yield return new WaitForSeconds(0.25f);

        if (ChatBubbleManager.Instance == null)
        {
            MpChatLog.Warn("[MPChat] Update notice skipped: title-bar chat host not ready.");
            yield break;
        }

        ChatBubbleManager.Instance.ShowTimedHeaderSystemMessage(text, 30f);
        MpChatLog.DebugLine("[MPChat] Update notice shown on main menu title bar.");
    }

    private static void OpenReleasePage()
    {
        try
        {
            Application.OpenURL(ReleasePageUrl);
        }
        catch (Exception ex)
        {
            MpChatLog.Warn($"[MPChat] Failed to open release page: {ex.Message}");
        }
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
        }

        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("User-Agent", "MultiplayerChat-Mod");
        req.downloadHandler = new DownloadHandlerBuffer();
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            yield break;

        var data = req.downloadHandler.data;
        if (data == null || data.Length == 0)
            yield break;

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
            MpChatLog.Warn($"[MPChat][CAU] Write/move failed: {ex.Message}");
            try
            {
                if (File.Exists(tmp))
                    File.Delete(tmp);
            }
            catch
            {
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
                StartCoroutine(ShowUpdateNoticeWhenMainMenuReady(openReleasePage: false));
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
            MpChatLog.Warn($"[MPChat][CAU] Failed to launch: {ex.Message}");
            StartCoroutine(ShowUpdateNoticeWhenMainMenuReady(openReleasePage: false));
        }
    }

    public void Dispose()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null;
    }

}
