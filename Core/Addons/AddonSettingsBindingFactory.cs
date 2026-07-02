using System;
using System.Collections.Generic;
using System.Reflection;
using HMUI;
using Zenject;

namespace MultiplayerChat.Core.Addons;

internal static class AddonSettingsBindingFactory
{
    internal static FlowCoordinator? CreateSettingsFlow(string addonId, Type flowType, DiContainer parentContainer)
    {
        var canonicalFlow = ResolveInjectType(addonId, flowType);
        if (canonicalFlow == null)
            return null;

        try
        {
            var sub = parentContainer.CreateSubContainer();
            BindDependencyGraph(sub, addonId, canonicalFlow);
            return sub.Resolve(canonicalFlow) as FlowCoordinator;
        }
        catch (Exception ex)
        {
            MpChatLog.Warn($"[MPChat][Addons] Settings bind factory failed for {addonId}: {ex.Message}");
            return null;
        }
    }

    private static void BindDependencyGraph(DiContainer sub, string addonId, Type flowType)
    {
        var pendingFlows = new Queue<Type>();
        var flowTypes = new HashSet<Type>();
        var viewTypes = new HashSet<Type>();

        pendingFlows.Enqueue(flowType);
        while (pendingFlows.Count > 0)
        {
            var type = pendingFlows.Dequeue();
            if (!flowTypes.Add(type))
                continue;

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.GetCustomAttribute<InjectAttribute>() == null)
                    continue;

                var depType = ResolveInjectType(addonId, field.FieldType);
                if (depType == null)
                    continue;

                if (typeof(ViewController).IsAssignableFrom(depType))
                {
                    viewTypes.Add(depType);
                    continue;
                }

                if (typeof(FlowCoordinator).IsAssignableFrom(depType) && !flowTypes.Contains(depType))
                    pendingFlows.Enqueue(depType);
            }
        }

        foreach (var viewType in viewTypes)
            sub.Bind(viewType).FromNewComponentAsViewController().AsTransient();

        foreach (var boundFlow in flowTypes)
            sub.Bind(boundFlow).FromNewComponentOnNewGameObject().AsTransient();
    }

    private static Type? ResolveInjectType(string addonId, Type type)
    {
        if (string.IsNullOrEmpty(type.FullName))
            return type;

        return AddonZenjectPreloader.ResolveType(addonId, type.FullName) ?? type;
    }
}
