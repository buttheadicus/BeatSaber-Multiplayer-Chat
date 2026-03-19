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

    public override void Serialize(NetDataWriter writer)
    {
        writer.PutBytesWithLength(EncryptedPayload ?? Array.Empty<byte>());
        writer.Put(TargetUserId ?? "");
        writer.Put(NormalizeHex(NameColor) ?? "");
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
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Rejected oversized packet: {payload.Length} bytes");
                EncryptedPayload = null;
                TargetUserId = null;
                return;
            }
            EncryptedPayload = payload;
            TargetUserId = null;
            NameColor = null;
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
        }
        catch (Exception ex)
        {
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Failed to deserialize packet: {ex.Message}");
            EncryptedPayload = null;
            TargetUserId = null;
            NameColor = null;
        }
    }

    private static string? NormalizeHex(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        hex = hex.Trim();
        if (hex.StartsWith("#")) hex = hex.Substring(1);
        if (hex.Length > 6) hex = hex.Substring(0, 6);
        return hex.Length == 6 ? hex : null;
    }
}
