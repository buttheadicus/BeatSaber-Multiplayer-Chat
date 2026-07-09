using System;
using System.Collections.Generic;
using MultiplayerChat.Contracts;
using MultiplayerChat.Core.AvatarColoring;
using SiraUtil.Zenject;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core.Addons;

internal static class AddonZenjectSettingsBinder
{
    private static DiContainer? _menuContainer;
    private static readonly List<Type> BoundTypes = new();
    private static readonly Dictionary<string, Type> BoundFlowTypes = new(StringComparer.Ordinal);
    private static bool _shimsInstalled;
    private static bool _menuInstallComplete;

    internal static int BindGeneration { get; private set; }

    internal static DiContainer? MenuContainer => _menuContainer;

    internal static bool TryGetBoundFlowType(string addonId, string flowTypeName, out Type flowType) =>
        BoundFlowTypes.TryGetValue($"{addonId}:{flowTypeName}", out flowType!);

    internal static T? TryResolveMenuSingleton<T>(string addonId, string typeFullName) where T : class
    {
        var container = MenuContainer;
        if (container == null)
            return null;

        var type = ResolveMenuType(addonId, typeFullName);
        if (type == null || !container.HasBinding(type))
            return null;

        try
        {
            return container.Resolve(type) as T;
        }
        catch (Exception ex)
        {
            MpChatLog.DebugLine($"[MPChat][Addons] Resolve menu singleton failed for {typeFullName}: {ex.Message}");
            return null;
        }
    }

    internal static object? TryResolveMenuSingleton(string addonId, string typeFullName, Type serviceType)
    {
        var container = MenuContainer;
        if (container == null)
            return null;

        var type = ResolveMenuType(addonId, typeFullName);
        if (type == null || serviceType != null && !serviceType.IsAssignableFrom(type))
            return null;

        if (!container.HasBinding(type))
            return null;

        try
        {
            return container.Resolve(type);
        }
        catch (Exception ex)
        {
            MpChatLog.DebugLine($"[MPChat][Addons] Resolve menu singleton failed for {typeFullName}: {ex.Message}");
            return null;
        }
    }

    private static Type? ResolveMenuType(string addonId, string typeFullName) =>
        AddonZenjectPreloader.ResolveType(addonId, typeFullName);

    internal static void InstallMenu(Zenjector zenjector)
    {
        zenjector.Install(Location.Menu, container =>
        {
            _menuContainer = container;
            InstallOnContainer(container, rebind: false);
            EnsureAddonsLoaded();
        });
    }

    internal static void EnsureAddonsLoaded()
    {
        if (AddonHost.Instance == null || AddonHost.Instance.LoadedCount > 0)
            return;

        MpChatLog.Info("[MPChat][Addons] Loading addons after menu Zenject bind.");
        AddonHost.Instance.LoadAll();
    }

    internal static void RefreshMenuBindings()
    {
        if (_menuContainer == null)
        {
            MpChatLog.Warn("[MPChat][Addons] Zenject menu refresh skipped: menu container missing.");
            return;
        }

        InstallOnContainer(_menuContainer, rebind: true);
    }

    private static void InstallOnContainer(DiContainer container, bool rebind)
    {
        if (!rebind && _menuInstallComplete && ReferenceEquals(_menuContainer, container))
        {
            MpChatLog.DebugLine("[MPChat][Addons] Menu Zenject install skipped (already complete).");
            return;
        }

        if (rebind)
        {
            UnbindTrackedTypes(container);
            _menuInstallComplete = false;
            AddonPatcherRegistry.Clear();
        }

        EnsurePreloaderHasCatalogAddons();

        BindFlowAndView(container, AddonIds.AvatarColoring,
            "MultiplayerChat.UI.AvatarNameEntryFlowCoordinator",
            "MultiplayerChat.UI.AvatarNameEntryViewController");
        BindFlowAndView(container, AddonIds.AvatarColoring,
            "MultiplayerChat.UI.AvatarLoadListFlowCoordinator",
            "MultiplayerChat.UI.AvatarLoadListViewController");

        BindAddonPatcher(container, AddonIds.AvatarColoring,
            "MultiplayerChat.AvatarColoring.AvatarColoringEditorPatcher");
        BindAddonPatcher(container, AddonIds.AvatarColoring,
            "MultiplayerChat.AvatarColoring.AvatarColoringAlphaSliderPatcher");

        if (!_shimsInstalled)
        {
            BindAffinityShim<AvatarColoringEditorAffinityShim>(container);
            BindAffinityShim<AvatarColoringAlphaSliderAffinityShim>(container);
            _shimsInstalled = true;
        }

        _menuInstallComplete = true;
        BindGeneration++;
        AddonSettingsFlowRegistry.Rebuild();
        AddonSettingsBridge.RefreshPresenterFlowRegistryGenerations();
        MpChatLog.DebugLine(
            $"[MPChat][Addons] Menu Zenject install complete ({BoundTypes.Count} bound type(s), settings registry gen {AddonSettingsFlowRegistry.Generation}).");
    }

