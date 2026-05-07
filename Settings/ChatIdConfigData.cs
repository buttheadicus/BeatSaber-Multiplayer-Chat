using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MultiplayerChat.Settings;

[DataContract]
public class ChatIdConfigData
{
    [DataMember(Name = "schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [DataMember(Name = "mutedChatIds")]
    public List<string> MutedChatIds { get; set; } = new();

    [DataMember(Name = "mutedPlatformUserIds")]
    public List<string> MutedPlatformUserIds { get; set; } = new();
}
