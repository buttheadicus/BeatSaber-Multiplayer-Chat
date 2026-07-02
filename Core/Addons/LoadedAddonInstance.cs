using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MultiplayerChat.Contracts;

namespace MultiplayerChat.Core.Addons;

internal sealed class LoadedAddonInstance : IDisposable
{
    private readonly List<IDisposable> _disposables = new();
    private readonly HarmonyLib.Harmony _harmony;

    internal LoadedAddonInstance(
        IMpChatAddon addon,
        Assembly assembly,
        string dllPath,
        string fileHash,
        HarmonyLib.Harmony harmony)
    {
        Addon = addon;
        Assembly = assembly;
        DllPath = dllPath;
        FileHash = fileHash;
        _harmony = harmony;
    }

    internal IMpChatAddon Addon { get; }

    internal Assembly Assembly { get; }

    internal string DllPath { get; }

    internal string FileHash { get; }

    internal void Track(IDisposable disposable) => _disposables.Add(disposable);

    internal HarmonyLib.Harmony Harmony => _harmony;

    internal void UnpatchHarmony() => AffinityHarmonyForwarder.UnpatchAssembly(_harmony);

    public void Dispose()
    {
        for (var i = _disposables.Count - 1; i >= 0; i--)
        {
            try
            {
                _disposables[i].Dispose();
            }
            catch
            {
                // ignored
            }
        }

        _disposables.Clear();
        UnpatchHarmony();
    }
}
