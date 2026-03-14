using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// An animal in the World scene. Supports tap (Success anim + bounce)
/// and drag (Floating anim + reposition). On drag release, stays in
/// Floating while falling with gravity back to ground, then returns to Idle.
/// </summary>
public class WorldAnimal : MonoBehaviour
{
    public string animalId;
    public float groundY = 20f; // Y position considered "ground" (set by WorldController)
    public RectTransform shadowTransform; // optional shadow that follows the animal

    private RectTransform rt;
    private UISpriteAnimator spriteAnim;
    private bool isTouchActive;
    private bool isDragging;
    private bool isFalling;
    private Vector2 dragOffset; // offset between finger and animal position at drag start

    // Shadow scaling based on height
    private Vector2 baseShadowSize;
    private float baseShadowAlpha;
    private Image shadowImage;
    private float maxLiftHeight = 500f; // height at which shadow reaches minimum size

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Start()
    {
        // UISpriteAnimator may be added after Awake by WorldController, so fetch in Start
        if (spriteAnim == null)
            spriteAnim = GetComponent<UISpriteAnimator>();

        // Cache base shadow properties for height-based scaling
        if (shadowTransform != null)
        {
            baseShadowSize = shadowTransform.sizeDelta;
            shadowImage = shadowTransform.GetComponent<Image>();
            if (shadowImage != null)
                baseShadowAlpha = shadowImage.color.a;
        }
    }

    public void OnTouchStart(Vector2 screenPos)
    {
        if (spriteAnim == null)
            spriteAnim = GetComponent<UISpriteAnimator>();

        isTouchActive = true;
        isDragging = false;
        isFalling = false;
        StopAllCoroutines();

        // Calculate offset between finger and animal center for smooth dragging
        RectTransform parentRT = rt.parent as RectTransform;
        if (parentRT != null)
        {
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT, screenPos, null, out localPoint))
            {
                dragOffset = rt.anchoredPosition - localPoint;
            }
        }
    }

    public void OnTap()
    {
        isTouchActive = false;

        // Trigger Success animation
        if (spriteAnim != null)
            spriteAnim.PlaySuccess();

        // Play animal name voice
        SoundLibrary.PlayAnimalName(animalId);

        // Bounce scale
        StopAllCoroutines();
        StartCoroutine(BounceAnim());
    }

    public void OnDrag(Vector2 screenPos)
    {
        if (!isTouchActive) return;

        if (!isDragging)
        {
            isDragging = true;
            if (spriteAnim != null)
                spriteAnim.PlayFloating();
        }

        RectTransform parentRT = rt.parent as RectTransform;
        if (parentRT != null)
        {
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT, screenPos, null, out localPoint))
            {
                rt.anchoredPosition = localPoint + dragOffset;
                UpdateShadow();
            }
        }
    }

    public void OnDragEnd()
    {
        isTouchActive = false;
        isDragging = false;

        // Stay in Floating while falling to ground
        StartCoroutine(FallToGround());
    }

    private IEnumerator FallToGround()
    {
        isFalling = true;
        float velocity = 0f;
        float gravity = 800f; // pixels/s²

        while (rt.anchoredPosition.y > groundY)
        {
            velocity += gravity * Time.deltaTime;
            float newY = rt.anchoredPosition.y - velocity * Time.deltaTime;

            if (newY <= groundY)
            {
                newY = groundY;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, newY);
                break;
            }

            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, newY);
            UpdateShadow();
            yield return null;
        }

        isFalling = false;

        // Nudge out of easel exclusion zone if dropped on it
        NudgeFromExclusionZone();

        // Small bounce on landing
        yield return StartCoroutine(LandBounce());

        // Return to Idle
        if (spriteAnim != null)
            spriteAnim.PlayIdle();
    }

    private IEnumerator LandBounce()
    {
        // Quick squash on impact
        float elapsed = 0f;
        float squashDur = 0.08f;
        Vector3 squash = new Vector3(1.15f, 0.85f, 1f);
        while (elapsed < squashDur)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.one, squash, elapsed / squashDur);
            yield return null;
        }

        // Stretch back
        elapsed = 0f;
        float stretchDur = 0.12f;
        while (elapsed < stretchDur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / stretchDur);
            transform.localScale = Vector3.Lerp(squash, Vector3.one, t);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    private IEnumerator BounceAnim()
    {
        transform.localScale = Vector3.one;

        float elapsed = 0f;
        float dur = 0.15f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.3f, elapsed / dur);
            yield return null;
        }

        elapsed = 0f;
        dur = 0.2f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            transform.localScale = Vector3.Lerp(Vector3.one * 1.3f, Vector3.one, t);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    private void NudgeFromExclusionZone()
    {
        float halfW = WorldController.ExclusionHalfWidth;
        if (halfW <= 0) return;

        float cx = WorldController.ExclusionCenterX;
        float x = rt.anchoredPosition.x;
        float halfAnimal = rt.sizeDelta.x * 0.5f;
        float dist = Mathf.Abs(x - cx);

        if (dist < halfW + halfAnimal)
        {
            // Nudge to nearest edge of the exclusion zone
            float edge = halfW + halfAnimal;
            float newX = (x < cx) ? cx - edge : cx + edge;
            rt.anchoredPosition = new Vector2(newX, rt.anchoredPosition.y);
            UpdateShadow();
        }
    }

    private void UpdateShadow()
    {
        if (shadowTransform == null) return;

        // Shadow stays at ground level, follows X position
        shadowTransform.anchoredPosition = new Vector2(rt.anchoredPosition.x, groundY - 8f);

        // Scale shadow based on height above ground
        float lift = Mathf.Max(0f, rt.anchoredPosition.y - groundY);
        float t = Mathf.Clamp01(lift / maxLiftHeight);

        // Shadow shrinks to 30% at max height
        float scaleFactor = Mathf.Lerp(1f, 0.3f, t);
        shadowTransform.sizeDelta = baseShadowSize * scaleFactor;

        // Shadow fades slightly when high
        if (shadowImage != null)
        {
            float alpha = Mathf.Lerp(baseShadowAlpha, baseShadowAlpha * 0.3f, t);
            var c = shadowImage.color;
            shadowImage.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}
