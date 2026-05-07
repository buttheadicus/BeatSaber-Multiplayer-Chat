using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MultiplayerChat.Settings;

namespace MultiplayerChat.Core;

// Local player's Chat ID (8-digit or 8-digit + IdGeneratedOfficial). Stored under ChatIdFilePaths.ChatIdFilePath with DPAPI.
public static class ChatPersistentId
{
    public const string IdGeneratedOfficialSuffix = "IdGeneratedOfficial";

    private const int MinId = 10_000_000;
    private const int MaxId = 99_999_999;

    private static readonly object LockObj = new();
    private static string? _cachedId;

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

    public static string FormatDisplayId(string userName)
    {
        var name = userName ?? "";
        return $"{Current}{name}";
    }

    public static bool IsOfficialTaggedChatId(string? value) =>
        IsValidFormat(value) &&
        value!.EndsWith(IdGeneratedOfficialSuffix, StringComparison.Ordinal) &&
        value.Length == 8 + IdGeneratedOfficialSuffix.Length;

    public static bool IsValidFormat(string? value)
    {
        if (value is null || value.Length == 0) return false;

        if (value.Length == 8)
        {
            if (!int.TryParse(value, out var n)) return false;
            return n >= MinId && n <= MaxId;
        }

        if (value.Length == 8 + IdGeneratedOfficialSuffix.Length &&
            value.EndsWith(IdGeneratedOfficialSuffix, StringComparison.Ordinal))
        {
            var head = value.Substring(0, 8);
            if (!int.TryParse(head, out var m)) return false;
            return m >= MinId && m <= MaxId;
        }

        return false;
    }

    public static bool ChatIdsSameEightDigitHead(string? a, string? b)
    {
        if (!IsValidFormat(a) || !IsValidFormat(b)) return false;
        return string.CompareOrdinal(a!, 0, b!, 0, 8) == 0;
    }

    public static bool IsOfficialLegacyEightDigitPair(string? a, string? b)
    {
        if (!ChatIdsSameEightDigitHead(a, b)) return false;
        return IsOfficialTaggedChatId(a) != IsOfficialTaggedChatId(b);
    }

    public static string PreferOfficialTaggedForm(string a, string b)
    {
        if (IsOfficialTaggedChatId(a)) return a;
        if (IsOfficialTaggedChatId(b)) return b;
        return b;
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
                MultiplayerChat.Plugin.Log?.Warn("[MPChat] ChatID.dat invalid or tampered; creating a new ID.");
            }
            catch (Exception ex)
            {
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat] ChatID.dat unreadable ({ex.Message}); creating a new ID.");
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
