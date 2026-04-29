using System;
using UnityEngine;

namespace MultiplayerChat.Settings;

/// <summary>
/// Persists mod settings using Unity <see cref="PlayerPrefs"/> (Windows: typically registry under
/// <c>HKCU\Software\Hyperbolic Magnetism\Beat Saber</c>, not the LocalLow folder). Chat id files use
/// <see cref="ChatIdFilePaths.RootDirectory"/> instead.
/// </summary>
public static class ModSettings
{
    private const string KeyBubbleDuration = "MultiplayerChat.BubbleDuration";
    private const string KeyShowSystemMessages = "MultiplayerChat.ShowSystemMessages";
    private const string KeyNameColor = "MultiplayerChat.NameColor";
    private const string KeyCustomPlacement = "MultiplayerChat.CustomPlacement";
    private const string KeyLobbyChatPosX = "MultiplayerChat.LobbyChatPosX";
    private const string KeyLobbyChatPosY = "MultiplayerChat.LobbyChatPosY";
    private const string KeyChatBubbleSounds = "MultiplayerChat.ChatBubbleSounds";
    private const string KeyMicInputDevice = "MultiplayerChat.MicInputDevice";
    private const string KeyPushToTalk = "MultiplayerChat.PushToTalk";
    private const string KeyPttBinding = "MultiplayerChat.PttBinding";
    private const string KeyVoiceDuckEnabled = "MultiplayerChat.VoiceDuckEnabled";
    private const string KeyVoiceDuckTargetPercent = "MultiplayerChat.VoiceDuckTargetPercent";
    private const string KeyMuteMicDuringSongPlaying = "MultiplayerChat.MuteMicDuringSongPlaying";
    private const string KeyDeafDuringSongPlaying = "MultiplayerChat.DeafDuringSongPlaying";
    private const string KeyEnableAvatarExtensions = "MultiplayerChat.EnableAvatarExtensions";

    private const float DefaultBubbleDuration = 15f;
    private const bool DefaultShowSystemMessages = true;
    private const bool DefaultCustomPlacement = false;
    private const string DefaultNameColor = "87CEEB";

    public static float BubbleDuration
    {
        get => PlayerPrefs.HasKey(KeyBubbleDuration) ? PlayerPrefs.GetFloat(KeyBubbleDuration) : DefaultBubbleDuration;
        set
        {
            var clamped = Math.Max(15f, Math.Min(60f, value));
            PlayerPrefs.SetFloat(KeyBubbleDuration, clamped);
            PlayerPrefs.Save();
        }
    }

