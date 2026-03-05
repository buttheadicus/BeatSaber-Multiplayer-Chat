using LiteNetLib.Utils;

namespace MultiplayerChat.Network;

/// <summary>
/// Packet sent when joining a lobby to indicate this player has the E2E Chat mod.
/// TargetUserId: when set, only that user should process it (targeted reply or ignored).
/// IsIgnoredFromSong: when true, recipient is in a song and should retry later.
/// </summary>
public class ModPresencePacket : MultiplayerCore.Networking.Abstractions.MpPacket
{
    /// <summary>When set, only this user should process the packet.</summary>
    public string? TargetUserId;

    /// <summary>When true, sender received presence but was in a song - retry in 3 seconds.</summary>
    public bool IsIgnoredFromSong;

    public override void Serialize(NetDataWriter writer)
    {
        writer.Put(TargetUserId ?? "");
        writer.Put((byte)(IsIgnoredFromSong ? 1 : 0));
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
    }
}
