using LiteNetLib.Utils;

namespace MultiplayerChat.Network;

/// <summary>
/// Notifies the target user that the sender has muted or unmuted them (client-side mute).
/// Only <see cref="TargetUserId"/> should handle it (the player who was muted/unmuted).
/// </summary>
public class MuteNotifyPacket : MultiplayerCore.Networking.Abstractions.MpPacket
{
    public string? TargetUserId;
    public bool IsMuted;

    /// <summary>Sender's username color as 6-char hex without # (optional).</summary>
    public string? SenderNameColor;

    public string? SenderChatId;

    /// <summary>Recipient's Chat ID (the player who was muted/unmuted).</summary>
    public string? TargetChatId;

    public override void Serialize(NetDataWriter writer)
    {
        writer.Put(TargetUserId ?? "");
        writer.Put((byte)(IsMuted ? 1 : 0));
        writer.Put(SenderNameColor ?? "");
        writer.Put(SenderChatId ?? "");
        writer.Put(TargetChatId ?? "");
    }

    public override void Deserialize(NetDataReader reader)
    {
        TargetUserId = null;
        IsMuted = false;
        SenderNameColor = null;
        SenderChatId = null;
        TargetChatId = null;
        if (reader.AvailableBytes <= 0)
            return;
        var t = reader.GetString();
        if (!string.IsNullOrEmpty(t))
            TargetUserId = t;
        if (reader.AvailableBytes > 0)
            IsMuted = reader.GetByte() != 0;
        if (reader.AvailableBytes > 0)
        {
            var c = reader.GetString();
            if (!string.IsNullOrEmpty(c))
                SenderNameColor = c;
        }
        if (reader.AvailableBytes > 0)
        {
            var s = reader.GetString();
            if (!string.IsNullOrEmpty(s))
                SenderChatId = s;
        }
        if (reader.AvailableBytes > 0)
        {
            var tc = reader.GetString();
            if (!string.IsNullOrEmpty(tc))
                TargetChatId = tc;
        }
    }
}
