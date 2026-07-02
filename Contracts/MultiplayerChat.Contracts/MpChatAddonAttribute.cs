using System;

namespace MultiplayerChat.Contracts;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MpChatAddonAttribute : Attribute
{
    public MpChatAddonAttribute(string id)
    {
        Id = id;
    }

    public string Id { get; }
}
