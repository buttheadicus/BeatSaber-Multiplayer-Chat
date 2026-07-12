using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace MultiplayerChat.Core.Addons;

// loads addon assemblies once at core init so menu settings flow coordinators can be Zenject-bound.
internal static class AddonZenjectPreloader
{
    private static readonly Dictionary<string, Assembly> AssembliesById = new(StringComparer.Ordinal);
    private static int _generation;

    internal static IReadOnlyDictionary<string, Assembly> Assemblies => AssembliesById;

    internal static int Generation => _generation;

    internal static void Run()
    {
        _generation++;
        AssembliesById.Clear();
        if (!Directory.Exists(AddonPaths.AddonsRoot))
            return;

        foreach (var entry in AddonCatalog.Scan())
        {
            try
            {
                var asm = AddonLoadContext.LoadFromFile(entry.DllPath);
                AssembliesById[entry.Manifest.Id] = asm;
            }
            catch (Exception ex)
            {
                MpChatLog.Warn($"[MPChat][Addons] Zenject preload failed for {entry.DllPath}: {ex.Message}");
            }
        }
    }

    internal static bool TryGetAssembly(string addonId, out Assembly assembly) =>
        AssembliesById.TryGetValue(addonId, out assembly!);

    internal static Type? ResolveType(string addonId, string fullTypeName)
    {
        if (!AssembliesById.TryGetValue(addonId, out var asm))
            return null;

        return asm.GetType(fullTypeName, throwOnError: false);
    }
}
