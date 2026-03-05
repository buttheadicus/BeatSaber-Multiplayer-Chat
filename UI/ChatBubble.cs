using System.Collections;
using HMUI;
using TMPro;
using UnityEngine;

namespace MultiplayerChat.UI;

/// <summary>
/// Chat bubble displayed above player name tag or avatar.
/// </summary>
public class ChatBubble : MonoBehaviour
{
    private const float DefaultYOffset = 0.35f;
    private const float ZOffset = -0.1f;

    private RectTransform? _rectTransform;
    private CanvasGroup? _canvasGroup;
    private ImageView? _bg;
    private CurvedTextMeshPro? _curvedText;
    private TMPro.TMP_Text? _textMesh;

    public void Awake()
    {
        _rectTransform = (RectTransform)transform;
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

        _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _bg = GetComponent<ImageView>();
        var img = GetComponent<UnityEngine.UI.Image>();
        if (img != null && _bg == null)
            img.color = new Color(0.08f, 0.08f, 0.12f, 0.98f);
        if (_bg != null)
            _bg.color = new Color(0.08f, 0.08f, 0.12f, 0.98f);

        var textObj = transform.Find("Text");
        if (textObj != null)
        {
            _curvedText = textObj.GetComponent<CurvedTextMeshPro>();
            if (_curvedText != null)
            {
                _textMesh = _curvedText;
                _curvedText.text = "";
                _curvedText.color = Color.white;
                _curvedText.fontSize = 4.8f;
                _curvedText.richText = true;
            }
            else
            {
                _textMesh = textObj.GetComponent<TMPro.TMP_Text>();
                if (_textMesh != null)
                {
                    _textMesh.text = "";
                    _textMesh.color = Color.white;
                }
            }
        }

        gameObject.SetActive(false);
    }

    public void SetText(string text)
    {
        if (_textMesh != null)
            _textMesh.text = text;
    }

    /// <param name="yOffset">Y offset (ignored when isStacked=true).</param>
    /// <param name="isStacked">When true, position/size are managed by parent layout; only alpha is animated.</param>
    public void Show(float duration, float yOffset = DefaultYOffset, bool isStacked = false)
    {
        if (_rectTransform == null || _canvasGroup == null || _textMesh == null)
            return;

        _rectTransform.localScale = Vector3.one * (isStacked ? 1f : 2f);
        _rectTransform.localRotation = Quaternion.identity;

        if (!isStacked)
        {
            var pos = _rectTransform.localPosition;
            pos.y = yOffset;
            pos.z = ZOffset;
            _rectTransform.localPosition = pos;

            var size = _textMesh != null ? _textMesh.bounds.size + new Vector3(8f, 4f, 0) : new Vector3(200, 30, 0);
            _rectTransform.sizeDelta = size;
        }

        if (_curvedText != null)
            _curvedText.ForceMeshUpdate();
        else
            _textMesh?.ForceMeshUpdate();

        _canvasGroup.alpha = 0f;
        gameObject.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(AnimateAndHide(duration));
    }

    private IEnumerator AnimateAndHide(float duration)
    {
        if (_canvasGroup == null)
            yield break;

        var elapsed = 0f;
        while (elapsed < 0.15f)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Clamp01(elapsed / 0.15f);
            yield return null;
        }

        _canvasGroup.alpha = 1f;
        yield return new WaitForSeconds(duration);

        elapsed = 0f;
        while (elapsed < 0.15f)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.15f);
            yield return null;
        }

        gameObject.SetActive(false);
    }
}
