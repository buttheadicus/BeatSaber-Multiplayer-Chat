using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using MultiplayerChat.Core;
using MultiplayerCore.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// Player grid for Mute, DM, per-player voice receive volume, listen-only filter, and talk-to selection.
/// </summary>
[ViewDefinition("MultiplayerChat.UI.PlayerList.bsml")]
public class PlayerListViewController : BSMLAutomaticViewController
{
    public enum Mode { Mute, DM, Volume, Listen, TalkTo }

    private const int SlotCount = 12;
    private const int MaxNameLen = 30;
    /// <summary>Each +/- adjusts stored percent by this amount (0…500 ↔ gain 0.0…5.0 in steps of 0.1).</summary>
    private const int VolumeStepPercent = 10;

    [Inject] private readonly ChatMuteManager _muteManager = null!;
    [Inject] private readonly ChatIdConfigStore _chatIdConfigStore = null!;
    [Inject] private readonly ChatDMState _dmState = null!;
    [Inject] private readonly ChatPlayerIdRegistry _chatPlayerIdRegistry = null!;
    [Inject] private readonly ChatManager _chatManager = null!;
    [Inject] private readonly IMultiplayerSessionManager _sessionManager = null!;

    [UIComponent("GridTitle")] private TMP_Text? _gridTitle;
    [UIComponent("GridHint")] private TMP_Text? _gridHint;
    [UIComponent("VoiceModeSwitchButton")] private Button? _voiceModeSwitchButton;
    [UIComponent("ClearAllMutesButton")] private Button? _clearAllMutesButton;

    [UIComponent("VolumeAdjustPanel")] private RectTransform? _volumeAdjustPanel;
    [UIComponent("VolumePlayerLabel")] private TMP_Text? _volumePlayerLabel;
    [UIComponent("PlayerVolumeStepLabel")] private TMP_Text? _playerVolumeStepLabel;
    [UIComponent("SaveVolumesButton")] private Button? _saveVolumesButton;

    [UIComponent("Slot0")] private Button? _slot0;
    [UIComponent("Slot1")] private Button? _slot1;
    [UIComponent("Slot2")] private Button? _slot2;
    [UIComponent("Slot3")] private Button? _slot3;
    [UIComponent("Slot4")] private Button? _slot4;
    [UIComponent("Slot5")] private Button? _slot5;
    [UIComponent("Slot6")] private Button? _slot6;
    [UIComponent("Slot7")] private Button? _slot7;
    [UIComponent("Slot8")] private Button? _slot8;
    [UIComponent("Slot9")] private Button? _slot9;
    [UIComponent("Slot10")] private Button? _slot10;
    [UIComponent("Slot11")] private Button? _slot11;

    private Button?[] _slots = Array.Empty<Button?>();
    private Mode _mode;
    private Action? _onDismiss;
    private List<IConnectedPlayer> _players = new();
    private string? _selectedVolumeUserId;

    public void SetMode(Mode mode, Action? onDismiss = null)
    {
        _mode = mode;
        _onDismiss = onDismiss;
    }

    [UIAction("#post-parse")]
    private void PostParse()
    {
        _slots = new[]
        {
            _slot0, _slot1, _slot2, _slot3, _slot4, _slot5,
            _slot6, _slot7, _slot8, _slot9, _slot10, _slot11
        };
        if (_clearAllMutesButton != null)
            _clearAllMutesButton.gameObject.SetActive(_mode == Mode.Mute);
        if (_volumeAdjustPanel != null)
            _volumeAdjustPanel.gameObject.SetActive(false);
        if (_mode == Mode.Volume)
            ResetVolumeAdjustPanel();

        ApplyTitle();
        UpdateVoiceChrome();
        ReloadGrid();
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        if (_clearAllMutesButton != null)
            _clearAllMutesButton.gameObject.SetActive(_mode == Mode.Mute);
        if (_volumeAdjustPanel != null && _mode != Mode.Volume)
            _volumeAdjustPanel.gameObject.SetActive(false);
        if (_mode == Mode.Volume)
            ResetVolumeAdjustPanel();
        ApplyTitle();
        UpdateVoiceChrome();
        ReloadGrid();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
    }

