using System;
using UnityEngine;

namespace MultiplayerChat.Core.Addons;

internal static class AddonCustomAvatarsBridge
{
    internal delegate bool LocalCaPoseSampler(
        out Vector3 headPosition,
        out Quaternion headRotation,
        out Vector3 rightHandPosition,
        out Quaternion rightHandRotation,
        out Vector3 leftHandPosition,
        out Quaternion leftHandRotation);

    private static LocalCaPoseSampler? _localCaPoseSampler;

    private static Action? _flushLobbyOnLeave;
    private static Action? _flushLobbyOnServerLeave;
    private static Action<string>? _clearRemote;
    private static Action<string, bool>? _notifyRemoteMayBeReady;
    private static Action? _pollDeferredAvatarUpdates;
    private static Action? _scheduleLobbySessionRejoinRefresh;
    private static Action? _onVoipPipelineReloaded;
    private static Action? _ensureActiveLobbyHostAfterArena;

    internal static void SetHandlers(
        Action? flushLobbyOnLeave,
        Action<string>? clearRemote,
        Action<string, bool>? notifyRemoteMayBeReady,
        Action? pollDeferredAvatarUpdates = null,
        Action? scheduleLobbySessionRejoinRefresh = null,
        Action? flushLobbyOnServerLeave = null,
        Action? onVoipPipelineReloaded = null,
        Action? ensureActiveLobbyHostAfterArena = null,
        LocalCaPoseSampler? localCaPoseSampler = null)
    {
        _localCaPoseSampler = localCaPoseSampler;
        _flushLobbyOnLeave = flushLobbyOnLeave;
        _flushLobbyOnServerLeave = flushLobbyOnServerLeave;
        _clearRemote = clearRemote;
        _notifyRemoteMayBeReady = notifyRemoteMayBeReady;
        _pollDeferredAvatarUpdates = pollDeferredAvatarUpdates;
        _scheduleLobbySessionRejoinRefresh = scheduleLobbySessionRejoinRefresh;
        _onVoipPipelineReloaded = onVoipPipelineReloaded;
        _ensureActiveLobbyHostAfterArena = ensureActiveLobbyHostAfterArena;
    }

    internal static void ClearHandlers()
    {
        _flushLobbyOnLeave = null;
        _flushLobbyOnServerLeave = null;
        _clearRemote = null;
        _notifyRemoteMayBeReady = null;
        _pollDeferredAvatarUpdates = null;
        _scheduleLobbySessionRejoinRefresh = null;
        _onVoipPipelineReloaded = null;
        _ensureActiveLobbyHostAfterArena = null;
        _localCaPoseSampler = null;
    }

    internal static bool TryGetLocalCaWorldDevicePoses(
        out Vector3 headPosition,
        out Quaternion headRotation,
        out Vector3 rightHandPosition,
        out Quaternion rightHandRotation,
        out Vector3 leftHandPosition,
        out Quaternion leftHandRotation)
    {
        headPosition = default;
        headRotation = Quaternion.identity;
        rightHandPosition = default;
        rightHandRotation = Quaternion.identity;
        leftHandPosition = default;
        leftHandRotation = Quaternion.identity;
        return _localCaPoseSampler != null &&
               _localCaPoseSampler(
                   out headPosition,
                   out headRotation,
                   out rightHandPosition,
                   out rightHandRotation,
                   out leftHandPosition,
                   out leftHandRotation);
    }

    internal static void FlushLobbyOnServerLeaveIfDisconnected() => _flushLobbyOnLeave?.Invoke();

    internal static void FlushLobbyCustomAvatarsOnServerLeave() => _flushLobbyOnServerLeave?.Invoke();

    internal static void ClearRemote(string userId)
    {
        if (!string.IsNullOrEmpty(userId))
            _clearRemote?.Invoke(userId);
    }

    internal static void NotifyRemoteAvatarMayBeReady(string userId, bool broadcastMetadata = false)
    {
        if (!string.IsNullOrEmpty(userId))
            _notifyRemoteMayBeReady?.Invoke(userId, broadcastMetadata);
    }

    internal static void PollDeferredAvatarUpdates() => _pollDeferredAvatarUpdates?.Invoke();

    internal static void ScheduleLobbySessionRejoinRefresh() => _scheduleLobbySessionRejoinRefresh?.Invoke();

    internal static void OnVoipPipelineReloaded() => _onVoipPipelineReloaded?.Invoke();

    internal static void EnsureActiveLobbyHostAfterArena() => _ensureActiveLobbyHostAfterArena?.Invoke();
}
