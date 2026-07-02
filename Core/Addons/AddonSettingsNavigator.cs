using HMUI;
using Zenject;

namespace MultiplayerChat.Core.Addons;

internal static class AddonSettingsNavigator
{
    internal static bool TryPresent(FlowCoordinator parent, string addonId, DiContainer container) =>
        AddonSettingsPresenter.TryPresent(parent, addonId, container);
}

internal interface IAddonSettingsChildFlow
{
    FlowCoordinator? ParentFlow { get; set; }
}
