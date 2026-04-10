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

    /// <summary>Recipient's persistent 8-digit Chat ID (from registry); required for 0.2.0 DM packets.</summary>
    public string? DMTargetChatId
    {
        get { lock (_lock) return _dmTargetChatId; }
    }

    public bool IsInDMMode => !string.IsNullOrEmpty(DMTargetUserId);

    private string? _dmTargetChatId;

    /// <summary>True until the first outbound DM message is sent after picking a target; resets each time a target is set.</summary>
    public bool PendingDmIntroForFirstMessage { get; private set; }

    /// <summary>Platform user id of a peer who sent us a DM intro; used to show mutual-DM line when we reply.</summary>
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
            // Only keep "they sent intro first" if we're DMing that same person.
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

    /// <summary>Called when we receive <see cref="Network.DmIntroNotifyPacket"/> addressed to us.</summary>
    public void SetReceivedDmIntroFrom(string? peerUserId)
    {
        lock (_lock)
        {
            _receivedDmIntroFromUserId = peerUserId;
        }
    }
}
