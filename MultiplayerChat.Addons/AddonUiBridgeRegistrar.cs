using HMUI;
using MultiplayerChat.Core.Addons;
using MultiplayerChat.UI;
using Zenject;

namespace MultiplayerChat.Addons;

internal sealed class AddonUiBridgeRegistrar : IInitializable
{
    [Inject] private readonly AddonsSettingsFlowCoordinator _addonsSettingsFlow = null!;

    public void Initialize() => AddonUiBridge.SetAddonsSettingsFlow(_addonsSettingsFlow);
}
