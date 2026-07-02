using SiraUtil.Objects.Multiplayer;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core;

internal static class MpChatLobbyAvatarZenject
{
    internal static void TryInjectFromFacade(MultiplayerConnectedPlayerFacade facade, MonoBehaviour target) =>
        TryInjectFromFacadeRoot(facade != null ? facade.transform : null, target);

    internal static void TryInjectFromFacadeRoot(Transform? facadeRoot, MonoBehaviour target)
    {
        if (target == null)
            return;

        if (facadeRoot != null)
        {
            try
            {
                var playerContext = FindPlayerContext(facadeRoot);
                if (playerContext != null)
                {
                    playerContext.Container.Inject(target);
                    return;
                }
            }
            catch
            {
            }
        }

        TryInject(target);
    }

    private static GameObjectContext? FindPlayerContext(Transform from)
    {
        var local = from.GetComponentInParent<MultiplayerLocalActivePlayerFacade>();
        var root = local != null
            ? local.transform
            : from.GetComponentInParent<MultiplayerConnectedPlayerFacade>()?.transform;
        return root != null ? root.GetComponent<GameObjectContext>() : null;
    }

    internal static void TryInject(MonoBehaviour target)
    {
        if (target == null)
            return;

        try
        {
            var playerContext = target.GetComponentInParent<GameObjectContext>(true);
            if (playerContext != null)
            {
                playerContext.Container.Inject(target);
                return;
            }

            var sceneContext = target.GetComponentInParent<SceneContext>(true);
            if (sceneContext == null)
            {
                foreach (var ctx in Object.FindObjectsOfType<SceneContext>())
                {
                    if (ctx.gameObject.scene == target.gameObject.scene)
                    {
                        sceneContext = ctx;
                        break;
                    }
                }
            }

            if (sceneContext != null)
            {
                sceneContext.Container.Inject(target);
                return;
            }

            ProjectContext.Instance?.Container.Inject(target);
        }
        catch
        {
        }
    }
}
