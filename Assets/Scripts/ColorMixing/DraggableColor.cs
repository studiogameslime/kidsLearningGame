using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A draggable color circle for the Color Mixing game.
/// Drag it into the mixing bowl to add the color.
/// </summary>
public class DraggableColor : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [HideInInspector] public string colorId;
    [HideInInspector] public Color color;

    private Image image;
    private Canvas canvas;
    private RectTransform rectTransform;
    private Vector2 homePosition;
    private Vector3 homeScale;
    private System.Action<DraggableColor> onDropped;
    private CanvasGroup canvasGroup;
    private bool isDragging;

    public void Init(string id, Color c, Canvas parentCanvas, System.Action<DraggableColor> dropCallback)
    {
        colorId = id;
        color = c;
        canvas = parentCanvas;
        onDropped = dropCallback;

        rectTransform = GetComponent<RectTransform>();
        homeScale = rectTransform.localScale;

        image = GetComponent<Image>();
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

    public void OnPointerDown(PointerEventData eventData)
    {
        // Slight scale up on touch
        rectTransform.localScale = homeScale * 1.15f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging)
            rectTransform.localScale = homeScale;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        canvasGroup.blocksRaycasts = false;
        rectTransform.localScale = homeScale * 1.2f;
        // Bring to front
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
        rectTransform.localScale = homeScale;

        // Notify controller
        onDropped?.Invoke(this);
    }

    /// <summary>
    /// Animate back to home position.
    /// </summary>
    public void ReturnHome(MonoBehaviour runner)
    {
        runner.StartCoroutine(AnimateHome());
    }

    private System.Collections.IEnumerator AnimateHome()
    {
        Vector2 start = rectTransform.anchoredPosition;
        float dur = 0.25f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            rectTransform.anchoredPosition = Vector2.Lerp(start, homePosition, t / dur);
            yield return null;
        }
        rectTransform.anchoredPosition = homePosition;
    }

    // Legacy support — unused but kept for compatibility
    public void SetSelected(bool selected) { }
}
