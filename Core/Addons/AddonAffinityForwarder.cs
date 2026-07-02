using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Zenject;

namespace MultiplayerChat.Core.Addons;

internal static class AddonAffinityForwarder
{
    private static readonly object WarnLock = new();
    private static readonly HashSet<string> WarnedKeys = new(StringComparer.Ordinal);
    private static readonly ConditionalWeakTable<object, object> ContextPatchers = new();

    internal static object? CreatePatcher(string addonId, string typeFullName, params object?[] contextualArgs)
    {
        if (AddonHost.Instance == null || !AddonHost.Instance.IsAddonLoaded(addonId))
        {
            WarnOnce($"{addonId}:{typeFullName}:not-loaded", $"Patcher skipped for {typeFullName}: addon not loaded.");
            return null;
        }

        if (!AddonPatcherRegistry.TryGet(addonId, typeFullName, out var patcherType))
        {
            WarnOnce($"{addonId}:{typeFullName}:unregistered", $"Patcher skipped for {typeFullName}: not registered.");
            return null;
        }

        var contextKey = GetContextKey(contextualArgs);
        if (contextKey != null && ContextPatchers.TryGetValue(contextKey, out var cached))
            return cached;

        var menuContainer = AddonZenjectSettingsBinder.MenuContainer;
        if (menuContainer == null)
        {
            WarnOnce($"{addonId}:{typeFullName}:no-menu", $"Patcher skipped for {typeFullName}: menu container missing.");
            return null;
        }

        try
        {
            var parentContainer = ResolveInjectionContainer(contextualArgs) ?? menuContainer;
            var patcher = InstantiatePatcher(parentContainer, patcherType, contextualArgs);

            if (contextKey != null)
                ContextPatchers.Add(contextKey, patcher);

            return patcher;
        }
        catch (Exception ex)
        {
            WarnOnce($"{addonId}:{typeFullName}:instantiate", $"Patcher instantiate failed for {typeFullName}: {ex.Message}");
            return null;
        }
    }

    private static object InstantiatePatcher(
        DiContainer parentContainer,
        Type patcherType,
        object?[] contextualArgs)
    {
        if (contextualArgs.Length == 0)
            return parentContainer.Instantiate(patcherType);

        var subContainer = parentContainer.CreateSubContainer();
        foreach (var arg in contextualArgs)
        {
            if (arg == null)
                continue;

            BindContextInstance(subContainer, arg);
        }

        return subContainer.Instantiate(patcherType);
    }

    private static void BindContextInstance(DiContainer container, object instance)
    {
        var instanceType = instance.GetType();
        container.Bind(instanceType).FromInstance(instance);
        for (var baseType = instanceType.BaseType; baseType != null && baseType != typeof(object); baseType = baseType.BaseType)
            container.Bind(baseType).FromInstance(instance);
        foreach (var iface in instanceType.GetInterfaces())
            container.Bind(iface).FromInstance(instance);
    }

    private static DiContainer? ResolveInjectionContainer(object?[] contextualArgs)
    {
        for (var i = 0; i < contextualArgs.Length; i++)
        {
            if (contextualArgs[i] is not Component component)
                continue;

            var gameObjectContext = component.GetComponentInParent<GameObjectContext>(true);
            if (gameObjectContext != null)
                return gameObjectContext.Container;

            var sceneContext = component.GetComponentInParent<SceneContext>(true);
            if (sceneContext != null)
                return sceneContext.Container;
        }

        return null;
    }

    internal static void ClearContextPatchers()
    {
        // ConditionalWeakTable has no Clear() on net472; entries expire when view controllers are collected.
    }

    internal static void InvokeVoid(object? target, string methodName, params object?[] args)
    {
        if (target == null)
            return;

        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        method?.Invoke(target, args);
    }

    internal static bool InvokePrefix(object? target, string methodName, params object?[] args)
    {
        if (target == null)
            return true;

        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
            return true;

        var result = method.Invoke(target, args);
        return result is not bool b || b;
    }

    internal static bool InvokeStaticPrefix(
        string addonId,
        string typeFullName,
        string methodName,
        params object?[] args)
    {
        if (AddonHost.Instance == null || !AddonHost.Instance.IsAddonLoaded(addonId))
            return true;

        if (!AddonPatcherRegistry.TryGet(addonId, typeFullName, out var patcherType))
            return true;

        var method = patcherType.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
            return true;

        try
        {
            var result = method.Invoke(null, args);
            return result is not bool b || b;
        }
        catch (Exception ex)
        {
            WarnOnce($"{addonId}:{typeFullName}:{methodName}", $"Static patcher call failed for {methodName}: {ex.Message}");
            return true;
        }
    }

    private static void WarnOnce(string key, string message)
    {
        lock (WarnLock)
        {
            if (!WarnedKeys.Add(key))
                return;
        }

        MpChatLog.Warn($"[MPChat][Addons] {message}");
    }

    internal static void ResetWarnings() => WarnedKeys.Clear();

    private static object? GetContextKey(object?[] contextualArgs)
    {
        for (var i = 0; i < contextualArgs.Length; i++)
        {
            if (contextualArgs[i] != null)
                return contextualArgs[i];
        }

        return null;
    }
}