    [UIAction("VoiceModeSwitchClicked")]
    private void VoiceModeSwitchClicked()
    {
        if (_mode != Mode.Listen && _mode != Mode.TalkTo)
            return;
        _mode = _mode == Mode.TalkTo ? Mode.Listen : Mode.TalkTo;
        ApplyTitle();
        UpdateVoiceChrome();
        ReloadGrid();
    }

    [UIAction("SaveVolumesAndCloseClicked")]
    private void SaveVolumesAndCloseClicked()
    {
        if (_mode != Mode.Volume) return;
        PlayerVoiceVolumeStore.FlushToDisk();
        ResetVolumeAdjustPanel();
        _onDismiss?.Invoke();
    }

    private void ResetVolumeAdjustPanel()
    {
        _selectedVolumeUserId = null;
        if (_volumeAdjustPanel != null)
            _volumeAdjustPanel.gameObject.SetActive(false);
    }

    [UIAction("ClearAllMutesClicked")]
    private void ClearAllMutesClicked()
    {
        if (_mode != Mode.Mute) return;
        _chatIdConfigStore.ClearAllMutes();
        ReloadGrid();
    }

    private void ApplyTitle()
    {
        if (_gridTitle != null)
        {
            _gridTitle.text = _mode switch
            {
                Mode.Mute => "Mute / Unmute",
                Mode.DM => "Press DM again to end the DM!",
                Mode.Volume => "Player volume (voice)",
                Mode.Listen => "Listen (voice)",
                Mode.TalkTo => "Talk to (voice)",
                _ => ""
            };
        }

        if (_gridHint != null)
        {
            _gridHint.text = _mode switch
            {
                Mode.Listen =>
                    "Tap names to add or remove (listening). If nobody is selected, you hear everyone.",
                Mode.TalkTo =>
                    "Listen and talk to a player (you can select as many people as you'd like, doesn't have to be one person; good for groups!).",
                Mode.Volume => "Tap a player, then use - / + from 0.0 to 5.0 . This applies to received hot mic and voice messages.",
                _ => "Tap a name"
            };
        }
    }

    private void UpdateVoiceChrome()
    {
        if (_voiceModeSwitchButton != null)
        {
            var show = _mode == Mode.Listen || _mode == Mode.TalkTo;
            _voiceModeSwitchButton.gameObject.SetActive(show);
            if (show)
            {
                var tmp = _voiceModeSwitchButton.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null)
                    tmp.text = _mode == Mode.TalkTo ? "Listen" : "Talk to";
            }
        }

