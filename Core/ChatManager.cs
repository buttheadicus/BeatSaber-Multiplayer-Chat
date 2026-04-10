using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MultiplayerChat.Network;
using MultiplayerChat.Settings;
using MultiplayerCore.Models;
using MultiplayerCore.Networking;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core;

public class ChatManager : IInitializable, IDisposable
{
    public static ChatManager? Instance { get; private set; }

    public event EventHandler<ChatMessageEventArgs>? MessageReceived;

    [Inject] private readonly IMultiplayerSessionManager _sessionManager = null!;
    [Inject] private readonly MpPacketSerializer _packetSerializer = null!;
    [Inject] private readonly EncryptionManager _encryption = null!;
    [Inject] private readonly ChatMuteManager _muteManager = null!;
    [Inject] private readonly ChatDMState _dmState = null!;
    [Inject] private readonly CoroutineHost _coroutineHost = null!;
    [Inject] private readonly ChatPlayerIdRegistry _chatPlayerIdRegistry = null!;

    private readonly Queue<byte[]> _voicePlaybackQueue = new();
    private bool _voicePlaybackRunning;
    private GameObject? _voicePlaybackGameObject;
    private Coroutine? _voicePlaybackCoroutine;

    private const float MuteNotifyCooldownSeconds = 60f;
    private float? _lastMuteNotifyShownAt;
    private float? _lastUnmuteNotifyShownAt;

    private const float OutgoingChatOrVoiceCooldownSeconds = 5f;
    private float? _lastOutgoingChatOrVoiceAt;

    private const float OutgoingMuteNotifyCooldownSeconds = 60f;
    private float? _lastOutgoingMuteNotifyAt;

    public void Initialize()
    {
        Instance = this;
        _packetSerializer.RegisterCallback<EncryptedChatPacket>(OnPacketReceived);
        _packetSerializer.RegisterCallback<VoiceMessagePacket>(OnVoicePacketReceived);
        _packetSerializer.RegisterCallback<DmIntroNotifyPacket>(OnDmIntroNotifyReceived);
        _packetSerializer.RegisterCallback<MuteNotifyPacket>(OnMuteNotifyReceived);
        _packetSerializer.RegisterCallback<DmStoppedNotifyPacket>(OnDmStoppedNotifyReceived);
        _sessionManager.playerConnectedEvent += OnPlayerConnected;
        _sessionManager.playerDisconnectedEvent += OnPlayerDisconnected;
        UpdateEncryptionKey();
    }

    public void Dispose()
    {
        Instance = null;
        _packetSerializer.UnregisterCallback<EncryptedChatPacket>();
        _packetSerializer.UnregisterCallback<VoiceMessagePacket>();
        _packetSerializer.UnregisterCallback<DmIntroNotifyPacket>();
        _packetSerializer.UnregisterCallback<MuteNotifyPacket>();
        _packetSerializer.UnregisterCallback<DmStoppedNotifyPacket>();
        _sessionManager.playerConnectedEvent -= OnPlayerConnected;
        _sessionManager.playerDisconnectedEvent -= OnPlayerDisconnected;
        ForceStopVoicePlayback();
        _voicePlaybackQueue.Clear();
        _voicePlaybackRunning = false;
    }

    private void OnPlayerConnected(IConnectedPlayer player) => UpdateEncryptionKey();
    private void OnPlayerDisconnected(IConnectedPlayer player) => UpdateEncryptionKey();

    /// <summary>
    /// Returns all players in the lobby (connected + local). Use this for player list UIs.
    /// </summary>
    public IConnectedPlayer[] GetLobbyPlayers()
    {
        var connected = _sessionManager.connectedPlayers ?? Array.Empty<IConnectedPlayer>();
        var local = _sessionManager.localPlayer;
        var list = connected.Where(p => p != null && !string.IsNullOrEmpty(p.userId)).ToList();
        if (local != null && !string.IsNullOrEmpty(local.userId) && !list.Any(p => p!.userId == local.userId))
            list.Insert(0, local);
        return list.ToArray();
    }

