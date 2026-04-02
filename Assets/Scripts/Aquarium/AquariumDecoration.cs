using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Draggable aquarium decoration (corals, plants, rocks).
/// Free drag within aquarium, but resting placement restricted to sand area.
/// If released above sand, sinks down with a playful wobble animation.
/// </summary>
public class AquariumDecoration : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public string itemId;
    public RectTransform dragBounds;
    public AquariumController controller;
    public float sandMaxY = 0.35f;

    private RectTransform rt;
    private bool isFalling;
    private bool isWiggling;
    private bool wasDragged;
    private float idleWiggleTimer;
    private float nextIdleWiggle;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        nextIdleWiggle = Random.Range(8f, 18f);
    }

    private void Update()
    {
        if (isFalling || isWiggling) return;

        // Occasional ambient wiggle
        idleWiggleTimer += Time.deltaTime;
        if (idleWiggleTimer >= nextIdleWiggle)
        {
            idleWiggleTimer = 0f;
            nextIdleWiggle = Random.Range(10f, 20f);
            StartCoroutine(Wiggle(4f, 0.3f));
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isFalling) StopAllCoroutines();
        isFalling = false;
        isWiggling = false;
        wasDragged = true;

        rt.SetAsLastSibling();
        rt.localScale = Vector3.one * 1.08f;
        rt.localRotation = Quaternion.identity;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rt.parent as RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPos);
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rt.parent as RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPos);

        // Free drag within full gameplay bounds
        if (dragBounds != null)
        {
            Rect bounds = dragBounds.rect;
            float halfW = rt.sizeDelta.x * 0.5f;
            float halfH = rt.sizeDelta.y * 0.5f;
            localPos.x = Mathf.Clamp(localPos.x, bounds.xMin + halfW, bounds.xMax - halfW);
            localPos.y = Mathf.Clamp(localPos.y, bounds.yMin + halfH, bounds.yMax - halfH);
        }

        rt.anchoredPosition = localPos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        rt.localScale = Vector3.one;

        // Calculate max Y for sand area (relative to gameplay bounds)
        float maxY = GetSandTopY();

        if (rt.anchoredPosition.y > maxY)
        {
            // Released above sand — sink down with wobble
            StartCoroutine(SinkToSand(maxY));
        }
        else
        {
            // Already on sand — persist position
            if (controller != null)
                controller.OnDecorationMoved(this);
        }
    }

    private float GetSandTopY()
    {
        if (dragBounds == null) return 0f;
        Rect bounds = dragBounds.rect;
        // sandMaxY is the normalized proportion of the gameplay area that counts as "sand"
        return bounds.yMin + bounds.height * sandMaxY;
    }

    private IEnumerator SinkToSand(float targetY)
    {
        isFalling = true;

        Vector2 startPos = rt.anchoredPosition;
        float halfH = rt.sizeDelta.y * 0.5f;
        float landY = targetY - Random.Range(0f, 30f); // slight variation in landing spot

        // Clamp landing Y so it doesn't go below bounds
        if (dragBounds != null)
            landY = Mathf.Max(landY, dragBounds.rect.yMin + halfH);

        Vector2 endPos = new Vector2(startPos.x, landY);
        float distance = startPos.y - landY;
        float dur = Mathf.Clamp(distance / 400f, 0.3f, 0.8f); // speed scales with distance

        float elapsed = 0f;
        float wobbleSpeed = Random.Range(8f, 12f);
        float wobbleAmp = Random.Range(6f, 12f);

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;

            // Ease-in fall (accelerating like sinking)
            float eased = t * t;
            Vector2 pos = Vector2.Lerp(startPos, endPos, eased);

            // Horizontal wobble while sinking
            float wobble = Mathf.Sin(elapsed * wobbleSpeed) * wobbleAmp * (1f - t);
            pos.x += wobble;

            // Slight rotation wobble
            float rotAngle = Mathf.Sin(elapsed * wobbleSpeed * 0.8f) * 8f * (1f - t);
            rt.localRotation = Quaternion.Euler(0, 0, rotAngle);

            rt.anchoredPosition = pos;
            yield return null;
        }

        // Landing settle — small bounce
        rt.localRotation = Quaternion.identity;
        Vector2 landedPos = endPos;
        float bounceH = 8f;
        float bounceDur = 0.15f;
        elapsed = 0f;
        while (elapsed < bounceDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / bounceDur;
            float bounceY = Mathf.Sin(t * Mathf.PI) * bounceH;
            rt.anchoredPosition = landedPos + new Vector2(0, bounceY);
            yield return null;
        }

        // Final tiny settle wobble
        float settleAngle = Random.Range(-4f, 4f); // slight random tilt
        elapsed = 0f;
        float settleDur = 0.12f;
        while (elapsed < settleDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / settleDur;
            float angle = Mathf.Lerp(0, settleAngle, t);
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            yield return null;
        }

        rt.anchoredPosition = landedPos;
        isFalling = false;

        if (controller != null)
            controller.OnDecorationMoved(this);
    }

    // ── Tap Wiggle ──

    public void OnPointerClick(PointerEventData eventData)
    {
        // Ignore if this was a drag, not a tap
        if (wasDragged) { wasDragged = false; return; }
        if (isFalling || isWiggling) return;

        StartCoroutine(Wiggle(8f, 0.4f));

        // Spawn a couple tiny bubbles
        if (controller != null)
            controller.SpawnBubblesAt(rt.anchoredPosition, 2);
    }

    private IEnumerator Wiggle(float maxAngle, float duration)
    {
        isWiggling = true;
        float t = 0f;
        Quaternion startRot = rt.localRotation;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            float angle = Mathf.Sin(p * Mathf.PI * 4f) * maxAngle * (1f - p);
            float scale = 1f + Mathf.Sin(p * Mathf.PI) * 0.05f;
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            rt.localScale = Vector3.one * scale;
            yield return null;
        }

        rt.localRotation = startRot;
        rt.localScale = Vector3.one;
        isWiggling = false;
    }
}
