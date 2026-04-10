using System;
using UnityEngine;

namespace MultiplayerChat.Settings;

/// <summary>
/// Persists mod settings using PlayerPrefs.
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
            return !string.IsNullOrEmpty(fromJson) ? fromJson : PlayerPrefs.GetString(KeyNameColor, DefaultNameColor);
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

}
