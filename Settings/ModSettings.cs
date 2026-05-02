using System;
using UnityEngine;

namespace MultiplayerChat.Settings;

/// <summary>
/// Mod preferences stored under Beat Saber LocalLow with Chat ID files (<see cref="ChatIdFilePaths.ModSettingsFilePath"/>).
/// Previously used Unity <see cref="PlayerPrefs"/> and a separate CAU flags file; those are migrated once when the JSON is missing or unreadable.
/// </summary>
public static class ModSettings
{
    private const string DefaultNameColor = "87CEEB";

    private static ModSettingsPersistence.Data D => ModSettingsPersistence.Instance;

    public static float BubbleDuration
    {
        get => D.BubbleDuration;
        set
        {
            D.BubbleDuration = Math.Max(15f, Math.Min(60f, value));
            ModSettingsPersistence.Save();
        }
    }

    public static bool ShowSystemMessages
    {
        get => D.ShowSystemMessages;
        set
        {
            D.ShowSystemMessages = value;
            ModSettingsPersistence.Save();
        }
    }

    public static string NameColor
    {
        get
        {
            var hex = D.NameColor?.Trim() ?? "";
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length == 6 && IsValidHex(hex))
                return hex;

            var fromJson = MultiplayerExtensionsJson.GetPlayerColorHex();
            return !string.IsNullOrEmpty(fromJson) ? fromJson! : DefaultNameColor;
        }
        set
        {
            var hex = (value ?? "").Trim();
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length > 6) hex = hex.Substring(0, 6);
            if (hex.Length != 6 || !IsValidHex(hex))
                return;

            D.NameColor = hex;
            ModSettingsPersistence.Save();
            MultiplayerExtensionsJson.SetPlayerColorHex(hex);
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
        get => D.CustomPlacement;
        set
        {
            D.CustomPlacement = value;
            ModSettingsPersistence.Save();
        }
    }

    public static Vector2 LobbyChatPosition
    {
        get => new Vector2(D.LobbyChatPosX, D.LobbyChatPosY);
        set
        {
            D.LobbyChatPosX = value.x;
            D.LobbyChatPosY = value.y;
            ModSettingsPersistence.Save();
        }
    }

    /// <summary>UI one-shot sounds for chat bubbles (not system lines).</summary>
    public static bool ChatBubbleSoundsEnabled
    {
        get => D.ChatBubbleSoundsEnabled;
        set
        {
            D.ChatBubbleSoundsEnabled = value;
            ModSettingsPersistence.Save();
        }
    }

    /// <summary>
    /// Windows recording device name from <see cref="UnityEngine.Microphone.devices"/>, or empty to use the system default device.
    /// </summary>
    public static string MicInputDeviceName
    {
        get => D.MicInputDeviceName ?? "";
        set
        {
            D.MicInputDeviceName = value ?? "";
            ModSettingsPersistence.Save();
        }
    }

    public static bool PushToTalkEnabled
    {
        get => D.PushToTalkEnabled;
        set
        {
            D.PushToTalkEnabled = value;
            ModSettingsPersistence.Save();
        }
    }

    /// <summary>0-3: Primary, Secondary, Trigger, Grip.</summary>
    public static int PttBindingIndex
    {
        get => Mathf.Clamp(D.PttBindingIndex, 0, 3);
        set
        {
            D.PttBindingIndex = Mathf.Clamp(value, 0, 3);
            ModSettingsPersistence.Save();
        }
    }

    /// <summary>Lower selected game <see cref="UnityEngine.AudioSource"/> volumes while incoming voice is active; MPChat playback sources are excluded by hierarchy name.</summary>
    public static bool VoiceDuckingEnabled
    {
        get => D.VoiceDuckingEnabled;
        set
        {
            D.VoiceDuckingEnabled = value;
            ModSettingsPersistence.Save();
        }
    }

    /// <summary>Game audio multiplier while ducked (5-100), as percent of baseline per-source volume.</summary>
    public static int VoiceDuckTargetPercent
    {
        get => Mathf.Clamp(D.VoiceDuckTargetPercent, 5, 100);
        set
        {
            D.VoiceDuckTargetPercent = Mathf.Clamp(value, 5, 100);
            ModSettingsPersistence.Save();
        }
    }

    /// <summary>v0.3.1: UI removed; behavior forced off until restored in a later release.</summary>
    private const bool SongPeriodMuteAndDeafTemporarilyDisabled = true;

    /// <summary>
    /// During active song / arena, force hot-mic mute (currently disabled for v0.3.1; see <see cref="SongPeriodMuteAndDeafTemporarilyDisabled"/>).
    /// </summary>
    public static bool MuteMicDuringSongPlaying
    {
        get => !SongPeriodMuteAndDeafTemporarilyDisabled && D.MuteMicDuringSongPlaying;
        set
        {
            D.MuteMicDuringSongPlaying = value;
            ModSettingsPersistence.Save();
        }
    }

    /// <summary>
    /// During active song / arena, force deafen (currently disabled for v0.3.1; see <see cref="SongPeriodMuteAndDeafTemporarilyDisabled"/>).
    /// </summary>
    public static bool DeafDuringSongPlaying
    {
        get => !SongPeriodMuteAndDeafTemporarilyDisabled && D.DeafDuringSongPlaying;
        set
        {
            D.DeafDuringSongPlaying = value;
            ModSettingsPersistence.Save();
        }
    }

    /// <summary>
    /// Opt-in Chat Auto Updater (CAU). Stored in <see cref="ChatIdFilePaths.ModSettingsFilePath"/> with other mod settings.
    /// </summary>
    public static bool EnableCau
    {
        get => D.EnableCau;
        set
        {
            D.EnableCau = value;
            ModSettingsPersistence.Save();
        }
    }

    /// <summary>
    /// Off by default. When on, Avatar Extras load at startup (editor + packed networking). Changing this requires a game restart.
    /// </summary>
    public static bool EnableAvatarExtensions
    {
        get => D.EnableAvatarExtensions;
        set
        {
            D.EnableAvatarExtensions = value;
            ModSettingsPersistence.Save();
        }
    }

}
