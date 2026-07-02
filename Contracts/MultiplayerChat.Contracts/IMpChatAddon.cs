using System;

namespace MultiplayerChat.Contracts;

public interface IMpChatAddon
{
    string Id { get; }

    string DisplayName { get; }

    Version Version { get; }

    void OnLoad(IMpChatHost host);

    void OnUnload();
}
