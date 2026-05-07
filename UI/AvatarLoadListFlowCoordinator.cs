using System;
using HMUI;
using MultiplayerChat.AvatarColoring;
using Zenject;

namespace MultiplayerChat.UI;

// Lists avatar presets from Avatar Storage and replaces AvatarData.dat when one is picked.
public sealed class AvatarLoadListFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly AvatarLoadListViewController _viewController = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            SetTitle("Load avatar");
            showBackButton = true;
            ProvideInitialViewControllers(_viewController);
        }

        if (addedToHierarchy)
        {
            _viewController.PresetSelected += OnPresetSelected;
            _viewController.Closed += OnClosed;
        }
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
        {
            _viewController.PresetSelected -= OnPresetSelected;
            _viewController.Closed -= OnClosed;
        }
    }

    private void OnPresetSelected(string presetFileName)
    {
        if (!AvatarDatOperations.ApplyPresetFromStorage(presetFileName))
            return;

        AvatarColoringEditorSession.RefreshAfterAvatarDatChangedOnDisk();
        Dismiss();
    }

    private void OnClosed() => Dismiss();

    protected override void BackButtonWasPressed(ViewController topViewController) => Dismiss();

    private void Dismiss()
    {
        if (ParentFlow != null)
            ParentFlow.DismissFlowCoordinator(this);
        else
            BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
    }
}
