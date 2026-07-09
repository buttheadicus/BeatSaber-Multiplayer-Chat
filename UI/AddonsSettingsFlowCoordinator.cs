using HMUI;
using MultiplayerChat.Contracts;
using MultiplayerChat.Core;
using MultiplayerChat.Core.Addons;
using Zenject;

namespace MultiplayerChat.UI;

public sealed class AddonsSettingsFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly AddonsSettingsViewController _addonsView = null!;
    [Inject] private readonly DiContainer _container = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            showBackButton = true;
            SetTitle("Addons");
            ProvideInitialViewControllers(_addonsView);
        }

        if (addedToHierarchy)
            _addonsView.AddonClicked += OnAddonClicked;
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
            _addonsView.AddonClicked -= OnAddonClicked;
    }

    private void OnAddonClicked(string addonId)
    {
        if (!AddonSettingsBridge.TryGetPresenter(addonId, out _))
        {
            var reloadHint = MpChatDebugMode.IsEnabled
                ? " Press J to reload addons after copying DLLs."
                : " Restart the game after copying addon DLLs.";
            MultiplayerChat.Plugin.Log?.Warn(
                $"[MPChat][Addons] {AddonEnablement.DisplayNameFor(addonId)} is not loaded.{reloadHint}");
            return;
        }

        if (!AddonSettingsNavigator.TryPresent(this, addonId, _container))
            MultiplayerChat.Plugin.Log?.Warn($"[MPChat][Addons] Could not open settings for {addonId}.");
    }

    protected override void BackButtonWasPressed(ViewController topViewController) => Dismiss();

    private void Dismiss()
    {
        if (ParentFlow != null)
            ParentFlow.DismissFlowCoordinator(this);
        else
            BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
    }
}
