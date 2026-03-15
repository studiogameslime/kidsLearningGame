using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A draggable color circle for the Color Mixing game.
/// Features glossy press/drag feedback and smooth return animation
/// with overshoot bounce. Supports idle breathing from controller.
/// </summary>
public class DraggableColor : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [HideInInspector] public string colorId;
    [HideInInspector] public Color color;

    public bool IsDragging => isDragging;

    private Canvas canvas;
    private RectTransform rectTransform;
    private Vector2 homePosition;
    private Vector3 homeScale;
    private System.Action<DraggableColor> onDropped;
    private CanvasGroup canvasGroup;
    private bool isDragging;
    private float idleScaleMultiplier = 1f;

    public void Init(string id, Color c, Canvas parentCanvas, System.Action<DraggableColor> dropCallback)
    {
        colorId = id;
        color = c;
        canvas = parentCanvas;
        onDropped = dropCallback;

        rectTransform = GetComponent<RectTransform>();
        homeScale = rectTransform.localScale;

        var image = GetComponent<Image>();
        if (image == null)
            image = gameObject.AddComponent<Image>();
        image.color = c;
        image.raycastTarget = true;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void SaveHomePosition()
    {
        homePosition = rectTransform.anchoredPosition;
    }

    /// <summary>
    /// Called by controller's breath loop to set a gentle idle scale.
    /// </summary>
    public void SetIdleScale(float multiplier)
    {
        if (isDragging) return;
        idleScaleMultiplier = multiplier;
        rectTransform.localScale = homeScale * multiplier;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        rectTransform.localScale = homeScale * 1.12f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging)
            rectTransform.localScale = homeScale * idleScaleMultiplier;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.9f;
        rectTransform.localScale = homeScale * 1.18f;
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null) return;
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        rectTransform.localScale = homeScale;
        onDropped?.Invoke(this);
    }

    public void ReturnHome(MonoBehaviour runner)
    {
        runner.StartCoroutine(AnimateHome());
    }

    private System.Collections.IEnumerator AnimateHome()
    {
        Vector2 start = rectTransform.anchoredPosition;
        float dur = 0.3f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            // Ease out with slight overshoot
            float ease = 1f + Mathf.Pow(2f, -8f * p) * Mathf.Sin(p * Mathf.PI * 1.5f) * -0.15f;
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, homePosition, ease);
            yield return null;
        }
        rectTransform.anchoredPosition = homePosition;
    }
}
