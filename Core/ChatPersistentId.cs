using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MultiplayerChat.Settings;

namespace MultiplayerChat.Core;

/// <summary>
/// Stable 8-digit chat identity for this Windows user + Beat Saber data folder.
/// Stored in ChatID.dat under DPAPI so casual edits invalidate the file (new ID is generated).
/// Username is not part of the stored ID so renames do not change it; use <see cref="FormatDisplayId"/> for UI.
/// </summary>
public static class ChatPersistentId
{
    private const int MinId = 10_000_000;
    private const int MaxId = 99_999_999;

    private static readonly object LockObj = new();
    private static string? _cachedId;

    /// <summary>8-digit numeric string, e.g. "99416729".</summary>
    public static string Current
    {
        get
        {
            lock (LockObj)
            {
                if (_cachedId != null) return _cachedId;
                _cachedId = LoadOrCreateId();
                return _cachedId;
            }
        }
    }

    public static void EnsureLoaded()
    {
        _ = Current;
    }

    /// <summary>Example display form: "99416729butthead" (not persisted).</summary>
    public static string FormatDisplayId(string userName)
    {
        var name = userName ?? "";
        return $"{Current}{name}";
    }

    public static bool IsValidFormat(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (value.Length != 8) return false;
        if (!int.TryParse(value, out var n)) return false;
        return n >= MinId && n <= MaxId;
    }

    private static string LoadOrCreateId()
    {
        Directory.CreateDirectory(ChatIdFilePaths.RootDirectory);
        var path = ChatIdFilePaths.ChatIdFilePath;

        if (File.Exists(path))
        {
            try
            {
                var protectedBytes = File.ReadAllBytes(path);
                var plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                var id = Encoding.UTF8.GetString(plain).Trim();
                if (IsValidFormat(id))
                    return id;
                MultiplayerChat.Plugin.Log?.Warn("[MPChat] ChatID.dat invalid or tampered; generating a new ID.");
            }
            catch (Exception ex)
            {
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat] ChatID.dat unreadable ({ex.Message}); generating a new ID.");
            }
        }

        var newId = GenerateRandomId();
        PersistId(newId);
        return newId;
    }

    private static string GenerateRandomId()
    {
        var buf = new byte[4];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(buf);
        var u = BitConverter.ToUInt32(buf, 0);
        var range = (uint)(MaxId - MinId + 1);
        var n = MinId + (int)(u % range);
        return n.ToString();
    }

    private static void PersistId(string id)
    {
        var path = ChatIdFilePaths.ChatIdFilePath;
        var plain = Encoding.UTF8.GetBytes(id);
        var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, protectedBytes);
        if (File.Exists(path))
            File.Delete(path);
        File.Move(tmp, path);
    }
}
