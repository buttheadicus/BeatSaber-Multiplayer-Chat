using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MultiplayerChat.Network;
using MultiplayerCore.Models;
using MultiplayerCore.Networking;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core;

/// <summary>
/// Tracks which players have the E2E Chat mod. Broadcasts our presence when connecting;
/// shows the chat icon on nametags for players in this set.
/// </summary>
public class ModPresenceManager : IInitializable, IDisposable
{
    public static ModPresenceManager? Instance { get; private set; }

    private readonly HashSet<string> _playersWithMod = new();
    private readonly object _lock = new();
    private Coroutine? _presenceRetryCoroutine;
    private bool _hasReceivedPresenceReply;

    [Inject] private readonly IMultiplayerSessionManager _sessionManager = null!;
    [Inject] private readonly MpPacketSerializer _packetSerializer = null!;
    [Inject] private readonly CoroutineHost _coroutineHost = null!;

    public void Initialize()
    {
        Instance = this;
        _packetSerializer.RegisterCallback<ModPresencePacket>(OnModPresenceReceived);
        _sessionManager.playerConnectedEvent += OnPlayerConnected;
        _sessionManager.playerDisconnectedEvent += OnPlayerDisconnected;

        // Add local player (we have the mod)
        var local = _sessionManager.localPlayer;
        if (local != null && !string.IsNullOrEmpty(local.userId))
        {
            lock (_lock) _playersWithMod.Add(local.userId);
        }

        // Presence sends immediately. Reply waits 6 seconds. Ignored from song -> retry in 3 seconds.
        MultiplayerChat.Plugin.Log?.Info("[E2EChat] ModPresenceManager initialized");
        BroadcastPresence();
        _coroutineHost.StartCoroutine(RepeatBroadcast());
    }

    /// <summary>Keep trying to send presence (even during song - others will reply "ignored").</summary>
    private IEnumerator RepeatBroadcast()
    {
        for (var i = 0; i < 20; i++) // Try for ~40 seconds
        {
            yield return new WaitForSeconds(2f);
            BroadcastPresence();
        }
    }

    public void Dispose()
    {
        Instance = null;
        _hasReceivedPresenceReply = false;
        CancelPresenceRetry();
        _packetSerializer.UnregisterCallback<ModPresencePacket>();
        _sessionManager.playerConnectedEvent -= OnPlayerConnected;
        _sessionManager.playerDisconnectedEvent -= OnPlayerDisconnected;
        lock (_lock) _playersWithMod.Clear();
    }

    public bool HasMod(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return false;
        var local = _sessionManager.localPlayer;
        if (local != null && local.userId == userId)
            return true;
        lock (_lock) return _playersWithMod.Contains(userId);
    }

    public event EventHandler? PresenceUpdated;

    /// <summary>Fired when we learn a remote player has the mod (userId, userName).</summary>
    public event EventHandler<PlayerWithModEventArgs>? PlayerWithModAdded;

