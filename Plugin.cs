using System;
using MultiplayerChat.AvatarExtras;
using MultiplayerChat.AvatarExtras.Assets;
using MultiplayerChat.AvatarExtras.Patches.App;
using MultiplayerChat.AvatarExtras.Patches.Menu;
using MultiplayerChat.Core;
using MultiplayerChat.Core.Addons;
using MultiplayerChat.HarmonyPatches;
using MultiplayerChat.Settings;
using MultiplayerChat.UI;
using HarmonyLib;
using UnityEngine;
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
    internal static AvatarExtrasConfig? AvatarExtrasConfig { get; private set; }

    private static Harmony? _avatarSerializationHarmony;

    [Init]
    public void Init(
        IPALogger logger,
        Zenjector zenjector)
    {
        Log = logger;
        MpChatLog.Init(logger);

        if (!AddonsAndContractsBootstrap.TryContinueAfterEnsuringInstalled(logger))
            return;

        try
        {
            _avatarSerializationHarmony ??= new Harmony("com.multiplayerchat.avatarserialize");
            AvatarDataMultiplayerSerializationPatches.Apply(_avatarSerializationHarmony);
        }
        catch (Exception ex)
        {
            logger.Warn($"[MPChat] AvatarData multiplayer serialization Harmony init failed: {ex.Message}");
        }

        try
        {
            var vrMenuHarmony = new Harmony("com.multiplayerchat.ignorevrsystemmenu");
            MpChatIgnoreVrSystemMenuPatches.Apply(vrMenuHarmony);
            logger.Info("[MPChat] VR system menu will not pause or fail gameplay.");
        }
        catch (Exception ex)
        {
            logger.Warn($"[MPChat] VR system menu ignore patches failed: {ex.Message}");
        }

        if (!MultiplayerExtensionsBootstrap.TryContinueAfterEnsuringStandaloneMpex(logger))
            return;

        if (!CustomAvatarDependenciesBootstrap.TryContinueAfterEnsuringDependencies(logger))
            return;

        CauBootstrap.DeleteCauExeIfEnabled();

        if (ModSettings.EnableAvatarExtensions)
        {
            AvatarExtrasConfig = AvatarExtrasConfigPersistence.LoadOrCreate();
            Log.Info("[MPChat] Avatar Extensions enabled for this session (toggle is in Multiplayer Chat settings; restart after changing).");
        }
        else
        {
            AvatarExtrasConfig = null;
        }

        SlzMode.Refresh();
        if (SlzMode.IsEnabled)
            Log.Info($"[MultiplayerChat] SLZ mode is ON ({SlzMode.MarkerFileName} next to mod DLL)");

        ChatPersistentId.EnsureLoaded();
        ModSettings.ApplyPersistedVoiceSelfState();
        MpChatDebugMode.Refresh();
        MpChatLog.Apply(MpChatDebugMode.IsEnabled);

        try
        {
            AddonSystemBridge.Initialize(logger, zenjector);
        }
        catch (Exception ex)
        {
            logger.Error($"[MPChat] Addon runtime init failed: {ex}");
            return;
        }

        if (ModSettings.EnableAvatarExtensions)
        {
            zenjector.Install(Location.App, container =>
                container.BindInterfacesAndSelfTo<AvatarVisualControllerPatcher>().AsSingle().NonLazy());
            zenjector.Install(Location.Menu, container =>
            {
                container.BindInterfacesAndSelfTo<ColorPickerButtonControllerPatcher>().AsSingle().NonLazy();
                container.BindInterfacesAndSelfTo<EditAvatarViewControllerPatcher>().AsSingle().NonLazy();
                container.BindInterfacesAndSelfTo<EditAvatarColorViewControllerPatcher>().AsSingle().NonLazy();
            });
        }

        zenjector.Install(Location.Menu, container =>
        {
            container.Bind<UpdateMessageViewController>().FromNewComponentAsViewController().AsTransient();
            container.BindInterfacesAndSelfTo<ChatBubbleManager>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
            container.BindInterfacesAndSelfTo<VersionChecker>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
            container.Bind<FusedModsSettingsViewController>().FromNewComponentAsViewController().AsSingle();
            container.Bind<FusedModsSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
        });

        zenjector.Install<MultiplayerLobbyInstaller>(container => InstallChatBindings(container, lobbyUi: true));

        zenjector.Install(Location.GameCore, container => InstallGameCoreBindings(container));

        if (CustomAvatarDependenciesBootstrap.IsSessionActive() && MpChatFeatures.LobbyCustomAvatarsInArena)
        {
            zenjector.Install(Location.ConnectedPlayer, container =>
            {
                container.RegisterRedecorator(new ConnectedPlayerRegistration(DecorateConnectedPlayer));
                container.RegisterRedecorator(new ConnectedPlayerDuelRegistration(DecorateConnectedPlayer));
            });
        }

        UnityEngine.Application.quitting += OnApplicationQuitting;
    }

    private static void OnApplicationQuitting()
    {
        UnityEngine.Application.quitting -= OnApplicationQuitting;
        AvatarExtrasConfigPersistence.Save(AvatarExtrasConfig);
        VoiceChatRuntimeState.ClearTalkToOnGameQuit();
        AddonSystemBridge.UnloadAll();
    }

    private static void InstallGameCoreBindings(DiContainer container)
    {
        InstallChatBindings(container, lobbyUi: false);
    }

    private static MultiplayerConnectedPlayerFacade DecorateConnectedPlayer(MultiplayerConnectedPlayerFacade original)
    {
        AddonGameplayBridge.RefreshArenaAttach(original);
        return original;
    }

    private static void InstallChatBindings(DiContainer container, bool lobbyUi)
    {
        container.BindInterfacesAndSelfTo<EncryptionManager>().AsSingle();
        container.BindInterfacesAndSelfTo<ChatIdConfigStore>().AsSingle().NonLazy();
        container.BindInterfacesAndSelfTo<LearnedChatIdsStore>().AsSingle().NonLazy();
        container.Bind<ChatPlayerIdRegistry>().AsSingle();
        container.Bind<ChatMuteManager>().AsSingle();
        container.Bind<ChatDMState>().AsSingle();
        container.BindInterfacesAndSelfTo<ChatManager>().AsSingle();
        container.BindInterfacesAndSelfTo<VoiceHotMicManager>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
        container.BindInterfacesAndSelfTo<ModPresenceManager>().AsSingle();
        container.Bind<CoroutineHost>().FromNewComponentOnNewGameObject().AsSingle();
        container.BindInterfacesAndSelfTo<ChatPresenceNotifier>().AsSingle().NonLazy();

        if (lobbyUi)
        {
            container.BindInterfacesAndSelfTo<FloorChatButton>().FromNewComponentOnNewGameObject().AsSingle();
            container.BindInterfacesAndSelfTo<FloatingHotMicMuteButton>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
            container.Bind<SettingsViewController>().FromNewComponentAsViewController().AsTransient();
            container.Bind<PlayerSettingsViewController>().FromNewComponentAsViewController().AsSingle();
            container.Bind<PlayerSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.Bind<MicSettingsViewController>().FromNewComponentAsViewController().AsSingle();
            container.Bind<MicSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.Bind<MultiplayerChatSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.Bind<VoiceDuckSettingsViewController>().FromNewComponentAsViewController().AsSingle();
            container.Bind<VoiceDuckSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.Bind<PerformanceSettingsViewController>().FromNewComponentAsViewController().AsSingle();
            container.Bind<PerformanceSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.Bind<PlayerListViewController>().FromNewComponentAsViewController().AsTransient();
            container.Bind<PlayerListFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.BindInterfacesAndSelfTo<LobbyChatTabRegistrar>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
            container.RegisterRedecorator(new LobbyAvatarRegistration(DecorateAvatar));
            container.RegisterRedecorator(new LobbyAvatarPlaceRegistration(DecorateAvatarPlace));
        }
    }

    private static MultiplayerLobbyAvatarController DecorateAvatar(MultiplayerLobbyAvatarController original)
    {
        AddChatBubbleAnchorToCaption(original.transform);
        AddonSystemBridge.DecorateLobbyAvatar(original);
        return original;
    }

    private static MultiplayerLobbyAvatarPlace DecorateAvatarPlace(MultiplayerLobbyAvatarPlace original)
    {
        AddonSystemBridge.DecorateLobbyAvatarPlace(original);
        return original;
    }

    private static void AddChatBubbleAnchorToCaption(Transform root)
    {
        var avatarCaption = root.Find("AvatarCaption") ?? FindInChildren(root, "AvatarCaption");
        if (avatarCaption == null || avatarCaption.GetComponent<ChatBubbleAnchor>() != null)
            return;

        var anchor = avatarCaption.gameObject.AddComponent<ChatBubbleAnchor>();
        MpChatLobbyAvatarZenject.TryInject(anchor);
        Log?.Debug($"[MultiplayerChat] Added ChatBubbleAnchor to {avatarCaption.name}");
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
    public void OnEnable()
    {
        if (!ModSettings.EnableAvatarExtensions)
            return;

        Sprites.Initialize();
        _ = BundleLoader.EnsureLoaded();
    }

    [OnDisable]
    public void OnDisable()
    {
        BundleLoader.Unload();
        try
        {
            _avatarSerializationHarmony?.UnpatchSelf();
        }
        catch
        {
            // ignored
        }

        _avatarSerializationHarmony = null;
    }
}
