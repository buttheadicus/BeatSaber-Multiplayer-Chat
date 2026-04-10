using System;
using LiteNetLib.Utils;

namespace MultiplayerChat.Network;

/// <summary>
/// End-to-end encrypted voice payload (encoded by <see cref="MultiplayerChat.Core.VoiceMessageCodec"/>).
/// </summary>
public class VoiceMessagePacket : MultiplayerCore.Networking.Abstractions.MpPacket
{
    /// <summary>Max voice blob size (bytes) before encryption (~24s mono @ 44.1kHz float PCM).</summary>
    internal const int MaxPlaintextVoiceBytes = 4_194_304;

    /// <summary>Max encrypted payload on the wire (generous upper bound).</summary>
    private const int MaxEncryptedPayloadSize = MaxPlaintextVoiceBytes + 4096;

    public byte[]? EncryptedPayload;

    /// <summary>When set, only sender and this user receive/play the voice (DM).</summary>
    public string? TargetUserId;

    /// <summary>Sender's username color as 6-char hex without # (optional; old clients omit).</summary>
    public string? NameColor;

    /// <summary>Sender's persistent 8-digit Chat ID (0.2.0 required).</summary>
    public string? SenderChatId;

    /// <summary>DM recipient's Chat ID when <see cref="TargetUserId"/> is set (0.2.0).</summary>
    public string? TargetChatId;

    public override void Serialize(NetDataWriter writer)
    {
        writer.PutBytesWithLength(EncryptedPayload ?? Array.Empty<byte>());
        writer.Put(TargetUserId ?? "");
        writer.Put(NameColor ?? "");
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

            if (payload.Length > MaxEncryptedPayloadSize)
            {
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Rejected oversized voice packet: {payload.Length} bytes");
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
                var t = reader.GetString();
                if (!string.IsNullOrEmpty(t))
                    TargetUserId = t;
            }
            if (reader.AvailableBytes > 0)
            {
                var c = reader.GetString();
                if (!string.IsNullOrEmpty(c))
                    NameColor = c;
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
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat] Failed to deserialize voice packet: {ex.Message}");
            EncryptedPayload = null;
            TargetUserId = null;
            NameColor = null;
            SenderChatId = null;
            TargetChatId = null;
        }
    }
}
