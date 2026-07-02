using System;
using System.IO;
using System.Reflection;
using SiraUtil.Objects.Multiplayer;
using SiraUtil.Zenject;
using IPALogger = IPA.Logging.Logger;

namespace MultiplayerChat.Core.Addons;

internal static class AddonSystemBridge
{
    private const string AddonsDllFileName = "MultiplayerChat.Addons.dll";
    private const string EmbeddedAddonsResourceName = "MultiplayerChat.Embedded.MultiplayerChat.Addons.dll";

    private static Assembly? _addonsAssembly;
    private static Type? _entryType;

    internal static void Initialize(IPALogger logger, Zenjector zenjector)
    {
        AddonContractsEarlyEnsure.Initialize();

        var addonsPath = GetAddonsDllPath();
        EnsureAddonsDllOnDisk(addonsPath);

        _addonsAssembly = Assembly.LoadFrom(addonsPath);
        _entryType = _addonsAssembly.GetType("MultiplayerChat.Addons.AddonRuntimeEntry", throwOnError: true);
        Invoke(nameof(Initialize), logger, zenjector);
    }

    internal static void UnloadAll() => Invoke(nameof(UnloadAll));

    internal static void DecorateLobbyAvatar(MultiplayerLobbyAvatarController original) =>
        Invoke(nameof(DecorateLobbyAvatar), original);

    internal static void DecorateLobbyAvatarPlace(MultiplayerLobbyAvatarPlace original) =>
        Invoke(nameof(DecorateLobbyAvatarPlace), original);

    private static string GetAddonsDllPath() =>
        Path.Combine(AddonPaths.PluginsDirectory, "MultiplayerChat", AddonsDllFileName);

    private static void EnsureAddonsDllOnDisk(string addonsPath)
    {
        if (File.Exists(addonsPath))
            return;

        var dir = Path.GetDirectoryName(addonsPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedAddonsResourceName);
        if (stream == null)
            throw new InvalidOperationException(
                $"[MPChat][Addons] Embedded addon runtime missing ({EmbeddedAddonsResourceName}).");

        using var file = File.Create(addonsPath);
        stream.CopyTo(file);
    }

    private static void Invoke(string methodName, params object[] args)
    {
        if (_entryType == null)
            throw new InvalidOperationException("[MPChat][Addons] Addon runtime is not loaded.");

        var method = _entryType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        if (method == null)
            throw new MissingMethodException(_entryType.FullName, methodName);

        method.Invoke(null, args);
    }
}
