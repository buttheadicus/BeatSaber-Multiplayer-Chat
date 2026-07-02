using System.Collections.Generic;
using MultiplayerChat.Contracts;

namespace MultiplayerChat.Core.Addons;

internal static class AddonLobbyAvatarBridge
{
    private static readonly List<IMpChatLobbyAvatarHook> Hooks = new();

    internal static void Register(IMpChatLobbyAvatarHook hook)
    {
        if (hook == null || Hooks.Contains(hook))
            return;
        Hooks.Add(hook);
    }

    internal static void Unregister(IMpChatLobbyAvatarHook hook)
    {
        if (hook == null)
            return;
        Hooks.Remove(hook);
    }

    internal static void Clear() => Hooks.Clear();

    internal static void DecorateLobbyAvatar(object lobbyAvatarController)
    {
        for (var i = 0; i < Hooks.Count; i++)
            Hooks[i].DecorateLobbyAvatar(lobbyAvatarController);
    }

    internal static void DecorateLobbyAvatarPlace(object lobbyAvatarPlace)
    {
        for (var i = 0; i < Hooks.Count; i++)
            Hooks[i].DecorateLobbyAvatarPlace(lobbyAvatarPlace);
    }
}
