using System;
using LiteNetLib.Utils;

namespace MultiplayerChat.Network;

/// <summary>
/// Packet containing end-to-end encrypted chat message.
/// Only players in the lobby can decrypt - the server relays encrypted bytes.
/// </summary>
public class EncryptedChatPacket : MultiplayerCore.Networking.Abstractions.MpPacket
{
    private const int MaxPayloadSize = 4096;

    /// <summary>
    /// Encrypted message bytes (AES-256-CBC + HMAC). Format: IV + Ciphertext + HMAC.
    /// </summary>
    public byte[]? EncryptedPayload;

    /// <summary>
    /// When set, this is a DM - only sender and this user should display the message.
    /// </summary>
    public string? TargetUserId;

    public override void Serialize(NetDataWriter writer)
    {
        writer.PutBytesWithLength(EncryptedPayload ?? Array.Empty<byte>());
        writer.Put(TargetUserId ?? "");
    }

    public override void Deserialize(NetDataReader reader)
    {
        try
        {
            var payload = reader.GetBytesWithLength();
            if (payload == null || payload.Length == 0)
            {
                EncryptedPayload = null;
                TargetUserId = null;
                return;
            }
            if (payload.Length > MaxPayloadSize)
            {
                MultiplayerChat.Plugin.Log?.Warn($"[E2EChat] Rejected oversized packet: {payload.Length} bytes");
                EncryptedPayload = null;
                TargetUserId = null;
                return;
            }
            EncryptedPayload = payload;
            TargetUserId = null;
            if (reader.AvailableBytes > 0)
            {
                var target = reader.GetString();
                if (!string.IsNullOrEmpty(target))
                    TargetUserId = target;
            }
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[E2EChat] Failed to deserialize packet: {ex.Message}");
            EncryptedPayload = null;
            TargetUserId = null;
        }
    }
}
