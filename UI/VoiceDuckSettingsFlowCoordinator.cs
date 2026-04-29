using System;
using HMUI;
using Zenject;

namespace MultiplayerChat.UI;

public class VoiceDuckSettingsFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly VoiceDuckSettingsViewController _duckView = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            showBackButton = true;
            SetTitle("Lower volume when speaking");
            ProvideInitialViewControllers(_duckView);
            _duckView.DuckSettingsApplied += OnDuckSettingsApplied;
        }
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
            _duckView.DuckSettingsApplied -= OnDuckSettingsApplied;
    }

    private void OnDuckSettingsApplied() => Dismiss();

    protected override void BackButtonWasPressed(ViewController topViewController) => Dismiss();

    private void Dismiss()
    {
        if (ParentFlow != null)
            ParentFlow.DismissFlowCoordinator(this);
        else
            BeatSaberMarkupLanguage.BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
    }
}
