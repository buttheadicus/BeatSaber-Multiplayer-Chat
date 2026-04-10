using System;

namespace MultiplayerChat.Core;

/// <summary>
/// Mutes by persistent chat ID (saved in ChatIDConfig.dat). If a player's ID is not known yet,
/// mute is stored by platform userId until presence provides their chat ID.
/// </summary>
public class ChatMuteManager
{
    private readonly ChatIdConfigStore _config = null!;
    private readonly ChatPlayerIdRegistry _registry = null!;

    public ChatMuteManager(ChatIdConfigStore config, ChatPlayerIdRegistry registry)
    {
        _config = config;
        _registry = registry;
    }

    public bool IsMuted(string platformUserId)
    {
        if (string.IsNullOrEmpty(platformUserId)) return false;
        if (_registry.TryGetChatId(platformUserId, out var chatId) && _config.IsMutedChatId(chatId))
            return true;
        return _config.IsMutedPlatformUserId(platformUserId);
    }

    public void ToggleMute(string platformUserId)
    {
        if (string.IsNullOrEmpty(platformUserId)) return;
        if (_registry.TryGetChatId(platformUserId, out var chatId))
            _config.ToggleMutedChatId(chatId);
        else
            _config.ToggleMutedPlatformUserId(platformUserId);
    }

    public bool SetMuted(string platformUserId, bool muted)
    {
        if (string.IsNullOrEmpty(platformUserId)) return false;
        var currently = IsMuted(platformUserId);
        if (currently == muted) return false;
        ToggleMute(platformUserId);
        return true;
    }

    public bool HasAnyMuted() => _config.HasAnyMutedEntry();

    public void ClearAllMutes() => _config.ClearAllMutes();

    /// <summary>Called when we learn a remote player's chat ID (e.g. from presence).</summary>
    public void OnPeerChatIdLearned(string platformUserId, string chatId)
    {
        _config.OnChatIdLearnedForUser(platformUserId, chatId);
    }
}
