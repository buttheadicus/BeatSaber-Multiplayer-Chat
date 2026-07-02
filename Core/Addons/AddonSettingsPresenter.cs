using System;
using System.Collections.Generic;
using System.Reflection;
using HMUI;
using Zenject;

namespace MultiplayerChat.Core.Addons;

internal static class AddonSettingsPresenter
{
    private static readonly List<FlowCoordinator> PresentedFlows = new();

    internal static void TeardownPresentedFlows()
    {
        for (var i = PresentedFlows.Count - 1; i >= 0; i--)
        {
            var fc = PresentedFlows[i];
            if (fc == null)
                continue;

            try
            {
                var parent = GetParentFlow(fc);
                if (parent != null)
                    parent.DismissFlowCoordinator(fc);
            }
            catch (Exception ex)
            {
                MpChatLog.DebugLine($"[MPChat][Addons] Dismiss presented settings flow failed: {ex.Message}");
            }
        }

        PresentedFlows.Clear();
    }

    internal static bool TryPresent(FlowCoordinator parent, string addonId, DiContainer container)
    {
        if (!AddonSettingsBridge.TryGetPresenter(addonId, out var entry))
            return false;

        try
        {
            if (entry.FlowRegistryGeneration != AddonSettingsFlowRegistry.Generation)
            {
                MpChatLog.Warn(
                    $"[MPChat][Addons] Settings registry stale for {addonId}. Press J to reload addons.");
                return false;
            }

            if (!AddonSettingsFlowRegistry.TryGet(addonId, out var binding))
            {
                MpChatLog.Warn($"[MPChat][Addons] Settings flow types missing for {addonId}.");
                return false;
            }

            var menu = AddonZenjectSettingsBinder.MenuContainer ?? container;
            var fc = AddonSettingsFlowInstantiator.Create(menu, binding);
            if (fc == null)
            {
                MpChatLog.Warn($"[MPChat][Addons] Settings flow create returned null for {addonId}.");
                return false;
            }

            SetParentFlow(fc, parent);
            if (!PresentedFlows.Contains(fc))
                PresentedFlows.Add(fc);
            parent.PresentFlowCoordinator(fc);
            return true;
        }
        catch (Exception ex)
        {
            MpChatLog.Warn($"[MPChat][Addons] Settings present failed for {addonId}: {ex.Message}");
            return false;
        }
    }

    private static void SetParentFlow(FlowCoordinator fc, FlowCoordinator parent)
    {
        var prop = fc.GetType().GetProperty("ParentFlow", BindingFlags.Instance | BindingFlags.Public);
        if (prop != null && prop.CanWrite)
            prop.SetValue(fc, parent);
    }

    private static FlowCoordinator? GetParentFlow(FlowCoordinator fc)
    {
        var prop = fc.GetType().GetProperty("ParentFlow", BindingFlags.Instance | BindingFlags.Public);
        return prop?.GetValue(fc) as FlowCoordinator;
    }
}
