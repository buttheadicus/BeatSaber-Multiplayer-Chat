using System;
using LiteNetLib.Utils;

namespace MultiplayerChat.Network;

// Encrypted VMSG or hot-mic chunk; DM fills TargetUserId and TargetChatId.
public class VoiceMessagePacket : MultiplayerCore.Networking.Abstractions.MpPacket
{
    internal const int MaxPlaintextVoiceBytes = 4_194_304;

    private const int MaxEncryptedPayloadSize = MaxPlaintextVoiceBytes + 4096;

    public byte[]? EncryptedPayload;

    public string? TargetUserId;

    public string? NameColor;

    public string? SenderChatId;

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
            // Blob first; trailing strings are optional so older packet layouts still deserialize.
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
                    TargetUserId = t.Trim();
            }
            if (reader.AvailableBytes > 0)
            {
                var c = reader.GetString();
                if (!string.IsNullOrEmpty(c))
                    NameColor = c.Trim();
            }
            if (reader.AvailableBytes > 0)
            {
                var sid = reader.GetString();
                if (!string.IsNullOrEmpty(sid))
                    SenderChatId = sid.Trim();
            }
            if (reader.AvailableBytes > 0)
            {
                var tc = reader.GetString();
                if (!string.IsNullOrEmpty(tc))
                    TargetChatId = tc.Trim();
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
