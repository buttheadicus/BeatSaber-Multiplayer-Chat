using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MultiplayerChat;
using MultiplayerChat.Network;
using MultiplayerChat.Settings;
using MultiplayerCore.Models;
using MultiplayerCore.Networking;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core;

// Broadcasts ModPresencePacket so lobby peers learn who runs MP Chat and can map platformUserId <-> SenderChatId when format-valid.
// Lobby-scoped instance sometimes hands off to GameCore; Dispose re-registers the lobby serializer when core tears down.
public class ModPresenceManager : IInitializable, IDisposable
{
    public static ModPresenceManager? Instance { get; private set; }

    private static ModPresenceManager? _lobbyScopeInstance;

    private readonly HashSet<string> _playersWithMod = new();
    private readonly HashSet<string> _playersWithLobbyCustomAvatars = new();
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
            lock (_lock)
            {
                _playersWithMod.Add(local.userId);
                SyncLocalLobbyCustomAvatarsTrackingLocked();
            }
        }

        // Presence sends immediately. Reply waits 6 seconds. Ignored from song -> retry in 3 seconds.
        MultiplayerChat.Plugin.Log?.Info("[MPChat] ModPresenceManager initialized");
        if (!MpChatLobbyDiagnostics.SongGameplayLikelyActive())
            BroadcastPresence();
        _coroutineHost.StartCoroutine(RepeatBroadcast());
    }

    private IEnumerator RepeatBroadcast()
    {
        for (var i = 0; i < 20; i++) // Try for ~40 seconds
        {
            yield return new WaitForSeconds(2f);
            if (!MpChatLobbyDiagnostics.SongGameplayLikelyActive())
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
        lock (_lock)
        {
            _playersWithMod.Clear();
            _playersWithLobbyCustomAvatars.Clear();
        }

        // GameCore disposes its ModPresenceManager while lobby instance may still be alive; restore lobby packet handler so presence keeps working.
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

    public bool HasLobbyCustomAvatars(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return false;

        lock (_lock) return _playersWithLobbyCustomAvatars.Contains(userId);
    }

    public event EventHandler? PresenceUpdated;

    public event EventHandler<PlayerWithModEventArgs>? PlayerWithModAdded;

    private void OnPlayerConnected(IConnectedPlayer player)
    {
        var local = _sessionManager.localPlayer;
        if (local != null && !string.IsNullOrEmpty(local.userId))
        {
            lock (_lock)
            {
                _playersWithMod.Add(local.userId);
                SyncLocalLobbyCustomAvatarsTrackingLocked();
            }
        }

        BroadcastPresence();
    }

    private void OnPlayerDisconnected(IConnectedPlayer player)
    {
        if (player == null || string.IsNullOrEmpty(player.userId))
            return;

        RemoveRemotePlayerWithMod(player.userId);
    }

    private void RemoveRemotePlayerWithMod(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return;

        var modRemoved = false;
        var avatarsRemoved = false;
        lock (_lock)
        {
            modRemoved = _playersWithMod.Remove(userId);
            avatarsRemoved = _playersWithLobbyCustomAvatars.Remove(userId);
        }

        if (modRemoved || avatarsRemoved)
            PresenceUpdated?.Invoke(this, EventArgs.Empty);
    }

    // Updates ChatPlayerIdRegistry when SenderChatId is valid; still drives has-mod UI when SenderChatId is temporarily invalid (reset/regenerate window).
    private void OnModPresenceReceived(ModPresencePacket packet, IConnectedPlayer sender)
    {
        if (string.IsNullOrEmpty(sender.userId)) return;

        var formatOk = ChatPersistentId.IsValidFormat(packet.SenderChatId);
        if (formatOk)
        {
            var packetOfficial = ChatPersistentId.IsOfficialTaggedChatId(packet.SenderChatId);
            var hasKnown = _chatPlayerIdRegistry.TryGetChatId(sender.userId, out var known);

            var canonicalChatId = packet.SenderChatId!;
            if (hasKnown && ChatPersistentId.IsOfficialLegacyEightDigitPair(known, packet.SenderChatId))
                canonicalChatId = ChatPersistentId.PreferOfficialTaggedForm(known, packet.SenderChatId!);

            if (MpChatVerboseDebug.IsOn)
            {
                var sb = new StringBuilder(384);
                sb.Append("OnModPresenceReceived APPLY mapping.\n");
                sb.Append("sender.platformUserId=").Append(MpChatVerboseDebug.TruncPlatformUserId(sender.userId)).Append('\n');
                sb.Append("packetOfficialTagged=").Append(packetOfficial).Append('\n');
                sb.Append("incoming SenderChatId=").Append(packet.SenderChatId).Append('\n');
                sb.Append("prior known=").Append(hasKnown ? known : "(none)").Append('\n');
                sb.Append("canonical stored=").Append(canonicalChatId).Append('\n');
                sb.Append("Stack:\n").Append(Environment.StackTrace);
                MpChatVerboseDebug.PresenceBlock(sb.ToString());
            }

            if (hasKnown && known != canonicalChatId && ModSettings.DebugLogging)
            {
                MultiplayerChat.Plugin.Log?.Debug(
                    "[MPChat][ChatId] Presence: peer Chat ID updated for " + sender.userId + " -> " + canonicalChatId + ".");
            }

            _chatPlayerIdRegistry.SetMapping(sender.userId, canonicalChatId);
            _chatMuteManager.OnPeerChatIdLearned(sender.userId, canonicalChatId);
        }
        else
        {
            if (MpChatVerboseDebug.IsOn)
                MpChatVerboseDebug.PresenceBlock(
                    "OnModPresenceReceived: SenderChatId invalid; skip Chat ID mapping, continue presence.\n" +
                    "sender.platformUserId=" + MpChatVerboseDebug.TruncPlatformUserId(sender.userId) + '\n' +
                    "SenderChatId literal=" + (packet.SenderChatId ?? "(null)") + '\n' +
                    "charCodes=" + MpChatVerboseDebug.CharCodes(packet.SenderChatId) + '\n' +
                    "Stack:\n" + Environment.StackTrace);

            if (ModSettings.DebugLogging)
            {
                MultiplayerChat.Plugin.Log?.Debug(
                    "[MPChat][ChatId] Presence from " + sender.userId +
                    ": SenderChatId missing or invalid; skipping registry update.");
            }
        }

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
            // Proper reply - they have the mod.
            _hasReceivedPresenceReply = true;
            CancelPresenceRetry();
            TryRegisterRemotePlayerWithMod(
                sender.userId,
                sender.userName ?? sender.userId,
                packet.SenderNameColor,
                packet.IsSlzCompanionClient);
            ApplyRemoteLobbyCustomAvatarsFlag(sender.userId, packet.HasLobbyCustomAvatarsEnabled);
            return;
        }

        // Broadcast presence from joining client (e.g. Lyra)
        if (!IsInLobby())
        {
            // We're in a song - send "ignored", don't process
            SendPresenceIgnoredTo(sender.userId);
            return;
        }

        // We're in lobby - register mod user and reply immediately.
        // Skip if sender already left (e.g. delayed packet)
        var connected = _sessionManager.connectedPlayers ?? Array.Empty<IConnectedPlayer>();
        if (!connected.Any(p => p.userId == sender.userId))
            return;
        TryRegisterRemotePlayerWithMod(
            sender.userId,
            sender.userName ?? sender.userId,
            packet.SenderNameColor,
            packet.IsSlzCompanionClient);
        ApplyRemoteLobbyCustomAvatarsFlag(sender.userId, packet.HasLobbyCustomAvatarsEnabled);

        SendPresenceTo(sender.userId);
    }

    private void ApplyRemoteLobbyCustomAvatarsFlag(string userId, bool enabled)
    {
        if (string.IsNullOrEmpty(userId))
            return;

        var changed = false;
        lock (_lock)
        {
            if (enabled)
                changed = _playersWithLobbyCustomAvatars.Add(userId);
            else
                changed = _playersWithLobbyCustomAvatars.Remove(userId);
        }

        if (changed)
            PresenceUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void SyncLocalLobbyCustomAvatarsTrackingLocked()
    {
        var local = _sessionManager.localPlayer;
        if (local == null || string.IsNullOrEmpty(local.userId))
            return;

        if (CustomAvatarDependenciesBootstrap.IsSessionActive())
            _playersWithLobbyCustomAvatars.Add(local.userId);
        else
            _playersWithLobbyCustomAvatars.Remove(local.userId);
    }

    public void RefreshLobbyCustomAvatarsPresenceAfterSettingsChange()
    {
        var changed = false;
        lock (_lock)
        {
            SyncLocalLobbyCustomAvatarsTrackingLocked();
            changed = true;
        }

        if (changed)
        {
            BroadcastPresence();
            PresenceUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void TryRegisterRemotePlayerWithMod(string userId, string userName, string? senderNameColor, bool isSlzCompanionClient)
    {
        if (string.IsNullOrEmpty(userId))
            return;

        var connected = _sessionManager.connectedPlayers ?? Array.Empty<IConnectedPlayer>();
        if (!connected.Any(p => p.userId == userId))
            return;

        lock (_lock)
        {
            if (!_playersWithMod.Add(userId))
                return;

            MultiplayerChat.Plugin.Log?.Info($"[MPChat] ModPresence: {userName} has chat mod");
            PresenceUpdated?.Invoke(this, EventArgs.Empty);
            PlayerWithModAdded?.Invoke(this, new PlayerWithModEventArgs(userId, userName, senderNameColor, isSlzCompanionClient));
        }
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

    private ModPresencePacket BuildPresencePacket(string? targetUserId = null, bool ignoredFromSong = false)
    {
        return new ModPresencePacket
        {
            TargetUserId = targetUserId,
            IsIgnoredFromSong = ignoredFromSong,
            SenderChatId = ChatPersistentId.Current,
            SenderNameColor = NormalizeNameColorForPacket(ModSettings.NameColor),
            IsSlzCompanionClient = SlzMode.IsEnabled,
            HasLobbyCustomAvatarsEnabled = CustomAvatarDependenciesBootstrap.IsSessionActive()
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

    private void SendPresenceIgnoredTo(string targetUserId)
    {
        if (string.IsNullOrEmpty(targetUserId)) return;
        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current)) return;
        _sessionManager.Send(BuildPresencePacket(targetUserId, ignoredFromSong: true));
    }

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

    // After gameplay when lobby UI is active again (presence was skipped during the song).
    public void RefreshAfterLobbyReturn()
    {
        if (MpChatLobbyDiagnostics.ShouldSkipMultiplayerPlayerSessionHooks())
            return;
        if (!MpChatLobbyDiagnostics.MultiplayerLobbyReturnContextActive())
            return;
        BroadcastPresence();
        PresenceUpdated?.Invoke(this, EventArgs.Empty);
    }
}

public class PlayerWithModEventArgs : EventArgs
{
    public string UserId { get; }
    public string UserName { get; }
    public string? NameColorHex { get; }

    public bool IsSlzCompanionClient { get; }

    public PlayerWithModEventArgs(string userId, string userName, string? nameColorHex = null, bool isSlzCompanionClient = false)
    {
        UserId = userId;
        UserName = userName;
        NameColorHex = nameColorHex;
        IsSlzCompanionClient = isSlzCompanionClient;
    }
}
