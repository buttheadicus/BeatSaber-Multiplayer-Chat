using System;
using System.Linq;
using HMUI;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Object = UnityEngine.Object;

namespace MultiplayerChat.UI;

/// <summary>
/// Creates a "TEXT CHAT" button on the lobby floor, to the left of the shoes/avatar edit button.
/// When the shoes button cannot be found, places button in the lobby UI near the floor area.
/// </summary>
public class FloorChatButton : MonoBehaviour
{
    public event EventHandler? Clicked;

    [Inject] private readonly MainFlowCoordinator _mainFlowCoordinator = null!;
    [Inject] private readonly KeyboardFlowCoordinator _keyboardFlowCoordinator = null!;

    private GameObject? _buttonRoot;

    private void Start()
    {
        CreateFloorButton();
    }

    private void OnDestroy()
    {
        if (_buttonRoot != null)
            Object.Destroy(_buttonRoot);
    }

    private void Update()
    {
        var inLobby = IsInMultiplayerLobby();
        if (inLobby && _buttonRoot == null)
        {
            // Retry creating button when we enter lobby (elements may not have been ready at Start)
            CreateFloorButton();
        }
        if (_buttonRoot != null)
            _buttonRoot.SetActive(inLobby);
    }

    private bool IsInMultiplayerLobby()
    {
        var centerStage = GameObject.Find("MultiplayerLobbyCenterStage");
        if (centerStage != null && centerStage.activeInHierarchy)
            return true;
        var lobbySetup = GameObject.Find("LobbySetup");
        if (lobbySetup != null && lobbySetup.activeInHierarchy)
            return true;
        var centerStageAlt = GameObject.Find("CenterStage");
        if (centerStageAlt != null && centerStageAlt.activeInHierarchy)
            return true;
        var title = GameObject.Find("Wrapper/MenuCore/UI/ScreenSystem/TopScreen/TitleViewController");
        if (title != null && title.activeInHierarchy)
            return true;
        return false;
    }

    private void CreateFloorButton()
    {
        if (CreateButtonInHostSetup())
        {
            // Created in Host Setup area (right of START, near Per Player Difficulty)
        }
        else if (FindShoesOrAvatarButton() is { } shoesButton)
        {
            CreateButtonNextToShoes(shoesButton);
        }
        else if (CreateButtonInTitleBar())
        {
            // Created in title bar
        }
        else if (CreateFloatingFloorButton())
        {
            // Created from ModsButton
        }
        else
        {
            CreateFromAnyButton();
        }

        if (_buttonRoot == null)
            CreateStandaloneCanvasButton();
    }

    /// <summary>
    /// Places TEXT CHAT button in Host Setup area (right of START, near Per Player Difficulty/Modifiers).
    /// </summary>
    private bool CreateButtonInHostSetup()
    {
        var startButton = FindStartButtonInLobby();
        if (startButton == null)
            return false;

        var parent = startButton.parent;
        var clone = Object.Instantiate(startButton.gameObject, parent);
        clone.name = "E2EChatButton";
        clone.transform.SetSiblingIndex(startButton.GetSiblingIndex() + 1);

        var rect = (RectTransform)clone.transform;
        var startRect = (RectTransform)startButton;
        rect.anchoredPosition = startRect.anchoredPosition + new Vector2(140f, 0f);

        var text = clone.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (text != null)
            text.text = "TEXT CHAT";

        var btn = clone.GetComponent<Button>();
        if (btn != null)
            btn.onClick.RemoveAllListeners();
        btn!.onClick.AddListener(OnButtonClicked);

        var hover = clone.GetComponent<HoverHint>();
        if (hover != null)
            hover.text = "Open text chat (E2E encrypted)";

        _buttonRoot = clone;
        _buttonRoot.SetActive(true);
        return true;
    }

