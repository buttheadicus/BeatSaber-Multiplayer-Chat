using System;
using System.IO;
using System.Security.Cryptography;

namespace MultiplayerChat.Core;

// shared MD5 helpers for lobby custom avatar settings validation (core-owned).
internal static class CustomAvatarHashUtil
{
    internal static bool LooksLikeMd5Hex(string? s)
    {
        if (s is null || s.Length != 32)
            return false;
        foreach (var c in s)
        {
            if (char.IsDigit(c))
                continue;
            if (c is >= 'A' and <= 'F')
                continue;
            if (c is >= 'a' and <= 'f')
                continue;
            return false;
        }

        return true;
    }

    internal static string Md5HexFile(string fullPath)
    {
        using var fs = File.OpenRead(fullPath);
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(fs);
        return BitConverter.ToString(hash).Replace("-", "");
    }

    internal static string Md5HexBytes(byte[] bytes)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "");
    }
}
