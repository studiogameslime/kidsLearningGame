using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Draggable cookie piece for the Bakery Game.
/// Handles drag input, idle wiggle animation, match celebration, and return-to-start.
/// </summary>
public class BakeryDraggable : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int cookieId;
    public bool isPlaced;

    private RectTransform rt;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Vector2 startPosition;
    private BakeryGameController controller;
    private Coroutine wiggleCoroutine;

    public RectTransform RT => rt;

    public void Init(int id, Canvas parentCanvas, BakeryGameController ctrl)
    {
        cookieId = id;
        canvas = parentCanvas;
        controller = ctrl;
        rt = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        startPosition = rt.anchoredPosition;
    }

    // ── Idle Animation ──

    public void StartWiggle()
    {
        if (wiggleCoroutine != null) StopCoroutine(wiggleCoroutine);
        wiggleCoroutine = StartCoroutine(WiggleLoop());
    }

    public void StopWiggle()
    {
        if (wiggleCoroutine != null) { StopCoroutine(wiggleCoroutine); wiggleCoroutine = null; }
        rt.localEulerAngles = Vector3.zero;
    }

    private IEnumerator WiggleLoop()
    {
        float phase = Random.Range(0f, Mathf.PI * 2f);
        while (!isPlaced)
        {
            phase += Time.deltaTime;
            float angle = 5f * Mathf.Sin(phase * 3.5f);
            rt.localEulerAngles = new Vector3(0, 0, angle);
            yield return null;
        }
        rt.localEulerAngles = Vector3.zero;
    }

    // ── Drag Handlers ──

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isPlaced) return;
        transform.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced) { eventData.pointerDrag = null; return; }
        StopWiggle();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.9f;
        rt.localScale = Vector3.one * 1.1f;
        controller.OnCookiePickedUp();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isPlaced) return;
        rt.anchoredPosition += eventData.delta / canvas.scaleFactor;
        controller.CheckProximity(this);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isPlaced) return;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        rt.localScale = Vector3.one;

        if (!controller.TryMatch(this))
            StartCoroutine(ReturnToStart());
    }

    // ── Animations ──

    private IEnumerator ReturnToStart()
    {
        Vector2 from = rt.anchoredPosition;
        float dur = 0.3f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0, 1, t / dur);
            rt.anchoredPosition = Vector2.Lerp(from, startPosition, p);
            yield return null;
        }
        rt.anchoredPosition = startPosition;
        StartWiggle();
    }

    public void Lock()
    {
        isPlaced = true;
        StopWiggle();
        canvasGroup.blocksRaycasts = false;
        rt.localEulerAngles = Vector3.zero;
    }

    public IEnumerator PlayMatchCelebration()
    {
        // Squash
        float t = 0f;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            float p = t / 0.1f;
            float sx = Mathf.Lerp(1f, 1.25f, p);
            float sy = Mathf.Lerp(1f, 0.8f, p);
            rt.localScale = new Vector3(sx, sy, 1f);
            yield return null;
        }
        // Stretch
        t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            float p = t / 0.12f;
            float sx = Mathf.Lerp(1.25f, 0.92f, p);
            float sy = Mathf.Lerp(0.8f, 1.1f, p);
            rt.localScale = new Vector3(sx, sy, 1f);
            yield return null;
        }
        // Settle
        t = 0f;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0, 1, t / 0.1f);
            rt.localScale = Vector3.Lerp(new Vector3(0.92f, 1.1f, 1f), Vector3.one, p);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}
