using System;
using System.Collections;
using System.Collections.Generic;
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

[ViewDefinition("MultiplayerChat.UI.PlayerList.bsml")]
public class PlayerListViewController : BSMLAutomaticViewController
{
    public enum Mode { Mute, DM, Volume, Listen, TalkTo }

    private const int SlotCount = 12;
    private const int MaxNameLen = 30;
    private const int VolumeStepPercent = 10;

    [Inject] private readonly ChatMuteManager _muteManager = null!;
    [Inject] private readonly ChatIdConfigStore _chatIdConfigStore = null!;
    [Inject] private readonly ChatDMState _dmState = null!;
    [Inject] private readonly ChatPlayerIdRegistry _chatPlayerIdRegistry = null!;
    [Inject] private readonly ChatManager _chatManager = null!;
    [Inject] private readonly IMultiplayerSessionManager _sessionManager = null!;

    [UIComponent("MuteDmPanel")] private GameObject? _muteDmPanel;
    [UIComponent("HearPanel")] private GameObject? _hearPanel;
    [UIComponent("GridTitle")] private TMP_Text? _gridTitle;
    [UIComponent("GridHint")] private TMP_Text? _gridHint;
    [UIComponent("HearGridHint")] private TMP_Text? _hearGridHint;
    [UIComponent("HearModeRow")] private GameObject? _hearModeRow;
    [UIComponent("TalkToModeButton")] private Button? _talkToModeButton;
    [UIComponent("ListenModeButton")] private Button? _listenModeButton;
    [UIComponent("HearFooterRow")] private GameObject? _hearFooterRow;
    [UIComponent("PlayerVolumeButton")] private Button? _playerVolumeButton;
    [UIComponent("ConfigureDuckButton")] private Button? _configureDuckButton;
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

    private static readonly Color AccentBlue = new(0.32f, 0.58f, 1f, 1f);
    private ColorBlock _hearModeDefaultColors;
    private bool _hearModeDefaultColorsCached;

    public event Action<Mode>? RequestSubMode;

    public event Action? RequestDuckSettings;

    public void SetMode(Mode mode, Action? onDismiss = null)
    {
        _mode = mode;
        _onDismiss = onDismiss;
        RefreshUi();
    }

    internal void ForceRefreshUi() => RefreshUi();

    private void RefreshUi()
    {
        if (!isActiveAndEnabled)
            return;

        if (_clearAllMutesButton != null)
            _clearAllMutesButton.gameObject.SetActive(_mode == Mode.Mute);
        if (_volumeAdjustPanel != null && _mode != Mode.Volume)
            _volumeAdjustPanel.gameObject.SetActive(false);
        if (_mode == Mode.Volume)
            ResetVolumeAdjustPanel();
        ApplyTitle();
        UpdateVoiceChrome();
        ReloadGrid();
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
        RefreshUi();
        BsmlDefaultStringCleanup.StripPlaceholderLabels(gameObject);
    }

    [UIAction("TalkToModeClicked")]
    private void TalkToModeClicked()
    {
        if (_mode != Mode.Listen && _mode != Mode.TalkTo)
            return;
        _mode = Mode.TalkTo;
        RefreshUi();
    }

    [UIAction("ListenModeClicked")]
    private void ListenModeClicked()
    {
        if (_mode != Mode.Listen && _mode != Mode.TalkTo)
            return;
        _mode = Mode.Listen;
        RefreshUi();
    }

    [UIAction("PlayerVolumeClicked")]
    private void PlayerVolumeClicked()
    {
        if (_mode != Mode.Listen && _mode != Mode.TalkTo)
            return;
        RequestSubMode?.Invoke(Mode.Volume);
    }

