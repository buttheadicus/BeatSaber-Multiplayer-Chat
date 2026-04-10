using HMUI;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// FlowCoordinator that presents the player list for Mute or DM selection.
/// </summary>
public class PlayerListFlowCoordinator : FlowCoordinator
{
    [Inject] private readonly PlayerListViewController _playerListViewController = null!;

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
        SetTitle(Mode == PlayerListViewController.Mode.Mute ? "Mute / Unmute" : "DM PLAYER");
        _playerListViewController.SetMode(Mode, () => ParentFlow?.DismissFlowCoordinator(this));
        ProvideInitialViewControllers(_playerListViewController);
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        if (removedFromHierarchy)
            ParentFlow = null;
    }

    protected override void BackButtonWasPressed(ViewController topViewController)
    {
        ParentFlow?.DismissFlowCoordinator(this);
    }
}
