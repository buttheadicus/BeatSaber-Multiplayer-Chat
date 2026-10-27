using HMUI;
using MultiplayerChat.Core;
using Zenject;

namespace MultiplayerChat.UI;

public sealed class ExperimentalSettingsFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly ExperimentalWarningViewController _warningView = null!;
    [Inject] private readonly ExperimentalSettingsViewController _hubView = null!;
    [Inject] private readonly ExperimentalChatSongFlowCoordinator _chatSongFlow = null!;
    [Inject] private readonly ExperimentalSkinSnatcherFlowCoordinator _skinSnatcherFlow = null!;
    [Inject] private readonly ExperimentalExtraStatsFlowCoordinator _extraStatsFlow = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
            showBackButton = true;

        // HMUI presents _providedMainViewController after DidActivate. stack is empty here
        // on reopen, so never ReplaceTopViewController in this method.
        if (addedToHierarchy)
        {
            ProvideBootPage();
            _warningView.Accepted += OnAccepted;
            _hubView.ChatSongClicked += OnChatSongClicked;
            _hubView.SkinSnatcherClicked += OnSkinSnatcherClicked;
            _hubView.ExtraStatsClicked += OnExtraStatsClicked;
        }
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (!removedFromHierarchy)
            return;
        _warningView.Accepted -= OnAccepted;
        _hubView.ChatSongClicked -= OnChatSongClicked;
        _hubView.SkinSnatcherClicked -= OnSkinSnatcherClicked;
        _hubView.ExtraStatsClicked -= OnExtraStatsClicked;
    }

    private void ProvideBootPage()
    {
        if (ExperimentalSession.AcceptedThisBoot)
        {
            SetTitle("EXPERIMENTAL");
            ProvideInitialViewControllers(_hubView);
        }
        else
        {
            SetTitle("WARNING");
            ProvideInitialViewControllers(_warningView);
        }
    }

    private void OnAccepted()
    {
        ExperimentalSession.AcceptedThisBoot = true;
        MultiplayerChat.Plugin.Log?.Info("[MPChat][Experimental] warning accepted for this boot");
        SetTitle("EXPERIMENTAL");
        ProvideInitialViewControllers(_hubView);
        ReplaceTopViewController(_hubView);
    }

    private void OnChatSongClicked()
    {
        if (FlowCoordinatorHelper.GetChildFlowCoordinator(this) == _chatSongFlow)
            return;
        _chatSongFlow.ParentFlow = this;
        PresentFlowCoordinator(_chatSongFlow);
    }

    private void OnSkinSnatcherClicked()
    {
        if (FlowCoordinatorHelper.GetChildFlowCoordinator(this) == _skinSnatcherFlow)
            return;
        _skinSnatcherFlow.ParentFlow = this;
        PresentFlowCoordinator(_skinSnatcherFlow);
    }

    private void OnExtraStatsClicked()
    {
        if (FlowCoordinatorHelper.GetChildFlowCoordinator(this) == _extraStatsFlow)
            return;
        _extraStatsFlow.ParentFlow = this;
        PresentFlowCoordinator(_extraStatsFlow);
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
