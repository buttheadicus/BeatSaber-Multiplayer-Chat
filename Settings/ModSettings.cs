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
    private const float DefaultBubbleDuration = 15f;
    private const bool DefaultShowSystemMessages = true;
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
        get => PlayerPrefs.GetString(KeyNameColor, DefaultNameColor);
        set
        {
            var hex = (value ?? "").Trim();
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            // Cap to 6 characters (RGB only, no alpha)
            if (hex.Length > 6) hex = hex.Substring(0, 6);
            if (hex.Length == 6 && IsValidHex(hex))
            {
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

}
