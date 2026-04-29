using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MultiplayerChat;
using MultiplayerChat.Network;
using MultiplayerChat.Settings;
using MultiplayerCore.Models;
using MultiplayerCore.Networking;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core;

/// <summary>
/// Tracks which players have the MPChat mod. Broadcasts our presence when connecting;
/// shows the chat icon on nametags for players in this set.
/// </summary>
public class ModPresenceManager : IInitializable, IDisposable
{
    public static ModPresenceManager? Instance { get; private set; }

    private static ModPresenceManager? _lobbyScopeInstance;

    private readonly HashSet<string> _playersWithMod = new();
    private readonly object _lock = new();
    private Coroutine? _presenceRetryCoroutine;
    private bool _hasReceivedPresenceReply;

    [Inject] private readonly IMultiplayerSessionManager _sessionManager = null!;
    [Inject] private readonly MpPacketSerializer _packetSerializer = null!;
    [Inject] private readonly CoroutineHost _coroutineHost = null!;
    [Inject] private readonly ChatPlayerIdRegistry _chatPlayerIdRegistry = null!;
    [Inject] private readonly ChatMuteManager _chatMuteManager = null!;

    public void Initialize()
    {
        Instance = this;
        if (!IsGameCoreContext())
            _lobbyScopeInstance = this;
        RegisterPresencePacketHandler();
        _sessionManager.playerConnectedEvent += OnPlayerConnected;
        _sessionManager.playerDisconnectedEvent += OnPlayerDisconnected;

        // Add local player (we have the mod)
        var local = _sessionManager.localPlayer;
        if (local != null && !string.IsNullOrEmpty(local.userId))
        {
            lock (_lock) _playersWithMod.Add(local.userId);
        }

        // Presence sends immediately. Reply waits 6 seconds. Ignored from song -> retry in 3 seconds.
        MultiplayerChat.Plugin.Log?.Info("[MPChat] ModPresenceManager initialized");
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
        var lobbyPeer = _lobbyScopeInstance;
        var iAmLobby = ReferenceEquals(_lobbyScopeInstance, this);
        var gameCore = IsGameCoreContext();

        if (iAmLobby)
            _lobbyScopeInstance = null;

        if (Instance == this)
        {
            if (gameCore && lobbyPeer != null && !ReferenceEquals(lobbyPeer, this))
                Instance = lobbyPeer;
            else
                Instance = null;
        }

        _hasReceivedPresenceReply = false;
        CancelPresenceRetry();
        _packetSerializer.UnregisterCallback<ModPresencePacket>();
        _sessionManager.playerConnectedEvent -= OnPlayerConnected;
        _sessionManager.playerDisconnectedEvent -= OnPlayerDisconnected;
        lock (_lock) _playersWithMod.Clear();

        if (gameCore && lobbyPeer != null && !ReferenceEquals(lobbyPeer, this))
        {
            try
            {
                lobbyPeer.RegisterPresencePacketHandler();
                MultiplayerChat.Plugin.Log?.Info("[MPChat] Lobby ModPresenceManager: re-registered ModPresencePacket after GameCore dispose");
            }
            catch (Exception ex)
            {
                MultiplayerChat.Plugin.Log?.Error($"[MPChat] Lobby ModPresence re-register failed: {ex.Message}");
            }
        }
    }

    private bool IsGameCoreContext() =>
        _coroutineHost != null && MpChatSceneScope.IsGameCoreHost(_coroutineHost);

    private void RegisterPresencePacketHandler() =>
        _packetSerializer.RegisterCallback<ModPresencePacket>(OnModPresenceReceived);

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

