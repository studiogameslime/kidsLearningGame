using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Detects free swipe gestures on the cutting board.
/// Shows a temporary visual cut trail, then reports the normalized X position
/// and swipe angle to the controller for auto-corrected slicing.
/// Accepts any direction — vertical, horizontal, diagonal.
/// </summary>
public class SwipeDetector : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private SaladGameController _controller;
    private RectTransform _fruitArea;
    private Canvas _canvas;
    private Vector2 _swipeStart;
    private Vector2 _swipeEnd;
    private bool _swiping;

    // Visual trail
    private GameObject _trailLine;
    private RectTransform _trailRT;
    private Image _trailImg;

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

        // Create trail line
        if (_trailLine == null)
        {
            _trailLine = new GameObject("CutTrail");
            _trailLine.transform.SetParent(transform, false);
            _trailRT = _trailLine.AddComponent<RectTransform>();
            _trailImg = _trailLine.AddComponent<Image>();
            _trailImg.color = new Color(1f, 1f, 1f, 0.6f);
            _trailImg.raycastTarget = false;
        }
        _trailLine.SetActive(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_swiping || _trailRT == null) return;

        _swipeEnd = eventData.position;

        // Update trail line position and rotation
        Vector2 startLocal, endLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform as RectTransform, _swipeStart, _canvas != null ? _canvas.worldCamera : null, out startLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform as RectTransform, _swipeEnd, _canvas != null ? _canvas.worldCamera : null, out endLocal);

        Vector2 mid = (startLocal + endLocal) / 2f;
        float length = Vector2.Distance(startLocal, endLocal);
        float angle = Mathf.Atan2(endLocal.y - startLocal.y, endLocal.x - startLocal.x) * Mathf.Rad2Deg;

        _trailRT.anchorMin = new Vector2(0.5f, 0.5f);
        _trailRT.anchorMax = new Vector2(0.5f, 0.5f);
        _trailRT.anchoredPosition = mid;
        _trailRT.sizeDelta = new Vector2(length, 4f);
        _trailRT.localRotation = Quaternion.Euler(0, 0, angle);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_swiping) return;
        _swiping = false;
        _swipeEnd = eventData.position;

        // Hide and fade trail
        if (_trailLine != null)
            StartCoroutine(FadeTrail());

        Vector2 delta = _swipeEnd - _swipeStart;

        // Must be at least 30px total distance
        if (delta.magnitude < 30f) return;

        // Calculate where the swipe crossed the fruit area (normalized X: 0=left, 1=right)
        if (_fruitArea == null) return;

        Vector2 midpoint = (_swipeStart + _swipeEnd) / 2f;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _fruitArea, midpoint, _canvas != null ? _canvas.worldCamera : null, out Vector2 localPos);

        Rect fruitRect = _fruitArea.rect;
        float normalizedX = (localPos.x - fruitRect.xMin) / fruitRect.width;
        normalizedX = Mathf.Clamp01(normalizedX);

        // Determine if swipe is more vertical or horizontal
        bool isVertical = Mathf.Abs(delta.y) > Mathf.Abs(delta.x);

        _controller.OnSwipe(normalizedX, isVertical);
    }

    private IEnumerator FadeTrail()
    {
        if (_trailImg == null) yield break;
        float dur = 0.2f, elapsed = 0f;
        Color startColor = _trailImg.color;
        while (elapsed < dur && _trailImg != null)
        {
            elapsed += Time.deltaTime;
            _trailImg.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * (1f - elapsed / dur));
            yield return null;
        }
        if (_trailLine != null) _trailLine.SetActive(false);
        if (_trailImg != null) _trailImg.color = new Color(1f, 1f, 1f, 0.6f);
    }
}
