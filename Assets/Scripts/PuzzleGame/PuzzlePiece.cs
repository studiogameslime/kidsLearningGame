using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// A draggable puzzle piece. Scales up when dragged, snaps into
/// its correct position on the board when close enough.
/// Pieces live on the canvas root so they can move freely between
/// the left pieces area and the right board area.
/// </summary>
public class PuzzlePiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    [HideInInspector] public int correctIndex;
    [HideInInspector] public bool isPlaced = false;

    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Vector2 correctBoardPos;       // correct position in boardArea local space
    private Vector2 correctCanvasPos;      // correct position in canvas local space
    private RectTransform boardAreaRT;
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

    public void Init(int index, Vector2 correctBoardLocalPos, Canvas parentCanvas,
                     PuzzleGameController ctrl, RectTransform boardArea)
    {
        correctIndex = index;
        correctBoardPos = correctBoardLocalPos;
        canvas = parentCanvas;
        controller = ctrl;
        boardAreaRT = boardArea;
    }

    public void SetCorrectCanvasPos(Vector2 pos) => correctCanvasPos = pos;
    public void SetTrayScale(Vector3 scale) => trayScale = scale;
    public void SetFullScale(Vector3 scale) => fullScale = scale;
    public void SetStartPosition(Vector2 pos) => startPosition = pos;

    /// <summary>Get the correct position in board-area local space.</summary>
    public Vector2 GetCorrectBoardPos() => correctBoardPos;
    /// <summary>Get the piece's scattered start position in canvas space.</summary>
    public Vector2 GetStartPosition() => startPosition;
    /// <summary>Get the correct snap position in canvas space.</summary>
    public Vector2 GetCorrectCanvasPos() => correctCanvasPos;

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
        if (controller != null)
            controller.OnPiecePickedUp();
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

        // Check snap distance against the canvas-space correct position
        float snapDistance = rectTransform.sizeDelta.x * 0.4f;
        if (Vector2.Distance(rectTransform.anchoredPosition, correctCanvasPos) < snapDistance)
        {
            // Snap into place
            rectTransform.anchoredPosition = correctCanvasPos;
            rectTransform.localScale = fullScale;
            isPlaced = true;
            canvasGroup.blocksRaycasts = false;

            if (controller != null)
                controller.OnPiecePlaced();
        }
        else
        {
            // Return to initial scattered position and scale
            rectTransform.anchoredPosition = startPosition;
            rectTransform.localScale = trayScale;
        }
    }
}
