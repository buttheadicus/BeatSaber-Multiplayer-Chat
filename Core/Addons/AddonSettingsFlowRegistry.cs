using System;
using System.Collections.Generic;
using MultiplayerChat.Contracts;

namespace MultiplayerChat.Core.Addons;

internal sealed class AddonSettingsFlowBinding
{
    internal string AddonId { get; init; } = string.Empty;

    internal Type FlowType { get; init; } = null!;

    internal Type ViewType { get; init; } = null!;

    internal Type? NestedFlowType { get; init; }

    internal Type? NestedViewType { get; init; }
}

internal static class AddonSettingsFlowRegistry
{
    private static readonly Dictionary<string, AddonSettingsFlowBinding> Bindings =
        new(StringComparer.Ordinal);

    internal static int Generation { get; private set; }

    internal static void Rebuild()
    {
        Bindings.Clear();
        Generation++;

        Register(
            AddonIds.QuickBinds,
            "MultiplayerChat.UI.QuickBindsSettingsFlowCoordinator",
            "MultiplayerChat.UI.QuickBindsSettingsViewController",
            "MultiplayerChat.UI.QuickBindsOptionsSettingsFlowCoordinator",
            "MultiplayerChat.UI.QuickBindsOptionsSettingsViewController");
        Register(
            AddonIds.AvatarColoring,
            "MultiplayerChat.UI.AvatarColoringExtensionsSettingsFlowCoordinator",
            "MultiplayerChat.UI.AvatarColoringExtensionsSettingsViewController");
        Register(
            AddonIds.CustomAvatars,
            "MultiplayerChat.UI.CustomAvatarsSettingsFlowCoordinator",
            "MultiplayerChat.UI.CustomAvatarsSettingsViewController");

        RegisterPresentersFromBindings();
    }

    private static void RegisterPresentersFromBindings()
    {
        foreach (var binding in Bindings.Values)
        {
            var label = AddonEnablement.DisplayNameFor(binding.AddonId);
            AddonSettingsBridge.RegisterPresenter(binding.AddonId, binding.FlowType, label);
        }
    }

    internal static bool TryGet(string addonId, out AddonSettingsFlowBinding binding) =>
        Bindings.TryGetValue(addonId, out binding!);

    private static void Register(
        string addonId,
        string flowTypeName,
        string viewTypeName,
        string? nestedFlowTypeName = null,
        string? nestedViewTypeName = null)
    {
        var flowType = AddonZenjectPreloader.ResolveType(addonId, flowTypeName);
        var viewType = AddonZenjectPreloader.ResolveType(addonId, viewTypeName);
        if (flowType == null || viewType == null)
            return;

        Type? nestedFlowType = null;
        Type? nestedViewType = null;
        if (!string.IsNullOrEmpty(nestedFlowTypeName) && !string.IsNullOrEmpty(nestedViewTypeName))
        {
            nestedFlowType = AddonZenjectPreloader.ResolveType(addonId, nestedFlowTypeName!);
            nestedViewType = AddonZenjectPreloader.ResolveType(addonId, nestedViewTypeName!);
            if (nestedFlowType == null || nestedViewType == null)
                return;
        }

        Bindings[addonId] = new AddonSettingsFlowBinding
        {
            AddonId = addonId,
            FlowType = flowType,
            ViewType = viewType,
            NestedFlowType = nestedFlowType,
            NestedViewType = nestedViewType
        };
    }
}
