using System;
using System.IO;
using System.Security.Cryptography;

namespace MultiplayerChat.Core;

internal static class CustomAvatarHashUtil
{
    internal static string Md5HexFile(string fullPath)
    {
        using var fs = File.OpenRead(fullPath);
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(fs);
        return BitConverter.ToString(hash).Replace("-", "");
    }

    internal static bool LooksLikeMd5Hex(string? s)
    {
        if (string.IsNullOrEmpty(s) || s.Length != 32)
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
}
