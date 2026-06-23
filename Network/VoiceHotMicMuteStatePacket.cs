using LiteNetLib.Utils;

namespace MultiplayerChat.Network;

public class VoiceHotMicMuteStatePacket : MultiplayerCore.Networking.Abstractions.MpPacket
{
    public bool IsHotMicMuted;
    public string? SenderChatId;
    public string? SenderNameColor;

    public override void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)(IsHotMicMuted ? 1 : 0));
        writer.Put(SenderChatId ?? "");
        writer.Put(SenderNameColor ?? "");
    }

    public override void Deserialize(NetDataReader reader)
    {
        IsHotMicMuted = false;
        SenderChatId = null;
        SenderNameColor = null;
        if (reader.AvailableBytes <= 0)
            return;
        IsHotMicMuted = reader.GetByte() != 0;
        if (reader.AvailableBytes > 0)
        {
            var s = reader.GetString();
            if (!string.IsNullOrEmpty(s))
                SenderChatId = s;
        }

        if (reader.AvailableBytes > 0)
        {
            var c = reader.GetString();
            if (!string.IsNullOrEmpty(c))
                SenderNameColor = c;
        }
    }
}