    private Transform? FindStartButtonInLobby()
    {
        // Try by name first (e.g. "StartButton")
        var byName = GameObject.Find("StartButton") ?? GameObject.Find("HostSetup/StartButton");
        if (byName != null)
        {
            var btn = byName.GetComponent<Button>();
            if (btn != null)
                return btn.transform;
        }

        // Try by button text "START"
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

    private Transform? FindShoesOrAvatarButton()
    {
        var centerStage = GameObject.Find("MultiplayerLobbyCenterStage")
            ?? GameObject.Find("CenterStage")
            ?? GameObject.Find("LobbySetup");
        if (centerStage == null)
            return null;

        foreach (var btn in centerStage.GetComponentsInChildren<Button>(true))
        {
            var name = btn.gameObject.name.ToLowerInvariant();
            if (name.Contains("avatar") || name.Contains("edit") || name.Contains("player") ||
                name.Contains("shoes") || name.Contains("customize"))
                return btn.transform;
        }

        var setupPanel = centerStage.transform.Find("CenterStage/PlayerSetup");
        if (setupPanel != null)
        {
            var firstButton = setupPanel.GetComponentInChildren<Button>(true);
            return firstButton?.transform;
        }

        return null;
    }

    private void CreateButtonNextToShoes(Transform shoesTransform)
    {
        var parent = shoesTransform.parent;
        var clone = Object.Instantiate(shoesTransform.gameObject, parent);
        clone.name = "E2EChatButton";
        clone.transform.SetSiblingIndex(shoesTransform.GetSiblingIndex());

        var rect = (RectTransform)clone.transform;
        rect.anchoredPosition += new Vector2(-80f, 0f);

        var text = clone.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (text != null)
            text.text = "TEXT CHAT";

        var btn = clone.GetComponent<Button>();
        if (btn != null)
            btn.onClick.RemoveAllListeners();
        btn!.onClick.AddListener(OnButtonClicked);

        var hover = clone.GetComponent<HoverHint>();
        if (hover != null)
            hover.text = "Open text chat (E2E encrypted)";

        _buttonRoot = clone;
        _buttonRoot.SetActive(true);
    }

    private bool CreateButtonInTitleBar()
    {
        // Try multiple paths - lobby and main menu use different hierarchies
        var titleView = GameObject.Find("Wrapper/MenuCore/UI/ScreenSystem/TopScreen/TitleViewController")
            ?? GameObject.Find("MenuCore/UI/ScreenSystem/TopScreen/TitleViewController")
            ?? GameObject.Find("TitleViewController");
        if (titleView == null)
            return false;

        var backButton = titleView.transform.Find("BackButton")
            ?? titleView.transform.Find("back-button")
            ?? titleView.transform.Find("Back");
        if (backButton == null)
            return false;

        var clone = Object.Instantiate(backButton.gameObject, titleView.transform);
        clone.name = "E2EChatButton";
        clone.transform.SetAsLastSibling();

        var rect = (RectTransform)clone.transform;
        var pos = rect.localPosition;
        rect.localPosition = new Vector3(pos.x - 70f, pos.y, pos.z);

        var text = clone.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (text != null)
            text.text = "TEXT CHAT";

        var btn = clone.GetComponent<Button>();
        if (btn != null)
            btn.onClick.RemoveAllListeners();
        btn!.onClick.AddListener(OnButtonClicked);

        var hover = clone.GetComponent<HoverHint>();
        if (hover != null)
            hover.text = "Open text chat (E2E encrypted)";

        var icon = clone.transform.Find("Icon");
        if (icon != null)
        {
            var img = icon.GetComponent<ImageView>();
            if (img != null)
                img.sprite = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name.Contains("Chat") || s.name.Contains("Edit"));
        }

        _buttonRoot = clone;
        _buttonRoot.SetActive(true);
        return true;
    }

    private bool CreateFloatingFloorButton()
    {
        // Fallback: clone any existing button (e.g. from MODS panel) and reposition
        var modsButton = GameObject.Find("ModsButton") ?? GameObject.Find("BSMLButton");
        if (modsButton != null)
        {
            var clone = Object.Instantiate(modsButton, modsButton.transform.parent);
            SetupClonedButton(clone, new Vector2(-60f, -35f));
            _buttonRoot = clone;
            _buttonRoot.SetActive(true);
            return true;
        }
        return false;
    }

    private void CreateFromAnyButton()
    {
        // Last resort: find ANY Button in the scene and clone it (lobby has various buttons)
        var buttons = Object.FindObjectsOfType<Button>();
        foreach (var btn in buttons)
        {
            if (btn == null || btn.gameObject.name == "E2EChatButton")
                continue;
            var rect = btn.transform as RectTransform;
            if (rect == null)
                continue;
            var parent = rect.parent;
            if (parent == null)
                continue;

            var clone = Object.Instantiate(btn.gameObject, parent);
            SetupClonedButton(clone, rect.anchoredPosition + new Vector2(-100f, 0f));
            _buttonRoot = clone;
            _buttonRoot.SetActive(true);
            return;
        }
    }

    private void CreateStandaloneCanvasButton()
    {
        // Last resort: create a Canvas + Button in center-right (Host Setup area)
        var canvasObj = new GameObject("E2EChatCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var buttonObj = new GameObject("E2EChatButton");
        buttonObj.transform.SetParent(canvasObj.transform, false);

        var rect = buttonObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0.5f);
        rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-180, 0);
        rect.sizeDelta = new Vector2(120, 40);

        var image = buttonObj.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.2f, 0.4f, 0.8f, 0.9f);

        var btn = buttonObj.AddComponent<Button>();
        btn.onClick.AddListener(OnButtonClicked);

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "TEXT CHAT";
        tmp.fontSize = 14;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;

        _buttonRoot = canvasObj;
        _buttonRoot.SetActive(true);
    }

    private void SetupClonedButton(GameObject clone, Vector2 position)
    {
        clone.name = "E2EChatButton";

        var rect = (RectTransform)clone.transform;
        rect.anchoredPosition = position;

        var text = clone.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (text != null)
            text.text = "TEXT CHAT";

        var btn = clone.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnButtonClicked);
        }

        var hover = clone.GetComponent<HoverHint>();
        if (hover != null)
            hover.text = "Open text chat (E2E encrypted)";
    }

    private void OnButtonClicked()
    {
        Clicked?.Invoke(this, EventArgs.Empty);
        _mainFlowCoordinator.PresentFlowCoordinator(_keyboardFlowCoordinator);
    }
}
