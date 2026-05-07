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

        if (MpChatFeatures.LobbyCustomAvatars)
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
        }

        if (!MultiplayerExtensionsBootstrap.TryContinueAfterEnsuringStandaloneMpex(logger))
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
        MpChatLog.Apply(ModSettings.DebugLogging);

        var globalAudioHost = new GameObject("MPChatGlobalAudioHost");
        UnityEngine.Object.DontDestroyOnLoad(globalAudioHost);
        globalAudioHost.AddComponent<Core.GlobalChatAudioHost>();
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
            container.BindInterfacesAndSelfTo<VersionChecker>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
            container.BindInterfacesAndSelfTo<SettingsMenuButton>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
        });

        zenjector.Install<MultiplayerLobbyInstaller>(container => InstallChatBindings(container, lobbyUi: true));

        zenjector.Install(Location.GameCore, container => InstallChatBindings(container, lobbyUi: false));

        UnityEngine.Application.quitting += OnApplicationQuitting;
    }

    private static void OnApplicationQuitting()
    {
        UnityEngine.Application.quitting -= OnApplicationQuitting;
        AvatarExtrasConfigPersistence.Save(AvatarExtrasConfig);
        VoiceChatRuntimeState.ClearTalkToOnGameQuit();
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
            container.BindInterfacesAndSelfTo<ChatBubbleManager>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
            container.BindInterfacesAndSelfTo<FloorChatButton>().FromNewComponentOnNewGameObject().AsSingle();
            container.BindInterfacesAndSelfTo<FloatingHotMicMuteButton>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
            container.Bind<SettingsViewController>().FromNewComponentAsViewController().AsTransient();
            container.Bind<VoiceSettingsViewController>().FromNewComponentAsViewController().AsTransient();
            container.Bind<MultiplayerChatSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.Bind<VoiceSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.Bind<VoiceDuckSettingsViewController>().FromNewComponentAsViewController().AsSingle();
            container.Bind<VoiceDuckSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.Bind<FusedModsSettingsViewController>().FromNewComponentAsViewController().AsSingle();
            container.Bind<FusedModsSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.Bind<AddonsSettingsViewController>().FromNewComponentAsViewController().AsSingle();
            container.Bind<AddonsSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            if (MpChatFeatures.LobbyCustomAvatars)
            {
                container.Bind<CustomAvatarsSettingsViewController>().FromNewComponentAsViewController().AsSingle();
                container.Bind<CustomAvatarsSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
                container.BindInterfacesAndSelfTo<MpCustomAvatarSyncManager>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
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
        if (MpChatFeatures.LobbyCustomAvatars && ModSettings.EnableLobbyCustomAvatars)
            original.gameObject.AddComponent<MpChatLobbyCustomAvatarDriver>();
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