        if (_saveVolumesButton != null)
            _saveVolumesButton.gameObject.SetActive(_mode == Mode.Volume);
    }

    [UIAction("OnSlot0")] private void OnSlot0() => OnSlotClicked(0);
    [UIAction("OnSlot1")] private void OnSlot1() => OnSlotClicked(1);
    [UIAction("OnSlot2")] private void OnSlot2() => OnSlotClicked(2);
    [UIAction("OnSlot3")] private void OnSlot3() => OnSlotClicked(3);
    [UIAction("OnSlot4")] private void OnSlot4() => OnSlotClicked(4);
    [UIAction("OnSlot5")] private void OnSlot5() => OnSlotClicked(5);
    [UIAction("OnSlot6")] private void OnSlot6() => OnSlotClicked(6);
    [UIAction("OnSlot7")] private void OnSlot7() => OnSlotClicked(7);
    [UIAction("OnSlot8")] private void OnSlot8() => OnSlotClicked(8);
    [UIAction("OnSlot9")] private void OnSlot9() => OnSlotClicked(9);
    [UIAction("OnSlot10")] private void OnSlot10() => OnSlotClicked(10);
    [UIAction("OnSlot11")] private void OnSlot11() => OnSlotClicked(11);

    private void OnSlotClicked(int index)
    {
        if (index < 0 || index >= _players.Count)
            return;
        OnPlayerSelected(_players[index]);
    }

    private void ReloadGrid()
    {
        if (_slots.Length == 0)
            return;

        _players = GetConnectedPlayers();
        var localId = _sessionManager?.localPlayer?.userId;
        if (!string.IsNullOrEmpty(localId))
            _players = _players.Where(p => p.userId != localId).ToList();

        for (var i = 0; i < SlotCount; i++)
        {
            var btn = i < _slots.Length ? _slots[i] : null;
            if (btn == null) continue;

            if (i >= _players.Count)
            {
                btn.gameObject.SetActive(false);
                continue;
            }

            btn.gameObject.SetActive(true);
            var p = _players[i];
            var label = FormatSlotLabel(p);
            SetButtonLabel(btn, label);
            btn.interactable = true;
        }
    }

    private string FormatSlotLabel(IConnectedPlayer p)
    {
        var raw = p.userName ?? p.userId ?? "?";
        var uid = p.userId ?? "";
        if (_mode == Mode.Mute && uid.Length > 0 && _muteManager.IsMuted(uid))
        {
            const string suffix = " (muted)";
            var baseName = TrimName(raw, Math.Max(1, MaxNameLen - suffix.Length));
            return baseName + suffix;
        }

        if (_mode == Mode.Volume && uid.Length > 0)
        {
            var v = PlayerVoiceVolumeStore.GetVolumePercent(uid);
            if (v != 100)
                return TrimName(raw, MaxNameLen) + $" ({v / 100f:F1})";
        }

        if (_mode == Mode.Listen && uid.Length > 0 && VoiceChatRuntimeState.IsListeningTo(uid))
        {
            const string suffix = " (listening)";
            return TrimName(raw, Math.Max(1, MaxNameLen - suffix.Length)) + suffix;
        }

        if (_mode == Mode.TalkTo && uid.Length > 0 && VoiceChatRuntimeState.IsTalkingTo(uid))
        {
            const string suffix = " (talking to)";
            return TrimName(raw, Math.Max(1, MaxNameLen - suffix.Length)) + suffix;
        }

        return TrimName(raw, MaxNameLen);
    }

    private static void SetButtonLabel(Button btn, string text)
    {
        var tmp = btn.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
            tmp.text = text;
    }

    [UIAction("PlayerVolumeDownClicked")]
    private void PlayerVolumeDownClicked() => AdjustSelectedVolumeStep(-VolumeStepPercent);

    [UIAction("PlayerVolumeUpClicked")]
    private void PlayerVolumeUpClicked() => AdjustSelectedVolumeStep(VolumeStepPercent);

    private void AdjustSelectedVolumeStep(int deltaPercent)
    {
        if (_mode != Mode.Volume || string.IsNullOrEmpty(_selectedVolumeUserId))
            return;
        var cur = PlayerVoiceVolumeStore.GetVolumePercent(_selectedVolumeUserId!);
        var next = Mathf.Clamp(cur + deltaPercent, 0, 500);
        PlayerVoiceVolumeStore.SetVolumePercent(_selectedVolumeUserId!, next, persist: false);
        RefreshVolumeStepLabel();
        ReloadGrid();
    }

    private void RefreshVolumeStepLabel()
    {
        if (string.IsNullOrEmpty(_selectedVolumeUserId)) return;
        WriteVolumeStepLabelForUser(_selectedVolumeUserId!);
    }

    private IEnumerator CoApplyVolumeStepUiForPlayer(string userId)
    {
        WriteVolumeStepLabelForUser(userId);
        yield return null;
        yield return null;
        if (_selectedVolumeUserId == userId)
            WriteVolumeStepLabelForUser(userId);
    }

    private void WriteVolumeStepLabelForUser(string userId)
    {
        if (_playerVolumeStepLabel == null || userId.Length == 0) return;
        var p = PlayerVoiceVolumeStore.GetVolumePercent(userId);
        _playerVolumeStepLabel.text = (p / 100f).ToString("F1");
    }

    private void OnPlayerSelected(IConnectedPlayer player)
    {
        if (player == null || string.IsNullOrEmpty(player.userId))
            return;

        if (_mode == Mode.Mute)
        {
            var wasMuted = _muteManager.IsMuted(player.userId);
            _muteManager.ToggleMute(player.userId);
            var nowMuted = _muteManager.IsMuted(player.userId);
            if (wasMuted != nowMuted)
                _chatManager.SendMuteNotifyTo(player.userId, nowMuted);
            ReloadGrid();
            return;
        }

        if (_mode == Mode.Volume)
        {
            var volUid = player.userId ?? "";
            if (volUid.Length == 0) return;
            _selectedVolumeUserId = volUid;
            if (_volumeAdjustPanel != null)
                _volumeAdjustPanel.gameObject.SetActive(true);
            var raw = player.userName ?? volUid;
            if (_volumePlayerLabel != null)
                _volumePlayerLabel.text = TrimName(raw, 40);
            StartCoroutine(CoApplyVolumeStepUiForPlayer(volUid));
            return;
        }

        if (_mode == Mode.Listen)
        {
            var prevListen = VoiceChatRuntimeState.CopyListenUserIds();
            VoiceChatRuntimeState.ToggleListen(player.userId);
            _chatManager.AfterListenSelectionChanged(prevListen);
            ReloadGrid();
            return;
        }

        if (_mode == Mode.TalkTo)
        {
            if (!_chatPlayerIdRegistry.TryGetChatId(player.userId, out _))
            {
                _chatManager.PostSystemMessage("That player's Chat ID isn't known yet. Wait until they appear in the lobby, then try again.");
                return;
            }

            var prev = VoiceChatRuntimeState.CopyTalkToUserIds();
            VoiceChatRuntimeState.ToggleTalkTo(player.userId);
            _chatManager.AfterTalkToSelectionChanged(prev);
            ReloadGrid();
            return;
        }

        // DM
        if (!_chatPlayerIdRegistry.TryGetChatId(player.userId, out var targetChatId))
        {
            _chatManager.PostSystemMessage("That player's Chat ID isn't known yet. Wait until they appear in the lobby, then try again.");
            return;
        }

        _dmState.SetDMTarget(player.userId, player.userName, targetChatId);
        var dmName = string.IsNullOrEmpty(player.userName) ? "Player" : player.userName;
        _chatManager.PostSystemMessageRich(
            ChatManager.SystemLineWithColoredPlayerName(dmName, " has been selected as the DM target.", null));
        _onDismiss?.Invoke();
    }

    private List<IConnectedPlayer> GetConnectedPlayers()
    {
        var list = new List<IConnectedPlayer>();

        var cm = _chatManager;
        if (cm != null)
        {
            var fromChat = cm.GetLobbyPlayers();
            foreach (var p in fromChat)
                if (p != null && !string.IsNullOrEmpty(p.userId) && !list.Any(x => x.userId == p.userId))
                    list.Add(p);
        }

        if (list.Count == 0)
        {
            foreach (var avatar in UnityEngine.Object.FindObjectsOfType<MultiplayerLobbyAvatarController>())
            {
                var p = GetPlayerFromAvatar(avatar);
                if (p != null && !string.IsNullOrEmpty(p.userId) && !list.Any(x => x.userId == p.userId))
                    list.Add(p);
            }

            foreach (var place in UnityEngine.Object.FindObjectsOfType<MultiplayerLobbyAvatarPlace>())
            {
                var p = GetPlayerFromPlace(place);
                if (p != null && !string.IsNullOrEmpty(p.userId) && !list.Any(x => x.userId == p.userId))
                    list.Add(p);
            }
        }

        return list;
    }

    private static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static IConnectedPlayer? GetPlayerFromAvatar(MultiplayerLobbyAvatarController ctrl)
    {
        if (ctrl == null) return null;
        var t = ctrl.GetType();
        foreach (var name in new[] { "_connectedPlayer", "_player", "m_ConnectedPlayer", "connectedPlayer" })
        {
            var f = t.GetField(name, Flags);
            if (f != null && typeof(IConnectedPlayer).IsAssignableFrom(f.FieldType))
                return f.GetValue(ctrl) as IConnectedPlayer;
        }

        foreach (var f in t.GetFields(Flags))
            if (typeof(IConnectedPlayer).IsAssignableFrom(f.FieldType))
                return f.GetValue(ctrl) as IConnectedPlayer;
        return null;
    }

    private static IConnectedPlayer? GetPlayerFromPlace(MultiplayerLobbyAvatarPlace place)
    {
        if (place == null) return null;
        var t = place.GetType();
        foreach (var name in new[] { "_connectedPlayer", "_player", "m_ConnectedPlayer", "connectedPlayer" })
        {
            var f = t.GetField(name, Flags);
            if (f != null && typeof(IConnectedPlayer).IsAssignableFrom(f.FieldType))
                return f.GetValue(place) as IConnectedPlayer;
        }

        foreach (var f in t.GetFields(Flags))
            if (typeof(IConnectedPlayer).IsAssignableFrom(f.FieldType))
                return f.GetValue(place) as IConnectedPlayer;
        return null;
    }

    private static string TrimName(string name, int maxLen)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (name.Length <= maxLen) return name;
        return name.Substring(0, maxLen) + "...";
    }
}
