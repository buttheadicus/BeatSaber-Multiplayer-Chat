using System;
using System.Collections.Generic;

namespace MultiplayerChat.Contracts;

public interface IMpChatHost
{
    Version CoreVersion { get; }

    string AddonsDirectory { get; }

    void LogInfo(string message);

    void LogWarn(string message);

    void LogError(string message);

    bool IsAddonEnabled(string addonId);

    T GetSetting<T>(string key, T defaultValue);

    void SetSetting<T>(string key, T value);

    object CreateHarmony(string addonId);

    void UnpatchHarmony(string addonId);

    void PatchAllHarmony(string addonId, System.Reflection.Assembly assembly);

    object GetAddonHarmony();

    IDisposable RegisterPacketCallback<TPacket>(Action<TPacket, object> handler) where TPacket : class;

    void UnregisterPacketCallback<TPacket>() where TPacket : class;

    object CreatePersistentHost(string name);

    void DestroyPersistentHost(object host);

    void RegisterLobbyAvatarHook(IMpChatLobbyAvatarHook hook);

    void UnregisterLobbyAvatarHook(IMpChatLobbyAvatarHook hook);

    void RegisterSettingsPage(IMpChatSettingsPage page);

    void UnregisterSettingsPage(IMpChatSettingsPage page);

    void RegisterSettingsPresenter(string addonId, Type flowCoordinatorType, string menuLabel);

    void UnregisterSettingsPresenter(string addonId);

    void SetCapability(AddonCapability capability, bool enabled);

    IReadOnlyList<LoadedAddonInfo> LoadedAddons { get; }

    object? GetService(Type serviceType);

    void Inject(object instance);
}

public sealed class LoadedAddonInfo
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public Version Version { get; init; } = new Version(0, 0, 0);

    public string AssemblyPath { get; init; } = string.Empty;

    public string? FileHash { get; init; }
}
