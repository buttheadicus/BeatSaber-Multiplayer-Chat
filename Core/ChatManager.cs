using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MultiplayerChat.Core.Addons;
using MultiplayerChat.Network;
using MultiplayerChat.Settings;
using MultiplayerChat.UI;
using MultiplayerCore.Models;
using MultiplayerCore.Networking;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core;

// Registers MP packet callbacks, encrypt/decrypt, DM state, voice playback, and outbound spam timers. Inbound paths call ChatPacketIdValidation.TryAcceptSenderChatId before handling content.
public class ChatManager : IInitializable, IDisposable
{
    public static ChatManager? Instance { get; private set; }

    internal MpPacketSerializer PacketSerializer => _packetSerializer;

    private static ChatManager? _lobbyScopeChatManager;

    public event EventHandler<ChatMessageEventArgs>? MessageReceived;

    [Inject] private readonly IMultiplayerSessionManager _sessionManager = null!;
    [Inject] private readonly MpPacketSerializer _packetSerializer = null!;
    [Inject] private readonly EncryptionManager _encryption = null!;
    [Inject] private readonly ChatMuteManager _muteManager = null!;
    [Inject] private readonly ChatDMState _dmState = null!;
    [Inject] private readonly CoroutineHost _coroutineHost = null!;
    [Inject] private readonly ChatPlayerIdRegistry _chatPlayerIdRegistry = null!;

    private readonly Queue<(string UserId, byte[] Blob)> _voicePlaybackQueue = new();
    private bool _voicePlaybackRunning;
    private GameObject? _voicePlaybackGameObject;
    private AudioSource? _voicePlaybackAudioSource;
    private Coroutine? _voicePlaybackCoroutine;

    private const float MuteNotifyCooldownSeconds = 60f;
    private float? _lastMuteNotifyShownAt;
    private float? _lastUnmuteNotifyShownAt;

    private const float TalkToNotifyDisplayCooldownSeconds = 60f;
    private float? _lastTalkToIntroDisplayAt;
    private float? _lastTalkToStopDisplayAt;

    private readonly HashSet<string> _talkToMutualPendingFrom = new(StringComparer.Ordinal);

    private const float OutgoingTalkToNotifyCooldownSeconds = 60f;
    private readonly Dictionary<string, float> _lastOutgoingTalkToNotifyAtByTarget = new(StringComparer.Ordinal);

    private const float ListenToNotifyDisplayCooldownSeconds = 60f;
    private float? _lastListenToIntroDisplayAt;
    private float? _lastListenToStopDisplayAt;

    private readonly HashSet<string> _listenToMutualPendingFrom = new(StringComparer.Ordinal);

    private const float OutgoingListenToNotifyCooldownSeconds = 60f;
    private readonly Dictionary<string, float> _lastOutgoingListenToNotifyAtByTarget = new(StringComparer.Ordinal);

    private const float OutgoingSpamCooldownSeconds = 5f;
    private float? _lastOutgoingTextChatAt;
    private float? _lastOutgoingVoiceMessageAt;

    private const float OutgoingMuteNotifyCooldownSeconds = 60f;
    private float? _lastOutgoingMutePlayerNotifyAt;
    private float? _lastOutgoingUnmutePlayerNotifyAt;

    private bool? _lastBroadcastDeafened;
    private bool? _lastBroadcastHotMicMuted;

    private readonly Dictionary<string, Queue<byte[]>> _hotMicIncoming = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Coroutine> _hotMicUserCoroutines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HotMicSequentialPlayer> _hotMicSequentialPlayers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _hotMicNextPlayDsp = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float[]> _hotMicLastScheduledFrameByUserId = new(StringComparer.Ordinal);

    private readonly Queue<byte[]> _hotMicOutboundPending = new();

    private const int MaxHotMicOutboundPendingChunks = 48;

    private const int MaxHotMicCoalesceChunks = 24;
    private const int HotMicCoalesceCrossfadeFrames = 96;
    private const int HotMicJitterPrefetchPackets = 1;
    private const float HotMicJitterPrefetchTimeoutSec = 0.09f;
    private const float HotMicInterChunkSpinTimeoutSec = 0.65f;
    private const float HotMicJitterEmptyQueueGiveUpSec = 0.4f;
    private const float HotMicCoalesceMergeTailWaitSec = 0.028f;
    private const int MaxHotMicClipsScheduledPerPump = 32;
    private const double HotMicPlayScheduleLeadSec = 0.25;
    private const double HotMicPlayScheduleMinLeadSec = 0.002;
    private const double HotMicPlayScheduleStaleChainSec = 0.08;
    private const int HotMicScheduledBoundaryCrossfadeFrames = 44;

    public void Initialize()
    {
        Instance = this;
        if (!IsGameCoreSceneContext())
            _lobbyScopeChatManager = this;
        VoiceReceiveDiagnostics.ResetSession();
        if (MpChatVerboseDebug.IsOn)
            MultiplayerChat.Plugin.Log?.Info("[MPChat] Voice receive diagnostics (throttled)  -  look for [VoiceRx DROP], [HotMicRx]");
        if (VoiceBareStreamMode.Enabled)
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] VoiceBareStreamMode.Enabled: throttled recv diagnostic lines suppressed until disabled.");
        RegisterPacketCallbacks();
        AddonPacketSerializerBridge.Attach(_packetSerializer);
        if (ShouldRunLobbyNametagVoiceSync())
        {
            BroadcastLocalNametagVoiceStatus(force: true);
            _coroutineHost.StartCoroutine(RetryBroadcastVoiceStatusAfterJoin());
        }

