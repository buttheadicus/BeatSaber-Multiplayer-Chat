using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MultiplayerChat.Core;

public static class PlayerVoiceVolumeStore
{
    private const string KeyV1 = "MultiplayerChat.PlayerVoiceVolumesV1";
    private const string KeyV2 = "MultiplayerChat.PlayerVoiceVolumesV2";

    public const int MaxVolumePercent = int.MaxValue;

    private static Dictionary<string, int>? _cache;

    private static Dictionary<string, int> Load()
    {
        if (_cache != null) return _cache;
        _cache = new Dictionary<string, int>(StringComparer.Ordinal);

        var rawV2 = PlayerPrefs.GetString(KeyV2, "");
        if (!string.IsNullOrEmpty(rawV2))
        {
            ParsePrefsString(rawV2, _cache);
            return _cache;
        }

        var rawV1 = PlayerPrefs.GetString(KeyV1, "");
        if (!string.IsNullOrEmpty(rawV1))
            ParsePrefsString(rawV1, _cache);

        var keys = new List<string>(_cache.Keys);
        foreach (var id in keys)
        {
            if (_cache[id] == 1)
                _cache[id] = 100;
        }

        if (_cache.Count > 0 || !string.IsNullOrEmpty(rawV1))
            Save();

        if (!string.IsNullOrEmpty(rawV1))
            PlayerPrefs.DeleteKey(KeyV1);

        return _cache;
    }

    private static void ParsePrefsString(string raw, Dictionary<string, int> into)
    {
        try
        {
            foreach (var part in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var i = part.IndexOf('=');
                if (i <= 0) continue;
                var id = Uri.UnescapeDataString(part.Substring(0, i));
                if (!int.TryParse(part.Substring(i + 1), out var v)) continue;
                into[id] = ClampStoredPercent(v);
            }
        }
        catch { /* ignore corrupt */ }
    }

    private static void Save()
    {
        if (_cache == null) return;
        var sb = new StringBuilder();
        foreach (var kv in _cache)
        {
            if (sb.Length > 0) sb.Append(';');
            sb.Append(Uri.EscapeDataString(kv.Key)).Append('=').Append(kv.Value);
        }
        PlayerPrefs.SetString(KeyV2, sb.ToString());
        PlayerPrefs.Save();
    }

    public static int GetVolumePercent(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return 100;
        var d = Load();
        return d.TryGetValue(userId, out var v) ? v : 100;
    }

    public static float GetVolume01(string userId) => Mathf.Max(0f, GetVolumePercent(userId) / 100f);

    public static void SetVolumePercent(string userId, int storedPercent, bool persist = true)
    {
        if (string.IsNullOrEmpty(userId)) return;
        Load()[userId] = ClampStoredPercent(storedPercent);
        if (persist) Save();
    }

    private static int ClampStoredPercent(int value)
    {
        if (value < 0)
            return 0;
        return value > MaxVolumePercent ? MaxVolumePercent : value;
    }

    public static void FlushToDisk() => Save();

    public static void ReloadFromDisk()
    {
        _cache = null;
        Load();
    }
}