    public static bool ShowSystemMessages
    {
        get => !PlayerPrefs.HasKey(KeyShowSystemMessages) || PlayerPrefs.GetInt(KeyShowSystemMessages) != 0;
        set
        {
            PlayerPrefs.SetInt(KeyShowSystemMessages, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static string NameColor
    {
        get
        {
            var fromJson = MultiplayerExtensionsJson.GetPlayerColorHex();
            return !string.IsNullOrEmpty(fromJson)
                ? fromJson!
                : PlayerPrefs.GetString(KeyNameColor, DefaultNameColor);
        }
        set
        {
            var hex = (value ?? "").Trim();
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length > 6) hex = hex.Substring(0, 6);
            if (hex.Length == 6 && IsValidHex(hex))
            {
                MultiplayerExtensionsJson.SetPlayerColorHex(hex);
                PlayerPrefs.SetString(KeyNameColor, hex);
                PlayerPrefs.Save();
            }
        }
    }

    private static bool IsValidHex(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length != 6) return false;
        foreach (var c in s)
            if (!char.IsDigit(c) && (c < 'a' || c > 'f') && (c < 'A' || c > 'F'))
                return false;
        return true;
    }

    /// <summary>Off = default position above HOST SETUP. On = custom placement with draggable handle.</summary>
    public static bool CustomPlacement
    {
        get => PlayerPrefs.HasKey(KeyCustomPlacement) && PlayerPrefs.GetInt(KeyCustomPlacement) != 0;
        set
        {
            PlayerPrefs.SetInt(KeyCustomPlacement, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static Vector2 LobbyChatPosition
    {
        get => new Vector2(
            PlayerPrefs.GetFloat(KeyLobbyChatPosX, 0f),
            PlayerPrefs.GetFloat(KeyLobbyChatPosY, 0f));
        set
        {
            PlayerPrefs.SetFloat(KeyLobbyChatPosX, value.x);
            PlayerPrefs.SetFloat(KeyLobbyChatPosY, value.y);
            PlayerPrefs.Save();
        }
    }

    /// <summary>UI one-shot sounds for chat bubbles (not system lines).</summary>
    public static bool ChatBubbleSoundsEnabled
    {
        get => !PlayerPrefs.HasKey(KeyChatBubbleSounds) || PlayerPrefs.GetInt(KeyChatBubbleSounds) != 0;
        set
        {
            PlayerPrefs.SetInt(KeyChatBubbleSounds, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Windows recording device name from <see cref="UnityEngine.Microphone.devices"/>, or empty to use the system default device.
    /// </summary>
    public static string MicInputDeviceName
    {
        get => PlayerPrefs.GetString(KeyMicInputDevice, "");
        set
        {
            PlayerPrefs.SetString(KeyMicInputDevice, value ?? "");
            PlayerPrefs.Save();
        }
    }

    public static bool PushToTalkEnabled
    {
        get => PlayerPrefs.HasKey(KeyPushToTalk) && PlayerPrefs.GetInt(KeyPushToTalk) != 0;
        set
        {
            PlayerPrefs.SetInt(KeyPushToTalk, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>0–3: Primary, Secondary, Trigger, Grip.</summary>
    public static int PttBindingIndex
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(KeyPttBinding, 0), 0, 3);
        set
        {
            PlayerPrefs.SetInt(KeyPttBinding, Mathf.Clamp(value, 0, 3));
            PlayerPrefs.Save();
        }
    }

    /// <summary>Lower selected game <see cref="UnityEngine.AudioSource"/> volumes while incoming voice is active; MPChat playback sources are excluded by hierarchy name.</summary>
    public static bool VoiceDuckingEnabled
    {
        get => PlayerPrefs.HasKey(KeyVoiceDuckEnabled) && PlayerPrefs.GetInt(KeyVoiceDuckEnabled) != 0;
        set
        {
            PlayerPrefs.SetInt(KeyVoiceDuckEnabled, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Game audio multiplier while ducked (5–100), as percent of baseline per-source volume.</summary>
    public static int VoiceDuckTargetPercent
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(KeyVoiceDuckTargetPercent, 35), 5, 100);
        set
        {
            PlayerPrefs.SetInt(KeyVoiceDuckTargetPercent, Mathf.Clamp(value, 5, 100));
            PlayerPrefs.Save();
        }
    }

    /// <summary>During active song / arena (GameCore or beatmap gameplay objects), force hot-mic mute; re-synced after VoIP reload and every frame. Restores when gameplay ends.</summary>
    public static bool MuteMicDuringSongPlaying
    {
        get => PlayerPrefs.HasKey(KeyMuteMicDuringSongPlaying) && PlayerPrefs.GetInt(KeyMuteMicDuringSongPlaying) != 0;
        set
        {
            PlayerPrefs.SetInt(KeyMuteMicDuringSongPlaying, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>During active song / arena, force deafen (incoming voice off; restores when leaving). Does not broadcast deafen packets.</summary>
    public static bool DeafDuringSongPlaying
    {
        get => PlayerPrefs.HasKey(KeyDeafDuringSongPlaying) && PlayerPrefs.GetInt(KeyDeafDuringSongPlaying) != 0;
        set
        {
            PlayerPrefs.SetInt(KeyDeafDuringSongPlaying, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Off by default. When on, Avatar Extras load at startup (editor + packed networking). Changing this requires a game restart.
    /// </summary>
    public static bool EnableAvatarExtensions
    {
        get => PlayerPrefs.HasKey(KeyEnableAvatarExtensions) && PlayerPrefs.GetInt(KeyEnableAvatarExtensions) != 0;
        set
        {
            PlayerPrefs.SetInt(KeyEnableAvatarExtensions, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

}
