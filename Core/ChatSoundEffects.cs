using System;
using System.Collections;
using System.IO;
using System.Reflection;
using MultiplayerChat.AvatarExtras.Assets;
using MultiplayerChat.Settings;
using UnityEngine;

namespace MultiplayerChat.Core;

public static class ChatSoundEffects
{
    private const string EmbeddedSoundPrefix = "MultiplayerChat.Assets.Sounds.";

    public static AudioClip? ChatClip { get; private set; }
    public static AudioClip? MutedClip { get; private set; }
    public static AudioClip? UnmutedClip { get; private set; }
    public static AudioClip? ErrorClip { get; private set; }

    private static bool _loadStarted;

    public static IEnumerator LoadClipsRoutine()
    {
        if (_loadStarted)
            yield break;
        _loadStarted = true;

        yield return LoadEmbeddedOgg("Chat.ogg", c => ChatClip = c);
        yield return LoadEmbeddedOgg("Muted.ogg", c => MutedClip = c);
        yield return LoadEmbeddedOgg("Unmuted.ogg", c => UnmutedClip = c);
        yield return LoadEmbeddedOgg("Error.ogg", c => ErrorClip = c);
    }

    private static IEnumerator LoadEmbeddedOgg(string fileName, Action<AudioClip?> setClip)
    {
        var resourceName = EmbeddedSoundPrefix + fileName;
        var bytes = ResourceHelpers.GetResource(Assembly.GetExecutingAssembly(), resourceName);
        if (bytes == null || bytes.Length == 0)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Could not find embedded sound: {resourceName}");
            setClip(null);
            yield break;
        }

        var cacheDir = Path.Combine(Application.temporaryCachePath, "MultiplayerChat", "Sounds");
        string path;
        try
        {
            Directory.CreateDirectory(cacheDir);
            path = Path.Combine(cacheDir, fileName);
            File.WriteAllBytes(path, bytes);
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Failed to stage embedded sound {fileName}: {ex.Message}");
            setClip(null);
            yield break;
        }

        yield return LoadOgg(path, setClip);
    }

    private static IEnumerator LoadOgg(string path, Action<AudioClip?> setClip)
    {
        if (!File.Exists(path))
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Sound missing: {path}");
            setClip(null);
            yield break;
        }

        var uri = "file:///" + path.Replace("\\", "/");
        using var www = new WWW(uri);
        yield return www;
        if (!string.IsNullOrEmpty(www.error))
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Failed to load sound {path}: {www.error}");
            setClip(null);
            yield break;
        }

        var clip = www.GetAudioClip(false, false);
        setClip(clip);
    }

    public static void PlayChatBubble()
    {
        if (!ModSettings.ChatBubbleSoundsEnabled || ChatClip == null)
            return;
        PlayOneShot(ChatClip, 1f);
    }

    public static void PlayMutedNotify()
    {
        if (!ModSettings.ChatBubbleSoundsEnabled || MutedClip == null)
            return;
        PlayOneShot(MutedClip, 1f);
    }

    public static void PlayUnmutedNotify()
    {
        if (!ModSettings.ChatBubbleSoundsEnabled || UnmutedClip == null)
            return;
        PlayOneShot(UnmutedClip, 1f);
    }

    public static void PlayError()
    {
        if (ErrorClip == null) return;
        PlayOneShot(ErrorClip, 1f);
    }

    private static void PlayOneShot(AudioClip clip, float volume01)
    {
        if (clip == null || volume01 <= 0f)
            return;
        var go = new GameObject("MPChatUISound");
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = Mathf.Clamp01(volume01);
        src.spatialBlend = 0f;
        src.bypassEffects = true;
        src.bypassListenerEffects = true;
        src.bypassReverbZones = true;
        src.Play();
        UnityEngine.Object.Destroy(go, clip.length + 0.05f);
    }
}
