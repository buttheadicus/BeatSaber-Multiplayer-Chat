using System.Text;
using MultiplayerChat.Settings;

namespace MultiplayerChat.Core;

internal static class MpChatVerboseDebug
{
    internal static bool IsOn => ModSettings.DebugLogging;

    internal static void ChatIdBlock(string block)
    {
        if (!IsOn) return;
        MultiplayerChat.Plugin.Log?.Debug("[MPChat][DebugSpam][ChatId]\n" + block);
    }

    internal static void ChatIdHotPathLine(string line)
    {
        if (!IsOn) return;
        MultiplayerChat.Plugin.Log?.Debug("[MPChat][DebugSpam][ChatId][HotPath] " + line);
    }

    internal static void PresenceBlock(string block)
    {
        if (!IsOn) return;
        MultiplayerChat.Plugin.Log?.Debug("[MPChat][DebugSpam][Presence]\n" + block);
    }

    internal static void LearnedStoreBlock(string block)
    {
        if (!IsOn) return;
        MultiplayerChat.Plugin.Log?.Debug("[MPChat][DebugSpam][LearnedIDs]\n" + block);
    }

    internal static string CharCodes(string? s)
    {
        if (s == null) return "null";
        var sb = new StringBuilder(s.Length * 10);
        for (var i = 0; i < s.Length; i++)
            sb.Append('[').Append(i).Append("]=").Append(((ushort)s[i]).ToString("X4")).Append(' ');
        return sb.ToString();
    }

    internal static string TruncPlatformUserId(string? userId)
    {
        var u = userId ?? "";
        if (u.Length == 0) return "(empty)";
        if (u.Length <= 14) return u;
        return u.Substring(0, 14) + "... len=" + u.Length;
    }
}
