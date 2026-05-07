using LiteNetLib.Utils;

namespace MultiplayerChat.Network;

public class ChatActivityPacket : MultiplayerCore.Networking.Abstractions.MpPacket
{
    public const byte TypingStart = 1;
    public const byte TypingStop = 2;
    public const byte RecordingVoiceStart = 3;
    public const byte RecordingVoiceStop = 4;

    public byte Activity;

    public string? SenderChatId;
    public string? SenderNameColor;

    public override void Serialize(NetDataWriter writer)
    {
        writer.Put(Activity);
        writer.Put(SenderChatId ?? "");
        writer.Put(SenderNameColor ?? "");
    }

    public override void Deserialize(NetDataReader reader)
    {
        Activity = 0;
        SenderChatId = null;
        SenderNameColor = null;
        if (reader.AvailableBytes <= 0)
            return;
        Activity = reader.GetByte();
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
