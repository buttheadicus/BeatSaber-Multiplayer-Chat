using System;
using UnityEngine;

namespace MultiplayerChat.Settings;

/// <summary>
/// Persists floating chat panel position/rotation. Uses PlayerPrefs.
/// </summary>
public static class ChatPositionSettings
{
    private const string KeyVersion = "MultiplayerChat.LayoutVersion";
    private const int CurrentVersion = 3; // Bump to reset saved position when layout changes
    private const string KeyPosX = "MultiplayerChat.PosX";
    private const string KeyPosY = "MultiplayerChat.PosY";
    private const string KeyPosZ = "MultiplayerChat.PosZ";
    private const string KeyRotX = "MultiplayerChat.RotX";
    private const string KeyRotY = "MultiplayerChat.RotY";
    private const string KeyRotZ = "MultiplayerChat.RotZ";
    private const string KeyRotW = "MultiplayerChat.RotW";

    private static readonly Vector3 DefaultPosition = new(0f, 1.2f, 2f);
    private static readonly Quaternion DefaultRotation = Quaternion.identity;

    public static Vector3 LoadPosition()
    {
        if (PlayerPrefs.GetInt(KeyVersion, 0) < CurrentVersion)
        {
            PlayerPrefs.SetInt(KeyVersion, CurrentVersion);
            PlayerPrefs.DeleteKey(KeyPosX);
            PlayerPrefs.DeleteKey(KeyPosY);
            PlayerPrefs.DeleteKey(KeyPosZ);
            PlayerPrefs.DeleteKey(KeyRotX);
            PlayerPrefs.DeleteKey(KeyRotY);
            PlayerPrefs.DeleteKey(KeyRotZ);
            PlayerPrefs.DeleteKey(KeyRotW);
            PlayerPrefs.Save();
            return DefaultPosition;
        }
        if (!PlayerPrefs.HasKey(KeyPosX)) return DefaultPosition;
        return new Vector3(
            PlayerPrefs.GetFloat(KeyPosX),
            PlayerPrefs.GetFloat(KeyPosY),
            PlayerPrefs.GetFloat(KeyPosZ));
    }

    public static Quaternion LoadRotation()
    {
        if (PlayerPrefs.GetInt(KeyVersion, 0) < CurrentVersion) return DefaultRotation;
        if (!PlayerPrefs.HasKey(KeyRotX)) return DefaultRotation;
        return new Quaternion(
            PlayerPrefs.GetFloat(KeyRotX),
            PlayerPrefs.GetFloat(KeyRotY),
            PlayerPrefs.GetFloat(KeyRotZ),
            PlayerPrefs.GetFloat(KeyRotW));
    }

    public static void SavePosition(Vector3 position)
    {
        PlayerPrefs.SetFloat(KeyPosX, position.x);
        PlayerPrefs.SetFloat(KeyPosY, position.y);
        PlayerPrefs.SetFloat(KeyPosZ, position.z);
        PlayerPrefs.Save();
    }

    public static void SaveRotation(Quaternion rotation)
    {
        PlayerPrefs.SetFloat(KeyRotX, rotation.x);
        PlayerPrefs.SetFloat(KeyRotY, rotation.y);
        PlayerPrefs.SetFloat(KeyRotZ, rotation.z);
        PlayerPrefs.SetFloat(KeyRotW, rotation.w);
        PlayerPrefs.Save();
    }

    private const string KeyOverlayX = "MultiplayerChat.OverlayX";
    private const string KeyOverlayY = "MultiplayerChat.OverlayY";

    public static Vector2 LoadOverlayPosition()
    {
        if (PlayerPrefs.GetInt(KeyVersion, 0) < 3) return new Vector2(50, -100);
        if (!PlayerPrefs.HasKey(KeyOverlayX)) return new Vector2(50, -100);
        return new Vector2(PlayerPrefs.GetFloat(KeyOverlayX), PlayerPrefs.GetFloat(KeyOverlayY));
    }

    public static void SaveOverlayPosition(Vector2 position)
    {
        PlayerPrefs.SetFloat(KeyOverlayX, position.x);
        PlayerPrefs.SetFloat(KeyOverlayY, position.y);
        PlayerPrefs.Save();
    }
}
