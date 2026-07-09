using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MultiplayerChat.Contracts;

namespace MultiplayerChat.Core.Addons;

internal sealed class AddonHost
{
    internal static AddonHost? Instance { get; private set; }

    private static int _nextHarmonySerial;
    private readonly List<LoadedAddonInstance> _loaded = new();
    private readonly Dictionary<string, string> _lastLoadedHashes = new(StringComparer.Ordinal);
    private AddonCapability _capabilities;

    internal static void EnsureInstance()
    {
        Instance ??= new AddonHost();
    }

    internal AddonCapability Capabilities => _capabilities;

    internal int LoadedCount => _loaded.Count;

    internal bool IsAddonLoaded(string addonId) =>
        _loaded.Any(l => string.Equals(l.Addon.Id, addonId, StringComparison.Ordinal));

    internal IReadOnlyList<LoadedAddonInfo> Snapshot() =>
        _loaded.Select(l => new LoadedAddonInfo
        {
            Id = l.Addon.Id,
            DisplayName = l.Addon.DisplayName,
            Version = l.Addon.Version,
            AssemblyPath = l.DllPath,
            FileHash = l.FileHash
        }).ToList();

    internal void SetCapability(AddonCapability capability, bool enabled)
    {
        if (enabled)
            _capabilities |= capability;
        else
            _capabilities &= ~capability;
    }

    internal void ReloadAll()
    {
        UnloadAll();
        LoadAll();
    }

    internal void LoadAll()
    {
        if (!Directory.Exists(AddonPaths.AddonsRoot))
        {
            Directory.CreateDirectory(AddonPaths.AddonsRoot);
            MpChatLog.Info($"[MPChat][Addons] Created addons folder at {AddonPaths.AddonsRoot}");
            return;
        }

        if (!File.Exists(AddonPaths.ContractsDllPath))
        {
            MpChatLog.Warn("[MPChat][Addons] MultiplayerChat.Contracts.dll missing from Plugins; addons disabled.");
            return;
        }

        var entries = AddonCatalog.Scan();
        if (entries.Count == 0)
        {
            MpChatLog.DebugLine("[MPChat][Addons] No addon DLLs found.");
            return;
        }

        foreach (var entry in entries)
        {
            if (!ShouldLoad(entry))
                continue;

            if (_lastLoadedHashes.TryGetValue(entry.Manifest.Id, out var priorHash) &&
                !string.Equals(priorHash, entry.FileHash, StringComparison.Ordinal))
            {
                MpChatLog.Warn(
                    $"[MPChat][Addons] {entry.Manifest.Id} changed on disk; loaded fresh copy for this menu session.");
            }

            TryLoadEntry(entry);
        }

        ModPresenceManager.Instance?.RefreshLobbyCustomAvatarsPresenceAfterSettingsChange();
        MpChatLog.Info(
            $"[MPChat][Addons] Loaded {_loaded.Count} addon(s). Registered settings presenters: {AddonSettingsBridge.PresentersSnapshot.Count}.");
    }

    private bool ShouldLoad(AddonCatalogEntry entry)
    {
        if (!Version.TryParse(entry.Manifest.MinCoreVersion, out var minCore))
            minCore = new Version(0, 0, 0);

        var coreVersion = typeof(MultiplayerChat.Plugin).Assembly.GetName().Version ?? new Version(0, 0, 0);
        if (coreVersion < minCore)
        {
            MpChatLog.Warn(
                $"[MPChat][Addons] Skipping {entry.Manifest.Id}: requires core {minCore}, have {coreVersion}.");
            return false;
        }

        if (!AddonEnablement.IsEnabled(entry.Manifest.Id))
            return false;

        if (entry.Manifest.Id == AddonIds.CustomAvatars &&
            !CustomAvatarDependenciesBootstrap.SessionDependenciesReady)
        {
            MpChatLog.Warn("[MPChat][Addons] Skipping customAvatars: dependencies not ready.");
            return false;
        }

        foreach (var dep in entry.Manifest.Dependencies)
        {
            if (!IsDependencySatisfied(dep))
            {
                MpChatLog.Warn($"[MPChat][Addons] Skipping {entry.Manifest.Id}: missing dependency {dep}.");
                return false;
            }
        }

        return true;
    }

    private static bool IsDependencySatisfied(string dependency) =>
        dependency switch
        {
            "CustomAvatar" => CustomAvatarDependenciesBootstrap.IsCustomAvatarModLoaded(),
            "MultiplayerCore" => true,
            _ => File.Exists(Path.Combine(UnityEngine.Application.dataPath, "..", "Plugins", dependency)) ||
                 File.Exists(Path.Combine(AddonPaths.AddonsRoot, dependency))
        };

    private void TryLoadEntry(AddonCatalogEntry entry)
    {
        try
        {
            if (!AddonZenjectPreloader.TryGetAssembly(entry.Manifest.Id, out var assembly))
            {
                MpChatLog.Warn($"[MPChat][Addons] No preloaded assembly for {entry.Manifest.Id}.");
                return;
            }

            var addon = DiscoverAddon(assembly);
            if (addon == null)
            {
                MpChatLog.Warn($"[MPChat][Addons] No IMpChatAddon found in {entry.DllPath}");
                return;
            }

            var harmonyId = $"com.multiplayerchat.addon.{addon.Id}.{_nextHarmonySerial++}";
            var harmony = new HarmonyLib.Harmony(harmonyId);
            var instance = new LoadedAddonInstance(addon, assembly, entry.DllPath, entry.FileHash, harmony);
            var host = new MpChatAddonHost(instance);

            try
            {
                addon.OnLoad(host);
            }
            catch (Exception ex)
            {
                AddonSettingsBridge.UnregisterPresenter(addon.Id);
                MpChatLog.Warn($"[MPChat][Addons] OnLoad failed for {addon.Id}: {ex.Message}");
                instance.Dispose();
                return;
            }

            _loaded.Add(instance);
            _lastLoadedHashes[addon.Id] = entry.FileHash;
            SetCapability(AddonEnablement.CapabilityFor(addon.Id), true);
        }
        catch (Exception ex)
        {
            MpChatLog.Warn($"[MPChat][Addons] Failed to load {entry.DllPath}: {ex.Message}");
        }
    }

    private static IMpChatAddon? DiscoverAddon(Assembly assembly)
    {
        foreach (var type in assembly.GetExportedTypes())
        {
            if (!typeof(IMpChatAddon).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                continue;

            if (Activator.CreateInstance(type) is IMpChatAddon addon)
                return addon;
        }

        return null;
    }

    internal void UnloadAll()
    {
        // ChatClientHandoff lives in core; SLZ releases on its own OnUnload.
        // Do not couple public Addons.dll to handoff APIs.
        SetCapability(AddonCapability.None, false);
        for (var i = _loaded.Count - 1; i >= 0; i--)
        {
            try
            {
                _loaded[i].Addon.OnUnload();
            }
            catch (Exception ex)
            {
                MpChatLog.Warn($"[MPChat][Addons] OnUnload failed for {_loaded[i].Addon.Id}: {ex.Message}");
            }

            _loaded[i].Dispose();
        }

        _loaded.Clear();
        AddonPacketBridge.Clear();
        AddonLobbyAvatarBridge.Clear();
        AddonSettingsBridge.Clear();
        AddonGameplayBridge.Clear();
        AddonCustomAvatarsBridge.ClearHandlers();
        AddonAvatarColoringBridge.ClearHandlers();
        _capabilities = AddonCapability.None;
    }
}
