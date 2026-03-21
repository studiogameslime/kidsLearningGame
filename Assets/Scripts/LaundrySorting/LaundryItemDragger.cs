using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles drag-and-drop for a single laundry sorting item.
/// Scales up on pick, follows finger, snaps back or reports drop to controller.
/// </summary>
public class LaundryItemDragger : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [HideInInspector] public bool isClothes;
    [HideInInspector] public LaundrySortingController controller;
    [HideInInspector] public Canvas canvas;
    [HideInInspector] public Vector2 startPosition;

    private RectTransform rt;
    private CanvasGroup canvasGroup;
    private bool isDragging;
    private int activePointerId = -1;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void Start()
    {
        startPosition = rt.anchoredPosition;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isDragging) return;
        activePointerId = eventData.pointerId;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;
        isDragging = true;

        // Scale up for feedback
        rt.localScale = Vector3.one * 1.15f;

        // Bring to front
        transform.SetAsLastSibling();

        // Allow drop-through for hit testing
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || eventData.pointerId != activePointerId) return;

        // Move with finger
        if (canvas != null)
        {
            rt.anchoredPosition += eventData.delta / canvas.scaleFactor;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging || eventData.pointerId != activePointerId) return;
        isDragging = false;
        activePointerId = -1;

        rt.localScale = Vector3.one;
        canvasGroup.blocksRaycasts = true;

        // Notify controller
        if (controller != null)
            controller.OnItemDropped(this);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;
        // EndDrag handles cleanup
    }
}
