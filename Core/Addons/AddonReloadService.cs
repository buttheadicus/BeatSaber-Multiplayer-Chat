using System;

namespace MultiplayerChat.Core.Addons;

internal static class AddonReloadService
{
    internal static void ReloadAddonsAndUiBindings(string reason)
    {
        MpChatLog.Info($"[MPChat][Addons] Reload requested ({reason}).");
        AddonSettingsPresenter.TeardownPresentedFlows();
        AddonHost.Instance?.UnloadAll();
        AddonZenjectPreloader.Run();
        AddonZenjectSettingsBinder.RefreshMenuBindings();
        AddonAffinityForwarder.ResetWarnings();
        AddonAffinityForwarder.ClearContextPatchers();
        AddonHost.Instance?.LoadAll();
        AddonSettingsBridge.RefreshPresenterFlowRegistryGenerations();
        LogAddonDiskHashes();
        MpChatLog.Info("[MPChat][Addons] Reload finished.");
    }

    private static void LogAddonDiskHashes()
    {
        foreach (var entry in AddonCatalog.Scan())
        {
            var hashPrefix = entry.FileHash.Length <= 12 ? entry.FileHash : entry.FileHash.Substring(0, 12);
            MpChatLog.DebugLine($"[MPChat][Addons] Disk hash {entry.Manifest.Id}: {hashPrefix}...");
        }
    }
}
