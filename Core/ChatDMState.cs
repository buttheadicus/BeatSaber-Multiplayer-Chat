using System;

namespace MultiplayerChat.Core;

/// <summary>
/// Tracks the current DM target. When set, messages are sent only to that player (display-wise).
/// </summary>
public class ChatDMState
{
    private string? _dmTargetUserId;
    private string? _dmTargetUserName;
    private readonly object _lock = new();

    public string? DMTargetUserId
    {
        get { lock (_lock) return _dmTargetUserId; }
    }

    public string? DMTargetUserName
    {
        get { lock (_lock) return _dmTargetUserName; }
    }

    public bool IsInDMMode => !string.IsNullOrEmpty(DMTargetUserId);

    public void SetDMTarget(string? userId, string? userName)
    {
        lock (_lock)
        {
            _dmTargetUserId = userId;
            _dmTargetUserName = userName;
        }
    }

    public void ClearDMTarget()
    {
        lock (_lock)
        {
            _dmTargetUserId = null;
            _dmTargetUserName = null;
        }
    }
}
