using System.Collections.Generic;

namespace MultiplayerChat.Contracts;

public sealed class AddonManifest
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string MinCoreVersion { get; set; } = "0.0.0";

    public bool EnabledByDefault { get; set; } = true;

    public List<string> Dependencies { get; set; } = new();

    public AddonCapability Capability { get; set; } = AddonCapability.None;

    public string SettingsModSettingsKey { get; set; } = string.Empty;
}
