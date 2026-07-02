using System;
using System.Collections.Generic;
using System.Linq;
using MultiplayerChat.Contracts;

namespace MultiplayerChat.Core.Addons;

internal sealed class AddonSettingsRow
{
    internal string AddonId { get; init; } = string.Empty;

    internal string Label { get; init; } = string.Empty;

    internal bool CanOpenSettings { get; init; }
}

internal static class AddonSettingsCatalog
{
    internal static readonly string[] KnownOptionalAddonIds =
    {
        AddonIds.QuickBinds,
        AddonIds.AvatarColoring,
        AddonIds.CustomAvatars
    };

    internal static IReadOnlyList<AddonSettingsRow> BuildRows()
    {
        var onDisk = AddonCatalog.Scan().ToDictionary(e => e.Manifest.Id, StringComparer.Ordinal);
        var loaded = AddonHost.Instance?.Snapshot().ToDictionary(s => s.Id, StringComparer.Ordinal)
            ?? new Dictionary<string, LoadedAddonInfo>(StringComparer.Ordinal);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in KnownOptionalAddonIds)
            ids.Add(id);
        foreach (var id in onDisk.Keys)
            ids.Add(id);
        foreach (var id in AddonSettingsBridge.PresentersSnapshot.Keys)
            ids.Add(id);

        var rows = new List<AddonSettingsRow>();
        foreach (var id in ids.OrderBy(id => ResolveDisplayName(id, onDisk), StringComparer.OrdinalIgnoreCase))
        {
            onDisk.TryGetValue(id, out var entry);
            loaded.TryGetValue(id, out var loadedInfo);
            var enabled = AddonEnablement.IsEnabled(id);
            var displayName = ResolveDisplayName(id, onDisk);
            var status = ResolveStatus(entry != null, enabled, loadedInfo);
            var label = $"{displayName} ({status})";
            var canOpen = loadedInfo != null && enabled && AddonSettingsBridge.TryGetPresenter(id, out _);
            rows.Add(new AddonSettingsRow
            {
                AddonId = id,
                Label = label,
                CanOpenSettings = canOpen
            });
        }

        return rows;
    }

    private static string ResolveDisplayName(string addonId, IReadOnlyDictionary<string, AddonCatalogEntry> onDisk)
    {
        if (onDisk.TryGetValue(addonId, out var entry) &&
            !string.IsNullOrWhiteSpace(entry.Manifest.DisplayName))
            return entry.Manifest.DisplayName;

        return AddonEnablement.DisplayNameFor(addonId);
    }

    private static string ResolveStatus(bool onDisk, bool enabled, LoadedAddonInfo? loadedInfo)
    {
        if (!onDisk)
            return "not installed";
        if (!enabled)
            return "disabled";
        if (loadedInfo == null)
            return "not loaded";
        return loadedInfo.Version == new Version(0, 0, 0) ? "loaded" : $"v{loadedInfo.Version}";
    }
}
