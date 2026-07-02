using System;

namespace MultiplayerChat.Core.Addons;

// Affinity shims live in MultiplayerChat.dll; addon runtime registers implementations here.
internal static class AddonAffinityShimBridge
{
    internal static Func<string, string, object?[], object?>? CreatePatcher { get; private set; }

    internal static Action<object?, string, object?[]>? InvokeVoid { get; private set; }

    internal static Func<object?, string, object?[], bool>? InvokePrefix { get; private set; }

    internal static Func<string, string, string, object?[], bool>? InvokeStaticPrefix { get; private set; }

    internal static void Register(
        Func<string, string, object?[], object?> createPatcher,
        Action<object?, string, object?[]> invokeVoid,
        Func<object?, string, object?[], bool> invokePrefix,
        Func<string, string, string, object?[], bool> invokeStaticPrefix)
    {
        CreatePatcher = createPatcher;
        InvokeVoid = invokeVoid;
        InvokePrefix = invokePrefix;
        InvokeStaticPrefix = invokeStaticPrefix;
    }

    internal static object? CreatePatcherOrNull(string addonId, string typeFullName, params object?[] contextualArgs) =>
        CreatePatcher?.Invoke(addonId, typeFullName, contextualArgs);

    internal static void InvokeVoidOrNoop(object? target, string methodName, params object?[] args) =>
        InvokeVoid?.Invoke(target, methodName, args);

    internal static bool InvokePrefixOrTrue(object? target, string methodName, params object?[] args) =>
        InvokePrefix?.Invoke(target, methodName, args) ?? true;

    internal static bool InvokeStaticPrefixOrTrue(
        string addonId,
        string typeFullName,
        string methodName,
        params object?[] args) =>
        InvokeStaticPrefix?.Invoke(addonId, typeFullName, methodName, args) ?? true;
}
