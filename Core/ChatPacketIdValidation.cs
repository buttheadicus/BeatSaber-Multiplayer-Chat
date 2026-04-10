using MultiplayerCore.Models;

namespace MultiplayerChat.Core;

/// <summary>
/// 0.2.0 wire rules: every MPChat packet must carry a valid sender Chat ID; DM packets also carry the recipient's Chat ID.
/// </summary>
internal static class ChatPacketIdValidation
{
    public static bool TryAcceptSenderChatId(string? senderChatId, IConnectedPlayer sender, ChatPlayerIdRegistry registry)
    {
        if (sender == null || string.IsNullOrEmpty(sender.userId))
            return false;
        if (!ChatPersistentId.IsValidFormat(senderChatId))
            return false;
        if (registry.TryGetChatId(sender.userId, out var known))
            return known == senderChatId;
        registry.SetMapping(sender.userId, senderChatId!);
        return true;
    }

    /// <summary>
    /// Global: both targets empty. DM: both non-empty. Any other combination is invalid for 0.2.0.</summary>
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

    /// <summary>
    /// After sender Chat ID validation: global everyone processes; DM only sender and recipient (matching platform id and Chat ID).</summary>
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
        return my == targetChatId;
    }
}
