using LiteNetLib.Utils;

namespace MultiplayerChat.Network;

/// <summary>
/// Sent when the sender ends DM mode; only <see cref="TargetUserId"/> should show the system line.
/// </summary>
public class DmStoppedNotifyPacket : MultiplayerCore.Networking.Abstractions.MpPacket
{
    public string? TargetUserId;

    /// <summary>Sender's username color as 6-char hex without # (optional).</summary>
    public string? SenderNameColor;

    public string? SenderChatId;

    /// <summary>Recipient's Chat ID (player who receives "stopped DMing you").</summary>
    public string? TargetChatId;

    public override void Serialize(NetDataWriter writer)
    {
        writer.Put(TargetUserId ?? "");
        writer.Put(SenderNameColor ?? "");
        writer.Put(SenderChatId ?? "");
        writer.Put(TargetChatId ?? "");
    }

    public override void Deserialize(NetDataReader reader)
    {
        TargetUserId = null;
        SenderNameColor = null;
        SenderChatId = null;
        TargetChatId = null;
        if (reader.AvailableBytes <= 0)
            return;
        var t = reader.GetString();
        if (!string.IsNullOrEmpty(t))
            TargetUserId = t;
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
