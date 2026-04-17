using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A draggable half (top or bottom) of a fruit image for the Half Puzzle game.
/// Snaps to the matching partner when dropped close enough.
/// Bounces back to start position if wrong or too far.
/// </summary>
public class DraggableHalf : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    [HideInInspector] public int pairId;
    [HideInInspector] public string animalId;

    public bool IsPlaced { get; private set; }

    private RectTransform _rt;
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private Vector2 _startPosition;
    private HalfPuzzleController _controller;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Init(int id, string animal, Canvas parentCanvas, HalfPuzzleController ctrl)
    {
        pairId = id;
        animalId = animal;
        _canvas = parentCanvas;
        _controller = ctrl;
        IsPlaced = false;
    }

    /// <summary>Must be called after the layout has positioned this element.</summary>
    public void CaptureStartPosition()
    {
        _startPosition = _rt.anchoredPosition;
    }

    public void Lock(Vector2 snapPosition)
    {
        IsPlaced = true;
        _canvasGroup.blocksRaycasts = false;
        _rt.anchoredPosition = snapPosition;
        if (gameObject.activeInHierarchy)
            StartCoroutine(MatchPop());
    }

    /// <summary>Mark as placed without animation (used when GO will be hidden immediately).</summary>
    public void LockWithoutAnimation()
    {
        IsPlaced = true;
        _canvasGroup.blocksRaycasts = false;
    }

    private IEnumerator MatchPop()
    {
        float dur = 0.15f, elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            _rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.2f, elapsed / dur);
            yield return null;
        }
        elapsed = 0f; dur = 0.2f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            _rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 1f, Mathf.SmoothStep(0, 1, elapsed / dur));
            yield return null;
        }
        _rt.localScale = Vector3.one;
    }

    // ── Drag ──

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsPlaced) return;
        transform.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsPlaced) return;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 0.85f;
        _rt.localScale = Vector3.one * 1.08f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (IsPlaced) return;
        _rt.anchoredPosition += eventData.delta / _canvas.scaleFactor;

        // Clamp to parent bounds so piece can't be lost off-screen
        var parentRT = _rt.parent as RectTransform;
        if (parentRT != null)
        {
            Vector2 pos = _rt.anchoredPosition;
            Rect bounds = parentRT.rect;
            pos.x = Mathf.Clamp(pos.x, bounds.xMin + 20f, bounds.xMax - 20f);
            pos.y = Mathf.Clamp(pos.y, bounds.yMin + 20f, bounds.yMax - 20f);
            _rt.anchoredPosition = pos;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (IsPlaced) return;
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha = 1f;
        _rt.localScale = Vector3.one;

        if (!_controller.TryMatch(this))
            StartCoroutine(BounceBack());
    }

    private IEnumerator BounceBack()
    {
        _canvasGroup.blocksRaycasts = false;
        Vector2 from = _rt.anchoredPosition;
        float dur = 0.25f, elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            _rt.anchoredPosition = Vector2.Lerp(from, _startPosition, Mathf.SmoothStep(0, 1, elapsed / dur));
            yield return null;
        }
        _rt.anchoredPosition = _startPosition;

        // Small bounce
        elapsed = 0f; dur = 0.15f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float bounce = Mathf.Sin((elapsed / dur) * Mathf.PI) * 0.06f;
            _rt.localScale = Vector3.one * (1f + bounce);
            yield return null;
        }
        _rt.localScale = Vector3.one;
        _canvasGroup.blocksRaycasts = true;
    }
}