    [UIAction("ConfigureDuckClicked")]
    private void ConfigureDuckClicked()
    {
        if (_mode != Mode.Listen && _mode != Mode.TalkTo)
            return;
        RequestDuckSettings?.Invoke();
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
                _ => ""
            };
        }

        if (_gridHint != null)
            _gridHint.gameObject.SetActive(_mode == Mode.Mute || _mode == Mode.DM);

        if (_hearGridHint != null)
        {
            var hearHint = _mode == Mode.Listen || _mode == Mode.TalkTo || _mode == Mode.Volume;
            _hearGridHint.gameObject.SetActive(hearHint);
            _hearGridHint.text = _mode switch
            {
                Mode.Listen =>
                    "Tap names to add or remove (listening). If nobody is selected, you hear everyone.",
                Mode.TalkTo =>
                    "Listen and talk to a player (you can select as many people as you'd like, doesn't have to be one person; good for groups!).",
                Mode.Volume => "Tap a player, then use - / + to adjust their volume. There is no cap, so you can adjust it as high as you want.",
                _ => ""
            };
        }
    }

    private Color _hearModeDefaultLabelColor = Color.white;
    private bool _uiRefsResolved;

    private void EnsureUiRefsResolved()
    {
        if (_uiRefsResolved)
            return;
        _uiRefsResolved = true;
        var root = transform;
        _muteDmPanel ??= BsmlUiRefs.FindChildGameObject(root, "MuteDmPanel");
        _hearPanel ??= BsmlUiRefs.FindChildGameObject(root, "HearPanel");
        _hearModeRow ??= BsmlUiRefs.FindChildGameObject(root, "HearModeRow");
        _hearFooterRow ??= BsmlUiRefs.FindChildGameObject(root, "HearFooterRow");
        if (_volumeAdjustPanel == null)
        {
            var volGo = BsmlUiRefs.FindChildGameObject(root, "VolumeAdjustPanel");
            if (volGo != null)
                _volumeAdjustPanel = volGo.GetComponent<RectTransform>();
        }
    }

    private void UpdateVoiceChrome()
    {
        EnsureUiRefsResolved();

        var muteOrDm = _mode == Mode.Mute || _mode == Mode.DM;
        var hearMode = _mode == Mode.Listen || _mode == Mode.TalkTo;
        var hearPanelVisible = _mode == Mode.Listen || _mode == Mode.TalkTo || _mode == Mode.Volume;

        BsmlUiRefs.SetActive(_muteDmPanel, muteOrDm);
        BsmlUiRefs.SetActive(_hearPanel, hearPanelVisible);

        BsmlUiRefs.SetActive(_hearModeRow, hearMode);
        BsmlUiRefs.SetActive(_hearFooterRow, hearMode);

        if (_hearGridHint != null)
            _hearGridHint.gameObject.SetActive(hearPanelVisible);

        if (_volumeAdjustPanel != null)
            _volumeAdjustPanel.gameObject.SetActive(_mode == Mode.Volume && !string.IsNullOrEmpty(_selectedVolumeUserId));

        if (_saveVolumesButton != null)
            _saveVolumesButton.gameObject.SetActive(_mode == Mode.Volume);

        if (_clearAllMutesButton != null)
            _clearAllMutesButton.gameObject.SetActive(_mode == Mode.Mute);

        SetButtonVisible(_talkToModeButton, hearMode);
        SetButtonVisible(_listenModeButton, hearMode);
        SetButtonVisible(_playerVolumeButton, hearMode);
        SetButtonVisible(_configureDuckButton, hearMode);

        if (muteOrDm)
        {
            if (_hearModeDefaultColorsCached)
            {
                RestoreHearModeButtonDefaultLook(_talkToModeButton);
                RestoreHearModeButtonDefaultLook(_listenModeButton);
            }

            _hearModeDefaultColorsCached = false;
            return;
        }

        if (hearMode)
        {
            if (!_hearModeDefaultColorsCached)
                CacheHearModeDefaultColorsOnce();
            ApplyHearModeHighlight(_talkToModeButton, _mode == Mode.TalkTo);
            ApplyHearModeHighlight(_listenModeButton, _mode == Mode.Listen);
            return;
        }

        if (_hearModeDefaultColorsCached)
        {
            RestoreHearModeButtonDefaultLook(_talkToModeButton);
            RestoreHearModeButtonDefaultLook(_listenModeButton);
        }

        _hearModeDefaultColorsCached = false;
    }

    private static void SetButtonVisible(Button? btn, bool visible)
    {
        if (btn != null)
            btn.gameObject.SetActive(visible);
    }

    private void RestoreHearModeButtonDefaultLook(Button? btn)
    {
        if (btn == null)
            return;
        btn.colors = _hearModeDefaultColors;
        var label = btn.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.color = _hearModeDefaultLabelColor;
    }

    private void CacheHearModeDefaultColorsOnce()
    {
        if (_hearModeDefaultColorsCached)
            return;
        var src = _listenModeButton ?? _talkToModeButton;
        if (src == null)
            return;
        _hearModeDefaultColors = src.colors;
        var label = src.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            _hearModeDefaultLabelColor = label.color;
        _hearModeDefaultColorsCached = true;
    }

    private void ApplyHearModeHighlight(Button? btn, bool active)
    {
        if (btn == null)
            return;

        var c = btn.colors;
        if (active)
        {
            c.normalColor = AccentBlue;
            c.highlightedColor = AccentBlue;
            c.selectedColor = AccentBlue;
            c.pressedColor = AccentBlue;
        }
        else if (_hearModeDefaultColorsCached)
        {
            c.normalColor = _hearModeDefaultColors.normalColor;
            c.highlightedColor = _hearModeDefaultColors.highlightedColor;
            c.selectedColor = _hearModeDefaultColors.selectedColor;
            c.pressedColor = _hearModeDefaultColors.pressedColor;
            c.disabledColor = _hearModeDefaultColors.disabledColor;
        }

        btn.colors = c;

        var label = btn.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.color = active ? Color.white : _hearModeDefaultLabelColor;
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
        {
            for (var i = _players.Count - 1; i >= 0; i--)
            {
                if (_players[i].userId == localId)
                    _players.RemoveAt(i);
            }
        }

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
        var sum = (long)cur + deltaPercent;
        int next;
        if (sum < 0L)
            next = 0;
        else if (sum > PlayerVoiceVolumeStore.MaxVolumePercent)
            next = PlayerVoiceVolumeStore.MaxVolumePercent;
        else
            next = (int)sum;
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
        var g = p / 100f;
        _playerVolumeStepLabel.text = Mathf.Abs(p) >= 100000 ? g.ToString("G4") : g.ToString("F1");
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
        var list = new List<IConnectedPlayer>(SlotCount);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var cm = _chatManager;
        if (cm != null)
        {
            foreach (var p in cm.GetLobbyPlayers())
            {
                if (p == null || string.IsNullOrEmpty(p.userId) || !seen.Add(p.userId))
                    continue;
                list.Add(p);
            }
        }

        if (list.Count == 0)
        {
            TryAddPlayersFromLobbyScene(list, seen);
        }

        var modPresence = ModPresenceManager.Instance;
        if (modPresence == null)
            return list;

        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (!modPresence.HasMod(list[i].userId))
                list.RemoveAt(i);
        }

        return list;
    }

    private static void TryAddPlayersFromLobbyScene(List<IConnectedPlayer> list, HashSet<string> seen)
    {
        foreach (var avatar in UnityEngine.Object.FindObjectsOfType<MultiplayerLobbyAvatarController>())
        {
            var p = GetPlayerFromAvatar(avatar);
            if (p == null || string.IsNullOrEmpty(p.userId) || !seen.Add(p.userId))
                continue;
            list.Add(p);
        }

        foreach (var place in UnityEngine.Object.FindObjectsOfType<MultiplayerLobbyAvatarPlace>())
        {
            var p = GetPlayerFromPlace(place);
            if (p == null || string.IsNullOrEmpty(p.userId) || !seen.Add(p.userId))
                continue;
            list.Add(p);
        }
    }

    private static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly string[] ConnectedPlayerFieldNames =
        { "_connectedPlayer", "_player", "m_ConnectedPlayer", "connectedPlayer" };

    private static FieldInfo? _lobbyAvatarConnectedPlayerField;
    private static FieldInfo? _lobbyPlaceConnectedPlayerField;
    private static bool _lobbyAvatarFieldResolved;
    private static bool _lobbyPlaceFieldResolved;

    private static IConnectedPlayer? GetPlayerFromAvatar(MultiplayerLobbyAvatarController ctrl)
    {
        if (ctrl == null)
            return null;

        var field = ResolveConnectedPlayerField(ctrl.GetType(), ref _lobbyAvatarConnectedPlayerField, ref _lobbyAvatarFieldResolved);
        return field?.GetValue(ctrl) as IConnectedPlayer;
    }

    private static IConnectedPlayer? GetPlayerFromPlace(MultiplayerLobbyAvatarPlace place)
    {
        if (place == null)
            return null;

        var field = ResolveConnectedPlayerField(place.GetType(), ref _lobbyPlaceConnectedPlayerField, ref _lobbyPlaceFieldResolved);
        return field?.GetValue(place) as IConnectedPlayer;
    }

    private static FieldInfo? ResolveConnectedPlayerField(
        Type type,
        ref FieldInfo? cachedField,
        ref bool resolved)
    {
        if (resolved)
            return cachedField;

        resolved = true;
        foreach (var name in ConnectedPlayerFieldNames)
        {
            var f = type.GetField(name, Flags);
            if (f != null && typeof(IConnectedPlayer).IsAssignableFrom(f.FieldType))
            {
                cachedField = f;
                return cachedField;
            }
        }

        foreach (var f in type.GetFields(Flags))
        {
            if (!typeof(IConnectedPlayer).IsAssignableFrom(f.FieldType))
                continue;
            cachedField = f;
            return cachedField;
        }

        return null;
    }

    private static string TrimName(string name, int maxLen)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (name.Length <= maxLen) return name;
        return name.Substring(0, maxLen) + "...";
    }
}
