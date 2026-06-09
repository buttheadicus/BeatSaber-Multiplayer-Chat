using System;
using MultiplayerChat.AvatarColoring;
using MultiplayerChat.Core;
using MultiplayerChat.AvatarExtras;
using MultiplayerChat.AvatarExtras.Assets;
using MultiplayerChat.AvatarExtras.Patches.App;
using MultiplayerChat.AvatarExtras.Patches.Menu;
using MultiplayerChat.HarmonyPatches;
using MultiplayerChat.Settings;
using HarmonyLib;
using UnityEngine;
using MultiplayerChat.UI;
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

    private static Harmony? _lobbyScaleHarmony;

    private static Harmony? _avatarSerializationHarmony;

    [Init]
    public void Init(
        IPALogger logger,
        Zenjector zenjector)
    {
        Log = logger;
        MpChatLog.Init(logger);

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

        if (MpChatFeatures.LobbyCustomAvatars && CustomAvatarDependenciesBootstrap.SessionDependenciesReady)
        {
            try
            {
                _lobbyScaleHarmony ??= new Harmony("com.multiplayerchat.lobbyscale");
                MpChatMultiplayerLobbyScaleAnimatorPatches.Apply(_lobbyScaleHarmony);
            }
            catch (Exception ex)
            {
                logger.Warn($"[MPChat] Lobby ScaleAnimator Harmony patches failed (custom lobby avatars may look wrong): {ex.Message}");
            }

            try
            {
                var arenaHarmony = new Harmony("com.multiplayerchat.arenaavatar");
                MpChatArenaAvatarHarmony.Apply(arenaHarmony);
            }
            catch (Exception ex)
            {
                logger.Warn($"[MPChat] Arena custom avatar Harmony failed: {ex.Message}");
            }
        }
        else if (MpChatFeatures.LobbyCustomAvatars && ModSettings.EnableLobbyCustomAvatars)
        {
            logger.Warn("[MPChat][CustomAvatars] Skipping lobby/arena Harmony patches until dependencies are installed.");
        }

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

        // Load local Chat ID from disk early so registry and encryption paths see a stable id during Zenject setup.
        ChatPersistentId.EnsureLoaded();
        MpChatDebugMode.Refresh();
        MpChatLog.Apply(MpChatDebugMode.IsEnabled);

        var globalAudioHost = new GameObject("MPChatGlobalAudioHost");
        UnityEngine.Object.DontDestroyOnLoad(globalAudioHost);
        globalAudioHost.AddComponent<Core.GlobalChatAudioHost>();

        var quickBindsHost = new GameObject("MPChatQuickBindsHost");
        UnityEngine.Object.DontDestroyOnLoad(quickBindsHost);
        quickBindsHost.AddComponent<Core.QuickBinds.QuickBindsRuntimeManager>();

        if (CustomAvatarDependenciesBootstrap.IsSessionActive())
        {
            var lobbyAvatarHost = new GameObject("MPChatLobbyAvatarLifecycleHost");
            UnityEngine.Object.DontDestroyOnLoad(lobbyAvatarHost);
            lobbyAvatarHost.AddComponent<Core.MpChatLobbyAvatarLifecycleHost>();
        }

        zenjector.UseLogger(logger);
        zenjector.UseMetadataBinder<Plugin>();

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

        if (ModSettings.EnableAvatarColoringExtensions)
        {
            zenjector.Install(Location.Menu, container =>
            {
                container.BindInterfacesAndSelfTo<AvatarColoringEditorPatcher>().AsSingle().NonLazy();
                container.BindInterfacesAndSelfTo<AvatarColoringAlphaSliderPatcher>().AsSingle().NonLazy();
                container.Bind<AvatarNameEntryViewController>().FromNewComponentAsViewController().AsSingle();
                container.Bind<AvatarNameEntryFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
                container.Bind<AvatarLoadListViewController>().FromNewComponentAsViewController().AsSingle();
                container.Bind<AvatarLoadListFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            });
        }

        zenjector.Install(Location.Menu, container =>
        {
            container.Bind<UpdateMessageViewController>().FromNewComponentAsViewController().AsTransient();
            container.BindInterfacesAndSelfTo<ChatBubbleManager>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
            container.BindInterfacesAndSelfTo<VersionChecker>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
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
    }

    private static void InstallGameCoreBindings(DiContainer container)
    {
        InstallChatBindings(container, lobbyUi: false);

        if (CustomAvatarDependenciesBootstrap.IsSessionActive() && MpChatFeatures.LobbyCustomAvatarsInArena)
        {
            container.BindInterfacesAndSelfTo<MpCustomAvatarSyncManager>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
        }
    }

    private static MultiplayerConnectedPlayerFacade DecorateConnectedPlayer(MultiplayerConnectedPlayerFacade original)
    {
        MpChatArenaAvatarAttach.RefreshAttachForGameplay(original);
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
            // ChatBubbleManager is bound once in Location.Menu (main menu + title-bar bubbles). Do not bind again here (Zenject 6+ rejects duplicate AsSingle).
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
            container.Bind<FusedModsSettingsViewController>().FromNewComponentAsViewController().AsSingle();
            container.Bind<FusedModsSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.Bind<AddonsSettingsViewController>().FromNewComponentAsViewController().AsSingle();
            container.Bind<AddonsSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.Bind<AvatarColoringExtensionsSettingsViewController>().FromNewComponentAsViewController().AsSingle();
            container.Bind<AvatarColoringExtensionsSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.Bind<PerformanceSettingsViewController>().FromNewComponentAsViewController().AsSingle();
            container.Bind<PerformanceSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.Bind<QuickBindsSettingsViewController>().FromNewComponentAsViewController().AsSingle();
            container.Bind<QuickBindsSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.Bind<QuickBindsOptionsSettingsViewController>().FromNewComponentAsViewController().AsSingle();
            container.Bind<QuickBindsOptionsSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            if (MpChatFeatures.LobbyCustomAvatars)
            {
                container.Bind<CustomAvatarsSettingsViewController>().FromNewComponentAsViewController().AsSingle();
                container.Bind<CustomAvatarsSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            }

            if (CustomAvatarDependenciesBootstrap.IsSessionActive())
            {
                container.BindInterfacesAndSelfTo<MpCustomAvatarSyncManager>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
                container.BindInterfacesAndSelfTo<MpCustomAvatarLobbyTransferManager>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
            }

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
        if (CustomAvatarDependenciesBootstrap.IsSessionActive())
        {
            original.gameObject.AddComponent<MpChatLobbyPedestalScaleGuard>();
            original.gameObject.AddComponent<MpChatLobbyCustomAvatarDriver>();
        }

        return original;
    }

    private static MultiplayerLobbyAvatarPlace DecorateAvatarPlace(MultiplayerLobbyAvatarPlace original)
    {
        return original;
    }

    private static void AddChatBubbleAnchorToCaption(Transform root)
    {
        var avatarCaption = root.Find("AvatarCaption") ?? FindInChildren(root, "AvatarCaption");
        if (avatarCaption == null || avatarCaption.GetComponent<ChatBubbleAnchor>() != null)
            return;

        var anchor = avatarCaption.gameObject.AddComponent<ChatBubbleAnchor>();
        MpChatLobbyAvatarZenject.TryInject(anchor);
        MultiplayerChat.Plugin.Log?.Debug($"[MultiplayerChat] Added ChatBubbleAnchor to {avatarCaption.name}");
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
    public async void OnEnable()
    {
        if (!ModSettings.EnableAvatarExtensions)
            return;

        Sprites.Initialize();
        await BundleLoader.EnsureLoaded();
    }

    [OnDisable]
    public void OnDisable()
    {
        BundleLoader.Unload();
        try
        {
            _lobbyScaleHarmony?.UnpatchSelf();
        }
        catch
        {
            // ignored
        }

        _lobbyScaleHarmony = null;

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