    private void UpdateEncryptionKey()
    {
        // connectedPlayers typically excludes local; include local so solo host can derive key
        var connected = _sessionManager.connectedPlayers ?? Array.Empty<IConnectedPlayer>();
        var local = _sessionManager.localPlayer;
        var allPlayerIds = connected
            .Where(p => p != null && !string.IsNullOrEmpty(p.userId))
            .Select(p => p!.userId)
            .Distinct()
            .ToList();
        if (local != null && !string.IsNullOrEmpty(local.userId) && !allPlayerIds.Contains(local.userId))
            allPlayerIds.Add(local.userId);

        // Fallback: when alone in lobby, connected can be empty and local may not be ready yet.
        // Use a placeholder so we can still encrypt (key updates when others join).
        if (allPlayerIds.Count == 0)
            allPlayerIds.Add("local");

        _encryption.UpdateSessionKey(allPlayerIds);
    }

    /// <summary>Sends an encrypted chat message. In DM mode, <see cref="EncryptedChatPacket.TargetUserId"/> is the intended recipient.</summary>
    /// <returns>True if the message was sent; false if rate-limited or validation/encrypt failed.</returns>
    /// <remarks>
    /// Multiplayer delivers custom packets to every client in the lobby. DM is enforced in software: only the sender
    /// and the client whose <c>userId</c> matches <c>TargetUserId</c> decrypt and display (see <see cref="OnPacketReceived"/>).
    /// 0.2.0 requires a valid local Chat ID and DM packets include the recipient's Chat ID.
    /// </remarks>
    public bool SendMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var now = Time.realtimeSinceStartup;
        if (_lastOutgoingChatOrVoiceAt.HasValue && now - _lastOutgoingChatOrVoiceAt.Value < OutgoingChatOrVoiceCooldownSeconds)
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

        if (_dmState.IsInDMMode)
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

        if (_dmState.PendingDmIntroForFirstMessage
            && !string.IsNullOrEmpty(_dmState.DMTargetUserId)
            && _dmState.DMTargetUserId == _dmState.ReceivedDmIntroFromUserId)
        {
            var peerName = string.IsNullOrEmpty(_dmState.DMTargetUserName) ? "Player" : (_dmState.DMTargetUserName ?? "Player");
            PostSystemMessageRich(BuildMutualDmLine(peerName, null));
        }

