using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Draggable tangram piece. Drag to move, snaps when close to target.
/// No rotation — pieces spawn at their correct angle.
/// </summary>
public class TangramPiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    [HideInInspector] public int pieceIndex;
    [HideInInspector] public bool isPlaced;

    private RectTransform rt;
    private CanvasGroup canvasGroup;
    private Canvas canvas;
    private TangramController controller;

    private Vector2 correctPosition;
    private Vector2 scatteredPosition;
    private bool isAnimating;
    private Vector3 normalScale;

    private const float SnapDistance = 55f;

    public void Init(int index, Vector2 correctPos, Canvas parentCanvas, TangramController ctrl)
    {
        pieceIndex = index;
        correctPosition = correctPos;
        canvas = parentCanvas;
        controller = ctrl;

        rt = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        normalScale = rt.localScale;
    }

    public void SetScatteredPosition(Vector2 pos)
    {
        scatteredPosition = pos;
        rt.anchoredPosition = pos;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isPlaced || isAnimating) return;
        transform.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced || isAnimating) return;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.85f;
        rt.localScale = normalScale * 1.08f;
        transform.SetAsLastSibling();
        controller?.OnPiecePickedUp();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isPlaced || isAnimating) return;
        rt.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isPlaced || isAnimating) return;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        rt.localScale = normalScale;

        float dist = Vector2.Distance(rt.anchoredPosition, correctPosition);

        if (dist < SnapDistance)
        {
            isPlaced = true;
            canvasGroup.blocksRaycasts = false;
            rt.anchoredPosition = correctPosition;
            StartCoroutine(SnapBounce());
            controller?.OnPiecePlaced(rt);
        }
        else
        {
            StartCoroutine(ReturnToScattered());
        }
    }

    private IEnumerator SnapBounce()
    {
        isAnimating = true;
        float duration = 0.2f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float scale = 1f + 0.15f * Mathf.Sin(p * Mathf.PI);
            rt.localScale = normalScale * scale;
            yield return null;
        }
        rt.localScale = normalScale;
        isAnimating = false;
    }

    private IEnumerator ReturnToScattered()
    {
        isAnimating = true;
        Vector2 from = rt.anchoredPosition;
        float duration = 0.25f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            rt.anchoredPosition = Vector2.Lerp(from, scatteredPosition, p);
            yield return null;
        }
        rt.anchoredPosition = scatteredPosition;
        isAnimating = false;
    }
}
