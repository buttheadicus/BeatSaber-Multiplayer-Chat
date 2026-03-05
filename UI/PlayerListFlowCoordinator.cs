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
    public System.Action? OnDMDismissed { get; set; }

    public void Present(HMUI.FlowCoordinator parent, PlayerListViewController.Mode mode, System.Action? onDmDismissed = null)
    {
        ParentFlow = parent;
        Mode = mode;
        OnDMDismissed = onDmDismissed;
        parent.PresentFlowCoordinator(this);
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        showBackButton = true;
        SetTitle(Mode == PlayerListViewController.Mode.Mute ? "Mute / Unmute Player" : "DM Player");
        _playerListViewController.SetMode(Mode, () =>
        {
            OnDMDismissed?.Invoke();
            ParentFlow?.DismissFlowCoordinator(this);
        });
        ProvideInitialViewControllers(_playerListViewController);
    }

    protected override void BackButtonWasPressed(ViewController topViewController)
    {
        ParentFlow?.DismissFlowCoordinator(this);
    }
}
