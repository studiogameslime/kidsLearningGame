using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A draggable animal sprite for the Shadow Match game.
/// Returns to start position if not matched, locks in place if matched.
/// </summary>
public class DraggableAnimal : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    [HideInInspector] public string animalId;

    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Vector2 startPosition;
    private ShadowMatchController controller;
    private bool isLocked;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Init(string id, Canvas parentCanvas, ShadowMatchController ctrl)
    {
        animalId = id;
        canvas = parentCanvas;
        controller = ctrl;
        isLocked = false;
        startPosition = rectTransform.anchoredPosition;
    }

    public void Lock()
    {
        isLocked = true;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isLocked) return;
        transform.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isLocked) return;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.8f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isLocked) return;
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isLocked) return;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        if (!controller.TryMatch(this))
        {
            // Wrong place — return to start
            rectTransform.anchoredPosition = startPosition;
        }
    }
}
