using System;
using System.Collections.Generic;
using System.Linq;
using MultiplayerChat.Contracts;

namespace MultiplayerChat.Core.Addons;

internal sealed class AddonSettingsPresenterEntry
{
    internal string AddonId { get; init; } = string.Empty;

    internal Type FlowCoordinatorType { get; init; } = null!;

    internal string MenuLabel { get; init; } = string.Empty;

    internal int PreloaderGeneration { get; init; }

    internal int FlowRegistryGeneration { get; init; }
}

internal static class AddonSettingsBridge
{
    private static readonly List<IMpChatSettingsPage> Pages = new();
    private static readonly Dictionary<string, AddonSettingsPresenterEntry> Presenters =
        new(StringComparer.Ordinal);

    internal static IReadOnlyList<IMpChatSettingsPage> PagesSnapshot => Pages;

    internal static IReadOnlyDictionary<string, AddonSettingsPresenterEntry> PresentersSnapshot => Presenters;

    internal static void Register(IMpChatSettingsPage page)
    {
        if (page == null || Pages.Contains(page))
            return;
        Pages.Add(page);
    }

    internal static void Unregister(IMpChatSettingsPage page)
    {
        if (page == null)
            return;
        Pages.Remove(page);
    }

    internal static void RegisterPresenter(string addonId, Type flowCoordinatorType, string menuLabel)
    {
        if (string.IsNullOrEmpty(addonId) || flowCoordinatorType == null)
            return;
        Presenters[addonId] = new AddonSettingsPresenterEntry
        {
            AddonId = addonId,
            FlowCoordinatorType = flowCoordinatorType,
            MenuLabel = menuLabel,
            PreloaderGeneration = AddonZenjectPreloader.Generation,
            FlowRegistryGeneration = AddonSettingsFlowRegistry.Generation
        };
        MpChatLog.DebugLine(
            $"[MPChat][Addons] Registered settings presenter: {addonId} ({flowCoordinatorType.FullName})");
    }

    internal static void UnregisterPresenter(string addonId)
    {
        if (string.IsNullOrEmpty(addonId))
            return;
        Presenters.Remove(addonId);
    }

    internal static bool TryGetPresenter(string addonId, out AddonSettingsPresenterEntry entry) =>
        Presenters.TryGetValue(addonId, out entry!);

    internal static void RefreshPresenterFlowRegistryGenerations()
    {
        var generation = AddonSettingsFlowRegistry.Generation;
        foreach (var addonId in Presenters.Keys.ToArray())
        {
            if (!Presenters.TryGetValue(addonId, out var entry))
                continue;

            Presenters[addonId] = new AddonSettingsPresenterEntry
            {
                AddonId = entry.AddonId,
                FlowCoordinatorType = entry.FlowCoordinatorType,
                MenuLabel = entry.MenuLabel,
                PreloaderGeneration = entry.PreloaderGeneration,
                FlowRegistryGeneration = generation
            };
        }
    }

    internal static void Clear()
    {
        Pages.Clear();
        Presenters.Clear();
    }
}
