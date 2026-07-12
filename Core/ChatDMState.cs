using System;

namespace MultiplayerChat.Core;

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

    public string? DMTargetChatId
    {
        get { lock (_lock) return _dmTargetChatId; }
    }

    public bool IsInDMMode => !string.IsNullOrEmpty(DMTargetUserId);

    private string? _dmTargetChatId;

    public bool PendingDmIntroForFirstMessage { get; private set; }

    public string? ReceivedDmIntroFromUserId
    {
        get { lock (_lock) return _receivedDmIntroFromUserId; }
    }

    private string? _receivedDmIntroFromUserId;

    public event EventHandler? DMTargetChanged;

    public void SetDMTarget(string? userId, string? userName, string? targetChatId)
    {
        lock (_lock)
        {
            _dmTargetUserId = userId;
            _dmTargetUserName = userName;
            _dmTargetChatId = targetChatId;
            // only keep "they sent intro first" if we're DMing that same person.
            if (string.IsNullOrEmpty(userId) || userId != _receivedDmIntroFromUserId)
                _receivedDmIntroFromUserId = null;
        }

        PendingDmIntroForFirstMessage = !string.IsNullOrEmpty(userId);
        DMTargetChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearDMTarget()
    {
        lock (_lock)
        {
            _dmTargetUserId = null;
            _dmTargetUserName = null;
            _dmTargetChatId = null;
            _receivedDmIntroFromUserId = null;
        }

        PendingDmIntroForFirstMessage = false;
        DMTargetChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MarkDmIntroSent() => PendingDmIntroForFirstMessage = false;

    public void SetReceivedDmIntroFrom(string? peerUserId)
    {
        lock (_lock)
        {
            _receivedDmIntroFromUserId = peerUserId;
        }
    }
}
