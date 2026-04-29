using System;
using HMUI;
using MultiplayerChat.Core;
using Zenject;

namespace MultiplayerChat.UI;

public class VoiceSettingsFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly VoiceSettingsViewController _voiceSettingsViewController = null!;
    [Inject] private readonly VoiceDuckSettingsFlowCoordinator _duckSettingsFlowCoordinator = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            showBackButton = true;
            SetTitle("Voice Settings");
            ProvideInitialViewControllers(_voiceSettingsViewController);
        }

        if (addedToHierarchy)
        {
            _voiceSettingsViewController.ApplyClicked += OnApply;
            _voiceSettingsViewController.ConfigureLowerVolumeWhenSpeakingClicked += OnConfigureLowerVolumeWhenSpeaking;
        }
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
        {
            _voiceSettingsViewController.ApplyClicked -= OnApply;
            _voiceSettingsViewController.ConfigureLowerVolumeWhenSpeakingClicked -= OnConfigureLowerVolumeWhenSpeaking;
        }
    }

    private void OnConfigureLowerVolumeWhenSpeaking()
    {
        var child = FlowCoordinatorHelper.GetChildFlowCoordinator(this);
        if (child == _duckSettingsFlowCoordinator)
            return;

        _duckSettingsFlowCoordinator.ParentFlow = this;
        PresentFlowCoordinator(_duckSettingsFlowCoordinator);
    }

    private void OnApply(object? sender, EventArgs e) => Dismiss();

    protected override void BackButtonWasPressed(ViewController topViewController) => Dismiss();

    private void Dismiss()
    {
        if (ParentFlow != null)
            ParentFlow.DismissFlowCoordinator(this);
        else
            BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
    }
}