    private static void BindFlowAndView(
        DiContainer container,
        string addonId,
        string flowTypeName,
        string viewTypeName,
        bool singleton = true)
    {
        var flowType = AddonZenjectPreloader.ResolveType(addonId, flowTypeName);
        var viewType = AddonZenjectPreloader.ResolveType(addonId, viewTypeName);
        if (flowType == null || viewType == null)
            return;

        try
        {
            PrepareTypeBinding(container, flowType);
            PrepareTypeBinding(container, viewType);

            if (singleton)
            {
                container.Bind(flowType).FromNewComponentOnNewGameObject().AsSingle();
                container.Bind(viewType).FromNewComponentAsViewController().AsSingle();
            }
            else
            {
                container.Bind(flowType).FromNewComponentOnNewGameObject().AsTransient();
                container.Bind(viewType).FromNewComponentAsViewController().AsTransient();
            }

            TrackType(flowType);
            TrackType(viewType);
            BoundFlowTypes[$"{addonId}:{flowTypeName}"] = flowType;
        }
        catch (Exception ex)
        {
            MpChatLog.Warn($"[MPChat][Addons] Zenject bind failed for {flowTypeName}: {ex.Message}");
        }
    }

    private static void BindAddonPatcher(DiContainer container, string addonId, string typeName)
    {
        var patcherType = AddonZenjectPreloader.ResolveType(addonId, typeName);
        if (patcherType == null)
            return;

        try
        {
            PrepareTypeBinding(container, patcherType);
            container.Bind(patcherType).AsTransient();
            TrackType(patcherType);
            AddonPatcherRegistry.Register(addonId, typeName, patcherType);
        }
        catch (Exception ex)
        {
            MpChatLog.Warn($"[MPChat][Addons] Zenject patcher bind failed for {typeName}: {ex.Message}");
        }
    }

    private static void EnsurePreloaderHasCatalogAddons()
    {
        foreach (var entry in AddonCatalog.Scan())
        {
            if (AddonZenjectPreloader.TryGetAssembly(entry.Manifest.Id, out _))
                continue;

            MpChatLog.DebugLine($"[MPChat][Addons] Preloader missing {entry.Manifest.Id}; reloading addon assemblies.");
            AddonZenjectPreloader.Run();
            return;
        }
    }

    private static void BindAffinityShim<TShim>(DiContainer container) where TShim : class
    {
        try
        {
            container.BindInterfacesAndSelfTo<TShim>().AsSingle().NonLazy();
        }
        catch (Exception ex)
        {
            MpChatLog.Warn($"[MPChat][Addons] Zenject affinity shim bind failed for {typeof(TShim).Name}: {ex.Message}");
        }
    }

    private static void PrepareTypeBinding(DiContainer container, Type type)
    {
        if (!container.HasBinding(type))
            return;

        DestroyBoundInstance(container, type);
        TryUnbind(container, type);
    }

    private static void UnbindTrackedTypes(DiContainer container)
    {
        foreach (var type in BoundTypes.ToArray())
        {
            DestroyBoundInstance(container, type);
            TryUnbind(container, type);
        }

        BoundTypes.Clear();
        BoundFlowTypes.Clear();
    }

    private static void DestroyBoundInstance(DiContainer container, Type type)
    {
        try
        {
            if (!container.HasBinding(type))
                return;

            var instance = container.TryResolve(type);
            if (instance is Component component && component != null)
                UnityEngine.Object.Destroy(component.gameObject);
        }
        catch (Exception ex)
        {
            MpChatLog.DebugLine($"[MPChat][Addons] Destroy bound instance failed for {type.FullName}: {ex.Message}");
        }
    }

    private static void TrackType(Type type)
    {
        if (!BoundTypes.Contains(type))
            BoundTypes.Add(type);
    }

    private static void TryUnbind(DiContainer container, Type type)
    {
        try
        {
            container.Unbind(type);
        }
        catch
        {
            // ignored
        }
    }
}