        _sessionManager.playerConnectedEvent += OnPlayerConnected;
        _sessionManager.playerDisconnectedEvent += OnPlayerDisconnected;
        UpdateEncryptionKey();
        MpChatRuntimeAudit.LogAfterLobbyChatInit(_sessionManager, _encryption, !IsGameCoreSceneContext());
    }

    public void Dispose()
    {
        var lobbyPeer = _lobbyScopeChatManager;
        var iAmLobbyScope = ReferenceEquals(_lobbyScopeChatManager, this);
        var gameCoreInstance = IsGameCoreSceneContext();

        VoiceReceiveDiagnostics.ResetSession();
        if (iAmLobbyScope)
        {
            _lobbyScopeChatManager = null;
            if (MpChatFeatures.LobbyCustomAvatars)
                AddonCustomAvatarsBridge.FlushLobbyOnServerLeaveIfDisconnected();
        }

        if (Instance == this)
        {
            if (gameCoreInstance && lobbyPeer != null && !ReferenceEquals(lobbyPeer, this))
                Instance = lobbyPeer;
            else
                Instance = null;
        }

        UnregisterPacketCallbacks();
        AddonPacketSerializerBridge.Detach(_packetSerializer);
        VoiceChatRuntimeState.ClearListenFilterOnly();
        _talkToMutualPendingFrom.Clear();
        _listenToMutualPendingFrom.Clear();
        _sessionManager.playerConnectedEvent -= OnPlayerConnected;
        _sessionManager.playerDisconnectedEvent -= OnPlayerDisconnected;
        ForceStopVoicePlayback();
        _voicePlaybackQueue.Clear();
        _voicePlaybackRunning = false;
        StopAllHotMicPlayback();
        ClearHotMicOutboundPending();

        if (gameCoreInstance && lobbyPeer != null && !ReferenceEquals(lobbyPeer, this))
        {
            try
            {
                lobbyPeer.ReloadVoiceHotMicPipeline();
                if (MpChatVerboseDebug.IsOn)
                    MultiplayerChat.Plugin.Log?.Info("[MPChat] Lobby ChatManager: ReloadVoiceHotMicPipeline after GameCore dispose (restore packet handlers)");
            }
            catch (Exception ex)
            {
                MultiplayerChat.Plugin.Log?.Error($"[MPChat] Lobby ChatManager reload after GameCore failed: {ex.Message}");
            }
        }
    }

    private bool IsGameCoreSceneContext() =>
        _coroutineHost != null && MpChatSceneScope.IsGameCoreHost(_coroutineHost);

    private bool ShouldRunLobbyNametagVoiceSync() =>
        !IsGameCoreSceneContext() && MpChatLobbyDiagnostics.NametagVoiceLobbySyncActive();

    public void ReloadVoiceHotMicPipeline()
    {
        LogVoipReloadContext("ReloadVoiceHotMicPipeline enter");
        StopAllHotMicPlayback();
        ForceStopVoicePlayback();
        ClearHotMicOutboundPending();
        UpdateEncryptionKey();
        UnregisterPacketCallbacks();
        RegisterPacketCallbacks();
        LogVoipReloadContext("ReloadVoiceHotMicPipeline exit");
    }

    public void ForceFullVoipReset()
    {
        MpChatLobbyDiagnostics.LogVoipTransition("ChatManager:ForceFullVoipReset:enter", null);
        LogVoipReloadContext("ForceFullVoipReset enter");
        ReloadVoiceHotMicPipeline();
        LogVoipReloadContext("ForceFullVoipReset exit");
        MpChatLobbyDiagnostics.LogVoipTransition("ChatManager:ForceFullVoipReset:exit", null);
    }

    public void LogVoipReloadContext(string tag)
    {
        if (!MpChatVerboseDebug.IsOn)
            return;
        var scene = _coroutineHost != null ? _coroutineHost.gameObject.scene.name : "?";
        var localId = _sessionManager.localPlayer?.userId ?? "(null)";
        MultiplayerChat.Plugin.Log?.Info(
            $"[MPChat][VoIP] {tag} scene={scene} chatMgr={GetHashCode()} coroutineHost={(_coroutineHost != null)} localPlayerId={localId} hotMicPlayers={_hotMicSequentialPlayers.Count} hotMicQueues={_hotMicIncoming.Count}");
    }

    private void RegisterPacketCallbacks()
    {
        _packetSerializer.RegisterCallback<EncryptedChatPacket>(OnPacketReceived);
        _packetSerializer.RegisterCallback<VoiceMessagePacket>(OnVoicePacketReceived);
        _packetSerializer.RegisterCallback<DmIntroNotifyPacket>(OnDmIntroNotifyReceived);
        _packetSerializer.RegisterCallback<MuteNotifyPacket>(OnMuteNotifyReceived);
        _packetSerializer.RegisterCallback<DmStoppedNotifyPacket>(OnDmStoppedNotifyReceived);
        _packetSerializer.RegisterCallback<TalkToNotifyPacket>(OnTalkToNotifyReceived);
        _packetSerializer.RegisterCallback<VoiceDeafenStatePacket>(OnVoiceDeafenStateReceived);
        _packetSerializer.RegisterCallback<ChatActivityPacket>(OnChatActivityReceived);
        // New packet types must be registered last so existing packet IDs stay stable across mod updates.
        _packetSerializer.RegisterCallback<ListenToNotifyPacket>(OnListenToNotifyReceived);
        _packetSerializer.RegisterCallback<VoiceHotMicMuteStatePacket>(OnVoiceHotMicMuteStateReceived);
    }

    private void UnregisterPacketCallbacks()
    {
        _packetSerializer.UnregisterCallback<EncryptedChatPacket>();
        _packetSerializer.UnregisterCallback<VoiceMessagePacket>();
        _packetSerializer.UnregisterCallback<DmIntroNotifyPacket>();
        _packetSerializer.UnregisterCallback<MuteNotifyPacket>();
        _packetSerializer.UnregisterCallback<DmStoppedNotifyPacket>();
        _packetSerializer.UnregisterCallback<TalkToNotifyPacket>();
        _packetSerializer.UnregisterCallback<VoiceDeafenStatePacket>();
        _packetSerializer.UnregisterCallback<ChatActivityPacket>();
        _packetSerializer.UnregisterCallback<ListenToNotifyPacket>();
        _packetSerializer.UnregisterCallback<VoiceHotMicMuteStatePacket>();
    }

    private Coroutine? _pendingSessionEncryptionRefresh;

    private string? _pendingSessionEncryptionExtraUserId;

    private void OnPlayerConnected(IConnectedPlayer player)
    {
        // connectedPlayers can lag the event by a frame; refresh key next frame with the new user id included.
        ScheduleSessionEncryptionRefresh(player?.userId);
        TryFlushHotMicOutboundQueue();

        if (MpChatFeatures.LobbyCustomAvatars && player != null && !string.IsNullOrEmpty(player.userId))
        {
            AddonCustomAvatarsBridge.NotifyRemoteAvatarMayBeReady(
                player.userId,
                broadcastMetadata: ModSettings.EnableLobbyCustomAvatars);
        }

        if (ShouldRunLobbyNametagVoiceSync())
        {
            BroadcastLocalNametagVoiceStatus(force: true);
            _coroutineHost.StartCoroutine(RetryBroadcastVoiceStatusAfterJoin());
        }
    }

    private void OnPlayerDisconnected(IConnectedPlayer player)
    {
        if (player != null && !string.IsNullOrEmpty(player.userId))
            NametagVoiceStatusRegistry.ClearUser(player.userId);
        if (player != null && !string.IsNullOrEmpty(player.userId))
        {
            ClearHotMicForUser(player.userId);
            VoiceChatRuntimeState.RemoveListenUserId(player.userId);
            if (MpChatFeatures.LobbyCustomAvatars)
                AddonCustomAvatarsBridge.ClearRemote(player.userId);
        }

        ScheduleSessionEncryptionRefresh();
        if (!HasRemotePeerInSession())
        {
            ClearHotMicOutboundPending();
            VoiceChatRuntimeState.ClearListenFilterOnly();
        }
    }

    private void ScheduleSessionEncryptionRefresh(string? extraUserId = null)
    {
        if (!string.IsNullOrEmpty(extraUserId))
            _pendingSessionEncryptionExtraUserId = extraUserId;

        if (_pendingSessionEncryptionRefresh != null)
            return;

        _pendingSessionEncryptionRefresh = _coroutineHost.StartCoroutine(RefreshSessionEncryptionNextFrame());
    }

    private IEnumerator RefreshSessionEncryptionNextFrame()
    {
        yield return null;

        var extra = _pendingSessionEncryptionExtraUserId;
        _pendingSessionEncryptionExtraUserId = null;
        _pendingSessionEncryptionRefresh = null;

        if (!string.IsNullOrEmpty(extra))
            UpdateEncryptionKey(extra);
        else
            UpdateEncryptionKey();
    }

    private void ClearHotMicForUser(string userId)
    {
        if (_hotMicUserCoroutines.TryGetValue(userId, out var c) && c != null)
            _coroutineHost.StopCoroutine(c);
        _hotMicUserCoroutines.Remove(userId);
        _hotMicIncoming.Remove(userId);
        _hotMicNextPlayDsp.Remove(userId);
        _hotMicLastScheduledFrameByUserId.Remove(userId);
        DestroyHotMicSequentialSource(userId);
    }

    public float GetHotMicQueuedDurationMs(string userId)
    {
        if (string.IsNullOrEmpty(userId) || !_hotMicIncoming.TryGetValue(userId, out var q))
            return 0f;
        return EstimateHotMicQueueDurationMs(q);
    }

    public bool IsIncomingVoiceAudible()
    {
        if (_voicePlaybackAudioSource != null && _voicePlaybackAudioSource && _voicePlaybackAudioSource.isPlaying)
            return true;

        if (_voicePlaybackQueue.Count > 0)
            return true;
        if (_voicePlaybackRunning && _voicePlaybackCoroutine != null)
            return true;

        foreach (var player in _hotMicSequentialPlayers.Values)
        {
            if (player?.Root == null) continue;
            // Cheaper than scanning each HM_seg with GetComponent<AudioSource>(); segments parent here immediately.
            if (player.Root.transform.childCount > 0)
                return true;
        }

        foreach (var kv in _hotMicIncoming)
        {
            if (kv.Value.Count > 0)
                return true;
        }

        foreach (var c in _hotMicUserCoroutines.Values)
        {
            if (c != null)
                return true;
        }

        return false;
    }

    public IConnectedPlayer[] GetLobbyPlayers()
    {
        var connected = _sessionManager.connectedPlayers ?? Array.Empty<IConnectedPlayer>();
        var local = _sessionManager.localPlayer;
        var list = connected.Where(p => p != null && !string.IsNullOrEmpty(p.userId)).ToList();
        if (local != null && !string.IsNullOrEmpty(local.userId) && !list.Any(p => p!.userId == local.userId))
            list.Insert(0, local);
        return list.ToArray();
    }

    private bool HasRemotePeerInSession()
    {
        if (GetLobbyPlayers().Length >= 2)
            return true;

        var localId = _sessionManager.localPlayer?.userId;
        foreach (var p in _sessionManager.connectedPlayers ?? Array.Empty<IConnectedPlayer>())
        {
            if (p == null || string.IsNullOrEmpty(p.userId)) continue;
            if (localId != null && p.userId == localId) continue;
            return true;
        }

        return false;
    }

    private List<string> CollectEncryptionParticipantIds(params string?[] extraUserIds)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in GetLobbyPlayers())
        {
            if (p != null && !string.IsNullOrEmpty(p.userId))
                ids.Add(p.userId);
        }

        foreach (var p in _sessionManager.connectedPlayers ?? Array.Empty<IConnectedPlayer>())
        {
            if (p != null && !string.IsNullOrEmpty(p.userId))
                ids.Add(p.userId);
        }

        var local = _sessionManager.localPlayer;
        if (local != null && !string.IsNullOrEmpty(local.userId))
            ids.Add(local.userId);

        foreach (var id in extraUserIds)
        {
            if (!string.IsNullOrEmpty(id))
                ids.Add(id!);
        }

        if (ids.Count == 0)
            ids.Add("local");

        return ids.ToList();
    }

    private void UpdateEncryptionKey(params string?[] extraUserIds)
    {
        _encryption.UpdateSessionKey(CollectEncryptionParticipantIds(extraUserIds));
    }

    public bool SendMessage(string text)
    {
        if (ChatClientHandoff.IsHumanClientSuppressed)
            return false;
        return SendMessageInternal(text, fromController: false);
    }

    /// <summary>
    /// Bot/controller send path. Allowed only while chat client is claimed.
    /// Bypasses the human spam cooldown so bot command lists can send quickly.
    /// </summary>
    public bool SendMessageFromController(string text)
    {
        if (!ChatClientHandoff.IsTakenOver)
            return false;
        return SendMessageInternal(text, fromController: true);
    }

    private bool SendMessageInternal(string text, bool fromController)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var now = Time.realtimeSinceStartup;
        if (!fromController &&
            _lastOutgoingTextChatAt.HasValue &&
            now - _lastOutgoingTextChatAt.Value < OutgoingSpamCooldownSeconds)
        {
            PostSystemMessage("Woah there! Sorry about this, some sort of spam prevention had to be in place...");
            return false;
        }

        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current))
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Cannot send chat: invalid local Chat ID.");
            return false;
        }

        text = text.Trim();
        if (text.Length > 500)
            text = text.Substring(0, 500);

        // Controllers always send as lobby broadcast (no human DM mode).
        var useDm = !fromController && _dmState.IsInDMMode;
        if (useDm)
        {
            if (string.IsNullOrEmpty(_dmState.DMTargetUserId) || !ChatPersistentId.IsValidFormat(_dmState.DMTargetChatId))
            {
                MultiplayerChat.Plugin.Log?.Warn("[MPChat] Cannot send in DM mode: invalid target Chat ID.");
                return false;
            }
        }

        UpdateEncryptionKey(); // Refresh key before send (session may not have been ready at init)
        var encrypted = _encryption.Encrypt(text);
        if (encrypted == null)
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Encrypt returned null (no session key?)");
            return false;
        }

        if (!fromController
            && _dmState.PendingDmIntroForFirstMessage
            && !string.IsNullOrEmpty(_dmState.DMTargetUserId)
            && _dmState.DMTargetUserId == _dmState.ReceivedDmIntroFromUserId)
        {
            var peerName = string.IsNullOrEmpty(_dmState.DMTargetUserName) ? "Player" : (_dmState.DMTargetUserName ?? "Player");
            PostSystemMessageRich(BuildMutualDmLine(peerName, null));
        }

        if (useDm && _dmState.PendingDmIntroForFirstMessage && !string.IsNullOrEmpty(_dmState.DMTargetUserId))
        {
            _sessionManager.Send(new DmIntroNotifyPacket
            {
                TargetUserId = _dmState.DMTargetUserId,
                TargetChatId = _dmState.DMTargetChatId,
                SenderChatId = ChatPersistentId.Current,
                SenderNameColor = NormalizeHexForPacket(ModSettings.NameColor)
            });
            _dmState.MarkDmIntroSent();
        }

        var packet = new EncryptedChatPacket
        {
            EncryptedPayload = encrypted,
            NameColor = NormalizeHexForPacket(ModSettings.NameColor),
            SenderChatId = ChatPersistentId.Current,
            DisplayNameOverride = fromController ? "BOT" : null
        };
        if (useDm)
        {
            packet.TargetUserId = _dmState.DMTargetUserId;
            packet.TargetChatId = _dmState.DMTargetChatId;
        }

        _sessionManager.Send(packet);
        _lastOutgoingTextChatAt = Time.realtimeSinceStartup;

        // Show our own message locally for immediate feedback
        var localPlayer = _sessionManager.localPlayer;
        if (localPlayer != null)
        {
            var nameColor = NormalizeHexForPacket(ModSettings.NameColor);
            var displayName = fromController ? "BOT" : localPlayer.userName;
            NotifyMessageReceived(new ChatMessageEventArgs(displayName, text, localPlayer.userId, useDm, nameColor: nameColor));
        }

        return true;
    }

    private void NotifyMessageReceived(ChatMessageEventArgs e)
    {
        if (!e.IsSystem)
            ChatSoundEffects.PlayChatBubble();
        MessageReceived?.Invoke(this, e);
    }

    public void SendMuteNotifyTo(string targetPlatformUserId, bool nowMuted)
    {
        if (string.IsNullOrEmpty(targetPlatformUserId)) return;
        var local = _sessionManager.localPlayer;
        if (local == null || targetPlatformUserId == local.userId) return;
        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current)) return;
        if (!_chatPlayerIdRegistry.TryGetChatId(targetPlatformUserId, out var targetCid) || !ChatPersistentId.IsValidFormat(targetCid))
            return;

        var now = Time.realtimeSinceStartup;
        if (nowMuted)
        {
            if (_lastOutgoingMutePlayerNotifyAt.HasValue && now - _lastOutgoingMutePlayerNotifyAt.Value < OutgoingMuteNotifyCooldownSeconds)
            {
                PostSystemMessage("nice try, i thought of this too, cant spam system messages to players! ;3");
                return;
            }
        }
        else
        {
            if (_lastOutgoingUnmutePlayerNotifyAt.HasValue && now - _lastOutgoingUnmutePlayerNotifyAt.Value < OutgoingMuteNotifyCooldownSeconds)
            {
                PostSystemMessage("nice try, i thought of this too, cant spam system messages to players! ;3");
                return;
            }
        }

        _sessionManager.Send(new MuteNotifyPacket
        {
            TargetUserId = targetPlatformUserId,
            TargetChatId = targetCid,
            IsMuted = nowMuted,
            SenderChatId = ChatPersistentId.Current,
            SenderNameColor = NormalizeHexForPacket(ModSettings.NameColor)
        });
        if (nowMuted)
            _lastOutgoingMutePlayerNotifyAt = Time.realtimeSinceStartup;
        else
            _lastOutgoingUnmutePlayerNotifyAt = Time.realtimeSinceStartup;
    }

    public void SendDmStoppedNotify(string targetPlatformUserId, string? targetChatId)
    {
        if (string.IsNullOrEmpty(targetPlatformUserId)) return;
        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current)) return;
        if (!ChatPersistentId.IsValidFormat(targetChatId)) return;
        var local = _sessionManager.localPlayer;
        if (local == null) return;
        _sessionManager.Send(new DmStoppedNotifyPacket
        {
            TargetUserId = targetPlatformUserId,
            TargetChatId = targetChatId,
            SenderChatId = ChatPersistentId.Current,
            SenderNameColor = NormalizeHexForPacket(ModSettings.NameColor)
        });
    }

    public void ForceStopVoicePlayback()
    {
        _voicePlaybackQueue.Clear();
        if (_voicePlaybackCoroutine != null)
        {
            _coroutineHost.StopCoroutine(_voicePlaybackCoroutine);
            _voicePlaybackCoroutine = null;
        }
        if (_voicePlaybackGameObject != null)
        {
            var src = _voicePlaybackGameObject.GetComponent<AudioSource>();
            if (src != null && src.clip != null)
                UnityEngine.Object.Destroy(src.clip);
            UnityEngine.Object.Destroy(_voicePlaybackGameObject);
            _voicePlaybackGameObject = null;
            _voicePlaybackAudioSource = null;
        }
        _voicePlaybackRunning = false;
        StopAllHotMicPlayback();
    }

    private void StopAllHotMicPlayback()
    {
        foreach (var kv in _hotMicUserCoroutines)
        {
            if (kv.Value != null)
                _coroutineHost.StopCoroutine(kv.Value);
        }

        _hotMicUserCoroutines.Clear();
        _hotMicIncoming.Clear();
        _hotMicNextPlayDsp.Clear();
        _hotMicLastScheduledFrameByUserId.Clear();
        DestroyAllHotMicSequentialSources();
    }

    public void SendVoiceDeafenStateNotify(bool isDeaf)
    {
        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current))
            return;
        _lastBroadcastDeafened = isDeaf;
        _sessionManager.Send(new VoiceDeafenStatePacket
        {
            IsDeaf = isDeaf,
            SenderChatId = ChatPersistentId.Current,
            SenderNameColor = NormalizeHexForPacket(ModSettings.NameColor)
        });
    }

    public string? LocalUserId => _sessionManager.localPlayer?.userId;

    public bool IsUserMutedByLocal(string userId) => _muteManager.IsMuted(userId);

    public void SendVoiceHotMicMuteStateNotify(bool isHotMicMuted)
    {
        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current))
            return;
        _lastBroadcastHotMicMuted = isHotMicMuted;
        _sessionManager.Send(new VoiceHotMicMuteStatePacket
        {
            IsHotMicMuted = isHotMicMuted,
            SenderChatId = ChatPersistentId.Current,
            SenderNameColor = NormalizeHexForPacket(ModSettings.NameColor)
        });
    }

    public void BroadcastLocalNametagVoiceStatus(bool force = false)
    {
        if (!ShouldRunLobbyNametagVoiceSync())
            return;

        var deaf = VoiceChatRuntimeState.IsDeaf;
        var micMuted = VoiceChatRuntimeState.IsHotMicMuted;
        if (force || _lastBroadcastDeafened != deaf)
            SendVoiceDeafenStateNotify(deaf);
        if (force || _lastBroadcastHotMicMuted != micMuted)
            SendVoiceHotMicMuteStateNotify(micMuted);
    }

    private IEnumerator RetryBroadcastVoiceStatusAfterJoin()
    {
        for (var i = 0; i < 90; i++)
        {
            if (!ShouldRunLobbyNametagVoiceSync())
            {
                yield return null;
                continue;
            }

            if (ChatPersistentId.IsValidFormat(ChatPersistentId.Current))
            {
                BroadcastLocalNametagVoiceStatus(force: true);
                ModPresenceManager.Instance?.BroadcastPresence();
                yield break;
            }

            yield return null;
        }
    }

    public void BroadcastChatActivity(byte activityKind)
    {
        if (activityKind == 0) return;
        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current))
            return;
        _sessionManager.Send(new ChatActivityPacket
        {
            Activity = activityKind,
            SenderChatId = ChatPersistentId.Current,
            SenderNameColor = NormalizeHexForPacket(ModSettings.NameColor)
        });
    }

    private void OnChatActivityReceived(ChatActivityPacket packet, IConnectedPlayer sender)
    {
        if (!ChatPacketIdValidation.TryAcceptSenderChatId(packet.SenderChatId, sender, _chatPlayerIdRegistry))
            return;

        var local = _sessionManager.localPlayer;
        if (local != null && sender.userId == local.userId)
            return;

        var bubbles = ChatBubbleManager.Instance;
        if (bubbles == null) return;

        var name = string.IsNullOrEmpty(sender.userName) ? "Player" : sender.userName;
        var uid = sender.userId ?? "";
        if (uid.Length == 0) return;

        switch (packet.Activity)
        {
            case ChatActivityPacket.TypingStart:
                bubbles.SetEphemeralTypingLine(uid, true,
                    SystemLineWithColoredPlayerName(name, " is typing...", packet.SenderNameColor));
                break;
            case ChatActivityPacket.TypingStop:
                bubbles.SetEphemeralTypingLine(uid, false, "");
                break;
            case ChatActivityPacket.RecordingVoiceStart:
                bubbles.SetEphemeralRecordingVoiceLine(uid, true,
                    SystemLineWithColoredPlayerName(name, " is recording a voice message...", packet.SenderNameColor));
                break;
            case ChatActivityPacket.RecordingVoiceStop:
                bubbles.SetEphemeralRecordingVoiceLine(uid, false, "");
                break;
        }
    }

    private void OnVoiceDeafenStateReceived(VoiceDeafenStatePacket packet, IConnectedPlayer sender)
    {
        if (!ChatPacketIdValidation.TryAcceptSenderChatId(packet.SenderChatId, sender, _chatPlayerIdRegistry))
            return;

        var local = _sessionManager.localPlayer;
        if (local != null && sender.userId == local.userId)
            return;

        NametagVoiceStatusRegistry.SetRemoteDeafened(sender.userId, packet.IsDeaf);
        if (MpChatLobbyDiagnostics.NametagVoiceLobbySyncActive())
            ChatBubbleAnchor.TickAllStatusIcons();
    }

    private void OnVoiceHotMicMuteStateReceived(VoiceHotMicMuteStatePacket packet, IConnectedPlayer sender)
    {
        if (!ChatPacketIdValidation.TryAcceptSenderChatId(packet.SenderChatId, sender, _chatPlayerIdRegistry))
            return;

        var local = _sessionManager.localPlayer;
        if (local != null && sender.userId == local.userId)
            return;

        NametagVoiceStatusRegistry.SetRemoteHotMicMuted(sender.userId, packet.IsHotMicMuted);
        if (MpChatLobbyDiagnostics.NametagVoiceLobbySyncActive())
            ChatBubbleAnchor.TickAllStatusIcons();
    }

    private static string? NormalizeHexForPacket(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        var h = hex!.Trim();
        if (h.StartsWith("#")) h = h.Substring(1);
        if (h.Length > 6) h = h.Substring(0, 6);
        return h.Length == 6 ? h : null;
    }

    private void OnDmIntroNotifyReceived(DmIntroNotifyPacket packet, IConnectedPlayer sender)
    {
        if (!ChatPacketIdValidation.TryAcceptSenderChatId(packet.SenderChatId, sender, _chatPlayerIdRegistry))
            return;
        if (string.IsNullOrEmpty(packet.TargetUserId))
            return;
        var localPlayer = _sessionManager.localPlayer;
        if (localPlayer == null || localPlayer.userId != packet.TargetUserId)
            return;
        if (!ChatPersistentId.IsValidFormat(packet.TargetChatId) || ChatPersistentId.Current != packet.TargetChatId)
            return;

        _dmState.SetReceivedDmIntroFrom(sender.userId);
        var name = string.IsNullOrEmpty(sender.userName) ? "Someone" : sender.userName;
        if (_dmState.IsInDMMode && _dmState.DMTargetUserId == sender.userId)
            PostSystemMessageRich(BuildMutualDmLine(name, packet.SenderNameColor));
        else
            PostSystemMessageRich(SystemLineWithColoredPlayerName(name, " is now DMing you, press the DM button to DM them back!", packet.SenderNameColor));
    }

    private void OnMuteNotifyReceived(MuteNotifyPacket packet, IConnectedPlayer sender)
    {
        if (!ChatPacketIdValidation.TryAcceptSenderChatId(packet.SenderChatId, sender, _chatPlayerIdRegistry))
            return;
        if (string.IsNullOrEmpty(packet.TargetUserId)) return;
        var localPlayer = _sessionManager.localPlayer;
        if (localPlayer == null || localPlayer.userId != packet.TargetUserId)
            return;
        if (!ChatPersistentId.IsValidFormat(packet.TargetChatId) || ChatPersistentId.Current != packet.TargetChatId)
            return;

        NametagVoiceStatusRegistry.SetPeerMutedLocalViewer(sender.userId, packet.IsMuted);

        var now = Time.realtimeSinceStartup;
        var name = string.IsNullOrEmpty(sender.userName) ? "Someone" : sender.userName;
        if (packet.IsMuted)
        {
            if (_lastMuteNotifyShownAt.HasValue && now - _lastMuteNotifyShownAt.Value < MuteNotifyCooldownSeconds)
                return;
            PostSystemMessageRich(SystemLineWithColoredPlayerName(name, " has muted you! Oh no!", packet.SenderNameColor));
            _lastMuteNotifyShownAt = now;
            ChatSoundEffects.PlayMutedNotify();
        }
        else
        {
            if (_lastUnmuteNotifyShownAt.HasValue && now - _lastUnmuteNotifyShownAt.Value < MuteNotifyCooldownSeconds)
                return;
            PostSystemMessageRich(SystemLineWithColoredPlayerName(name, " has unmuted you! Yahoo!", packet.SenderNameColor));
            _lastUnmuteNotifyShownAt = now;
            ChatSoundEffects.PlayUnmutedNotify();
        }
    }

    private void OnDmStoppedNotifyReceived(DmStoppedNotifyPacket packet, IConnectedPlayer sender)
    {
        if (!ChatPacketIdValidation.TryAcceptSenderChatId(packet.SenderChatId, sender, _chatPlayerIdRegistry))
            return;
        if (string.IsNullOrEmpty(packet.TargetUserId)) return;
        var localPlayer = _sessionManager.localPlayer;
        if (localPlayer == null || localPlayer.userId != packet.TargetUserId)
            return;
        if (!ChatPersistentId.IsValidFormat(packet.TargetChatId) || ChatPersistentId.Current != packet.TargetChatId)
            return;

        var name = string.IsNullOrEmpty(sender.userName) ? "Someone" : sender.userName;
        PostSystemMessageRich(SystemLineWithColoredPlayerName(name, " has stopped DMing you.", packet.SenderNameColor));
    }

    private void OnTalkToNotifyReceived(TalkToNotifyPacket packet, IConnectedPlayer sender)
    {
        if (!ChatPacketIdValidation.TryAcceptSenderChatId(packet.SenderChatId, sender, _chatPlayerIdRegistry))
            return;
        if (string.IsNullOrEmpty(packet.TargetUserId))
            return;
        var localPlayer = _sessionManager.localPlayer;
        if (localPlayer == null || localPlayer.userId != packet.TargetUserId)
            return;
        if (!ChatPersistentId.IsValidFormat(packet.TargetChatId) || ChatPersistentId.Current != packet.TargetChatId)
            return;

        var name = string.IsNullOrEmpty(sender.userName) ? "Someone" : sender.userName;
        var now = Time.realtimeSinceStartup;

        if (packet.IsStopped)
        {
            _talkToMutualPendingFrom.Remove(sender.userId);
            if (_lastTalkToStopDisplayAt.HasValue &&
                now - _lastTalkToStopDisplayAt.Value < TalkToNotifyDisplayCooldownSeconds)
                return;
            _lastTalkToStopDisplayAt = now;
            PostSystemMessageRich(SystemLineWithColoredPlayerName(name, " has stopped talking to you.", packet.SenderNameColor));
            return;
        }

        if (VoiceChatRuntimeState.IsTalkingTo(sender.userId))
        {
            PostSystemMessageRich(BuildMutualTalkToLine(name, packet.SenderNameColor));
            _talkToMutualPendingFrom.Remove(sender.userId);
            return;
        }

        _talkToMutualPendingFrom.Add(sender.userId);

        if (_lastTalkToIntroDisplayAt.HasValue &&
            now - _lastTalkToIntroDisplayAt.Value < TalkToNotifyDisplayCooldownSeconds)
            return;
        _lastTalkToIntroDisplayAt = now;

        const string introTail =
            " is now talking to you. To talk back, press Hear → Talk to and select their username.";
        var alsoOthers = BuildTalkToAlsoOthersSuffix(packet.AlsoTalkingToOthersCsv);
        PostSystemMessageRich(SystemLineWithColoredPlayerName(name, introTail + alsoOthers, packet.SenderNameColor));
    }

    private void OnListenToNotifyReceived(ListenToNotifyPacket packet, IConnectedPlayer sender)
    {
        if (!ChatPacketIdValidation.TryAcceptSenderChatId(packet.SenderChatId, sender, _chatPlayerIdRegistry))
            return;
        if (string.IsNullOrEmpty(packet.TargetUserId))
            return;
        var localPlayer = _sessionManager.localPlayer;
        if (localPlayer == null || localPlayer.userId != packet.TargetUserId)
            return;
        if (!ChatPersistentId.IsValidFormat(packet.TargetChatId) || ChatPersistentId.Current != packet.TargetChatId)
            return;

        var name = string.IsNullOrEmpty(sender.userName) ? "Someone" : sender.userName;
        var now = Time.realtimeSinceStartup;

        if (packet.IsStopped)
        {
            _listenToMutualPendingFrom.Remove(sender.userId);
            if (_lastListenToStopDisplayAt.HasValue &&
                now - _lastListenToStopDisplayAt.Value < ListenToNotifyDisplayCooldownSeconds)
                return;
            _lastListenToStopDisplayAt = now;
            PostSystemMessageRich(SystemLineWithColoredPlayerName(name, " has stopped listening to you.", packet.SenderNameColor));
            return;
        }

        if (VoiceChatRuntimeState.IsListeningTo(sender.userId))
        {
            PostSystemMessageRich(BuildMutualListenLine(name, packet.SenderNameColor));
            _listenToMutualPendingFrom.Remove(sender.userId);
            return;
        }

        _listenToMutualPendingFrom.Add(sender.userId);

        if (_lastListenToIntroDisplayAt.HasValue &&
            now - _lastListenToIntroDisplayAt.Value < ListenToNotifyDisplayCooldownSeconds)
            return;
        _lastListenToIntroDisplayAt = now;

        const string introTail =
            " has started listening to you.";
        var alsoOthers = BuildListenToAlsoOthersSuffix(packet.AlsoListeningToOthersCsv);
        PostSystemMessageRich(SystemLineWithColoredPlayerName(name, introTail + alsoOthers, packet.SenderNameColor));
    }

    private static string BuildOxfordAmpersandList(IReadOnlyList<string> escapedDisplayNames)
    {
        if (escapedDisplayNames.Count == 0) return "";
        if (escapedDisplayNames.Count == 1) return escapedDisplayNames[0];
        if (escapedDisplayNames.Count == 2) return $"{escapedDisplayNames[0]} & {escapedDisplayNames[1]}";
        return string.Join(", ", escapedDisplayNames.Take(escapedDisplayNames.Count - 1)) + " & " +
               escapedDisplayNames[escapedDisplayNames.Count - 1];
    }

    private string BuildTalkToAlsoOthersSuffix(string? csv)
    {
        if (string.IsNullOrEmpty(csv))
            return "";
        var ids = csv!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var names = new List<string>();
        foreach (var raw in ids)
        {
            var id = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(id))
                continue;
            var n = ResolveConnectedDisplayName(id);
            var label = string.IsNullOrEmpty(n) ? id : n;
            names.Add(ChatRichTextEscape.ForDisplay(label ?? ""));
        }

        if (names.Count == 0)
            return "";
        return " They are also talking to " + BuildOxfordAmpersandList(names) + ".";
    }

    private string BuildListenToAlsoOthersSuffix(string? csv)
    {
        if (string.IsNullOrEmpty(csv))
            return "";
        var ids = csv!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var names = new List<string>();
        foreach (var raw in ids)
        {
            var id = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(id))
                continue;
            var n = ResolveConnectedDisplayName(id);
            var label = string.IsNullOrEmpty(n) ? id : n;
            names.Add(ChatRichTextEscape.ForDisplay(label ?? ""));
        }

        if (names.Count == 0)
            return "";
        return " They are also listening to " + BuildOxfordAmpersandList(names) + ".";
    }

    private void OnPacketReceived(EncryptedChatPacket packet, IConnectedPlayer sender)
    {
        if (packet.EncryptedPayload == null || packet.EncryptedPayload.Length == 0)
            return;

        if (!ChatPacketIdValidation.TryAcceptSenderChatId(packet.SenderChatId, sender, _chatPlayerIdRegistry))
            return;

        if (_muteManager.IsMuted(sender.userId))
            return;

        if (!ChatPacketIdValidation.TryParseDmRouting(packet.TargetUserId, packet.TargetChatId, out var isDm))
            return;

        var localPlayer = _sessionManager.localPlayer;
        if (!ChatPacketIdValidation.IsLocalParticipant(packet.TargetUserId, packet.TargetChatId, isDm, localPlayer?.userId, sender.userId))
            return;

        UpdateEncryptionKey(sender.userId, packet.TargetUserId);
        var decrypted = _encryption.Decrypt(packet.EncryptedPayload);
        if (decrypted == null)
        {
            UpdateEncryptionKey(sender.userId, packet.TargetUserId, localPlayer?.userId);
            decrypted = _encryption.Decrypt(packet.EncryptedPayload);
        }

        if (decrypted == null)
        {
            VoiceReceiveDiagnostics.LogDecryptFailedWithFingerprintThrottled(sender.userId, _encryption.LastSessionStateFingerprint);
            return;
        }

        var displayName = !string.IsNullOrWhiteSpace(packet.DisplayNameOverride)
            ? packet.DisplayNameOverride!.Trim()
            : ModPresenceManager.Instance != null &&
              ModPresenceManager.Instance.IsSlzCompanionClient(sender.userId)
                ? "BOT"
                : sender.userName;
        NotifyMessageReceived(new ChatMessageEventArgs(displayName, decrypted, sender.userId, isDm, nameColor: packet.NameColor));
    }

    public event Action<string>? SystemMessageRemovalRequested;

    public void PostSystemMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        MessageReceived?.Invoke(this, new ChatMessageEventArgs("", message, "", false, isSystem: true, nameColor: null));
    }

    public void RequestRemoveSystemMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        SystemMessageRemovalRequested?.Invoke(message);
    }

    public void PostSystemMessageRich(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        MessageReceived?.Invoke(this, new ChatMessageEventArgs("", message, "", false, isSystem: true, nameColor: null, systemMessageRichText: true));
    }

    public static string SystemLineWithColoredPlayerName(string playerDisplayName, string tailAfterName, string? nameColorHex)
    {
        var hex = NormalizeHexForPacket(nameColorHex) ?? "87CEEB";
        var safeName = ChatRichTextEscape.ForDisplay(playerDisplayName ?? "");
        var safeTail = ChatRichTextEscape.ForDisplay(tailAfterName ?? "");
        return $"<color=#{hex}>{safeName}</color>{safeTail}";
    }

    public static string BuildMutualDmLine(string peerDisplayName, string? peerNameColorHex)
    {
        var hex = NormalizeHexForPacket(peerNameColorHex) ?? "87CEEB";
        var safeName = ChatRichTextEscape.ForDisplay(peerDisplayName ?? "");
        return "You and " + $"<color=#{hex}>{safeName}</color> are now DMing eachother.";
    }

    public static string BuildMutualTalkToLine(string peerDisplayName, string? peerNameColorHex)
    {
        var hex = NormalizeHexForPacket(peerNameColorHex) ?? "87CEEB";
        var safeName = ChatRichTextEscape.ForDisplay(peerDisplayName ?? "");
        return "You and " + $"<color=#{hex}>{safeName}</color> are now talking to each other.";
    }

    public static string BuildMutualListenLine(string peerDisplayName, string? peerNameColorHex)
    {
        var hex = NormalizeHexForPacket(peerNameColorHex) ?? "87CEEB";
        var safeName = ChatRichTextEscape.ForDisplay(peerDisplayName ?? "");
        return "You and " + $"<color=#{hex}>{safeName}</color> are now listening to each other.";
    }

    public void AfterTalkToSelectionChanged(HashSet<string> previousTalkToIds)
    {
        var current = VoiceChatRuntimeState.CopyTalkToUserIds();
        foreach (var id in previousTalkToIds)
        {
            if (!current.Contains(id))
            {
                _talkToMutualPendingFrom.Remove(id);
                TrySendTalkToNotify(id, stopped: true);
            }
        }

        foreach (var id in current)
        {
            if (previousTalkToIds.Contains(id))
                continue;
            if (_talkToMutualPendingFrom.Contains(id))
            {
                var n = ResolveConnectedDisplayName(id);
                PostSystemMessageRich(BuildMutualTalkToLine(string.IsNullOrEmpty(n) ? "Player" : n!, null));
                _talkToMutualPendingFrom.Remove(id);
            }

            TrySendTalkToNotify(id, stopped: false);
        }
    }

    private string? ResolveConnectedDisplayName(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return null;
        foreach (var p in GetLobbyPlayers())
        {
            if (p != null && p.userId == userId)
                return string.IsNullOrEmpty(p.userName) ? userId : p.userName;
        }

        return null;
    }

    private void TrySendTalkToNotify(string targetPlatformUserId, bool stopped)
    {
        if (string.IsNullOrEmpty(targetPlatformUserId))
            return;
        var local = _sessionManager.localPlayer;
        if (local == null || targetPlatformUserId == local.userId)
            return;
        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current))
            return;
        if (!_chatPlayerIdRegistry.TryGetChatId(targetPlatformUserId, out var targetCid) ||
            !ChatPersistentId.IsValidFormat(targetCid))
            return;

        var now = Time.realtimeSinceStartup;
        if (!stopped)
        {
            if (_lastOutgoingTalkToNotifyAtByTarget.TryGetValue(targetPlatformUserId, out var last) &&
                now - last < OutgoingTalkToNotifyCooldownSeconds)
            {
                PostSystemMessage("nice try, i thought of this too, cant spam system messages to players! ;3");
                return;
            }
        }

        string? othersCsv = null;
        if (!stopped)
        {
            var parts = new List<string>();
            foreach (var id in VoiceChatRuntimeState.TalkToUserIds)
            {
                if (string.IsNullOrEmpty(id) || id == targetPlatformUserId)
                    continue;
                parts.Add(id);
            }

            if (parts.Count > 0)
                othersCsv = string.Join(",", parts);
        }

        _sessionManager.Send(new TalkToNotifyPacket
        {
            TargetUserId = targetPlatformUserId,
            TargetChatId = targetCid,
            IsStopped = stopped,
            SenderChatId = ChatPersistentId.Current,
            SenderNameColor = NormalizeHexForPacket(ModSettings.NameColor),
            AlsoTalkingToOthersCsv = othersCsv
        });

        if (!stopped)
            _lastOutgoingTalkToNotifyAtByTarget[targetPlatformUserId] = now;
        else
            _lastOutgoingTalkToNotifyAtByTarget.Remove(targetPlatformUserId);
    }

    public void AfterListenSelectionChanged(HashSet<string> previousListenUserIds)
    {
        var current = VoiceChatRuntimeState.CopyListenUserIds();
        foreach (var id in previousListenUserIds)
        {
            if (!current.Contains(id))
            {
                _listenToMutualPendingFrom.Remove(id);
                TrySendListenToNotify(id, stopped: true);
            }
        }

        foreach (var id in current)
        {
            if (previousListenUserIds.Contains(id))
                continue;
            if (_listenToMutualPendingFrom.Contains(id))
            {
                var n = ResolveConnectedDisplayName(id);
                PostSystemMessageRich(BuildMutualListenLine(string.IsNullOrEmpty(n) ? "Player" : n!, null));
                _listenToMutualPendingFrom.Remove(id);
            }

            TrySendListenToNotify(id, stopped: false);
        }
    }

    private void TrySendListenToNotify(string targetPlatformUserId, bool stopped)
    {
        if (string.IsNullOrEmpty(targetPlatformUserId))
            return;
        var local = _sessionManager.localPlayer;
        if (local == null || targetPlatformUserId == local.userId)
            return;
        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current))
            return;
        if (!_chatPlayerIdRegistry.TryGetChatId(targetPlatformUserId, out var targetCid) ||
            !ChatPersistentId.IsValidFormat(targetCid))
            return;

        var now = Time.realtimeSinceStartup;
        if (!stopped)
        {
            if (_lastOutgoingListenToNotifyAtByTarget.TryGetValue(targetPlatformUserId, out var last) &&
                now - last < OutgoingListenToNotifyCooldownSeconds)
            {
                PostSystemMessage("nice try, i thought of this too, cant spam system messages to players! ;3");
                return;
            }
        }

        string? othersCsv = null;
        if (!stopped)
        {
            var parts = new List<string>();
            foreach (var id in VoiceChatRuntimeState.ListenUserIds)
            {
                if (string.IsNullOrEmpty(id) || id == targetPlatformUserId)
                    continue;
                parts.Add(id);
            }

            if (parts.Count > 0)
                othersCsv = string.Join(",", parts);
        }

        _sessionManager.Send(new ListenToNotifyPacket
        {
            TargetUserId = targetPlatformUserId,
            TargetChatId = targetCid,
            IsStopped = stopped,
            SenderChatId = ChatPersistentId.Current,
            SenderNameColor = NormalizeHexForPacket(ModSettings.NameColor),
            AlsoListeningToOthersCsv = othersCsv
        });

        if (!stopped)
            _lastOutgoingListenToNotifyAtByTarget[targetPlatformUserId] = now;
        else
            _lastOutgoingListenToNotifyAtByTarget.Remove(targetPlatformUserId);
    }

    public bool SendVoiceMessage(byte[] voicePlainBlob)
    {
        if (voicePlainBlob == null || voicePlainBlob.Length == 0)
            return false;

        var now = Time.realtimeSinceStartup;
        if (_lastOutgoingVoiceMessageAt.HasValue && now - _lastOutgoingVoiceMessageAt.Value < OutgoingSpamCooldownSeconds)
        {
            PostSystemMessage("Woah there! Sorry about this, some sort of spam prevention had to be in place...");
            return false;
        }

        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current))
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Cannot send voice: invalid local Chat ID.");
            return false;
        }

        if (VoiceChatRuntimeState.TalkToUserIds.Count == 0 && _dmState.IsInDMMode)
        {
            if (string.IsNullOrEmpty(_dmState.DMTargetUserId) || !ChatPersistentId.IsValidFormat(_dmState.DMTargetChatId))
            {
                MultiplayerChat.Plugin.Log?.Warn("[MPChat] Cannot send voice in DM: invalid target Chat ID.");
                return false;
            }
        }

        UpdateEncryptionKey();
        var encrypted = _encryption.Encrypt(voicePlainBlob);
        if (encrypted == null)
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Voice encrypt failed (no session key?)");
            return false;
        }

        if (!TrySendEncryptedVoiceToTargets(encrypted))
            return false;

        _lastOutgoingVoiceMessageAt = Time.realtimeSinceStartup;

        var localPlayer = _sessionManager.localPlayer;
        if (localPlayer != null)
        {
            var name = string.IsNullOrEmpty(localPlayer.userName) ? "Player" : localPlayer.userName;
            var localHex = NormalizeHexForPacket(ModSettings.NameColor);
            if (VoiceChatRuntimeState.TalkToUserIds.Count > 0)
                PostSystemMessageRich(SystemLineWithColoredPlayerName(name, " has sent a voice message", localHex));
            else if (_dmState.IsInDMMode)
                PostSystemMessageRich(SystemLineWithColoredPlayerName(name, " sent a DM Voice Message", localHex));
            else
                PostSystemMessageRich(SystemLineWithColoredPlayerName(name, " has sent a voice message", localHex));
        }

        return true;
    }

    private void ClearHotMicOutboundPending() => _hotMicOutboundPending.Clear();

    private void EnqueueHotMicOutboundPending(byte[] voicePlainBlob)
    {
        while (_hotMicOutboundPending.Count >= MaxHotMicOutboundPendingChunks)
            _hotMicOutboundPending.Dequeue();
        _hotMicOutboundPending.Enqueue((byte[])voicePlainBlob.Clone());
    }

    private bool IsHotMicEncryptionSessionReady()
    {
        if (VoiceChatRuntimeState.TalkToUserIds.Count > 0)
            return true;
        if (_dmState.IsInDMMode)
            return true;
        return HasRemotePeerInSession();
    }

    private void TryFlushHotMicOutboundQueue()
    {
        if (!IsHotMicEncryptionSessionReady())
            return;
        while (_hotMicOutboundPending.Count > 0)
        {
            var b = _hotMicOutboundPending.Dequeue();
            SendVoiceHotMicChunkInternal(b);
        }
    }

    private bool SendVoiceHotMicChunkInternal(byte[] voicePlainBlob)
    {
        UpdateEncryptionKey();
        var encrypted = _encryption.Encrypt(voicePlainBlob);
        if (encrypted == null)
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Hot mic encrypt failed (no session key?)");
            return false;
        }

        var ok = TrySendEncryptedVoiceToTargets(encrypted);
        if (ok)
            VoipPipelineTrace.TxDispatch(voicePlainBlob.Length, encrypted.Length);
        return ok;
    }

    public bool SendVoiceHotMicChunk(byte[] voicePlainBlob)
    {
        if (voicePlainBlob == null || voicePlainBlob.Length == 0)
            return false;

        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current))
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Cannot send hot mic: invalid local Chat ID.");
            return false;
        }

        if (VoiceChatRuntimeState.TalkToUserIds.Count == 0 && _dmState.IsInDMMode)
        {
            if (string.IsNullOrEmpty(_dmState.DMTargetUserId) || !ChatPersistentId.IsValidFormat(_dmState.DMTargetChatId))
            {
                MultiplayerChat.Plugin.Log?.Warn("[MPChat] Cannot send hot mic in DM: invalid target Chat ID.");
                return false;
            }
        }

        if (!IsHotMicEncryptionSessionReady())
        {
            EnqueueHotMicOutboundPending(voicePlainBlob);
            return true;
        }

        TryFlushHotMicOutboundQueue();
        return SendVoiceHotMicChunkInternal(voicePlainBlob);
    }

    private bool TrySendEncryptedVoiceToTargets(byte[] encrypted)
    {
        var nameColor = NormalizeHexForPacket(ModSettings.NameColor);
        var senderChatId = ChatPersistentId.Current;

        if (VoiceChatRuntimeState.TalkToUserIds.Count > 0)
        {
            var any = false;
            foreach (var uid in VoiceChatRuntimeState.TalkToUserIds)
            {
                if (!_chatPlayerIdRegistry.TryGetChatId(uid, out var cid) || !ChatPersistentId.IsValidFormat(cid))
                {
                    MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Talk-to: missing or invalid Chat ID for {uid}");
                    continue;
                }

                SendVoiceMessagePacket(new VoiceMessagePacket
                {
                    EncryptedPayload = encrypted,
                    NameColor = nameColor,
                    SenderChatId = senderChatId,
                    TargetUserId = uid,
                    TargetChatId = cid
                });
                any = true;
            }

            if (!any)
                MultiplayerChat.Plugin.Log?.Warn("[MPChat] Talk-to voice: no recipients with known Chat IDs.");
            return any;
        }

        var voicePacket = new VoiceMessagePacket
        {
            EncryptedPayload = encrypted,
            NameColor = nameColor,
            SenderChatId = senderChatId
        };
        if (_dmState.IsInDMMode)
        {
            voicePacket.TargetUserId = _dmState.DMTargetUserId;
            voicePacket.TargetChatId = _dmState.DMTargetChatId;
        }

        SendVoiceMessagePacket(voicePacket);
        return true;
    }

    private void SendVoiceMessagePacket(VoiceMessagePacket voicePacket) => _sessionManager.Send(voicePacket);

    private static bool IsEffectivelyDeafForIncomingVoice() => VoiceChatRuntimeState.IsDeaf;

    private void OnVoicePacketReceived(VoiceMessagePacket packet, IConnectedPlayer sender)
    {
        if (packet.EncryptedPayload == null || packet.EncryptedPayload.Length == 0)
        {
            VoiceReceiveDiagnostics.LogVoiceReceiveDropThrottled("empty_voice_payload", null);
            return;
        }

        if (!ChatPacketIdValidation.TryAcceptSenderChatId(packet.SenderChatId, sender, _chatPlayerIdRegistry,
                voiceHotPath: true))
        {
            VoiceReceiveDiagnostics.LogVoiceReceiveDropThrottled("reject_sender_chat_id", sender.userId);
            return;
        }

        if (_muteManager.IsMuted(sender.userId))
        {
            VoiceReceiveDiagnostics.LogVoiceReceiveDropThrottled("sender_muted", sender.userId);
            return;
        }

        if (!ChatPacketIdValidation.TryParseDmRouting(packet.TargetUserId, packet.TargetChatId, out var isDm))
        {
            VoiceReceiveDiagnostics.LogVoiceReceiveDropThrottled("dm_routing_parse_failed", null);
            return;
        }

        var localPlayer = _sessionManager.localPlayer;
        if (!ChatPacketIdValidation.IsLocalParticipant(packet.TargetUserId, packet.TargetChatId, isDm, localPlayer?.userId, sender.userId))
        {
            VoiceReceiveDiagnostics.LogVoiceReceiveDropThrottled("not_local_dm_target_or_routing",
                $"isDm={isDm} local={localPlayer?.userId} from={sender.userId}");
            return;
        }

        var enc = packet.EncryptedPayload!;
        UpdateEncryptionKey(sender.userId, packet.TargetUserId);
        var decrypted = _encryption.DecryptToBytes(enc);
        if (decrypted == null)
        {
            UpdateEncryptionKey(sender.userId, packet.TargetUserId, localPlayer?.userId);
            decrypted = _encryption.DecryptToBytes(enc);
        }

        if (decrypted == null)
        {
            VoiceReceiveDiagnostics.LogDecryptFailedWithFingerprintThrottled(sender.userId, _encryption.LastSessionStateFingerprint);
            return;
        }

        if (!VoiceChatRuntimeState.ShouldPlayIncomingVoiceFrom(sender.userId))
        {
            VoiceReceiveDiagnostics.LogFilterSnapshotForBlockedSender(sender.userId);
            return;
        }

        if (VoiceHotMicCodec.IsHotMicBlob(decrypted))
        {
            if (IsEffectivelyDeafForIncomingVoice())
            {
                VoiceReceiveDiagnostics.LogVoiceReceiveDropThrottled("local_deaf_hot_mic", sender.userId);
                return;
            }

            VoiceReceiveDiagnostics.LogHotMicFirstChunkFromUser(sender.userId, decrypted.Length);
            EnqueueIncomingHotMic(sender.userId, decrypted);
            return;
        }

        if (IsEffectivelyDeafForIncomingVoice())
        {
            VoiceReceiveDiagnostics.LogVoiceReceiveDropThrottled("local_deaf", null);
            return;
        }

        if (!VoiceMessageCodec.IsVoiceMessageBlob(decrypted))
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Voice payload is neither VMSG nor VHOT; dropping.");
            return;
        }

        var name = string.IsNullOrEmpty(sender.userName) ? "Player" : sender.userName;
        if (isDm)
            PostSystemMessageRich(SystemLineWithColoredPlayerName(name, " sent a DM Voice Message", packet.NameColor));
        else
            PostSystemMessageRich(SystemLineWithColoredPlayerName(name, " has sent a voice message", packet.NameColor));

        EnqueueVoicePlayback(sender.userId, decrypted);
    }

    private void EnqueueVoicePlayback(string userId, byte[] decodedBlob)
    {
        _voicePlaybackQueue.Enqueue((userId, decodedBlob));
        NametagVoiceStatusRegistry.NotifyTalking(userId);
        TrimVoiceMessageQueueToTargetLatency();
        if (!_voicePlaybackRunning)
            _voicePlaybackCoroutine = _coroutineHost.StartCoroutine(VoicePlaybackQueueRunner());
    }

    private void TrimVoiceMessageQueueToTargetLatency()
    {
    }

    private static float EstimateHotMicQueueDurationMs(Queue<byte[]> q)
    {
        var total = 0f;
        foreach (var b in q)
        {
            if (VoiceHotMicCodec.TryGetDurationMs(b, out var ms))
                total += ms;
        }

        return total;
    }

    private IEnumerator VoicePlaybackQueueRunner()
    {
        _voicePlaybackRunning = true;
        try
        {
            while (_voicePlaybackQueue.Count > 0)
            {
                TrimVoiceMessageQueueToTargetLatency();
                if (_voicePlaybackQueue.Count == 0)
                    break;
                var (uid, blob) = _voicePlaybackQueue.Dequeue();
                yield return PlayVoiceClipCoroutine(blob, uid);
            }
        }
        finally
        {
            _voicePlaybackRunning = false;
            _voicePlaybackCoroutine = null;
            if (_voicePlaybackQueue.Count > 0)
                _voicePlaybackCoroutine = _coroutineHost.StartCoroutine(VoicePlaybackQueueRunner());
        }
    }

    private static IEnumerator WaitForAudioSourceClipEnd(AudioSource src, AudioClip clip)
    {
        if (src == null || clip == null)
            yield break;

        src.Play();
        GlobalChatAudioHost.NotifyIncomingVoiceActivity();
        yield return null;

        var deadline = Time.realtimeSinceStartup + clip.length + 2f;
        while (src != null && src.isPlaying && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    private HotMicSequentialPlayer GetOrCreateHotMicSequentialPlayer(string userId)
    {
        if (_hotMicSequentialPlayers.TryGetValue(userId, out var existing) && existing != null && existing.Root != null)
            return existing;

        _hotMicSequentialPlayers.Remove(userId);
        var player = new HotMicSequentialPlayer(_coroutineHost.transform);
        _hotMicSequentialPlayers[userId] = player;
        return player;
    }

    private void DestroyHotMicSequentialSource(string userId)
    {
        if (!_hotMicSequentialPlayers.TryGetValue(userId, out var player))
            return;
        _hotMicSequentialPlayers.Remove(userId);
        _hotMicNextPlayDsp.Remove(userId);
        _hotMicLastScheduledFrameByUserId.Remove(userId);
        if (player?.Root != null)
            UnityEngine.Object.Destroy(player.Root);
    }

    private void DestroyAllHotMicSequentialSources()
    {
        foreach (var kv in _hotMicSequentialPlayers)
        {
            if (kv.Value?.Root != null)
                UnityEngine.Object.Destroy(kv.Value.Root);
        }

        _hotMicSequentialPlayers.Clear();
    }

    private static void AppendSamplesWithCrossfade(List<float> merged, float[] next, int channels)
    {
        if (next == null || next.Length == 0 || channels < 1)
            return;
        if (merged.Count == 0)
        {
            merged.AddRange(next);
            return;
        }

        var crossFrames = Mathf.Min(HotMicCoalesceCrossfadeFrames, merged.Count / channels, next.Length / channels);
        if (crossFrames < 2)
        {
            merged.AddRange(next);
            return;
        }

        var cross = crossFrames * channels;
        for (var f = 0; f < crossFrames; f++)
        {
            var u = (f + 1f) / crossFrames;
            var t = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * u);
            for (var c = 0; c < channels; c++)
            {
                var idxOld = merged.Count - cross + f * channels + c;
                var idxNew = f * channels + c;
                merged[idxOld] = merged[idxOld] * (1f - t) + next[idxNew] * t;
            }
        }

        for (var i = cross; i < next.Length; i++)
            merged.Add(next[i]);
    }

    private IEnumerator PlayVoiceClipCoroutine(byte[] blob, string senderUserId)
    {
        if (!VoiceMessageCodec.TryDecodeToFloatSamples(blob, out var samples, out var rate, out var ch))
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Could not decode voice message");
            yield break;
        }

        var peakDecodeVm = ComputePeakAbs(samples);
        var gainVm = VoiceChatAudioLevel.GetVoiceChatPlaybackGain(senderUserId);
        if (!VoiceBareStreamMode.Enabled)
        {
            if (VoiceReceiveDiagnostics.ShouldLogVoiceMessageChunkLine(senderUserId))
            {
                var vpc = PlayerVoiceVolumeStore.GetVolumePercent(senderUserId);
                VoiceReceiveDiagnostics.LogVoiceMessageChunkLine(senderUserId, vpc, gainVm, peakDecodeVm, rate, ch, samples.Length);
            }
        }

        VoiceChatAudioLevel.ApplyReceiveGainToSamples(samples, senderUserId);

        var clip = VoiceMessageCodec.CreateAudioClipFromDecodedSamples(samples, ch, rate);
        if (clip == null)
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Could not build voice message clip");
            yield break;
        }

        var go = new GameObject("MPChatVoicePlayback");
        _voicePlaybackGameObject = go;
        var src = go.AddComponent<AudioSource>();
        _voicePlaybackAudioSource = src;
        src.clip = clip;
        src.volume = 1f;
        src.spatialBlend = 0f;
        src.bypassEffects = true;
        src.bypassListenerEffects = true;
        src.bypassReverbZones = true;
        yield return WaitForAudioSourceClipEnd(src, clip);
        UnityEngine.Object.Destroy(clip);
        if (_voicePlaybackGameObject == go)
        {
            _voicePlaybackGameObject = null;
            _voicePlaybackAudioSource = null;
        }

        UnityEngine.Object.Destroy(go);
    }

    private void EnqueueIncomingHotMic(string senderUserId, byte[] decryptedBlob)
    {
        if (string.IsNullOrEmpty(senderUserId)) return;

        if (!_hotMicIncoming.TryGetValue(senderUserId, out var q))
        {
            q = new Queue<byte[]>();
            _hotMicIncoming[senderUserId] = q;
        }

        q.Enqueue(decryptedBlob);
        NametagVoiceStatusRegistry.NotifyTalking(senderUserId);
        VoipPipelineTrace.RxEnqueue(senderUserId, q.Count, decryptedBlob.Length);
        if (!_hotMicUserCoroutines.ContainsKey(senderUserId))
        {
            var c = _coroutineHost.StartCoroutine(PlayHotMicUserChunks(senderUserId));
            _hotMicUserCoroutines[senderUserId] = c;
        }
    }

    private IEnumerator PlayHotMicUserChunks(string userId)
    {
        try
        {
            if (!_hotMicIncoming.TryGetValue(userId, out var q))
                yield break;

            var receivePrimed = false;

            while (true)
            {
                if (!receivePrimed)
                {
                    var waitStart = Time.realtimeSinceStartup;
                    while (q.Count < HotMicJitterPrefetchPackets)
                    {
                        if (q.Count == 0)
                        {
                            if (Time.realtimeSinceStartup - waitStart >= HotMicJitterEmptyQueueGiveUpSec)
                                yield break;
                            yield return null;
                            continue;
                        }

                        if (q.Count >= 1 && Time.realtimeSinceStartup - waitStart >= HotMicJitterPrefetchTimeoutSec)
                            break;

                        yield return null;
                    }
                }
                else
                {
                    var spinStart = Time.realtimeSinceStartup;
                    while (q.Count == 0)
                    {
                        if (Time.realtimeSinceStartup - spinStart >= HotMicInterChunkSpinTimeoutSec)
                            yield break;
                        yield return null;
                    }
                }

                if (q.Count == 0)
                    yield break;

                receivePrimed = true;

                for (var pump = 0; pump < MaxHotMicClipsScheduledPerPump && q.Count > 0; pump++)
                {
                    var blob = q.Dequeue();
                    if (!VoiceHotMicCodec.TryDecodeToFloatSamples(blob, out var samples, out var rate, out var ch))
                    {
                        VoiceReceiveDiagnostics.LogHotMicDecodeFailed(userId, blob.Length);
                        continue;
                    }

                    var peakDecode = ComputePeakAbs(samples);
                    var gainHm = VoiceChatAudioLevel.GetVoiceChatPlaybackGain(userId);
                    if (VoiceReceiveDiagnostics.ShouldLogHotMicChunkLine(userId))
                    {
                        var vpc = PlayerVoiceVolumeStore.GetVolumePercent(userId);
                        VoiceReceiveDiagnostics.LogHotMicChunkLine(userId, blob.Length, vpc, gainHm, peakDecode, peakDecode, rate,
                            ch, samples.Length);
                    }

                    VoiceChatAudioLevel.ApplyReceiveGainToSamples(samples, userId);

                    var merged = new List<float>(samples.Length * MaxHotMicCoalesceChunks);
                    merged.AddRange(samples);
                    var coalesced = 1;

                    while (coalesced < MaxHotMicCoalesceChunks && q.Count > 0)
                    {
                        var peekBlob = q.Peek();
                        if (!VoiceHotMicCodec.TryDecodeToFloatSamples(peekBlob, out var nextSamples, out var nextRate, out var nextCh))
                            break;
                        if (nextRate != rate || nextCh != ch)
                            break;
                        q.Dequeue();
                        VoiceChatAudioLevel.ApplyReceiveGainToSamples(nextSamples, userId);
                        AppendSamplesWithCrossfade(merged, nextSamples, ch);
                        coalesced++;
                    }

                    if (coalesced < MaxHotMicCoalesceChunks)
                    {
                        var tailWait = Time.realtimeSinceStartup;
                        while (q.Count == 0 && coalesced < MaxHotMicCoalesceChunks &&
                               Time.realtimeSinceStartup - tailWait < HotMicCoalesceMergeTailWaitSec)
                            yield return null;
                        while (coalesced < MaxHotMicCoalesceChunks && q.Count > 0)
                        {
                            var peekBlob = q.Peek();
                            if (!VoiceHotMicCodec.TryDecodeToFloatSamples(peekBlob, out var nextSamples, out var nextRate, out var nextCh))
                                break;
                            if (nextRate != rate || nextCh != ch)
                                break;
                            q.Dequeue();
                            VoiceChatAudioLevel.ApplyReceiveGainToSamples(nextSamples, userId);
                            AppendSamplesWithCrossfade(merged, nextSamples, ch);
                            coalesced++;
                        }
                    }

                    var pcm = merged.ToArray();
                    ApplyHotMicScheduledBoundaryCrossfadeInPlace(userId, pcm, ch);
                    var pcmFrames = ch > 0 ? pcm.Length / ch : 0;
                    var estSec = rate > 0 && ch > 0 ? (float)pcm.Length / (rate * ch) : 0f;
                    VoipPipelineTrace.RxMerge(userId, coalesced, pcmFrames, ch, rate, estSec, q.Count);

                    var clip = VoiceHotMicCodec.CreateAudioClipFromDecodedSamples(pcm, ch, rate);
                    if (clip == null)
                        continue;

                    StoreHotMicLastScheduledFrame(userId, pcm, ch, pcmFrames);
                    ScheduleHotMicClip(userId, clip, 1f);
                }

                yield return null;
            }
        }
        finally
        {
            _hotMicUserCoroutines.Remove(userId);
            if (_hotMicIncoming.TryGetValue(userId, out var q2) && q2.Count > 0)
            {
                var c = _coroutineHost.StartCoroutine(PlayHotMicUserChunks(userId));
                _hotMicUserCoroutines[userId] = c;
            }
            else
            {
                _hotMicNextPlayDsp.Remove(userId);
                _hotMicLastScheduledFrameByUserId.Remove(userId);
            }
        }
    }

    private void ApplyHotMicScheduledBoundaryCrossfadeInPlace(string userId, float[] pcm, int channels)
    {
        if (pcm == null || channels < 1 || pcm.Length < channels * 2) return;
        var frameCount = pcm.Length / channels;

        if (!_hotMicLastScheduledFrameByUserId.TryGetValue(userId, out var prevFrame) || prevFrame.Length != channels)
            return;

        var cf = Mathf.Min(HotMicScheduledBoundaryCrossfadeFrames, frameCount);
        if (cf < 2) return;

        for (var f = 0; f < cf; f++)
        {
            var u = (f + 1f) / cf;
            var t = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * u);
            for (var c = 0; c < channels; c++)
            {
                var idx = f * channels + c;
                pcm[idx] = prevFrame[c] * (1f - t) + pcm[idx] * t;
            }
        }
    }

    private void StoreHotMicLastScheduledFrame(string userId, float[] pcm, int channels, int frameCount)
    {
        if (pcm == null || channels < 1 || frameCount < 1 || pcm.Length < frameCount * channels)
            return;
        var last = new float[channels];
        var li = frameCount - 1;
        for (var c = 0; c < channels; c++)
            last[c] = pcm[li * channels + c];
        _hotMicLastScheduledFrameByUserId[userId] = last;
    }

    private void ScheduleHotMicClip(string userId, AudioClip clip, float volume01)
    {
        if (clip == null) return;

        var player = GetOrCreateHotMicSequentialPlayer(userId);
        var segGo = new GameObject("HM_seg");
        segGo.transform.SetParent(player.Root.transform, false);
        var src = segGo.AddComponent<AudioSource>();
        ConfigureHotMicVoipAudioSource(src);
        src.volume = Mathf.Clamp01(volume01);

        var dspNow = AudioSettings.dspTime;
        double startDsp;
        if (!_hotMicNextPlayDsp.TryGetValue(userId, out var chainNext) ||
            chainNext < dspNow - HotMicPlayScheduleStaleChainSec)
            startDsp = dspNow + HotMicPlayScheduleLeadSec;
        else
            startDsp = chainNext;

        if (startDsp < dspNow + HotMicPlayScheduleMinLeadSec)
            startDsp = dspNow + HotMicPlayScheduleMinLeadSec;

        var dur = HotMicClipDurationSeconds(clip);
        var endDsp = startDsp + dur;
        _hotMicNextPlayDsp[userId] = endDsp;

        src.clip = clip;
        src.PlayScheduled(startDsp);
        GlobalChatAudioHost.NotifyIncomingVoiceActivity();
        _coroutineHost.StartCoroutine(DestroyHotMicClipAfterDsp(segGo, src, clip, endDsp));
    }

    private static void ConfigureHotMicVoipAudioSource(AudioSource src)
    {
        src.spatialBlend = 0f;
        src.pitch = 1f;
        src.ignoreListenerPause = true;
        src.bypassEffects = true;
        src.bypassListenerEffects = true;
        src.bypassReverbZones = true;
    }

    private static IEnumerator DestroyHotMicClipAfterDsp(GameObject segmentGo, AudioSource src, AudioClip clip, double endDsp)
    {
        while (AudioSettings.dspTime < endDsp - 1e-4)
            yield return null;

        if (src != null && src && ReferenceEquals(src.clip, clip))
            src.clip = null;
        if (clip != null)
            UnityEngine.Object.Destroy(clip);
        if (segmentGo != null)
            UnityEngine.Object.Destroy(segmentGo);
    }

    private static double HotMicClipDurationSeconds(AudioClip clip)
    {
        if (clip == null) return 0;
        if (clip.length > 0.0001f) return clip.length;
        var hz = Mathf.Max(1, clip.frequency);
        var ch = Mathf.Max(1, clip.channels);
        return clip.samples / (double)(hz * ch);
    }

    private sealed class HotMicSequentialPlayer
    {
        public readonly GameObject Root;

        public HotMicSequentialPlayer(Transform parent)
        {
            Root = new GameObject("MPChatHotMicPlayer");
            Root.transform.SetParent(parent, false);
        }
    }

    private static float ComputePeakAbs(float[] samples)
    {
        if (samples == null || samples.Length == 0) return 0f;
        var peak = 0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var a = Mathf.Abs(samples[i]);
            if (a > peak) peak = a;
        }
        return peak;
    }

}

public class ChatMessageEventArgs : EventArgs
{
    public string UserName { get; }
    public string Message { get; }
    public string UserId { get; }
    public bool IsDM { get; }
    public bool IsSystem { get; }
    public string? NameColor { get; }

    public bool SystemMessageRichText { get; }

    public ChatMessageEventArgs(string userName, string message, string userId, bool isDm = false, bool isSystem = false, string? nameColor = null, bool systemMessageRichText = false)
    {
        UserName = userName;
        Message = message;
        UserId = userId;
        IsDM = isDm;
        IsSystem = isSystem;
        NameColor = nameColor;
        SystemMessageRichText = systemMessageRichText;
    }
}
