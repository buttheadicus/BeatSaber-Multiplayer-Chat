using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerChat.UI;

/// <summary>
/// Shows the update message in the game view (Screen Space - Camera so it appears in VR and desktop).
/// </summary>
public class UpdateMessager : MonoBehaviour
{
    private const float DisplayDuration = 45f;

    private GameObject? _rootObj;
    private CanvasGroup? _canvasGroup;
    private TMP_Text? _textMesh;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void ShowUpdateMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        MultiplayerChat.Plugin.Log?.Info("[E2EChat] UpdateMessager.ShowUpdateMessage called");
        EnsureRoot();
        if (_textMesh == null)
        {
            MultiplayerChat.Plugin.Log?.Warn("[E2EChat] UpdateMessager: _textMesh is null after EnsureRoot");
            return;
        }
        _textMesh.text = message;
        if (_rootObj != null)
        {
            _rootObj.SetActive(true);
            StartCoroutine(AnimateAndHide());
            MultiplayerChat.Plugin.Log?.Info("[E2EChat] UpdateMessager: panel shown");
        }
        else
        {
            MultiplayerChat.Plugin.Log?.Warn("[E2EChat] UpdateMessager: _rootObj is null");
        }
    }

    private void EnsureRoot()
    {
        if (_rootObj != null && _rootObj.activeInHierarchy) return;
        if (_rootObj != null)
        {
            _rootObj.SetActive(true);
            return;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            cam = Object.FindObjectOfType<Camera>();
            MultiplayerChat.Plugin.Log?.Info($"[E2EChat] UpdateMessager: Camera.main was null, using FindObjectOfType: {cam?.name ?? "null"}");
        }

        _rootObj = new GameObject("E2EUpdateMessager");
        _rootObj.transform.SetParent(transform);

        var canvas = _rootObj.AddComponent<Canvas>();
        canvas.renderMode = cam != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = cam;
        canvas.planeDistance = 5f;
        canvas.sortingOrder = 300;
        _rootObj.AddComponent<CanvasScaler>();
        _rootObj.AddComponent<GraphicRaycaster>();

        _canvasGroup = _rootObj.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        var panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(_rootObj.transform, false);

        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(700f, 120f);

        var bg = panelObj.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0.98f);

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(panelObj.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16, 12);
        textRect.offsetMax = new Vector2(-16, -12);

        _textMesh = textObj.AddComponent<TextMeshProUGUI>();
        _textMesh.fontSize = 24;
        _textMesh.color = Color.white;
        _textMesh.richText = true;
        _textMesh.alignment = TextAlignmentOptions.Center;
        _textMesh.enableWordWrapping = true;
        _textMesh.raycastTarget = false;

        MultiplayerChat.Plugin.Log?.Info($"[E2EChat] UpdateMessager: root created, renderMode={canvas.renderMode}, camera={cam?.name ?? "null"}");
    }

    private IEnumerator AnimateAndHide()
    {
        if (_canvasGroup == null) yield break;

        var elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Clamp01(elapsed / 0.2f);
            yield return null;
        }

        _canvasGroup.alpha = 1f;
        yield return new WaitForSeconds(DisplayDuration);

        elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.2f);
            yield return null;
        }

        if (_rootObj != null)
            _rootObj.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_rootObj != null)
            Destroy(_rootObj);
    }
}
