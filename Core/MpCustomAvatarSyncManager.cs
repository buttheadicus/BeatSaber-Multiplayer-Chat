using System;
using System.Collections;
using System.Collections.Generic;
using MultiplayerChat.Network;
using MultiplayerChat.Settings;
using MultiplayerCore.Models;
using MultiplayerCore.Networking;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core;

public sealed class MpCustomAvatarSyncManager : MonoBehaviour, IInitializable
{
    public static MpCustomAvatarSyncManager? Instance { get; private set; }

    public static event Action<string>? RemoteLobbyAvatarUpdated;

    private static readonly object RemoteLock = new();

    private static readonly Dictionary<string, MpCustomAvatarRemoteState> RemoteByUserId =
        new(StringComparer.Ordinal);

    private const float BroadcastIntervalSeconds = 0.2f;

    private const float DescriptorKeepaliveSeconds = 4f;

    [Inject] private readonly IMultiplayerSessionManager _sessionManager = null!;

    private Coroutine? _broadcastRoutine;

    private string? _lastSentDescriptor;

    private byte[]? _lastSentFbtBlob;

    private float _lastSendRealtime;

    public void Initialize()
    {
        if (MpChatSceneScope.IsGameCoreHost(this))
        {
            Instance = this;
            MultiplayerChat.Plugin.Log?.Debug("[MPChat][LobbyAvatar] Sync manager active (GameCore host)");
        }
        else if (Instance == null || !MpChatSceneScope.IsGameCoreHost(Instance))
        {
            Instance = this;
            MultiplayerChat.Plugin.Log?.Debug("[MPChat][LobbyAvatar] Sync manager active (lobby host)");
        }

        ClearBroadcastDedupeState();
        StartBroadcastLoop();
    }

    public static bool TryGetRemoteState(string userId, out MpCustomAvatarRemoteState state)
    {
        state = null!;
        if (string.IsNullOrEmpty(userId))
            return false;
        lock (RemoteLock)
            return RemoteByUserId.TryGetValue(userId, out state!);
    }

    public static void ApplyReceived(string userId, MpCustomAvatarPosePacket packet)
    {
        if (!MpChatFeatures.LobbyCustomAvatars)
            return;
        if (string.IsNullOrEmpty(userId))
            return;

        MpCustomAvatarFbtPose? fbt = null;
        if (packet.FbtBlob != null && MpCustomAvatarFbtBlob.TryDecode(packet.FbtBlob, out var decoded))
            fbt = decoded;

        lock (RemoteLock)
        {
            if (!RemoteByUserId.TryGetValue(userId, out var row))
            {
                row = new MpCustomAvatarRemoteState();
                RemoteByUserId[userId] = row;
            }

            row.AvatarDescriptorId = packet.AvatarDescriptorId;
            row.LastFbtPose = fbt;
            row.ReceivedAtRealtime = Time.realtimeSinceStartup;
        }

        RemoteLobbyAvatarUpdated?.Invoke(userId);
    }

    public static void ClearRemote(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return;
        lock (RemoteLock)
            RemoteByUserId.Remove(userId);
    }

    public static void InvalidateOutboundDedupe()
    {
        if (Instance != null)
            Instance.ClearBroadcastDedupeState();
    }

    private void StartBroadcastLoop()
    {
        if (_broadcastRoutine != null)
            StopCoroutine(_broadcastRoutine);
        _broadcastRoutine = StartCoroutine(BroadcastLoop());
    }

    private IEnumerator BroadcastLoop()
    {
        var wait = new WaitForSeconds(BroadcastIntervalSeconds);
        while (true)
        {
            yield return wait;
            TryBroadcast();
        }
    }

    private void ClearBroadcastDedupeState()
    {
        _lastSentDescriptor = null;
        _lastSentFbtBlob = null;
        _lastSendRealtime = 0f;
    }

    private void TryBroadcast()
    {
        if (!MpChatFeatures.LobbyCustomAvatars)
            return;
        if (!ModSettings.EnableLobbyCustomAvatars || string.IsNullOrEmpty(ModSettings.LobbyCustomAvatarContentHash))
            return;

        if (!ReferenceEquals(Instance, this))
            return;

        var local = _sessionManager.localPlayer;
        if (local == null || string.IsNullOrEmpty(local.userId))
            return;

        var descriptor = ModSettings.LobbyCustomAvatarContentHash.Trim().ToUpperInvariant();
        if (descriptor.Length > MpCustomAvatarPosePacket.MaxDescriptorChars)
            descriptor = descriptor.Substring(0, MpCustomAvatarPosePacket.MaxDescriptorChars);

        byte[]? fbtBlob = null;
        if (MpCustomAvatarLocalPoseSource.TryGetPelvisPose(out var pose))
            fbtBlob = MpCustomAvatarFbtBlob.EncodeV1(in pose);

        var now = Time.realtimeSinceStartup;
        var needsKeepalive =
            !string.IsNullOrEmpty(descriptor) &&
            now - _lastSendRealtime >= DescriptorKeepaliveSeconds;

        if (!needsKeepalive &&
            string.Equals(descriptor, _lastSentDescriptor, StringComparison.Ordinal) &&
            BlobSeqEqual(fbtBlob, _lastSentFbtBlob))
            return;

        byte flags = 0;
        if (!string.IsNullOrEmpty(descriptor))
            flags |= MpCustomAvatarPosePacket.FlagHasDescriptor;
        if (fbtBlob != null && fbtBlob.Length > 0)
            flags |= MpCustomAvatarPosePacket.FlagHasFbtBlob;

        var pkt = new MpCustomAvatarPosePacket
        {
            Flags = flags,
            AvatarDescriptorId = string.IsNullOrEmpty(descriptor) ? null : descriptor,
            FbtBlob = fbtBlob
        };

        try
        {
            _sessionManager.Send(pkt);
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat][LobbyAvatar] Send failed: {ex.Message}");
            return;
        }

        _lastSentDescriptor = descriptor;
        _lastSentFbtBlob = fbtBlob;
        _lastSendRealtime = now;
    }

    private static bool BlobSeqEqual(byte[]? a, byte[]? b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null || a.Length != b.Length)
            return false;
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }
}

public sealed class MpCustomAvatarRemoteState
{
    public string? AvatarDescriptorId;

    public MpCustomAvatarFbtPose? LastFbtPose;

    public float ReceivedAtRealtime;
}
