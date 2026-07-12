using System;
using System.IO;
using System.Reflection;

namespace MultiplayerChat.Core.Addons;

internal static class AddonPaths
{
    private static string? _pluginsDirectory;

    internal static string PluginsDirectory
    {
        get
        {
            if (!string.IsNullOrEmpty(_pluginsDirectory))
                return _pluginsDirectory!;

            var fromAssembly = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(fromAssembly))
                throw new InvalidOperationException("[MPChat][Addons] Could not resolve Plugins directory.");

            _pluginsDirectory = fromAssembly;
            return _pluginsDirectory;
        }
    }

    internal static string ContractsDllPath =>
        Path.Combine(PluginsDirectory, "MultiplayerChat.Contracts.dll");

    internal static string AddonsRoot =>
        Path.Combine(PluginsDirectory, "MultiplayerChat", "Addons");

    internal static string ManifestPathForDll(string dllPath) =>
        Path.ChangeExtension(dllPath, ".addon.json");
}
