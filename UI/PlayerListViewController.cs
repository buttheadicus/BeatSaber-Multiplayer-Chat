using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using MultiplayerChat.Core;
using MultiplayerCore.Models;
using MultiplayerCore.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace MultiplayerChat.UI;

/// <summary>
/// Displays a scrollable list of players in the lobby. Used for Mute (toggle mute on select) and DM (select target, then dismiss).
/// Uses BSML for layout and populates buttons in code (custom-list TableView was not rendering).
/// </summary>
[ViewDefinition("MultiplayerChat.UI.PlayerList.bsml")]
public class PlayerListViewController : BSMLAutomaticViewController
{
    public enum Mode { Mute, DM }

    [Inject] private readonly ChatMuteManager _muteManager = null!;
    [Inject] private readonly ChatDMState _dmState = null!;
    [Inject] private readonly IMultiplayerSessionManager _sessionManager = null!;

    [UIComponent("player_list_content")]
    private RectTransform? _contentRoot;

    private Mode _mode;
    private Action? _onDismiss;
    private List<IConnectedPlayer> _players = new();

    public void SetMode(Mode mode, Action? onDismiss = null)
    {
        _mode = mode;
        _onDismiss = onDismiss;
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        StartCoroutine(DeferredReloadList());
    }

    private IEnumerator DeferredReloadList()
    {
        yield return null;
        if (_contentRoot == null)
        {
            var scroll = GetComponentInChildren<ScrollRect>();
            if (scroll?.content != null)
                _contentRoot = scroll.content;
        }
        if (_contentRoot == null)
        {
            var root = FindRootVertical(transform);
            if (root != null && root.childCount >= 2)
            {
                var second = root.GetChild(1);
                var scroll = second.GetComponent<ScrollRect>();
                _contentRoot = scroll?.content ?? (RectTransform)second;
                if (_contentRoot == null && second.childCount > 0)
                    _contentRoot = (RectTransform)second.GetChild(0);
            }
        }
        if (_contentRoot == null)
        {
            var vlg = GetComponentsInChildren<VerticalLayoutGroup>();
            foreach (var v in vlg)
            {
                var rt = (RectTransform)v.transform;
                if (rt.childCount == 0 && rt != transform && rt.GetComponent<TextMeshProUGUI>() == null)
                {
                    _contentRoot = rt;
                    break;
                }
            }
        }
        ReloadList();
    }

    private static Transform? FindRootVertical(Transform t)
    {
        if (t.childCount == 0) return null;
        foreach (Transform c in t)
        {
            var vlg = c.GetComponent<VerticalLayoutGroup>();
            if (vlg != null && c.childCount >= 2)
            {
                var first = c.GetChild(0).GetComponentInChildren<TextMeshProUGUI>();
                if (first != null && first.text.Contains("Select"))
                    return c;
            }
            var found = FindRootVertical(c);
            if (found != null) return found;
        }
        return null;
    }

    private void ReloadList()
    {
        if (_contentRoot == null && transform.childCount > 0)
        {
            var first = transform.GetChild(0);
            if (first.childCount >= 2)
                _contentRoot = (RectTransform)first.GetChild(1);
        }
        if (_contentRoot == null && transform.childCount > 0)
        {
            var container = new GameObject("PlayerListContent");
            container.transform.SetParent(transform.GetChild(0), false);
            var rect = container.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(10, 10);
            rect.offsetMax = new Vector2(-10, -10);
            var vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            container.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _contentRoot = rect;
        }
        if (_contentRoot == null) return;

        MakeScrollViewportTransparent();

        for (var i = _contentRoot.childCount - 1; i >= 0; i--)
            Destroy(_contentRoot.GetChild(i).gameObject);

        _players = GetConnectedPlayers();

        EnsureContentLayout();

        if (_mode == Mode.DM && _dmState.IsInDMMode)
        {
            var exitBtn = CreateLabelButton("← Exit DM", () =>
            {
                _dmState.ClearDMTarget();
                _onDismiss?.Invoke();
            });
            exitBtn.transform.SetParent(_contentRoot, false);
        }

        if (_players.Count == 0)
        {
            var msg = ChatManager.Instance == null ? "Not in lobby" : "No players in lobby";
            var empty = CreateLabel(msg);
            empty.transform.SetParent(_contentRoot, false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRoot);
            return;
        }

        var localId = _sessionManager?.localPlayer?.userId;
        foreach (var p in _players)
        {
            if (_mode == Mode.DM && p.userId == localId) continue; // Don't DM yourself
            var muted = _mode == Mode.Mute && _muteManager.IsMuted(p.userId);
            var label = muted ? $"{p.userName} (muted)" : p.userName;
            var btn = CreatePlayerButton(label, p);
            btn.transform.SetParent(_contentRoot, false);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRoot);
    }