    private void OnPlayerConnected(IConnectedPlayer player)
    {
        var local = _sessionManager.localPlayer;
        if (local != null && !string.IsNullOrEmpty(local.userId))
        {
            lock (_lock) _playersWithMod.Add(local.userId);
        }
        BroadcastPresence();
        PresenceUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void OnPlayerDisconnected(IConnectedPlayer player)
    {
        lock (_lock) _playersWithMod.Remove(player.userId);
        PresenceUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void OnModPresenceReceived(ModPresencePacket packet, IConnectedPlayer sender)
    {
        if (string.IsNullOrEmpty(sender.userId)) return;

        var local = _sessionManager.localPlayer;
        if (local == null || string.IsNullOrEmpty(local.userId)) return;

        // Targeted packet: only the intended recipient should process it
        if (packet.TargetUserId != null)
        {
            if (packet.TargetUserId != local.userId)
                return; // Not for us - ignore
            // We are the target (e.g. Lyra)
            if (packet.IsIgnoredFromSong)
            {
                // They're in a song - retry only if we haven't gotten a proper reply yet (ignore stale "ignored" packets)
                if (!_hasReceivedPresenceReply)
                    SchedulePresenceRetry();
                return;
            }
            // Proper reply - they have the mod. Lyra waits 6 seconds before showing "X has chat".
            _hasReceivedPresenceReply = true;
            _coroutineHost.StartCoroutine(ShowPlayerWithModAfter6Seconds(sender.userId, sender.userName ?? sender.userId));
            CancelPresenceRetry();
            return;
        }

        // Broadcast presence from joining client (e.g. Lyra)
        if (!IsInLobby())
        {
            // We're in a song - send "ignored", don't process
            SendPresenceIgnoredTo(sender.userId);
            return;
        }

        // We're in lobby - process immediately (everyone sees "Lyra has chat" right away), reply immediately
        // Skip if sender already left (e.g. delayed packet)
        var connected = _sessionManager.connectedPlayers ?? Array.Empty<IConnectedPlayer>();
        if (!connected.Any(p => p.userId == sender.userId))
            return;
        lock (_lock)
        {
            if (_playersWithMod.Add(sender.userId))
            {
                MultiplayerChat.Plugin.Log?.Info($"[E2EChat] ModPresence: {sender.userName} has chat mod");
                PresenceUpdated?.Invoke(this, EventArgs.Empty);
                PlayerWithModAdded?.Invoke(this, new PlayerWithModEventArgs(sender.userId, sender.userName ?? sender.userId));
            }
        }

        SendPresenceTo(sender.userId);
    }

    private void SchedulePresenceRetry()
    {
        CancelPresenceRetry();
        _presenceRetryCoroutine = _coroutineHost.StartCoroutine(PresenceRetryLoop());
    }

    private void CancelPresenceRetry()
    {
        if (_presenceRetryCoroutine != null)
        {
            _coroutineHost.StopCoroutine(_presenceRetryCoroutine);
            _presenceRetryCoroutine = null;
        }
    }

    private IEnumerator PresenceRetryLoop()
    {
        yield return new WaitForSeconds(3f);
        _presenceRetryCoroutine = null;
        BroadcastPresence();
        MultiplayerChat.Plugin.Log?.Info("[E2EChat] Presence retry (was ignored from song)");
    }

    /// <summary>Lyra waits 6 seconds before showing "X has chat" when she receives a reply.</summary>
    private IEnumerator ShowPlayerWithModAfter6Seconds(string userId, string userName)
    {
        yield return new WaitForSeconds(6f);
        // Don't add if they left during the delay
        var connected = _sessionManager.connectedPlayers ?? Array.Empty<IConnectedPlayer>();
        if (!connected.Any(p => p.userId == userId))
            yield break;
        lock (_lock)
        {
            if (_playersWithMod.Add(userId))
            {
                MultiplayerChat.Plugin.Log?.Info($"[E2EChat] ModPresence reply: {userName} has chat mod");
                PresenceUpdated?.Invoke(this, EventArgs.Empty);
                PlayerWithModAdded?.Invoke(this, new PlayerWithModEventArgs(userId, userName));
            }
        }
    }

    /// <summary>Sends "ignored from song" - recipient should retry in 3 seconds.</summary>
    private void SendPresenceIgnoredTo(string targetUserId)
    {
        if (string.IsNullOrEmpty(targetUserId)) return;
        _sessionManager.Send(new ModPresencePacket { TargetUserId = targetUserId, IsIgnoredFromSong = true });
    }

    /// <summary>True when we're in the lobby (not during gameplay or results).</summary>
    private static bool IsInLobby()
    {
        var center = GameObject.Find("MultiplayerLobbyCenterStage");
        if (center != null && center.activeInHierarchy) return true;
        var lobby = GameObject.Find("LobbySetup");
        if (lobby != null && lobby.activeInHierarchy) return true;
        var alt = GameObject.Find("CenterStage");
        if (alt != null && alt.activeInHierarchy) return true;
        return false;
    }

    /// <summary>Sends presence only to the specified user (targeted reply).</summary>
    private void SendPresenceTo(string targetUserId)
    {
        if (string.IsNullOrEmpty(targetUserId)) return;
        _sessionManager.Send(new ModPresencePacket { TargetUserId = targetUserId });
        MultiplayerChat.Plugin.Log?.Info($"[E2EChat] Sent presence reply to {targetUserId}");
    }

    private void BroadcastPresence()
    {
        _sessionManager.Send(new ModPresencePacket());
    }
}

public class PlayerWithModEventArgs : EventArgs
{
    public string UserId { get; }
    public string UserName { get; }
    public PlayerWithModEventArgs(string userId, string userName)
    {
        UserId = userId;
        UserName = userName;
    }
}
