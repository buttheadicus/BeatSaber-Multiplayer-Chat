using System;
using System.Collections.Generic;
using System.IO;
using MultiplayerChat.Settings;
using Newtonsoft.Json;
using UnityEngine;

namespace MultiplayerChat.AvatarExtras;

/// <summary>
/// Sidecar JSON for <see cref="AvatarExtrasConfig"/> — avoids a second IPA <c>Generated&lt;T&gt;</c> on the same <c>Config</c> instance.
/// </summary>
internal static class AvatarExtrasConfigPersistence
{
    private static readonly string FilePath = Path.Combine(ChatIdFilePaths.RootDirectory, "MultiplayerChatAvatarExtras.json");

    internal static AvatarExtrasConfig LoadOrCreate()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AvatarExtrasConfig();

            var json = File.ReadAllText(FilePath);
            var dto = JsonConvert.DeserializeObject<PersistDto>(json);
            if (dto?.BackupColors == null || dto.BackupColors.Count == 0)
                return new AvatarExtrasConfig();

            var cfg = new AvatarExtrasConfig();
            foreach (var kv in dto.BackupColors)
            {
                var e = kv.Value;
                cfg.BackupColors[kv.Key] = new Color(e.r, e.g, e.b, e.a);
            }

            return cfg;
        }
        catch
        {
            return new AvatarExtrasConfig();
        }
    }

    internal static void Save(AvatarExtrasConfig? config)
    {
        if (config == null)
            return;
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var dto = new PersistDto { BackupColors = new Dictionary<string, ColorEntry>() };
            foreach (var kv in config.BackupColors)
            {
                var c = kv.Value;
                dto.BackupColors[kv.Key] = new ColorEntry { r = c.r, g = c.g, b = c.b, a = c.a };
            }

            File.WriteAllText(FilePath, JsonConvert.SerializeObject(dto, Formatting.Indented));
        }
        catch
        {
            // ignore disk errors
        }
    }

    [Serializable]
    private class PersistDto
    {
        public Dictionary<string, ColorEntry>? BackupColors { get; set; }
    }

    [Serializable]
    private class ColorEntry
    {
        public float r, g, b, a;
    }
}
