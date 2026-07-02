using System;
using System.Collections.Generic;
using System.Reflection;
using MultiplayerChat.Contracts;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core.Addons;

internal sealed class MpChatAddonHost : IMpChatHost
{
    private readonly LoadedAddonInstance _owner;
    private readonly Dictionary<string, object> _persistentHosts = new(StringComparer.Ordinal);

    internal MpChatAddonHost(LoadedAddonInstance owner)
    {
        _owner = owner;
    }

    public Version CoreVersion => typeof(MultiplayerChat.Plugin).Assembly.GetName().Version ?? new Version(0, 0, 0);

    public string AddonsDirectory => AddonPaths.AddonsRoot;

    public IReadOnlyList<LoadedAddonInfo> LoadedAddons => AddonHost.Instance?.Snapshot() ?? Array.Empty<LoadedAddonInfo>();

    public void LogInfo(string message) => MpChatLog.Info(message);

    public void LogWarn(string message) => MpChatLog.Warn(message);

    public void LogError(string message) => MpChatLog.Error(message);

    public bool IsAddonEnabled(string addonId) => AddonEnablement.IsEnabled(addonId);

    public T GetSetting<T>(string key, T defaultValue) => defaultValue;

    public void SetSetting<T>(string key, T value)
    {
    }

    public object CreateHarmony(string addonId) =>
        new HarmonyLib.Harmony($"com.multiplayerchat.addon.{addonId}");

    public void UnpatchHarmony(string addonId) => _owner.UnpatchHarmony();

    public object GetAddonHarmony() => _owner.Harmony;

    public void PatchAllHarmony(string addonId, Assembly assembly) =>
        AffinityHarmonyForwarder.PatchAssembly(_owner.Harmony, assembly);

    public IDisposable RegisterPacketCallback<TPacket>(Action<TPacket, object> handler) where TPacket : class
    {
        var reg = AddonPacketBridge.Register(handler);
        _owner.Track(reg);
        return reg;
    }

    public void UnregisterPacketCallback<TPacket>() where TPacket : class =>
        AddonPacketBridge.Unregister<TPacket>();

    public object CreatePersistentHost(string name)
    {
        if (_persistentHosts.TryGetValue(name, out var existing) && existing is GameObject go && go)
        {
            PreparePersistentHost(go);
            return go;
        }

        DestroyStraySceneHost(name);

        var hostGo = new GameObject(name);
        UnityEngine.Object.DontDestroyOnLoad(hostGo);
        _persistentHosts[name] = hostGo;
        return hostGo;
    }

    internal static void PreparePersistentHost(GameObject host)
    {
        foreach (var component in host.GetComponents<MonoBehaviour>())
        {
            if (component != null)
                UnityEngine.Object.DestroyImmediate(component);
        }
    }

    private static void DestroyStraySceneHost(string name)
    {
        var stray = GameObject.Find(name);
        if (stray == null)
            return;

        UnityEngine.Object.DestroyImmediate(stray);
    }

    public void DestroyPersistentHost(object host)
    {
        if (host is string name)
        {
            DestroyPersistentHostByName(name);
            return;
        }

        if (host is not GameObject go || !go)
            return;

        var keyToRemove = string.Empty;
        foreach (var kv in _persistentHosts)
        {
            if (ReferenceEquals(kv.Value, go))
            {
                keyToRemove = kv.Key;
                break;
            }
        }

        if (!string.IsNullOrEmpty(keyToRemove))
            _persistentHosts.Remove(keyToRemove);

        UnityEngine.Object.DestroyImmediate(go);
    }

    private void DestroyPersistentHostByName(string name)
    {
        if (_persistentHosts.TryGetValue(name, out var tracked) && tracked is GameObject trackedGo)
        {
            _persistentHosts.Remove(name);
            if (trackedGo)
                UnityEngine.Object.DestroyImmediate(trackedGo);
        }

        var stray = GameObject.Find(name);
        if (stray != null)
            UnityEngine.Object.DestroyImmediate(stray);
    }

    public void RegisterLobbyAvatarHook(IMpChatLobbyAvatarHook hook) => AddonLobbyAvatarBridge.Register(hook);

    public void UnregisterLobbyAvatarHook(IMpChatLobbyAvatarHook hook) => AddonLobbyAvatarBridge.Unregister(hook);

    public void RegisterSettingsPage(IMpChatSettingsPage page) => AddonSettingsBridge.Register(page);

    public void UnregisterSettingsPage(IMpChatSettingsPage page) => AddonSettingsBridge.Unregister(page);

    public void RegisterSettingsPresenter(string addonId, Type flowCoordinatorType, string menuLabel)
    {
        var canonicalType = ResolvePresenterType(addonId, flowCoordinatorType);
        AddonSettingsBridge.RegisterPresenter(addonId, canonicalType, menuLabel);
    }

    private static Type ResolvePresenterType(string addonId, Type flowCoordinatorType)
    {
        var fullName = flowCoordinatorType.FullName;
        if (string.IsNullOrEmpty(fullName))
            return flowCoordinatorType;

        return AddonZenjectPreloader.ResolveType(addonId, fullName) ?? flowCoordinatorType;
    }

    public void UnregisterSettingsPresenter(string addonId) =>
        AddonSettingsBridge.UnregisterPresenter(addonId);

    public void SetCapability(AddonCapability capability, bool enabled) =>
        AddonHost.Instance?.SetCapability(capability, enabled);

    public object? GetService(Type serviceType)
    {
        try
        {
            return ProjectContext.Instance?.Container?.TryResolve(serviceType);
        }
        catch
        {
            return null;
        }
    }

    public void Inject(object instance)
    {
        var zenjectContainer = ProjectContext.Instance?.Container ?? AddonZenjectSettingsBinder.MenuContainer;
        if (zenjectContainer == null)
            return;

        try
        {
            zenjectContainer.Inject(instance);
        }
        catch (Exception ex)
        {
            LogWarn($"[MPChat][Addons] Inject failed for {instance.GetType().FullName}: {ex.Message}");
        }
    }
}
