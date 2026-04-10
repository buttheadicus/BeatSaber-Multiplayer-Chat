using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MultiplayerChat.Settings;

/// <summary>JSON model for LearnedIDs.dat (platform user id → persistent chat id).</summary>
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
