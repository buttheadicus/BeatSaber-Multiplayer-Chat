using System;
using System.Collections.Generic;
using UnityEngine;

namespace MultiplayerChat.Core;

internal enum NametagMicIconState
{
    Unmuted,
    Muted,
    Talking,
    PlayerMuted
}

internal enum NametagHeadphoneIconState
{
    Undeafened,
    Deafened,
    CannotHearYou
}

// per-peer voice icon state for nametag overlays. Remote fields update from network packets; talking uses a short holdover.
internal static class NametagVoiceStatusRegistry
{
    private const float TalkingHoldoverSec = 0.25f;

    private sealed class PeerVoiceState
    {
        public bool HotMicMuted;
        public bool Deafened;
        public bool MutedLocalViewer;
        public float LastTalkingRealtime = -999f;
    }

    private static readonly Dictionary<string, PeerVoiceState> Peers = new(StringComparer.Ordinal);

    public static void ClearUser(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return;
        Peers.Remove(userId!);
    }

    public static void SetRemoteHotMicMuted(string? userId, bool muted)
    {
        if (string.IsNullOrEmpty(userId))
            return;
        var state = GetOrCreate(userId!);
        if (state.HotMicMuted == muted)
            return;
        state.HotMicMuted = muted;
    }

    public static void SetRemoteDeafened(string? userId, bool deafened)
    {
        if (string.IsNullOrEmpty(userId))
            return;
        var state = GetOrCreate(userId!);
        if (state.Deafened == deafened)
            return;
        state.Deafened = deafened;
    }

    public static void SetPeerMutedLocalViewer(string? peerUserId, bool muted)
    {
        if (string.IsNullOrEmpty(peerUserId))
            return;
        var state = GetOrCreate(peerUserId!);
        if (state.MutedLocalViewer == muted)
            return;
        state.MutedLocalViewer = muted;
    }

    public static void NotifyTalking(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return;
        GetOrCreate(userId!).LastTalkingRealtime = Time.realtimeSinceStartup;
    }

    public static void ResolveIconStates(
        string? userId,
        string? localUserId,
        bool mutedByLocalViewer,
        out NametagMicIconState mic,
        out NametagHeadphoneIconState headphone)
    {
        mic = NametagMicIconState.Unmuted;
        headphone = NametagHeadphoneIconState.Undeafened;
        if (string.IsNullOrEmpty(userId))
            return;

        var peerUserId = userId!;
        var isLocal = !string.IsNullOrEmpty(localUserId) && peerUserId == localUserId;
        Peers.TryGetValue(peerUserId, out var remote);

        var hotMicMuted = isLocal ? VoiceChatRuntimeState.IsHotMicMuted : remote?.HotMicMuted == true;
        var deafened = isLocal ? VoiceChatRuntimeState.IsDeaf : remote?.Deafened == true;
        var peerMutedLocalViewer = remote?.MutedLocalViewer == true;
        var talking = IsTalkingRecently(remote);
        var micShowsMuted = hotMicMuted || deafened;

        if (mutedByLocalViewer)
            mic = NametagMicIconState.PlayerMuted;
        else if (talking && !micShowsMuted)
            mic = NametagMicIconState.Talking;
        else if (micShowsMuted)
            mic = NametagMicIconState.Muted;

        if (peerMutedLocalViewer)
            headphone = NametagHeadphoneIconState.CannotHearYou;
        else if (deafened)
            headphone = NametagHeadphoneIconState.Deafened;
    }

    private static bool IsTalkingRecently(PeerVoiceState? remote)
    {
        if (remote == null)
            return false;
        return Time.realtimeSinceStartup - remote.LastTalkingRealtime < TalkingHoldoverSec;
    }

    private static PeerVoiceState GetOrCreate(string userId)
    {
        if (!Peers.TryGetValue(userId, out var state))
        {
            state = new PeerVoiceState();
            Peers[userId] = state;
        }

        return state;
    }
}
