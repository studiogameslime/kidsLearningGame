using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// A draggable puzzle piece. Scales up when dragged, snaps into
/// its correct position on the reference image when close enough.
/// </summary>
public class PuzzlePiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    [HideInInspector] public int correctIndex;
    [HideInInspector] public bool isPlaced = false;

    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Vector2 correctAnchoredPos;
    private PuzzleGameController controller;
    private Vector3 trayScale = Vector3.one;
    private Vector3 fullScale = Vector3.one;
    private Vector2 startPosition;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Init(int index, Vector2 correctPos, Canvas parentCanvas, PuzzleGameController ctrl)
    {
        correctIndex = index;
        correctAnchoredPos = correctPos;
        canvas = parentCanvas;
        controller = ctrl;
    }

    public void SetTrayScale(Vector3 scale) => trayScale = scale;
    public void SetFullScale(Vector3 scale) => fullScale = scale;
    public void SetStartPosition(Vector2 pos) => startPosition = pos;

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
        // Scale up to full size when dragging
        rectTransform.localScale = fullScale;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isPlaced) return;
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isPlaced) return;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        float snapDistance = rectTransform.sizeDelta.x * 0.4f;
        if (Vector2.Distance(rectTransform.anchoredPosition, correctAnchoredPos) < snapDistance)
        {
            // Snap into place
            rectTransform.anchoredPosition = correctAnchoredPos;
            rectTransform.localScale = fullScale;
            isPlaced = true;
            canvasGroup.blocksRaycasts = false;

            if (controller != null)
                controller.OnPiecePlaced();
        }
        else
        {
            // Return to initial tray position and scale
            rectTransform.anchoredPosition = startPosition;
            rectTransform.localScale = trayScale;
        }
    }
}