    private void MakeScrollViewportTransparent()
    {
        var scroll = GetComponentInChildren<ScrollRect>();
        if (scroll?.viewport != null)
        {
            var img = scroll.viewport.GetComponent<Image>();
            if (img != null)
            {
                img.color = new Color(1, 1, 1, 0.01f);
                img.sprite = BeatSaberMarkupLanguage.Utilities.ImageResources.BlankSprite;
            }
        }
    }

    private void EnsureContentLayout()
    {
        var vlg = _contentRoot!.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = _contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(4, 4, 4, 4);
        }
        vlg.childForceExpandWidth = true;
        vlg.childControlWidth = true;
    }

    private GameObject CreatePlayerButton(string label, IConnectedPlayer player)
    {
        var go = new GameObject("PlayerButton");
        var rect = go.transform as RectTransform ?? go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0, 36);

        var image = go.AddComponent<Image>();
        image.sprite = BeatSaberMarkupLanguage.Utilities.ImageResources.BlankSprite;
        image.color = new Color(0.15f, 0.2f, 0.3f, 0.95f);
        image.raycastTarget = true;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => OnPlayerClicked(player));

        var textRect = (RectTransform)go.transform;
        var tmp = BeatSaberMarkupLanguage.BeatSaberUI.CreateText(textRect, label, Vector2.zero, new Vector2(400, 36));
        tmp.rectTransform.anchorMin = Vector2.zero;
        tmp.rectTransform.anchorMax = Vector2.one;
        tmp.rectTransform.offsetMin = new Vector2(8, 4);
        tmp.rectTransform.offsetMax = new Vector2(-8, -4);
        tmp.fontSize = 6;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = new Color(1f, 1f, 1f, 1f);
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.transform.SetAsLastSibling();

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 36;
        le.minHeight = 36;
        le.minWidth = 100;
        le.flexibleWidth = 1;

        return go;
    }

    private GameObject CreateLabel(string text)
    {
        var go = new GameObject("Label");
        var rect = go.AddComponent<RectTransform>();
        var tmp = BeatSaberMarkupLanguage.BeatSaberUI.CreateText(rect, text, Vector2.zero, new Vector2(400, 24));
        tmp.fontSize = 3;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.7f, 0.7f, 0.7f);
        tmp.raycastTarget = false;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 24;
        le.flexibleWidth = 1;

        return go;
    }

    private GameObject CreateLabelButton(string label, Action onClick)
    {
        var go = new GameObject("LabelButton");
        var rect = go.transform as RectTransform ?? go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0, 36);

        var image = go.AddComponent<Image>();
        image.sprite = BeatSaberMarkupLanguage.Utilities.ImageResources.BlankSprite;
        image.color = new Color(0.2f, 0.15f, 0.25f, 0.95f);
        image.raycastTarget = true;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onClick());

        var tmp = BeatSaberMarkupLanguage.BeatSaberUI.CreateText((RectTransform)go.transform, label, Vector2.zero, new Vector2(400, 36));
        tmp.rectTransform.anchorMin = Vector2.zero;
        tmp.rectTransform.anchorMax = Vector2.one;
        tmp.rectTransform.offsetMin = new Vector2(8, 4);
        tmp.rectTransform.offsetMax = new Vector2(-8, -4);
        tmp.fontSize = 5;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.9f, 0.6f, 0.6f);
        tmp.raycastTarget = false;
        tmp.transform.SetAsLastSibling();

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 36;
        le.minHeight = 36;
        le.minWidth = 100;
        le.flexibleWidth = 1;

        return go;
    }

    private void OnPlayerClicked(IConnectedPlayer player)
    {
        if (player == null || string.IsNullOrEmpty(player.userId)) return;

        if (_mode == Mode.Mute)
        {
            _muteManager.ToggleMute(player.userId);
            ReloadList();
        }
        else
        {
            _dmState.SetDMTarget(player.userId, player.userName);
            _onDismiss?.Invoke();
        }
    }

    private List<IConnectedPlayer> GetConnectedPlayers()
    {
        var list = new List<IConnectedPlayer>();

        var cm = ChatManager.Instance;
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
}
