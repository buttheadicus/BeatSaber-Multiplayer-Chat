using System;
using SiraUtil.Objects.Multiplayer;

namespace MultiplayerChat.Core.Addons;

internal static class AddonGameplayBridge
{
    private static Action<MultiplayerConnectedPlayerFacade>? _arenaAttachHandler;

    internal static void SetArenaAttachHandler(Action<MultiplayerConnectedPlayerFacade>? handler) =>
        _arenaAttachHandler = handler;

    internal static void RefreshArenaAttach(MultiplayerConnectedPlayerFacade facade) =>
        _arenaAttachHandler?.Invoke(facade);

    internal static void Clear() => _arenaAttachHandler = null;
}
