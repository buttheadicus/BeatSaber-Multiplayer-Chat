using LiteNetLib.Utils;

namespace MultiplayerChat.Network;

// Lobby discovery and SenderChatId publication; targeted variants carry TargetUserId for reply routing.
public class ModPresencePacket : MultiplayerCore.Networking.Abstractions.MpPacket
{
    public string? TargetUserId;

    public bool IsIgnoredFromSong;

    public string? SenderChatId;

    public string? SenderNameColor;

    public bool IsSlzCompanionClient;

    public bool HasLobbyCustomAvatarsEnabled;

    public bool VoiceIsDeafened;

    public bool VoiceIsHotMicMuted;

    public override void Serialize(NetDataWriter writer)
    {
        writer.Put(TargetUserId ?? "");
        writer.Put((byte)(IsIgnoredFromSong ? 1 : 0));
        writer.Put(SenderChatId ?? "");
        writer.Put(SenderNameColor ?? "");
        writer.Put((byte)(IsSlzCompanionClient ? 1 : 0));
        writer.Put((byte)(HasLobbyCustomAvatarsEnabled ? 1 : 0));
        writer.Put((byte)(VoiceIsDeafened ? 1 : 0));
        writer.Put((byte)(VoiceIsHotMicMuted ? 1 : 0));
    }

    public override void Deserialize(NetDataReader reader)
    {
        if (reader.AvailableBytes > 0)
        {
            var target = reader.GetString();
            TargetUserId = string.IsNullOrEmpty(target) ? null : target;
        }
        else
        {
            TargetUserId = null;
        }

        IsIgnoredFromSong = reader.AvailableBytes > 0 && reader.GetByte() != 0; // backward compat: old packets have no byte

        SenderChatId = null;
        if (reader.AvailableBytes > 0)
        {
            var id = reader.GetString();
            if (!string.IsNullOrEmpty(id))
                SenderChatId = id;
        }

        SenderNameColor = null;
        if (reader.AvailableBytes > 0)
        {
            var c = reader.GetString();
            if (!string.IsNullOrEmpty(c))
                SenderNameColor = c;
        }

        IsSlzCompanionClient = reader.AvailableBytes > 0 && reader.GetByte() != 0;

        HasLobbyCustomAvatarsEnabled = reader.AvailableBytes > 0 && reader.GetByte() != 0;

        VoiceIsDeafened = reader.AvailableBytes > 0 && reader.GetByte() != 0;
        VoiceIsHotMicMuted = reader.AvailableBytes > 0 && reader.GetByte() != 0;
    }
}
