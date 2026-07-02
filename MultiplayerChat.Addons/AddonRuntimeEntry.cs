using MultiplayerChat.Core;
using MultiplayerChat.Core.Addons;
using MultiplayerChat.UI;
using SiraUtil.Extras;
using SiraUtil.Objects.Multiplayer;
using SiraUtil.Zenject;
using UnityEngine;
using Zenject;
using IPALogger = IPA.Logging.Logger;

namespace MultiplayerChat.Addons;

public static class AddonRuntimeEntry
{
    public static void Initialize(IPALogger logger, Zenjector zenjector)
    {
        AddonAffinityShimBridge.Register(
            AddonAffinityForwarder.CreatePatcher,
            AddonAffinityForwarder.InvokeVoid,
            AddonAffinityForwarder.InvokePrefix,
            AddonAffinityForwarder.InvokeStaticPrefix);
        AddonMenuResolveBridge.Register(AddonZenjectSettingsBinder.TryResolveMenuSingleton);

        AddonZenjectPreloader.Run();

        var globalAudioHost = new GameObject("MPChatGlobalAudioHost");
        Object.DontDestroyOnLoad(globalAudioHost);
        if (globalAudioHost.GetComponent<GlobalChatAudioHost>() == null)
            globalAudioHost.AddComponent<GlobalChatAudioHost>();

        AddonHost.EnsureInstance();
        AddonLifecycleHost.EnsureRunning();

        zenjector.UseLogger(logger);
        zenjector.UseMetadataBinder<Plugin>();

        AddonZenjectSettingsBinder.InstallMenu(zenjector);

        zenjector.Install(Location.Menu, container =>
        {
            container.Bind<AddonsSettingsViewController>().FromNewComponentAsViewController().AsSingle();
            container.Bind<AddonsSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            container.BindInterfacesAndSelfTo<AddonUiBridgeRegistrar>().AsSingle().NonLazy();
        });
    }

    public static void UnloadAll() => AddonHost.Instance?.UnloadAll();

    public static void DecorateLobbyAvatar(MultiplayerLobbyAvatarController original) =>
        AddonLobbyAvatarBridge.DecorateLobbyAvatar(original);

    public static void DecorateLobbyAvatarPlace(MultiplayerLobbyAvatarPlace original) =>
        AddonLobbyAvatarBridge.DecorateLobbyAvatarPlace(original);
}
