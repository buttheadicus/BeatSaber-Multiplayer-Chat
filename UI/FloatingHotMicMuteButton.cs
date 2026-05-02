using System;
using MultiplayerChat.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerChat.UI;

/// <summary>
/// Floating "Mute" / "Unmute" for hot mic, placed above the host START / lobby setup bar when possible.
/// </summary>
public class FloatingHotMicMuteButton : MonoBehaviour
{
    private static readonly Color AccentBlue = new(0.32f, 0.58f, 1f, 1f);

    private GameObject? _buttonRoot;

    private float _nextLobbyPresencePollAt = -999f;

    private bool _cachedInLobbyState;

    private void Start()
    {
        VoiceChatRuntimeState.Changed += OnVoiceStateChanged;
        CreateButton();
    }

    private void OnDestroy()
    {
        VoiceChatRuntimeState.Changed -= OnVoiceStateChanged;
        if (_buttonRoot != null)
            UnityEngine.Object.Destroy(_buttonRoot);
    }

    private void Update()
    {
        var now = Time.realtimeSinceStartup;
        if (now >= _nextLobbyPresencePollAt)
        {
            _nextLobbyPresencePollAt = now + 0.25f;
            _cachedInLobbyState = MpChatLobbyDiagnostics.LobbyHierarchyLooksLikeMultiplayerLobby();
            if (_cachedInLobbyState && _buttonRoot == null)
                CreateButton();
        }

        if (_buttonRoot != null)
            _buttonRoot.SetActive(_cachedInLobbyState);
    }

    private void OnVoiceStateChanged() => UpdateLabel();

    private void CreateButton()
    {
        if (_buttonRoot != null) return;
        CreateAboveStartButton();
    }

    private bool CreateAboveStartButton()
    {
        var startButton = FindStartButtonInLobby();
        if (startButton == null)
            return false;

        var parent = startButton.parent;
        var clone = UnityEngine.Object.Instantiate(startButton.gameObject, parent);
        clone.name = "MPChatFloatingHotMicMute";
        clone.transform.SetSiblingIndex(startButton.GetSiblingIndex());

        var rect = (RectTransform)clone.transform;
        var startRect = (RectTransform)startButton;
        rect.anchoredPosition = startRect.anchoredPosition + new Vector2(0f, 48f);

        var text = clone.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (text != null)
            text.text = "Mute";

        var btn = clone.GetComponent<Button>();
        if (btn != null)
            btn.onClick.RemoveAllListeners();
        btn!.onClick.AddListener(OnClicked);

        ApplyDefaultBlueVisual(clone);
        _buttonRoot = clone;
        _buttonRoot.SetActive(true);
        UpdateLabel();
        return true;
    }

    private static void ApplyDefaultBlueVisual(GameObject clone)
    {
        var img = clone.GetComponentInChildren<Image>(true);
        if (img != null)
            img.color = AccentBlue;
    }

    private void OnClicked()
    {
        VoiceChatRuntimeState.SetHotMicMuted(!VoiceChatRuntimeState.IsHotMicMuted);
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (_buttonRoot == null) return;
        var text = _buttonRoot.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (text != null)
            text.text = VoiceChatRuntimeState.IsHotMicMuted ? "Unmute" : "Mute";
    }

    private static Transform? FindStartButtonInLobby()
    {
        var byName = GameObject.Find("StartButton") ?? GameObject.Find("HostSetup/StartButton");
        if (byName != null)
        {
            var btn = byName.GetComponent<Button>();
            if (btn != null)
                return btn.transform;
        }

        var roots = new[] { "MultiplayerLobbyCenterStage", "CenterStage", "LobbySetup", "HostSetup" };
        foreach (var rootName in roots)
        {
            var root = GameObject.Find(rootName);
            if (root == null)
                continue;

            foreach (var btn in root.GetComponentsInChildren<Button>(true))
            {
                var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                if (tmp != null && tmp.text.IndexOf("START", StringComparison.OrdinalIgnoreCase) >= 0)
                    return btn.transform;
            }
        }

        return null;
    }
}
