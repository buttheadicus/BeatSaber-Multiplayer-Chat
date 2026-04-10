using LiteNetLib.Utils;

namespace MultiplayerChat.Network;

/// <summary>
/// Sent with the first DM message of a session so the recipient sees a one-time system line.
/// Only <see cref="TargetUserId"/> should handle it (the intended DM recipient).
/// </summary>
public class DmIntroNotifyPacket : MultiplayerCore.Networking.Abstractions.MpPacket
{
    public string? TargetUserId;

    /// <summary>Sender's username color as 6-char hex without # (optional; old clients omit).</summary>
    public string? SenderNameColor;

    /// <summary>Sender's persistent Chat ID (0.2.0).</summary>
    public string? SenderChatId;

    /// <summary>Recipient's Chat ID; must match the local player's persistent ID (0.2.0).</summary>
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