        if (_dmState.IsInDMMode && _dmState.PendingDmIntroForFirstMessage && !string.IsNullOrEmpty(_dmState.DMTargetUserId))
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
            SenderChatId = ChatPersistentId.Current
        };
        if (_dmState.IsInDMMode)
        {
            packet.TargetUserId = _dmState.DMTargetUserId;
            packet.TargetChatId = _dmState.DMTargetChatId;
        }

        _sessionManager.Send(packet);
        _lastOutgoingChatOrVoiceAt = Time.realtimeSinceStartup;
        MultiplayerChat.Plugin.Log?.Info($"[MPChat] Sent message, invoking MessageReceived");

        // Show our own message locally for immediate feedback
        var localPlayer = _sessionManager.localPlayer;
        if (localPlayer != null)
        {
            var isDm = _dmState.IsInDMMode;
            var nameColor = NormalizeHexForPacket(ModSettings.NameColor);
            NotifyMessageReceived(new ChatMessageEventArgs(localPlayer.userName, text, localPlayer.userId, isDm, nameColor: nameColor));
        }

        return true;
    }

    private void NotifyMessageReceived(ChatMessageEventArgs e)
    {
        if (!e.IsSystem)
            ChatSoundEffects.PlayChatBubble();
        MessageReceived?.Invoke(this, e);
    }

    /// <summary>Notifies the target that they have been muted or unmuted by the local player.</summary>
    public void SendMuteNotifyTo(string targetPlatformUserId, bool nowMuted)
    {
        if (string.IsNullOrEmpty(targetPlatformUserId)) return;
        var local = _sessionManager.localPlayer;
        if (local == null || targetPlatformUserId == local.userId) return;
        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current)) return;
        if (!_chatPlayerIdRegistry.TryGetChatId(targetPlatformUserId, out var targetCid) || !ChatPersistentId.IsValidFormat(targetCid))
            return;

        var now = Time.realtimeSinceStartup;
        if (_lastOutgoingMuteNotifyAt.HasValue && now - _lastOutgoingMuteNotifyAt.Value < OutgoingMuteNotifyCooldownSeconds)
        {
            PostSystemMessage("nice try, i thought of this too, cant spam system messages to players! ;3");
            return;
        }

        _sessionManager.Send(new MuteNotifyPacket
        {
            TargetUserId = targetPlatformUserId,
            TargetChatId = targetCid,
            IsMuted = nowMuted,
            SenderChatId = ChatPersistentId.Current,
            SenderNameColor = NormalizeHexForPacket(ModSettings.NameColor)
        });
        _lastOutgoingMuteNotifyAt = Time.realtimeSinceStartup;
    }

    /// <summary>Notifies the DM partner that the local player ended DM mode.</summary>
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

    /// <summary>Stops the currently playing voice clip and clears the playback queue.</summary>
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
        }
        _voicePlaybackRunning = false;
    }

    private static string? NormalizeHexForPacket(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        hex = hex.Trim();
        if (hex.StartsWith("#")) hex = hex.Substring(1);
        if (hex.Length > 6) hex = hex.Substring(0, 6);
        return hex.Length == 6 ? hex : null;
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

        UpdateEncryptionKey();

        var decrypted = _encryption.Decrypt(packet.EncryptedPayload);
        if (decrypted == null)
            return;

        decrypted = decrypted.Replace("<", "&lt;").Replace(">", "&gt;");
        NotifyMessageReceived(new ChatMessageEventArgs(sender.userName, decrypted, sender.userId, isDm, nameColor: packet.NameColor));
    }

    /// <summary>Post a system message to the chat (e.g. "USERNAME has chat! They can see your messages!").</summary>
    public void PostSystemMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        message = message.Replace("<", "&lt;").Replace(">", "&gt;");
        MessageReceived?.Invoke(this, new ChatMessageEventArgs("", message, "", false, isSystem: true, nameColor: null));
    }

    /// <summary>System line with TMP rich text (caller must escape user-controlled segments; use <see cref="SystemLineWithColoredPlayerName"/>).</summary>
    public void PostSystemMessageRich(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        MessageReceived?.Invoke(this, new ChatMessageEventArgs("", message, "", false, isSystem: true, nameColor: null, systemMessageRichText: true));
    }

    /// <summary>Builds a system line with the player's display name in their chosen color; escapes name and suffix for TMP.</summary>
    public static string SystemLineWithColoredPlayerName(string playerDisplayName, string tailAfterName, string? nameColorHex)
    {
        var hex = NormalizeHexForPacket(nameColorHex) ?? "87CEEB";
        var safeName = (playerDisplayName ?? "").Replace("<", "&lt;").Replace(">", "&gt;");
        var safeTail = (tailAfterName ?? "").Replace("<", "&lt;").Replace(">", "&gt;");
        return $"<color=#{hex}>{safeName}</color>{safeTail}";
    }

    /// <summary>Rich system line: "You and NAME are now DMing eachother."</summary>
    public static string BuildMutualDmLine(string peerDisplayName, string? peerNameColorHex)
    {
        var hex = NormalizeHexForPacket(peerNameColorHex) ?? "87CEEB";
        var safeName = (peerDisplayName ?? "").Replace("<", "&lt;").Replace(">", "&gt;");
        return "You and " + $"<color=#{hex}>{safeName}</color> are now DMing eachother.";
    }

    /// <summary>Sends encrypted voice. In DM mode, <see cref="VoiceMessagePacket.TargetUserId"/> is the intended recipient.</summary>
    /// <returns>True if the voice packet was sent.</returns>
    /// <remarks>Same broadcast + client filter model as text chat; see <see cref="SendMessage"/>.</remarks>
    public bool SendVoiceMessage(byte[] voicePlainBlob)
    {
        if (voicePlainBlob == null || voicePlainBlob.Length == 0)
            return false;

        var now = Time.realtimeSinceStartup;
        if (_lastOutgoingChatOrVoiceAt.HasValue && now - _lastOutgoingChatOrVoiceAt.Value < OutgoingChatOrVoiceCooldownSeconds)
        {
            PostSystemMessage("Woah there! Sorry about this, some sort of spam prevention had to be in place...");
            return false;
        }

        if (!ChatPersistentId.IsValidFormat(ChatPersistentId.Current))
        {
            MultiplayerChat.Plugin.Log?.Warn("[MPChat] Cannot send voice: invalid local Chat ID.");
            return false;
        }

        if (_dmState.IsInDMMode)
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

        var voicePacket = new VoiceMessagePacket
        {
            EncryptedPayload = encrypted,
            NameColor = NormalizeHexForPacket(ModSettings.NameColor),
            SenderChatId = ChatPersistentId.Current
        };
        if (_dmState.IsInDMMode)
        {
            voicePacket.TargetUserId = _dmState.DMTargetUserId;
            voicePacket.TargetChatId = _dmState.DMTargetChatId;
        }

        _sessionManager.Send(voicePacket);
        _lastOutgoingChatOrVoiceAt = Time.realtimeSinceStartup;

        var localPlayer = _sessionManager.localPlayer;
        if (localPlayer != null)
        {
            var name = string.IsNullOrEmpty(localPlayer.userName) ? "Player" : localPlayer.userName;
            var localHex = NormalizeHexForPacket(ModSettings.NameColor);
            if (_dmState.IsInDMMode)
                PostSystemMessageRich(SystemLineWithColoredPlayerName(name, " sent a DM Voice Message", localHex));
            else
                PostSystemMessageRich(SystemLineWithColoredPlayerName(name, " has sent a voice message", localHex));
        }

        return true;
    }

    private void OnVoicePacketReceived(VoiceMessagePacket packet, IConnectedPlayer sender)
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

        UpdateEncryptionKey();
        var decrypted = _encryption.DecryptToBytes(packet.EncryptedPayload);
        if (decrypted == null)
            return;

        var name = string.IsNullOrEmpty(sender.userName) ? "Player" : sender.userName;
        if (isDm)
            PostSystemMessageRich(SystemLineWithColoredPlayerName(name, " sent a DM Voice Message", packet.NameColor));
        else
            PostSystemMessageRich(SystemLineWithColoredPlayerName(name, " has sent a voice message", packet.NameColor));

        EnqueueVoicePlayback(decrypted);
    }

    private void EnqueueVoicePlayback(byte[] decodedBlob)
    {
        _voicePlaybackQueue.Enqueue(decodedBlob);
        if (!_voicePlaybackRunning)
            _voicePlaybackCoroutine = _coroutineHost.StartCoroutine(VoicePlaybackQueueRunner());
    }

    private IEnumerator VoicePlaybackQueueRunner()
    {
        _voicePlaybackRunning = true;
        try
        {
            while (_voicePlaybackQueue.Count > 0)
            {
                var blob = _voicePlaybackQueue.Dequeue();
                var clip = VoiceMessageCodec.CreateAudioClip(blob);
                if (clip == null)
                {
                    MultiplayerChat.Plugin.Log?.Warn("[MPChat] Could not decode voice message");
                    continue;
                }
                yield return PlayVoiceClipCoroutine(clip);
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

    private IEnumerator PlayVoiceClipCoroutine(AudioClip clip)
    {
        var go = new GameObject("MPChatVoicePlayback");
        _voicePlaybackGameObject = go;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = 1f;
        src.spatialBlend = 0f;
        src.bypassEffects = true;
        src.bypassListenerEffects = true;
        src.bypassReverbZones = true;
        src.Play();
        yield return new WaitForSeconds(clip.length + 0.08f);
        UnityEngine.Object.Destroy(clip);
        if (_voicePlaybackGameObject == go)
            _voicePlaybackGameObject = null;
        UnityEngine.Object.Destroy(go);
    }

}

public class ChatMessageEventArgs : EventArgs
{
    public string UserName { get; }
    public string Message { get; }
    public string UserId { get; }
    public bool IsDM { get; }
    public bool IsSystem { get; }
    /// <summary>Sender's name color as 6-char hex (e.g. "87CEEB"). Null = use default.</summary>
    public string? NameColor { get; }

    /// <summary>When true with <see cref="IsSystem"/>, <see cref="Message"/> is preformatted TMP rich text (tags already balanced; user text escaped by sender).</summary>
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
