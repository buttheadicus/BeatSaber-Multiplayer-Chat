using System;
using System.Collections;
using System.IO;
using System.Reflection;
using MultiplayerChat.Settings;
using UnityEngine;

namespace MultiplayerChat.Core;

public static class ChatSoundEffects
{
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

        var dllPath = Assembly.GetExecutingAssembly().Location;
        var pluginDir = Path.GetDirectoryName(dllPath);
        if (string.IsNullOrEmpty(pluginDir))
            yield break;

        yield return LoadOggFromSearchRoots(pluginDir, "Chat.ogg", c => ChatClip = c);
        yield return LoadOggFromSearchRoots(pluginDir, "Muted.ogg", c => MutedClip = c);
        yield return LoadOggFromSearchRoots(pluginDir, "Unmuted.ogg", c => UnmutedClip = c);
        yield return LoadOggFromSearchRoots(pluginDir, "Error.ogg", c => ErrorClip = c);
    }

    private static IEnumerator LoadOggFromSearchRoots(string pluginDir, string fileName, Action<AudioClip?> setClip)
    {
        var candidates = new[]
        {
            Path.Combine(pluginDir, "Sounds", fileName),
            Path.Combine(pluginDir, "..", "Sounds", fileName),
            Path.Combine(pluginDir, "MultiplayerChat", "Sounds", fileName)
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path))
                continue;
            yield return LoadOgg(path, setClip);
            yield break;
        }

        MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Sound missing (searched next to DLL, ../Sounds, MultiplayerChat/Sounds): {fileName}");
        setClip(null);
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
