using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MultiplayerChat.UI;

public class ChatMoveHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private RectTransform? _parentRect;
    private RectTransform? _referenceRect;
    private Vector2 _lastScreenPos;

    private void Awake()
    {
        _parentRect = transform.parent?.GetComponent<RectTransform>();
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            _referenceRect = canvas.GetComponent<RectTransform>() ?? _parentRect?.parent as RectTransform;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_parentRect != null)
            _lastScreenPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_parentRect == null) return;
        var delta = eventData.position - _lastScreenPos;
        _lastScreenPos = eventData.position;
        if (_referenceRect != null)
        {
            var scale = _referenceRect.lossyScale.x;
            if (scale > 0.001f)
                delta /= scale;
        }
        _parentRect.anchoredPosition += delta;
    }
}
