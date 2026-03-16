using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A draggable animal sprite for the Shadow Match game.
/// Switches to floating animation while dragged.
/// Returns to start position with bounce if not matched.
/// Locks in place with celebration if matched.
/// Can play a subtle attention pulse to encourage the child to interact.
/// </summary>
public class DraggableAnimal : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    [HideInInspector] public string animalId;
    [HideInInspector] public string soundName;
    public bool isPlaced => isLocked;

    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Vector2 startPosition;
    private ShadowMatchController controller;
    private bool isLocked;
    private UISpriteAnimator animator;
    private bool isPulsing;
    private Coroutine pulseCoroutine;

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
        animator = GetComponent<UISpriteAnimator>();
    }

    public void Lock()
    {
        isLocked = true;
        StopGuidancePulse();
        canvasGroup.blocksRaycasts = false;
    }

    // ── Guidance pulse (attention animation) ──

    /// <summary>Start a subtle attention pulse on this animal to encourage interaction.</summary>
    public void StartGuidancePulse()
    {
        if (isLocked || isPulsing) return;
        isPulsing = true;
        pulseCoroutine = StartCoroutine(GuidancePulseLoop());
    }

    /// <summary>Stop the attention pulse.</summary>
    public void StopGuidancePulse()
    {
        isPulsing = false;
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
        if (rectTransform != null) rectTransform.localScale = Vector3.one;
    }

    private IEnumerator GuidancePulseLoop()
    {
        while (isPulsing && !isLocked)
        {
            // Gentle bounce up
            float dur = 0.5f;
            float elapsed = 0f;
            while (elapsed < dur && isPulsing)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                float scale = 1f + 0.08f * Mathf.Sin(t * Mathf.PI);
                rectTransform.localScale = Vector3.one * scale;
                yield return null;
            }

            rectTransform.localScale = Vector3.one;
            yield return new WaitForSeconds(1.0f);
        }

        rectTransform.localScale = Vector3.one;
    }

    // ── Drag handlers ──

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isLocked) return;
        transform.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isLocked) return;

        // Stop pulse when child starts dragging
        StopGuidancePulse();

        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.9f;

        // Switch to floating animation
        if (animator != null) animator.PlayFloating();

        // Slight scale up for "picked up" feel
        rectTransform.localScale = Vector3.one * 1.1f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isLocked) return;
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;

        // Check if near matching shadow for proximity hint
        controller.CheckProximity(this);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isLocked) return;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        rectTransform.localScale = Vector3.one;

        // Return to idle animation
        if (animator != null) animator.PlayIdle();

        if (!controller.TryMatch(this))
        {
            // Wrong place — bounce back
            StartCoroutine(BounceBack());
        }
    }

    private IEnumerator BounceBack()
    {
        canvasGroup.blocksRaycasts = false;

        Vector2 from = rectTransform.anchoredPosition;
        Vector2 to = startPosition;

        // Quick move back
        float dur = 0.25f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            rectTransform.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }
        rectTransform.anchoredPosition = startPosition;

        // Small bounce at destination
        elapsed = 0f;
        dur = 0.15f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float bounce = Mathf.Sin(t * Mathf.PI) * 0.08f;
            rectTransform.localScale = Vector3.one * (1f + bounce);
            yield return null;
        }
        rectTransform.localScale = Vector3.one;

        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>Play celebration animation on successful match.</summary>
    public void PlayMatchCelebration()
    {
        if (animator != null)
            animator.PlaySuccess();
        StartCoroutine(MatchBounce());
    }

    private IEnumerator MatchBounce()
    {
        // Scale pop: 1 → 1.25 → 1
        float dur = 0.15f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.25f, t);
            yield return null;
        }

        elapsed = 0f;
        dur = 0.2f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, t);
            yield return null;
        }
        rectTransform.localScale = Vector3.one;
    }
}
