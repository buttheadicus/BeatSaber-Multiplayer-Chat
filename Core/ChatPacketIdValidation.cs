using System;
using System.Text;
using MultiplayerChat.Settings;
using MultiplayerCore.Models;

namespace MultiplayerChat.Core;

internal static class ChatPacketIdValidation
{
    // Official-tagged SenderChatId always passes when format-valid and overwrites prior mapping.
    // If a peer sends a different valid SenderChatId than we learned, we adopt it (Chat ID is settings-like, not an auth boundary).
    // Invalid/missing SenderChatId with no learned mapping still accepts the packet (trust session platform user id).
    public static bool TryAcceptSenderChatId(string? senderChatId, IConnectedPlayer sender, ChatPlayerIdRegistry registry,
        bool voiceHotPath = false)
    {
        if (sender == null || string.IsNullOrEmpty(sender.userId))
        {
            if (MpChatVerboseDebug.IsOn)
                MpChatVerboseDebug.ChatIdBlock(
                    "TryAccept REJECT: sender or sender.userId missing.\n" +
                    (voiceHotPath ? "" : "Stack:\n" + Environment.StackTrace));
            return false;
        }

        var uid = sender.userId;
        var validSender = ChatPersistentId.IsValidFormat(senderChatId);
        var official = ChatPersistentId.IsOfficialTaggedChatId(senderChatId);

        if (MpChatVerboseDebug.IsOn)
        {
            registry.TryGetChatId(uid, out var dbgKnown);
            var sb = new StringBuilder(512);
            sb.Append("TryAccept ENTRY voiceHotPath=").Append(voiceHotPath).Append('\n');
            sb.Append("platformUserId=").Append(MpChatVerboseDebug.TruncPlatformUserId(uid)).Append('\n');
            sb.Append("SenderChatId len=").Append(senderChatId?.Length ?? -1).Append(" validFormat=").Append(validSender)
                .Append(" officialTagged=").Append(official).Append('\n');
            sb.Append("SenderChatId literal=").Append(senderChatId ?? "(null)").Append('\n');
            sb.Append("SenderChatId charCodes=").Append(MpChatVerboseDebug.CharCodes(senderChatId)).Append('\n');
            sb.Append("registry.TryGetChatId -> ").Append(string.IsNullOrEmpty(dbgKnown) ? "(none)" : dbgKnown).Append('\n');
            sb.Append("registry known charCodes=").Append(MpChatVerboseDebug.CharCodes(dbgKnown)).Append('\n');
            if (!voiceHotPath)
                sb.Append("Stack:\n").Append(Environment.StackTrace);
            MpChatVerboseDebug.ChatIdBlock(sb.ToString());
        }

        if (!validSender)
        {
            if (registry.TryGetChatId(sender.userId, out var learned) &&
                ChatPersistentId.IsValidFormat(learned))
            {
                if (ModSettings.DebugLogging)
                {
                    MultiplayerChat.Plugin.Log?.Debug(
                        "[MPChat][ChatId] SenderChatId missing or invalid on packet; using learned Chat ID for " +
                        sender.userId + " (backward compatible path).");
                }

                if (MpChatVerboseDebug.IsOn)
                    MpChatVerboseDebug.ChatIdHotPathLine(
                        "ACCEPT via backward-compat learned path (packet SenderChatId invalid).");

                return true;
            }

            if (ModSettings.DebugLogging)
            {
                MultiplayerChat.Plugin.Log?.Debug(
                    "[MPChat][ChatId] SenderChatId missing or invalid and no learned Chat ID yet for " + sender.userId +
                    "; accepting packet (no registry update).");
            }

            if (MpChatVerboseDebug.IsOn)
                MpChatVerboseDebug.ChatIdBlock(
                    "TryAccept ACCEPT: SenderChatId invalid/missing and no learned mapping (permissive path).\n" +
                    (voiceHotPath ? "" : "Stack:\n" + Environment.StackTrace));

            return true;
        }

        if (official)
        {
            registry.SetMapping(sender.userId, senderChatId!);
            if (MpChatVerboseDebug.IsOn)
                MpChatVerboseDebug.ChatIdBlock(
                    "TryAccept ACCEPT + OVERWRITE: official-tagged SenderChatId always wins for this platform user.\n" +
                    "Final mapped ChatId=" + senderChatId + '\n' +
                    (voiceHotPath ? "" : "Stack:\n" + Environment.StackTrace));
            return true;
        }

        if (registry.TryGetChatId(sender.userId, out var known))
        {
            if (known == senderChatId)
                return true;

            if (ChatPersistentId.IsOfficialLegacyEightDigitPair(known, senderChatId))
            {
                var canon = ChatPersistentId.PreferOfficialTaggedForm(known, senderChatId!);
                registry.SetMapping(sender.userId, canon);
                if (MpChatVerboseDebug.IsOn)
                    MpChatVerboseDebug.ChatIdBlock(
                        "TryAccept ACCEPT: same 8-digit Chat ID, legacy vs official; canonical=" + canon + '\n' +
                        (voiceHotPath ? "" : "Stack:\n" + Environment.StackTrace));
                return true;
            }

            registry.SetMapping(sender.userId, senderChatId!);
            if (ModSettings.DebugLogging)
            {
                MultiplayerChat.Plugin.Log?.Debug(
                    "[MPChat][ChatId] Peer Chat ID changed for " + sender.userId + "; mapping updated to " + senderChatId + ".");
            }

            if (MpChatVerboseDebug.IsOn)
                MpChatVerboseDebug.ChatIdBlock(
                    "TryAccept UPDATE: SenderChatId differs from prior mapping; adopting incoming Chat ID.\n" +
                    "priorKnown=" + known + "\nincoming=" + senderChatId + '\n' +
                    (voiceHotPath ? "" : "Stack:\n" + Environment.StackTrace));
            return true;
        }

        registry.SetMapping(sender.userId, senderChatId!);
        if (MpChatVerboseDebug.IsOn)
            MpChatVerboseDebug.ChatIdBlock(
                "TryAccept ACCEPT: first learn for platform user.\n" +
                (voiceHotPath ? "" : "Stack:\n" + Environment.StackTrace));
        return true;
    }

    public static bool TryParseDmRouting(string? targetUserId, string? targetChatId, out bool isDm)
    {
        var hasUser = !string.IsNullOrEmpty(targetUserId);
        var hasChat = !string.IsNullOrEmpty(targetChatId);
        if (hasUser && hasChat)
        {
            isDm = true;
            return true;
        }

        if (!hasUser && !hasChat)
        {
            isDm = false;
            return true;
        }

        isDm = false;
        return false;
    }

    public static bool IsLocalParticipant(string? targetUserId, string? targetChatId, bool isDm, string? localUserId, string senderUserId)
    {
        if (!isDm)
            return true;
        if (string.IsNullOrEmpty(localUserId))
            return false;
        if (localUserId == senderUserId)
            return true;
        if (localUserId != targetUserId)
            return false;
        var my = ChatPersistentId.Current;
        if (!ChatPersistentId.IsValidFormat(my))
            return false;
        if (!ChatPersistentId.IsValidFormat(targetChatId))
            return false;
        return my == targetChatId;
    }
}
