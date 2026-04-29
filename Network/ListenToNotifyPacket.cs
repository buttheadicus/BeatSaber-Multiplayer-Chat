using LiteNetLib.Utils;

namespace MultiplayerChat.Network;

/// <summary>
/// Notifies a player that the sender added or removed them in listen-only voice routing.
/// Only <see cref="TargetUserId"/> (recipient) should handle it.
/// </summary>
public class ListenToNotifyPacket : MultiplayerCore.Networking.Abstractions.MpPacket
{
    public string? TargetUserId;

    public bool IsStopped;

    public string? SenderNameColor;

    public string? SenderChatId;

    public string? TargetChatId;

    /// <summary>Comma-separated platform user ids (other listen targets besides <see cref="TargetUserId"/>).</summary>
    public string? AlsoListeningToOthersCsv;

    public override void Serialize(NetDataWriter writer)
    {
        writer.Put(TargetUserId ?? "");
        writer.Put((byte)(IsStopped ? 1 : 0));
        writer.Put(SenderNameColor ?? "");
        writer.Put(SenderChatId ?? "");
        writer.Put(TargetChatId ?? "");
        writer.Put(AlsoListeningToOthersCsv ?? "");
    }

    public override void Deserialize(NetDataReader reader)
    {
        TargetUserId = null;
        IsStopped = false;
        SenderNameColor = null;
        SenderChatId = null;
        TargetChatId = null;
        AlsoListeningToOthersCsv = null;
        if (reader.AvailableBytes <= 0)
            return;
        var t = reader.GetString();
        if (!string.IsNullOrEmpty(t))
            TargetUserId = t;
        if (reader.AvailableBytes > 0)
            IsStopped = reader.GetByte() != 0;
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

        if (reader.AvailableBytes > 0)
        {
            var csv = reader.GetString();
            if (!string.IsNullOrEmpty(csv))
                AlsoListeningToOthersCsv = csv;
        }
    }
}
