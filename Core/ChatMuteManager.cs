using System;

namespace MultiplayerChat.Core;

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

    public void OnPeerChatIdLearned(string platformUserId, string chatId)
    {
        _config.OnChatIdLearnedForUser(platformUserId, chatId);
    }
}
