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
            var name = AddonEnablement.DisplayNameFor(addonId);
            var onDisk = false;
            foreach (var entry in AddonCatalog.Scan())
            {
                if (!string.Equals(entry.Manifest.Id, addonId, System.StringComparison.Ordinal))
                    continue;
                onDisk = true;
                break;
            }
            // this is just for me, or... if you want to make your own addon, you should probably just make an independent mod because my codebase is so amazing that you will probably kill yourself trying to make an addon with it.. on second though, this doesnt even work that well, resetting the game is WAY more reliable when i change an addon... im too lazy to change it, but not lazy enough to write an amazing comment about it
            var reloadHint = MpChatDebugMode.IsEnabled
                ? " Press J to reload addons after copying DLLs."
                : " Restart the game after copying addon DLLs.";
            if (!onDisk)
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat][Addons] {name} DLL is not installed.{reloadHint}");
            else
                MultiplayerChat.Plugin.Log?.Warn($"[MPChat][Addons] {name} is not loaded.{reloadHint}");
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
