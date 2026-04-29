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

    /// <summary>
    /// Sender's name color as 6-char hex (e.g. "87CEEB"). Used by other clients to display username in correct color.
    /// </summary>
    public string? NameColor;

    /// <summary>Sender's persistent 8-digit Chat ID (0.2.0 required).</summary>
    public string? SenderChatId;

    /// <summary>When set with <see cref="TargetUserId"/>, DM recipient's Chat ID (0.2.0).</summary>
    public string? TargetChatId;

    public override void Serialize(NetDataWriter writer)
    {
        writer.PutBytesWithLength(EncryptedPayload ?? Array.Empty<byte>());
        writer.Put(TargetUserId ?? "");
        writer.Put(NormalizeHex(NameColor) ?? "");
        writer.Put(SenderChatId ?? "");
        writer.Put(TargetChatId ?? "");
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
                NameColor = null;
                SenderChatId = null;
                TargetChatId = null;
                return;
            }
            if (payload.Length > MaxPayloadSize)
            {
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Rejected oversized packet: {payload.Length} bytes");
                EncryptedPayload = null;
                TargetUserId = null;
                NameColor = null;
                SenderChatId = null;
                TargetChatId = null;
                return;
            }
            EncryptedPayload = payload;
            TargetUserId = null;
            NameColor = null;
            SenderChatId = null;
            TargetChatId = null;
            if (reader.AvailableBytes > 0)
            {
                var target = reader.GetString();
                if (!string.IsNullOrEmpty(target))
                    TargetUserId = target;
            }
            if (reader.AvailableBytes > 0)
            {
                var color = reader.GetString();
                if (!string.IsNullOrEmpty(color))
                    NameColor = NormalizeHex(color);
            }
            if (reader.AvailableBytes > 0)
            {
                var sid = reader.GetString();
                if (!string.IsNullOrEmpty(sid))
                    SenderChatId = sid;
            }
            if (reader.AvailableBytes > 0)
            {
                var tc = reader.GetString();
                if (!string.IsNullOrEmpty(tc))
                    TargetChatId = tc;
            }
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Failed to deserialize packet: {ex.Message}");
            EncryptedPayload = null;
            TargetUserId = null;
            NameColor = null;
            SenderChatId = null;
            TargetChatId = null;
        }
    }

    private static string? NormalizeHex(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        var h = hex!.Trim();
        if (h.StartsWith("#")) h = h.Substring(1);
        if (h.Length > 6) h = h.Substring(0, 6);
        return h.Length == 6 ? h : null;
    }
}
