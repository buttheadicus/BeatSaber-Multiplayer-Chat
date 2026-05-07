using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MultiplayerChat.Settings;

[DataContract]
public class LearnedChatIdsData
{
    [DataMember(Name = "schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [DataMember(Name = "entries")]
    public List<LearnedIdEntry> Entries { get; set; } = new();
}

[DataContract]
public class LearnedIdEntry
{
    [DataMember(Name = "platformUserId")]
    public string? PlatformUserId { get; set; }

    [DataMember(Name = "chatId")]
    public string? ChatId { get; set; }
}
