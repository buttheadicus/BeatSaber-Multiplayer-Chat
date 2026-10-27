using System;
using System.Collections;
using System.Collections.Generic;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Core;
using TMPro;
using Zenject;

namespace MultiplayerChat.UI;

[ViewDefinition("MultiplayerChat.UI.ExperimentalSkinSnatcherView.bsml")]
public sealed class ExperimentalSkinSnatcherViewController : BSMLAutomaticViewController
{
    [Inject] private readonly IMultiplayerSessionManager _sessionManager = null!;

    [UIComponent("SkinSnatcherPlayerDropdown")] private DropDownListSetting? _playerDropdown;
    [UIComponent("SkinSnatcherStatus")] private TMP_Text? _status;

    private readonly List<object> _playerOptions = new() { "(none)" };
    private readonly List<IConnectedPlayer?> _playersByOption = new() { null };

    [UIValue("SkinSnatcherPlayerOptions")]
    public IList SkinSnatcherPlayerOptions => _playerOptions;

    [UIAction("#post-parse")]
    private void PostParse()
    {
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
        RebuildPlayerDropdown();
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
        RebuildPlayerDropdown();
    }

    [UIAction("RefreshPlayersClicked")]
    private void OnRefreshPlayersClicked()
    {
        RebuildPlayerDropdown();
        if (_status != null)
            _status.text = _playersByOption.Count <= 1 ? "no other players in the lobby" : "list refreshed";
    }

    [UIAction("SnatchClicked")]
    private void OnSnatchClicked()
    {
        var player = SelectedPlayer();
        if (player == null)
        {
            SetStatus("pick a player first");
            return;
        }

        var userName = string.IsNullOrWhiteSpace(player.userName) ? player.userId : player.userName;
        var ok = SkinSnatcherExporter.TrySaveToAvatarStorage(player.userId ?? "", userName ?? "", out var error);
        SetStatus(ok ? "saved " + userName : (error ?? "snatch failed"));
    }

    private IConnectedPlayer? SelectedPlayer()
    {
        if (_playerDropdown == null)
            return null;
        var label = _playerDropdown.Value?.ToString() ?? "";
        for (var i = 0; i < _playerOptions.Count; i++)
        {
            if (string.Equals(_playerOptions[i]?.ToString(), label, StringComparison.Ordinal))
                return i < _playersByOption.Count ? _playersByOption[i] : null;
        }

        return null;
    }

    private void RebuildPlayerDropdown()
    {
        _playerOptions.Clear();
        _playersByOption.Clear();

        var connected = _sessionManager?.connectedPlayers;
        if (connected != null)
        {
            foreach (var p in connected)
            {
                if (p == null || p.isMe || !p.isConnected)
                    continue;
                var name = string.IsNullOrWhiteSpace(p.userName) ? p.userId : p.userName;
                if (string.IsNullOrEmpty(name))
                    continue;
                _playerOptions.Add(name);
                _playersByOption.Add(p);
            }
        }

        if (_playerOptions.Count == 0)
        {
            _playerOptions.Add("(none)");
            _playersByOption.Add(null);
        }

        if (_playerDropdown == null)
            return;

        _playerDropdown.Values = _playerOptions;
        _playerDropdown.UpdateChoices();
        _playerDropdown.Value = _playerOptions[0];
        _playerDropdown.ReceiveValue();
    }

    private void SetStatus(string text)
    {
        if (_status != null)
            _status.text = text;
    }
}
