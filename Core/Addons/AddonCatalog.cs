using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MultiplayerChat.Contracts;
using Newtonsoft.Json;

namespace MultiplayerChat.Core.Addons;

internal sealed class AddonCatalogEntry
{
    internal string DllPath { get; init; } = string.Empty;

    internal string? ManifestPath { get; init; }

    internal AddonManifest Manifest { get; init; } = new();

    internal string FileHash { get; init; } = string.Empty;
}

internal static class AddonCatalog
{
    internal static IReadOnlyList<AddonCatalogEntry> Scan()
    {
        var root = AddonPaths.AddonsRoot;
        if (!Directory.Exists(root))
            return Array.Empty<AddonCatalogEntry>();

        var results = new List<AddonCatalogEntry>();
        foreach (var dllPath in Directory.EnumerateFiles(root, "MultiplayerChat.Addon.*.dll", SearchOption.TopDirectoryOnly))
        {
            var manifestPath = AddonPaths.ManifestPathForDll(dllPath);
            var manifest = ReadManifest(manifestPath, dllPath);
            results.Add(new AddonCatalogEntry
            {
                DllPath = dllPath,
                ManifestPath = File.Exists(manifestPath) ? manifestPath : null,
                Manifest = manifest,
                FileHash = ComputeFileHash(dllPath)
            });
        }

        return results.OrderBy(e => e.Manifest.Id, StringComparer.Ordinal).ToList();
    }

    private static AddonManifest ReadManifest(string manifestPath, string dllPath)
    {
        if (File.Exists(manifestPath))
        {
            try
            {
                var json = File.ReadAllText(manifestPath);
                var parsed = JsonConvert.DeserializeObject<AddonManifest>(json);
                if (parsed != null && !string.IsNullOrWhiteSpace(parsed.Id))
                    return parsed;
            }
            catch (Exception ex)
            {
                MpChatLog.Warn($"[MPChat][Addons] Failed to read manifest {manifestPath}: {ex.Message}");
            }
        }

        return InferManifestFromDllName(dllPath);
    }

    private static AddonManifest InferManifestFromDllName(string dllPath)
    {
        var file = Path.GetFileNameWithoutExtension(dllPath);
        var suffix = file.StartsWith("MultiplayerChat.Addon.", StringComparison.Ordinal)
            ? file.Substring("MultiplayerChat.Addon.".Length)
            : file;

        var id = suffix switch
        {
            "QuickBinds" => AddonIds.QuickBinds,
            "AvatarColoring" => AddonIds.AvatarColoring,
            "CustomAvatars" => AddonIds.CustomAvatars,
            _ => suffix
        };

        return new AddonManifest
        {
            Id = id,
            DisplayName = suffix,
            EnabledByDefault = true,
            MinCoreVersion = "0.0.0"
        };
    }

    internal static string ComputeFileHash(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = sha.ComputeHash(stream);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
