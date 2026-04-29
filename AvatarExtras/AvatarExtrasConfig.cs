using System.Collections.Generic;
using BeatSaber.BeatAvatarSDK;
using UnityEngine;

namespace MultiplayerChat.AvatarExtras;

/// <summary>
/// Backup colors for the avatar editor rainbow toggle (persisted in <see cref="AvatarExtrasConfigPersistence"/>, not IPA <c>Config</c>).
/// </summary>
public class AvatarExtrasConfig
{
    public Dictionary<string, Color> BackupColors { get; } = new();

    public void StoreBackupColor(Color colorValue, AvatarPart editPart)
    {
        BackupColors[editPart.ToString()] = colorValue;
    }

    public Color? GetBackupColor(AvatarPart editPart)
    {
        if (BackupColors.TryGetValue(editPart.ToString(), out var color))
            return color;

        return null;
    }
}
