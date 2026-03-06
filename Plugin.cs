using System.Reflection;
using MultiplayerChat.Core;
using UnityEngine;
using MultiplayerChat.UI;
using HarmonyLib;
using IPA;
using SiraUtil.Extras;
using SiraUtil.Objects.Multiplayer;
using SiraUtil.Zenject;
using Zenject;
using IPALogger = IPA.Logging.Logger;

namespace MultiplayerChat;

[Plugin(RuntimeOptions.DynamicInit)]
public class Plugin
{
    internal static IPALogger Log { get; private set; } = null!;

    private Harmony? _harmony;

    [Init]
    public void Init(IPALogger logger, Zenjector zenjector)
    {
        Log = logger;
        zenjector.UseLogger(logger);
        _harmony = new Harmony("MultiplayerChat");
        _harmony.PatchAll(Assembly.GetExecutingAssembly());

        // Main menu: version check, update UI (FlowCoordinator + menu tab)
        zenjector.Install(Location.Menu, container =>
        {
            container.Bind<UpdateMessageViewController>().FromNewComponentAsViewController().AsTransient();
            container.Bind<UpdateFlowCoordinator>().FromNewComponentOnNewGameObject().AsTransient();
            container.BindInterfacesAndSelfTo<VersionChecker>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
        });

        zenjector.Install<MultiplayerLobbyInstaller>(container =>
        {
            InstallChatBindings(container);
        });
    }

    private static void InstallChatBindings(DiContainer container)
    {
        container.BindInterfacesAndSelfTo<EncryptionManager>().AsSingle();
        container.Bind<ChatMuteManager>().AsSingle();
        container.Bind<ChatDMState>().AsSingle();
        container.BindInterfacesAndSelfTo<ChatManager>().AsSingle();
        container.BindInterfacesAndSelfTo<ModPresenceManager>().AsSingle();
        container.Bind<CoroutineHost>().FromNewComponentOnNewGameObject().AsSingle();
        container.BindInterfacesAndSelfTo<ChatPresenceNotifier>().AsSingle().NonLazy();
        container.BindInterfacesAndSelfTo<ChatBubbleManager>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
        container.BindInterfacesAndSelfTo<FloorChatButton>().FromNewComponentOnNewGameObject().AsSingle();
        container.Bind<LobbyChatTabHost>().AsSingle();
        container.Bind<PlayerListViewController>().FromNewComponentAsViewController().AsTransient();
        container.Bind<PlayerListFlowCoordinator>().FromNewComponentOnNewGameObject().AsTransient();
        container.BindInterfacesAndSelfTo<LobbyChatTabRegistrar>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
        container.RegisterRedecorator(new LobbyAvatarRegistration(DecorateAvatar));
        container.RegisterRedecorator(new LobbyAvatarPlaceRegistration(DecorateAvatarPlace));
    }

    private static MultiplayerLobbyAvatarController DecorateAvatar(MultiplayerLobbyAvatarController original)
    {
        AddChatBubbleAnchorToCaption(original.transform);
        return original;
    }

    private static MultiplayerLobbyAvatarPlace DecorateAvatarPlace(MultiplayerLobbyAvatarPlace original)
    {
        AddChatBubbleAnchorToCaption(original.transform);
        return original;
    }

    private static void AddChatBubbleAnchorToCaption(Transform root)
    {
        var avatarCaption = root.Find("AvatarCaption") ?? FindInChildren(root, "AvatarCaption");
        if (avatarCaption != null && avatarCaption.GetComponent<ChatBubbleAnchor>() == null)
        {
            avatarCaption.gameObject.AddComponent<ChatBubbleAnchor>();
            MultiplayerChat.Plugin.Log?.Debug($"[MultiplayerChat] Added ChatBubbleAnchor to {avatarCaption.name}");
        }
    }

    private static Transform? FindInChildren(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindInChildren(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    [OnEnable]
    public void OnEnable() { }

    [OnDisable]
    public void OnDisable()
    {
        _harmony?.UnpatchAll("MultiplayerChat");
    }
}
