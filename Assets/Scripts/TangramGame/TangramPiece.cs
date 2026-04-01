using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Draggable tangram piece. Tap to rotate 45°, drag to move, snaps when close to target.
/// </summary>
public class TangramPiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [HideInInspector] public int pieceIndex;
    [HideInInspector] public bool isPlaced;

    private RectTransform rt;
    private CanvasGroup canvasGroup;
    private Canvas canvas;
    private TangramController controller;

    private Vector2 correctPosition;
    private float correctRotation;
    private Vector2 scatteredPosition;
    private float currentRotation;
    private bool isDragging;
    private bool isAnimating;
    private Vector3 normalScale;

    private const float SnapDistance = 50f;
    private const float SnapAngleTolerance = 25f;
    private const float RotationStep = 45f;

    public void Init(int index, Vector2 correctPos, float correctRot, Canvas parentCanvas, TangramController ctrl)
    {
        pieceIndex = index;
        correctPosition = correctPos;
        correctRotation = NormalizeAngle(correctRot);
        canvas = parentCanvas;
        controller = ctrl;

        rt = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        normalScale = rt.localScale;
    }

    public void SetScatteredPosition(Vector2 pos, float rotation)
    {
        scatteredPosition = pos;
        currentRotation = rotation;
        rt.anchoredPosition = pos;
        rt.localEulerAngles = new Vector3(0, 0, -rotation);
    }

    // ── Tap to Rotate ──────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isPlaced || isDragging || isAnimating) return;
        currentRotation = NormalizeAngle(currentRotation + RotationStep);
        StartCoroutine(AnimateRotation());
    }

    private IEnumerator AnimateRotation()
    {
        isAnimating = true;
        float from = rt.localEulerAngles.z;
        float to = -currentRotation;
        float duration = 0.15f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            rt.localEulerAngles = new Vector3(0, 0, Mathf.LerpAngle(from, to, p));
            yield return null;
        }
        rt.localEulerAngles = new Vector3(0, 0, to);
        isAnimating = false;
    }

    // ── Drag ───────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced || isAnimating) return;
        isDragging = true;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.85f;
        rt.localScale = normalScale * 1.08f;
        transform.SetAsLastSibling();
        controller?.OnPiecePickedUp();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        rt.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        isDragging = false;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        rt.localScale = normalScale;

        // Check snap: position close enough AND rotation matches
        float dist = Vector2.Distance(rt.anchoredPosition, correctPosition);
        float angleDiff = Mathf.Abs(Mathf.DeltaAngle(currentRotation, correctRotation));

        if (dist < SnapDistance && angleDiff < SnapAngleTolerance)
        {
            Snap();
        }
        else
        {
            // Return to scattered position
            StartCoroutine(ReturnToScattered());
        }
    }

    // ── Snap ───────────────────────────────────────────────────

    private void Snap()
    {
        isPlaced = true;
        canvasGroup.blocksRaycasts = false;
        rt.anchoredPosition = correctPosition;
        currentRotation = correctRotation;
        rt.localEulerAngles = new Vector3(0, 0, -correctRotation);
        StartCoroutine(SnapBounce());
        controller?.OnPiecePlaced(rt);
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

    // ── Helpers ─────────────────────────────────────────────────

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f) angle += 360f;
        return angle;
    }
}
