using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A fish that swims horizontally in the water area.
/// Handles movement, direction flipping, tap detection, and catch animations.
/// </summary>
public class SwimmingFish : MonoBehaviour
{
    public string fishId;
    public Image fishImage;
    public RectTransform rt;

    [HideInInspector] public float speed = 80f;
    [HideInInspector] public float swimMinX;
    [HideInInspector] public float swimMaxX;
    [HideInInspector] public bool movingRight;
    [HideInInspector] public bool locked; // locked during catch attempt

    private float wobbleOffset;
    private float wobbleSpeed;
    private float baseY;
    private Vector3 baseScale;

    public void Init(Sprite sprite, string id, float minX, float maxX,
                     float startX, float y, float fishSpeed, bool startRight)
    {
        fishId = id;
        fishImage = GetComponent<Image>();
        if (fishImage == null) fishImage = gameObject.AddComponent<Image>();
        fishImage.sprite = sprite;
        fishImage.preserveAspect = true;
        fishImage.raycastTarget = true;

        rt = GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(startX, y);
        rt.sizeDelta = new Vector2(130, 130);

        swimMinX = minX;
        swimMaxX = maxX;
        speed = fishSpeed;
        movingRight = startRight;
        locked = false;

        baseY = y;
        baseScale = rt.localScale;
        wobbleOffset = Random.Range(0f, Mathf.PI * 2f);
        wobbleSpeed = Random.Range(1.5f, 2.5f);

        // Flip sprite based on direction
        UpdateDirection();
    }

    private void Update()
    {
        if (locked) return;

        // Horizontal movement
        float dx = speed * Time.deltaTime * (movingRight ? 1f : -1f);
        var pos = rt.anchoredPosition;
        pos.x += dx;

        // Boundary turn-around
        if (pos.x >= swimMaxX)
        {
            pos.x = swimMaxX;
            movingRight = false;
            UpdateDirection();
        }
        else if (pos.x <= swimMinX)
        {
            pos.x = swimMinX;
            movingRight = true;
            UpdateDirection();
        }

        // Gentle vertical wobble
        float wobble = Mathf.Sin((Time.time + wobbleOffset) * wobbleSpeed) * 6f;
        pos.y = baseY + wobble;

        rt.anchoredPosition = pos;
    }

    private void UpdateDirection()
    {
        // Fish sprites face LEFT by default.
        // Moving left = default scale, moving right = flip X.
        var scale = baseScale;
        scale.x = movingRight ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
        rt.localScale = scale;
    }

    /// <summary>Lock fish in place (during catch attempt).</summary>
    public void Lock()
    {
        locked = true;
    }

    /// <summary>Unlock and resume swimming.</summary>
    public void Unlock()
    {
        locked = false;
    }

    /// <summary>Small squash reaction when hooked.</summary>
    public void PlayHookReaction()
    {
        // Quick squash-stretch
        LeanTweenHelper.PunchScale(rt, 0.15f);
    }
}

/// <summary>Simple tween helper — fallback if LeanTween isn't available.</summary>
public static class LeanTweenHelper
{
    public static void PunchScale(RectTransform rt, float duration)
    {
        if (rt == null) return;
        var mono = rt.GetComponent<MonoBehaviour>();
        if (mono != null)
            mono.StartCoroutine(PunchScaleCoroutine(rt, duration));
    }

    private static System.Collections.IEnumerator PunchScaleCoroutine(RectTransform rt, float duration)
    {
        Vector3 orig = rt.localScale;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float s = 1f + 0.2f * Mathf.Sin(t / duration * Mathf.PI);
            rt.localScale = new Vector3(
                orig.x > 0 ? Mathf.Abs(orig.x) * s : -Mathf.Abs(orig.x) * s,
                Mathf.Abs(orig.y) * s, orig.z);
            yield return null;
        }
        rt.localScale = orig;
    }
}
