using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Http;
using UnityEngine.Networking;

namespace MultiplayerChat.Core.Addons;

internal static class AddonGitHubDownload
{
    private const string UserAgent = "MultiplayerChat-AddonUpdater";

    internal static bool TryDownloadReleaseJsonSync(string apiUrl, out string json, out string error)
    {
        json = "";
        error = "";
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

        try
        {
            using var handler = new HttpClientHandler();
            using var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromMinutes(2)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
            json = client.GetStringAsync(apiUrl).ConfigureAwait(false).GetAwaiter().GetResult();
            return !string.IsNullOrEmpty(json);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    internal static IEnumerator FetchReleaseJsonCoroutine(string apiUrl, Action<bool, string> onComplete)
    {
        using var request = UnityWebRequest.Get(apiUrl);
        request.SetRequestHeader("User-Agent", UserAgent);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            var body = request.downloadHandler?.text?.Trim();
            var error = request.error ?? "request failed";
            if (body is { Length: > 0 and <= 512 })
                error = $"{error} | {body}";
            onComplete(false, error);
            yield break;
        }

        onComplete(true, request.downloadHandler?.text ?? "");
    }

    internal static bool TryDownloadFileSync(string url, string destPath, out string error)
    {
        error = "";
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

        var tmp = destPath + ".download.tmp";
        try
        {
            if (File.Exists(tmp))
                File.Delete(tmp);

            using var handler = new HttpClientHandler();
            using var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
            var bytes = client.GetByteArrayAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();
            if (bytes == null || bytes.Length == 0)
            {
                error = "empty response";
                return false;
            }

            var dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(tmp, bytes);
            if (File.Exists(destPath))
                File.Delete(destPath);
            File.Move(tmp, destPath);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            try
            {
                if (File.Exists(tmp))
                    File.Delete(tmp);
            }
            catch
            {
                /* ignore */
            }

            return false;
        }
    }

    internal static IEnumerator DownloadFileCoroutine(string url, string destPath, Action<bool, string> onComplete)
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

        using var request = UnityWebRequest.Get(url);
        request.SetRequestHeader("User-Agent", UserAgent);
        request.downloadHandler = new DownloadHandlerBuffer();
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            onComplete(false, request.error ?? "request failed");
            yield break;
        }

        var data = request.downloadHandler.data;
        if (data == null || data.Length == 0)
        {
            onComplete(false, "empty response");
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
            onComplete(true, "");
        }
        catch (Exception ex)
        {
            onComplete(false, ex.Message);
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
}
