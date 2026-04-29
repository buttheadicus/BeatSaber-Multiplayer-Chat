using System;
using System.Collections.Generic;

namespace MultiplayerChat.Core;

/// <summary>Lobby voice UI state (deafen, hot-mic mute, listen-only filter, talk-to group).</summary>
public static class VoiceChatRuntimeState
{
    private static readonly HashSet<string> ListenUserIdsInternal = new(StringComparer.Ordinal);
    private static readonly HashSet<string> TalkToUserIdsInternal = new(StringComparer.Ordinal);

    /// <summary>Ignore all incoming voice message + hot mic audio.</summary>
    public static bool IsDeaf { get; private set; }

    /// <summary>Stop sending hot mic (voice messages unaffected).</summary>
    public static bool IsHotMicMuted { get; private set; }

    private static bool? _hotMicMutedBeforeDeaf;

    /// <summary>When non-empty, only play hot mic / voice messages from these platform user ids. Empty = hear everyone (unless <see cref="TalkToUserIds"/> applies).</summary>
    public static IReadOnlyCollection<string> ListenUserIds => ListenUserIdsInternal;

    /// <summary>When non-empty, voice is sent only to these players (multi-DM) and only their voice is played. Empty = broadcast + hear everyone.</summary>
    public static IReadOnlyCollection<string> TalkToUserIds => TalkToUserIdsInternal;

    public static bool IsListenFilterActive => ListenUserIdsInternal.Count > 0;

    public static bool IsTalkToActive => TalkToUserIdsInternal.Count > 0;

    public static event Action? Changed;

    public static void NotifyChanged() => Changed?.Invoke();

    /// <summary>Deaf also forces hot-mic mute; undeaf restores the mic mute state from before deafening.</summary>
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

    /// <summary>Clears listen filter and talk-to (legacy / admin use).</summary>
    public static void ResetLobbyVoiceFilters()
    {
        ListenUserIdsInternal.Clear();
        TalkToUserIdsInternal.Clear();
        NotifyChanged();
    }

    /// <summary>Clears listen-only routing (e.g. leaving multiplayer or scene-scoped ChatManager dispose). Does not clear talk-to.</summary>
    public static void ClearListenFilterOnly()
    {
        if (ListenUserIdsInternal.Count == 0) return;
        ListenUserIdsInternal.Clear();
        NotifyChanged();
    }

    /// <summary>Removes one user from the listen filter when they leave the session.</summary>
    public static void RemoveListenUserId(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return;
        if (!ListenUserIdsInternal.Remove(userId!)) return;
        NotifyChanged();
    }

    /// <summary>Talk-to is meant to persist for the whole process; call on <see cref="UnityEngine.Application.quitting"/>.</summary>
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

    /// <summary>Incoming voice (hot mic + voice messages): apply talk-to then listen-only filters.</summary>
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