        if (!ChatPersistentId.IsValidFormat(packet.SenderChatId))
            return;
        if (_chatPlayerIdRegistry.TryGetChatId(sender.userId, out var known) && known != packet.SenderChatId)
            return;
        _chatPlayerIdRegistry.SetMapping(sender.userId, packet.SenderChatId!);
        _chatMuteManager.OnPeerChatIdLearned(sender.userId, packet.SenderChatId!);

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
            _coroutineHost.StartCoroutine(ShowPlayerWithModAfter6Seconds(sender.userId, sender.userName ?? sender.userId, packet.SenderNameColor, packet.IsSlzCompanionClient));
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
                MultiplayerChat.Plugin.Log?.Info($"[MPChat] ModPresence: {sender.userName} has chat mod");
                PresenceUpdated?.Invoke(this, EventArgs.Empty);
                PlayerWithModAdded?.Invoke(this, new PlayerWithModEventArgs(sender.userId, sender.userName ?? sender.userId, packet.SenderNameColor, packet.IsSlzCompanionClient));
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
        MultiplayerChat.Plugin.Log?.Info("[MPChat] Presence retry (was ignored from song)");
    }

    /// <summary>Lyra waits 6 seconds before showing "X has chat" when she receives a reply.</summary>
    private IEnumerator ShowPlayerWithModAfter6Seconds(string userId, string userName, string? senderNameColor, bool isSlzCompanionClient)
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
                MultiplayerChat.Plugin.Log?.Info($"[MPChat] ModPresence reply: {userName} has chat mod");
                PresenceUpdated?.Invoke(this, EventArgs.Empty);
                PlayerWithModAdded?.Invoke(this, new PlayerWithModEventArgs(userId, userName, senderNameColor, isSlzCompanionClient));
            }
        }
    }

    private ModPresencePacket BuildPresencePacket(string? targetUserId = null, bool ignoredFromSong = false)
    {
        return new ModPresencePacket
        {
            TargetUserId = targetUserId,
            IsIgnoredFromSong = ignoredFromSong,
            SenderChatId = ChatPersistentId.Current,
            SenderNameColor = NormalizeNameColorForPacket(ModSettings.NameColor),
            IsSlzCompanionClient = SlzMode.IsEnabled
        };
    }

    private static string? NormalizeNameColorForPacket(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        var h = hex!.Trim();
        if (h.StartsWith("#")) h = h.Substring(1);
        if (h.Length > 6) h = h.Substring(0, 6);
        return h.Length == 6 ? h : null;
    }

    /// <summary>Sends "ignored from song" - recipient should retry in 3 seconds.</summary>
    private void SendPresenceIgnoredTo(string targetUserId)
    {
        if (string.IsNullOrEmpty(targetUserId)) return;
        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current)) return;
        _sessionManager.Send(BuildPresencePacket(targetUserId, ignoredFromSong: true));
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
        var host = GameObject.Find("HostSetup");
        return host != null && host.activeInHierarchy;
    }

    /// <summary>Sends presence only to the specified user (targeted reply).</summary>
    private void SendPresenceTo(string targetUserId)
    {
        if (string.IsNullOrEmpty(targetUserId)) return;
        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current)) return;
        _sessionManager.Send(BuildPresencePacket(targetUserId));
        MultiplayerChat.Plugin.Log?.Info($"[MPChat] Sent presence reply to {targetUserId}");
    }

    private void BroadcastPresence()
    {
        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current)) return;
        _sessionManager.Send(BuildPresencePacket());
    }
}

public class PlayerWithModEventArgs : EventArgs
{
    public string UserId { get; }
    public string UserName { get; }
    /// <summary>6-char hex without # from presence packet; null if unknown or old client.</summary>
    public string? NameColorHex { get; }

    /// <summary>True when the sender runs SLZ companion mode (separate system line for non-SLZ clients).</summary>
    public bool IsSlzCompanionClient { get; }

    public PlayerWithModEventArgs(string userId, string userName, string? nameColorHex = null, bool isSlzCompanionClient = false)
    {
        UserId = userId;
        UserName = userName;
        NameColorHex = nameColorHex;
        IsSlzCompanionClient = isSlzCompanionClient;
    }
}
