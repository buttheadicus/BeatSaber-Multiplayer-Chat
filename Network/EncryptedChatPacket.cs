using System;
using LiteNetLib.Utils;

namespace MultiplayerChat.Network;

// Text chat payload; DM mode sets TargetUserId and TargetChatId for routing and decryption scope.
public class EncryptedChatPacket : MultiplayerCore.Networking.Abstractions.MpPacket
{
    private const int MaxPayloadSize = 4096;

    public byte[]? EncryptedPayload;

    public string? TargetUserId;

    public string? NameColor;

    public string? SenderChatId;

    public string? TargetChatId;

    /// <summary>
    /// Optional display name override (e.g. "BOT" for SLZ). Appended for forward compat; old clients ignore.
    /// </summary>
    public string? DisplayNameOverride;

    public override void Serialize(NetDataWriter writer)
    {
        writer.PutBytesWithLength(EncryptedPayload ?? Array.Empty<byte>());
        writer.Put(TargetUserId ?? "");
        writer.Put(NormalizeHex(NameColor) ?? "");
        writer.Put(SenderChatId ?? "");
        writer.Put(TargetChatId ?? "");
        writer.Put(DisplayNameOverride ?? "");
    }

    public override void Deserialize(NetDataReader reader)
    {
        try
        {
            // Blob first; routing and Chat ID fields follow only if bytes remain (compat with minimal payloads).
            var payload = reader.GetBytesWithLength();
            if (payload == null || payload.Length == 0)
            {
                EncryptedPayload = null;
                TargetUserId = null;
                NameColor = null;
                SenderChatId = null;
                TargetChatId = null;
                DisplayNameOverride = null;
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
                DisplayNameOverride = null;
                return;
            }
            EncryptedPayload = payload;
            TargetUserId = null;
            NameColor = null;
            SenderChatId = null;
            TargetChatId = null;
            DisplayNameOverride = null;
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
            if (reader.AvailableBytes > 0)
            {
                var display = reader.GetString();
                if (!string.IsNullOrEmpty(display))
                    DisplayNameOverride = display;
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
            DisplayNameOverride = null;
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
