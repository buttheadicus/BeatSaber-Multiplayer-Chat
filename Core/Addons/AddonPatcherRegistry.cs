using System;
using System.Collections.Generic;

namespace MultiplayerChat.Core.Addons;

internal static class AddonPatcherRegistry
{
    private static readonly Dictionary<string, Type> Patchers =
        new(StringComparer.Ordinal);

    internal static void Clear() => Patchers.Clear();

    internal static void Register(string addonId, string typeFullName, Type patcherType) =>
        Patchers[$"{addonId}:{typeFullName}"] = patcherType;

    internal static bool TryGet(string addonId, string typeFullName, out Type patcherType) =>
        Patchers.TryGetValue($"{addonId}:{typeFullName}", out patcherType!);
}
