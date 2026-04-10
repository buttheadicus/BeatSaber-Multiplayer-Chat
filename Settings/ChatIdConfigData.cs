using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MultiplayerChat.Settings;

/// <summary>JSON model for ChatIDConfig.dat (mute lists and future ID-scoped options).</summary>
[DataContract]
public class ChatIdConfigData
{
    [DataMember(Name = "schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Mute by others' persistent 8-digit chat IDs (survives restarts).</summary>
    [DataMember(Name = "mutedChatIds")]
    public List<string> MutedChatIds { get; set; } = new();

    /// <summary>Provisional mutes before we learn the player's chat ID (same session / until presence).</summary>
    [DataMember(Name = "mutedPlatformUserIds")]
    public List<string> MutedPlatformUserIds { get; set; } = new();
}
