using System;
using HMUI;
using MultiplayerChat.Core;
using Zenject;

namespace MultiplayerChat.UI;

public sealed class AddonsSettingsFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly AddonsSettingsViewController _addonsView = null!;

    [InjectOptional] private readonly CustomAvatarsSettingsFlowCoordinator? _customAvatarsFlow;

    [InjectOptional] private readonly QuickBindsSettingsFlowCoordinator? _quickBindsFlow;

    [InjectOptional] private readonly AvatarColoringExtensionsSettingsFlowCoordinator? _avatarColoringFlow;

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
        {
            _addonsView.CustomAvatarsClicked += OnCustomAvatarsClicked;
            _addonsView.QuickBindsClicked += OnQuickBindsClicked;
            _addonsView.AvatarColoringClicked += OnAvatarColoringClicked;
        }
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
        {
            _addonsView.CustomAvatarsClicked -= OnCustomAvatarsClicked;
            _addonsView.QuickBindsClicked -= OnQuickBindsClicked;
            _addonsView.AvatarColoringClicked -= OnAvatarColoringClicked;
        }
    }

    private void OnAvatarColoringClicked()
    {
        if (_avatarColoringFlow == null)
            return;

        var child = FlowCoordinatorHelper.GetChildFlowCoordinator(this);
        if (child == _avatarColoringFlow)
            return;

        _avatarColoringFlow.ParentFlow = this;
        PresentFlowCoordinator(_avatarColoringFlow);
    }

    private void OnCustomAvatarsClicked()
    {
        if (_customAvatarsFlow == null)
            return;

        var child = FlowCoordinatorHelper.GetChildFlowCoordinator(this);
        if (child == _customAvatarsFlow)
            return;

        _customAvatarsFlow.ParentFlow = this;
        PresentFlowCoordinator(_customAvatarsFlow);
    }

    private void OnQuickBindsClicked()
    {
        if (_quickBindsFlow == null)
            return;

        var child = FlowCoordinatorHelper.GetChildFlowCoordinator(this);
        if (child == _quickBindsFlow)
            return;

        _quickBindsFlow.ParentFlow = this;
        PresentFlowCoordinator(_quickBindsFlow);
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
