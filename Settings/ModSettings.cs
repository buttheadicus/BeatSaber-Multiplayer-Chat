using System;
using UnityEngine;
using MultiplayerChat.Core;

namespace MultiplayerChat.Settings;

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

    public static bool ChatBubbleSoundsEnabled
    {
        get => D.ChatBubbleSoundsEnabled;
        set
        {
            D.ChatBubbleSoundsEnabled = value;
            ModSettingsPersistence.Save();
        }
    }

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

    public static int PttBindingIndex
    {
        get => Mathf.Clamp(D.PttBindingIndex, 0, 3);
        set
        {
            D.PttBindingIndex = Mathf.Clamp(value, 0, 3);
            ModSettingsPersistence.Save();
        }
    }

    public static bool VoiceDuckingEnabled
    {
        get => D.VoiceDuckingEnabled;
        set
        {
            D.VoiceDuckingEnabled = value;
            ModSettingsPersistence.Save();
        }
    }

    public static int VoiceDuckTargetPercent
    {
        get => Mathf.Clamp(D.VoiceDuckTargetPercent, 5, 100);
        set
        {
            D.VoiceDuckTargetPercent = Mathf.Clamp(value, 5, 100);
            ModSettingsPersistence.Save();
        }
    }

    public static bool MuteMicDuringSongPlaying
    {
        get => D.MuteMicDuringSongPlaying;
        set
        {
            D.MuteMicDuringSongPlaying = value;
            ModSettingsPersistence.Save();
        }
    }

    public static bool DeafDuringSongPlaying
    {
        get => D.DeafDuringSongPlaying;
        set
        {
            D.DeafDuringSongPlaying = value;
            ModSettingsPersistence.Save();
        }
    }

    public static bool EnableCau
    {
        get => D.EnableCau;
        set
        {
            D.EnableCau = value;
            ModSettingsPersistence.Save();
        }
    }

    public static bool DebugLogging
    {
        get => D.DebugLogging;
        set
        {
            D.DebugLogging = value;
            ModSettingsPersistence.Save();
            MpChatLog.Apply(value);
        }
    }

    public static bool EnableAvatarExtensions
    {
        get => D.EnableAvatarExtensions;
        set
        {
            D.EnableAvatarExtensions = value;
            ModSettingsPersistence.Save();
        }
    }

    public static bool EnableAvatarColoringExtensions
    {
        get => D.Addons.EnableAvatarColoringExtensions;
        set
        {
            D.Addons.EnableAvatarColoringExtensions = value;
            ModSettingsPersistence.Save();
            if (value)
                AvatarColoring.AvatarDatOperations.EnsureAvatarStorageExists();
        }
    }

    public static bool AvatarColorRgbWideRangeEnabled
    {
        get => D.Addons.AvatarColorRgbWideRange;
        set
        {
            D.Addons.AvatarColorRgbWideRange = value;
            ModSettingsPersistence.Save();
        }
    }

    public static bool AvatarColorDirectNumberEntryEnabled
    {
        get => D.Addons.AvatarColorDirectNumberEntry;
        set
        {
            D.Addons.AvatarColorDirectNumberEntry = value;
            ModSettingsPersistence.Save();
        }
    }

    public static bool EnableLobbyCustomAvatars
    {
        get => D.EnableLobbyCustomAvatars;
        set
        {
            D.EnableLobbyCustomAvatars = value;
            ModSettingsPersistence.Save();
        }
    }

    public static string LobbyCustomAvatarRelativePath
    {
        get => D.LobbyCustomAvatarRelativePath ?? "";
        set
        {
            var s = (value ?? "").Trim().Replace('\\', '/');
            if (s.Length > 260)
                s = s.Substring(0, 260);
            D.LobbyCustomAvatarRelativePath = s;
            ModSettingsPersistence.Save();
        }
    }

    public static string LobbyCustomAvatarContentHash
    {
        get => D.LobbyCustomAvatarContentHash ?? "";
        set
        {
            var h = (value ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(h))
            {
                D.LobbyCustomAvatarContentHash = "";
                ModSettingsPersistence.Save();
                return;
            }

            if (h.Length > 32)
                h = h.Substring(0, 32);
            D.LobbyCustomAvatarContentHash = CustomAvatarHashUtil.LooksLikeMd5Hex(h) ? h : "";
            ModSettingsPersistence.Save();
        }
    }

}
