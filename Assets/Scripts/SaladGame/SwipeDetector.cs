using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Detects vertical swipe gestures on the cutting board.
/// Reports the normalized X position (0-1) of where the swipe crossed the fruit area.
/// Auto-corrects: any roughly vertical swipe triggers a clean cut at the nearest guide line.
/// </summary>
public class SwipeDetector : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private SaladGameController _controller;
    private RectTransform _fruitArea;
    private Canvas _canvas;
    private Vector2 _swipeStart;
    private bool _swiping;

    public void Init(SaladGameController controller, RectTransform fruitArea, Canvas canvas)
    {
        _controller = controller;
        _fruitArea = fruitArea;
        _canvas = canvas;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _swipeStart = eventData.position;
        _swiping = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Visual feedback could go here (draw line)
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_swiping) return;
        _swiping = false;

        Vector2 swipeEnd = eventData.position;
        Vector2 delta = swipeEnd - _swipeStart;

        // Must be a mostly vertical swipe (at least 40px vertical, more vertical than horizontal)
        if (Mathf.Abs(delta.y) < 40f) return;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.5f) return; // too horizontal

        // Calculate where the swipe crossed the fruit area (normalized X: 0=left, 1=right)
        if (_fruitArea == null || _canvas == null) return;

        // Convert screen position to local position within fruit area
        Vector2 midpoint = (_swipeStart + swipeEnd) / 2f;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _fruitArea, midpoint, _canvas.worldCamera, out Vector2 localPos);

        Rect fruitRect = _fruitArea.rect;
        float normalizedX = (localPos.x - fruitRect.xMin) / fruitRect.width;
        normalizedX = Mathf.Clamp01(normalizedX);

        _controller.OnSwipe(normalizedX);
    }
}
