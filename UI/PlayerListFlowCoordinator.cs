using HMUI;
using MultiplayerChat.Core;
using Zenject;

namespace MultiplayerChat.UI;

public class PlayerListFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly PlayerListViewController _playerListViewController = null!;
    [Inject] private readonly VoiceDuckSettingsFlowCoordinator _duckSettingsFlowCoordinator = null!;

    public HMUI.FlowCoordinator? ParentFlow { get; set; }
    public PlayerListViewController.Mode Mode { get; set; }

    public void Present(HMUI.FlowCoordinator parent, PlayerListViewController.Mode mode)
    {
        ParentFlow = parent;
        Mode = mode;
        parent.PresentFlowCoordinator(this);
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        showBackButton = true;
        SetTitle(Mode switch
        {
            PlayerListViewController.Mode.Mute => "Mute / Unmute",
            PlayerListViewController.Mode.DM => "DM PLAYER",
            PlayerListViewController.Mode.Volume => "Player volume",
            PlayerListViewController.Mode.Listen => "Hear",
            PlayerListViewController.Mode.TalkTo => "Hear",
            _ => "Players"
        });
        if (Mode == PlayerListViewController.Mode.Volume)
            PlayerVoiceVolumeStore.ReloadFromDisk();

        _playerListViewController.SetMode(Mode, () => ParentFlow?.DismissFlowCoordinator(this));

        if (addedToHierarchy)
        {
            _playerListViewController.RequestSubMode += OnRequestSubMode;
            _playerListViewController.RequestDuckSettings += OnRequestDuckSettings;
        }

        ProvideInitialViewControllers(_playerListViewController);
        _playerListViewController.ForceRefreshUi();
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
        {
            _playerListViewController.RequestSubMode -= OnRequestSubMode;
            _playerListViewController.RequestDuckSettings -= OnRequestDuckSettings;
            ParentFlow = null;
        }
    }

    private void OnRequestSubMode(PlayerListViewController.Mode mode)
    {
        Mode = mode;
        SetTitle(Mode switch
        {
            PlayerListViewController.Mode.Mute => "Mute / Unmute",
            PlayerListViewController.Mode.DM => "DM PLAYER",
            PlayerListViewController.Mode.Volume => "Player volume",
            PlayerListViewController.Mode.Listen => "Hear",
            PlayerListViewController.Mode.TalkTo => "Hear",
            _ => "Players"
        });
        _playerListViewController.SetMode(Mode, () => ParentFlow?.DismissFlowCoordinator(this));
        _playerListViewController.ForceRefreshUi();
    }

    private void OnRequestDuckSettings()
    {
        var child = FlowCoordinatorHelper.GetChildFlowCoordinator(this);
        if (child == _duckSettingsFlowCoordinator)
            return;

        _duckSettingsFlowCoordinator.ParentFlow = this;
        PresentFlowCoordinator(_duckSettingsFlowCoordinator);
    }

    protected override void BackButtonWasPressed(ViewController topViewController)
    {
        if (Mode == PlayerListViewController.Mode.Volume)
            PlayerVoiceVolumeStore.ReloadFromDisk();
        ParentFlow?.DismissFlowCoordinator(this);
    }
}
