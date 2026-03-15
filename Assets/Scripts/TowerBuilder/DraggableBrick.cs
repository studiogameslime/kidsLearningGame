using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A draggable LEGO-style brick for the Tower Builder game.
/// Follows the PuzzlePiece drag pattern: drag across canvas,
/// snap on correct drop, shake+return on wrong drop.
/// </summary>
public class DraggableBrick : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    [HideInInspector] public string brickType;
    [HideInInspector] public string brickColor;
    [HideInInspector] public bool isPlaced;

    private Canvas canvas;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 homePosition;
    private Vector3 homeScale;
    private Vector3 dragScale;
    private System.Action<DraggableBrick> onDropped;

    public void Init(string type, string color, Canvas parentCanvas,
        Vector3 paletteScale, Vector3 fullScale, System.Action<DraggableBrick> dropCallback)
    {
        brickType = type;
        brickColor = color;
        canvas = parentCanvas;
        homeScale = paletteScale;
        dragScale = fullScale;
        onDropped = dropCallback;

        rectTransform = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        var img = GetComponent<Image>();
        if (img != null) img.raycastTarget = true;
    }

    public void SaveHomePosition()
    {
        homePosition = rectTransform.anchoredPosition;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isPlaced) return;
        transform.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced) return;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.85f;
        rectTransform.localScale = dragScale;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isPlaced) return;
        if (canvas == null) return;
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isPlaced) return;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        onDropped?.Invoke(this);
    }

    /// <summary>
    /// Snap this brick to the exact position and size of the slot's RectTransform,
    /// then play a LEGO-style connect animation: quick press-down + micro-bounce settle.
    /// </summary>
    public void SnapToSlot(RectTransform slotRT, MonoBehaviour runner)
    {
        isPlaced = true;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 1f;

        // Match pivot, position, and size exactly — guarantees pixel-perfect alignment
        rectTransform.pivot = slotRT.pivot;
        rectTransform.anchoredPosition = slotRT.anchoredPosition;
        rectTransform.sizeDelta = slotRT.sizeDelta;
        rectTransform.localScale = Vector3.one;

        runner.StartCoroutine(LegoConnect(slotRT.anchoredPosition));
    }

    public void ReturnToStart(MonoBehaviour runner)
    {
        runner.StartCoroutine(AnimateReturn());
    }

    private System.Collections.IEnumerator AnimateReturn()
    {
        // Quick shake
        Vector2 orig = rectTransform.anchoredPosition;
        float t = 0;
        float shakeDur = 0.25f;
        while (t < shakeDur)
        {
            t += Time.deltaTime;
            float decay = 1f - t / shakeDur;
            float x = Mathf.Sin(t * 45f) * 6f * decay;
            rectTransform.anchoredPosition = orig + new Vector2(x, 0);
            yield return null;
        }

        // Smooth slide back to palette
        Vector2 current = rectTransform.anchoredPosition;
        t = 0;
        float slideDur = 0.25f;
        while (t < slideDur)
        {
            t += Time.deltaTime;
            float p = t / slideDur;
            float ease = p * p * (3f - 2f * p);
            rectTransform.anchoredPosition = Vector2.Lerp(current, homePosition, ease);
            yield return null;
        }
        rectTransform.anchoredPosition = homePosition;
        rectTransform.localScale = homeScale;
    }

    /// <summary>
    /// LEGO-style connect: brick presses down onto studs, then settles.
    /// 1. Quick drop 4px down (pressing onto studs)
    /// 2. Snap back to exact position
    /// 3. Tiny upward bounce (1.5px) + settle
    /// Total duration ~0.18s — short, precise, tactile.
    /// </summary>
    private System.Collections.IEnumerator LegoConnect(Vector2 targetPos)
    {
        float dropPx = 4f;
        float bouncePx = 1.5f;

        // Phase 1: Quick press down (0.06s)
        float t = 0;
        float pressDur = 0.06f;
        while (t < pressDur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / pressDur);
            float offset = -dropPx * p;
            rectTransform.anchoredPosition = targetPos + new Vector2(0, offset);
            yield return null;
        }

        // Phase 2: Snap back to target + tiny overshoot up (0.05s)
        t = 0;
        float snapDur = 0.05f;
        while (t < snapDur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / snapDur);
            // From -dropPx to +bouncePx
            float offset = Mathf.Lerp(-dropPx, bouncePx, p);
            rectTransform.anchoredPosition = targetPos + new Vector2(0, offset);
            yield return null;
        }

        // Phase 3: Settle from bouncePx back to 0 (0.07s)
        t = 0;
        float settleDur = 0.07f;
        while (t < settleDur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / settleDur);
            float ease = p * p; // ease-in for clean settle
            float offset = Mathf.Lerp(bouncePx, 0f, ease);
            rectTransform.anchoredPosition = targetPos + new Vector2(0, offset);
            yield return null;
        }

        // Ensure pixel-perfect final position
        rectTransform.anchoredPosition = targetPos;
    }
}
