using System;
using System.Collections.Generic;

namespace MultiplayerChat.Core;

/// <summary>
/// Tracks which players are muted. Muted players' messages are not shown locally.
/// </summary>
public class ChatMuteManager
{
    private readonly HashSet<string> _mutedUserIds = new();
    private readonly object _lock = new();

    public bool IsMuted(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return false;
        lock (_lock) return _mutedUserIds.Contains(userId);
    }

    public void ToggleMute(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return;
        lock (_lock)
        {
            if (_mutedUserIds.Contains(userId))
                _mutedUserIds.Remove(userId);
            else
                _mutedUserIds.Add(userId);
        }
    }

    public bool SetMuted(string userId, bool muted)
    {
        if (string.IsNullOrEmpty(userId)) return false;
        lock (_lock)
        {
            if (muted)
                return _mutedUserIds.Add(userId);
            return _mutedUserIds.Remove(userId);
        }
    }
}
