using HMUI;

namespace MultiplayerChat.Core.Addons;

internal static class AddonUiBridge
{
    internal static FlowCoordinator? AddonsSettingsFlow { get; private set; }

    internal static void SetAddonsSettingsFlow(FlowCoordinator? flowCoordinator) =>
        AddonsSettingsFlow = flowCoordinator;
}
