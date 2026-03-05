using System;
using System.Linq;
using MultiplayerChat.Network;
using MultiplayerCore.Models;
using MultiplayerCore.Networking;
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

    public void Initialize()
    {
        Instance = this;
        _packetSerializer.RegisterCallback<EncryptedChatPacket>(OnPacketReceived);
        _sessionManager.playerConnectedEvent += OnPlayerConnected;
        _sessionManager.playerDisconnectedEvent += OnPlayerDisconnected;
        UpdateEncryptionKey();
    }

    public void Dispose()
    {
        Instance = null;
        _packetSerializer.UnregisterCallback<EncryptedChatPacket>();
        _sessionManager.playerConnectedEvent -= OnPlayerConnected;
        _sessionManager.playerDisconnectedEvent -= OnPlayerDisconnected;
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

    /// <summary>
    /// Sends an encrypted chat message. When in DM mode, only sender and target display it.
    /// </summary>
    public void SendMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        text = text.Trim();
        if (text.Length > 500)
            text = text.Substring(0, 500);

        UpdateEncryptionKey(); // Refresh key before send (session may not have been ready at init)
        var encrypted = _encryption.Encrypt(text);
        if (encrypted == null)
        {
            MultiplayerChat.Plugin.Log?.Warn("[E2EChat] Encrypt returned null (no session key?)");
            return;
        }

        var packet = new EncryptedChatPacket { EncryptedPayload = encrypted };
        if (_dmState.IsInDMMode)
            packet.TargetUserId = _dmState.DMTargetUserId;
        _sessionManager.Send(packet);
        MultiplayerChat.Plugin.Log?.Info($"[E2EChat] Sent message, invoking MessageReceived");

        // Show our own message locally for immediate feedback
        var localPlayer = _sessionManager.localPlayer;
        if (localPlayer != null)
        {
            var isDm = _dmState.IsInDMMode;
            MessageReceived?.Invoke(this, new ChatMessageEventArgs(localPlayer.userName, text, localPlayer.userId, isDm));
        }
    }

    private void OnPacketReceived(EncryptedChatPacket packet, IConnectedPlayer sender)
    {
        if (packet.EncryptedPayload == null || packet.EncryptedPayload.Length == 0)
            return;

        if (_muteManager.IsMuted(sender.userId))
            return;

        var localPlayer = _sessionManager.localPlayer;
        if (packet.TargetUserId != null)
        {
            if (localPlayer == null) return;
            var myId = localPlayer.userId;
            if (myId != packet.TargetUserId && myId != sender.userId)
                return;
        }

        UpdateEncryptionKey();

        var decrypted = _encryption.Decrypt(packet.EncryptedPayload);
        if (decrypted == null)
            return;

        decrypted = decrypted.Replace("<", "&lt;").Replace(">", "&gt;");
        var isDm = packet.TargetUserId != null;
        MessageReceived?.Invoke(this, new ChatMessageEventArgs(sender.userName, decrypted, sender.userId, isDm));
    }

    /// <summary>Post a system message to the chat (e.g. "USERNAME has chat! They can see your messages!").</summary>
    public void PostSystemMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        MessageReceived?.Invoke(this, new ChatMessageEventArgs("", message, "", false, isSystem: true));
    }
}

public class ChatMessageEventArgs : EventArgs
{
    public string UserName { get; }
    public string Message { get; }
    public string UserId { get; }
    public bool IsDM { get; }
    public bool IsSystem { get; }

    public ChatMessageEventArgs(string userName, string message, string userId, bool isDm = false, bool isSystem = false)
    {
        UserName = userName;
        Message = message;
        UserId = userId;
        IsDM = isDm;
        IsSystem = isSystem;
    }
}
