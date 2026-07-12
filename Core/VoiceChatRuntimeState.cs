using System;
using System.Collections.Generic;

namespace MultiplayerChat.Core;

public static class VoiceChatRuntimeState
{
    private static readonly HashSet<string> ListenUserIdsInternal = new(StringComparer.Ordinal);
    private static readonly HashSet<string> TalkToUserIdsInternal = new(StringComparer.Ordinal);

    public static bool IsDeaf { get; private set; }

    public static bool IsHotMicMuted { get; private set; }

    private static bool? _hotMicMutedBeforeDeaf;

    public static IReadOnlyCollection<string> ListenUserIds => ListenUserIdsInternal;

    public static IReadOnlyCollection<string> TalkToUserIds => TalkToUserIdsInternal;

    public static bool IsListenFilterActive => ListenUserIdsInternal.Count > 0;

    public static bool IsTalkToActive => TalkToUserIdsInternal.Count > 0;

    public static event Action? Changed;

    public static void NotifyChanged() => Changed?.Invoke();

    public static (bool Deafened, bool HotMicMutedWhenUndeafened) CapturePersistenceSnapshot()
    {
        if (IsDeaf)
            return (true, _hotMicMutedBeforeDeaf ?? IsHotMicMuted);
        return (false, IsHotMicMuted);
    }

    // reapply saved lobby self-mute/deaf state (game launch or settings reload).
    public static void RestoreFromPersistence(bool deafened, bool hotMicMutedWhenUndeafened)
    {
        _hotMicMutedBeforeDeaf = null;
        IsDeaf = false;
        IsHotMicMuted = false;

        if (deafened)
        {
            _hotMicMutedBeforeDeaf = hotMicMutedWhenUndeafened;
            IsHotMicMuted = true;
            IsDeaf = true;
            NotifyChanged();
            return;
        }

        if (hotMicMutedWhenUndeafened)
            SetHotMicMuted(true);
        else
            NotifyChanged();
    }

    public static void SetDeaf(bool value)
    {
        if (IsDeaf == value) return;
        if (value)
        {
            _hotMicMutedBeforeDeaf = IsHotMicMuted;
            if (!IsHotMicMuted)
                SetHotMicMuted(true);
        }
        else
        {
            if (_hotMicMutedBeforeDeaf.HasValue)
            {
                var restore = _hotMicMutedBeforeDeaf.Value;
                _hotMicMutedBeforeDeaf = null;
                SetHotMicMuted(restore);
            }
        }

        IsDeaf = value;
        NotifyChanged();
    }

    public static void SetHotMicMuted(bool value)
    {
        if (IsHotMicMuted == value) return;
        IsHotMicMuted = value;
        NotifyChanged();
    }

    public static HashSet<string> CopyListenUserIds() => new(ListenUserIdsInternal, StringComparer.Ordinal);

    public static HashSet<string> CopyTalkToUserIds() => new(TalkToUserIdsInternal, StringComparer.Ordinal);

    public static void ToggleListen(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return;
        if (!ListenUserIdsInternal.Add(userId))
            ListenUserIdsInternal.Remove(userId);
        NotifyChanged();
    }

    public static void ToggleTalkTo(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return;
        if (!TalkToUserIdsInternal.Add(userId))
            TalkToUserIdsInternal.Remove(userId);
        NotifyChanged();
    }

    public static void ResetLobbyVoiceFilters()
    {
        ListenUserIdsInternal.Clear();
        TalkToUserIdsInternal.Clear();
        NotifyChanged();
    }

    public static void ClearListenFilterOnly()
    {
        if (ListenUserIdsInternal.Count == 0) return;
        ListenUserIdsInternal.Clear();
        NotifyChanged();
    }

    public static void RemoveListenUserId(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return;
        if (!ListenUserIdsInternal.Remove(userId!)) return;
        NotifyChanged();
    }

    public static void ClearTalkToOnGameQuit()
    {
        if (TalkToUserIdsInternal.Count == 0) return;
        TalkToUserIdsInternal.Clear();
        NotifyChanged();
    }

    public static bool IsListeningTo(string userId) =>
        !string.IsNullOrEmpty(userId) && ListenUserIdsInternal.Contains(userId);

    public static bool IsTalkingTo(string userId) =>
        !string.IsNullOrEmpty(userId) && TalkToUserIdsInternal.Contains(userId);

    public static bool ShouldPlayIncomingVoiceFrom(string senderUserId)
    {
        if (string.IsNullOrEmpty(senderUserId)) return false;
        if (TalkToUserIdsInternal.Count > 0)
            return TalkToUserIdsInternal.Contains(senderUserId);
        if (ListenUserIdsInternal.Count > 0)
            return ListenUserIdsInternal.Contains(senderUserId);
        return true;
    }
}
