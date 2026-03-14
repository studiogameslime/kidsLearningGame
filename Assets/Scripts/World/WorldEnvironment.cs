using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the World scene environment: layered backgrounds, day/night cycle,
/// sun/moon toggle with animations, color transitions, and night stars.
/// </summary>
public class WorldEnvironment : MonoBehaviour
{
    [Header("Sky & Background Layers")]
    public Image skyBackground;
    public Image hillsLargeLayer;
    public Image hillsLayer;
    public Image groundBackLayer;
    public Image groundFrontLayer;

    [Header("Sun & Moon")]
    public RectTransform sunRT;
    public RectTransform moonRT;
    public Image sunImage;
    public Image moonImage;
    public Image sunGlow;

    [Header("Stars")]
    public RectTransform starsContainer;

    // Day colors
    private static readonly Color DaySky = HexColor("#8FD4F5");
    private static readonly Color DayHillsLarge = HexColor("#B7D7D6");
    private static readonly Color DayHills = HexColor("#9FCBC5");
    private static readonly Color DayGroundBack = HexColor("#8ED36B");
    private static readonly Color DayGroundFront = HexColor("#79C956");
    private static readonly Color DaySunGlow = new Color(1f, 0.95f, 0.6f, 0.25f);

    // Night colors
    private static readonly Color NightSky = HexColor("#345A8A");
    private static readonly Color NightHillsLarge = HexColor("#6E93A0");
    private static readonly Color NightHills = HexColor("#5F8491");
    private static readonly Color NightGroundBack = HexColor("#4F8E4F");
    private static readonly Color NightGroundFront = HexColor("#447A44");
    public bool IsNight { get; private set; }
    private bool isTransitioning;

    private float sunRestY;   // resting Y for sun (visible)
    private float moonRestY;  // resting Y for moon (visible)
    private float offScreenY; // Y below visible area

    // Stars
    private List<Image> starImages = new List<Image>();
    private float[] starMaxAlphas;      // per-star max brightness
    private float[] starTwinkleSpeed;   // per-star twinkle frequency
    private float[] starTwinkleOffset;  // per-star phase offset
    private const int StarCount = 120;

    private void Start()
    {
        // Calculate positions based on parent
        // Sun/moon use anchor at 0.93 of content height, so anchoredPosition.y=0 is the rest position.
        // offScreenY is a negative offset that moves them down behind the ground layers.
        sunRestY = sunRT != null ? sunRT.anchoredPosition.y : 0f;
        moonRestY = moonRT != null ? moonRT.anchoredPosition.y : 0f;
        RectTransform parentRT = sunRT != null ? sunRT.parent as RectTransform : null;
        float contentHeight = parentRT != null ? parentRT.rect.height : 1810f;
        // Move from anchor 0.93 down to ~0.30 of content (behind ground at 0.45)
        offScreenY = -(contentHeight * 0.63f);

        // Start in day mode
        IsNight = false;
        ApplyColors(0f);

        // Sun visible, moon hidden below
        if (sunRT != null) sunRT.anchoredPosition = new Vector2(sunRT.anchoredPosition.x, sunRestY);
        if (moonRT != null) moonRT.anchoredPosition = new Vector2(moonRT.anchoredPosition.x, offScreenY);

        // Create stars (invisible during day)
        CreateStars();
        SetStarsAlpha(0f);
    }

    private void CreateStars()
    {
        if (starsContainer == null) return;

        starMaxAlphas = new float[StarCount];
        starTwinkleSpeed = new float[StarCount];
        starTwinkleOffset = new float[StarCount];

        for (int i = 0; i < StarCount; i++)
        {
            var go = new GameObject($"Star{i}");
            go.transform.SetParent(starsContainer, false);
            var rt = go.AddComponent<RectTransform>();

            // Random position across the container
            float x = Random.Range(0.02f, 0.98f);
            float y = Random.Range(0.05f, 0.95f);
            rt.anchorMin = new Vector2(x, y);
            rt.anchorMax = new Vector2(x, y);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // Tiny size (2-8px) — more variety with denser field
            float size = Random.Range(2f, 8f);
            rt.sizeDelta = new Vector2(size, size);

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;

            // Soft white, pale blue, or warm yellow tint
            float tint = Random.Range(0f, 1f);
            if (tint < 0.5f)
                img.color = new Color(1f, 1f, 1f, 0f);           // white
            else if (tint < 0.8f)
                img.color = new Color(0.85f, 0.92f, 1f, 0f);     // pale blue
            else
                img.color = new Color(1f, 0.95f, 0.8f, 0f);      // warm yellow

            starImages.Add(img);

            // Per-star twinkle parameters
            starMaxAlphas[i] = Random.Range(0.3f, 0.95f);
            starTwinkleSpeed[i] = Random.Range(0.5f, 2.5f);
            starTwinkleOffset[i] = Random.Range(0f, Mathf.PI * 2f);
        }

        StartCoroutine(TwinkleStars());
    }

    private IEnumerator TwinkleStars()
    {
        while (true)
        {
            if (IsNight && !isTransitioning)
            {
                for (int i = 0; i < starImages.Count; i++)
                {
                    if (starImages[i] == null) continue;
                    float twinkle = 0.7f + 0.3f * Mathf.Sin(Time.time * starTwinkleSpeed[i] + starTwinkleOffset[i]);
                    var c = starImages[i].color;
                    starImages[i].color = new Color(c.r, c.g, c.b, starMaxAlphas[i] * twinkle);
                }
            }
            yield return null;
        }
    }

    private void SetStarsAlpha(float alpha)
    {
        foreach (var img in starImages)
        {
            if (img == null) continue;
            var c = img.color;
            // Each star has a slightly different max brightness for variety
            img.color = new Color(c.r, c.g, c.b, alpha);
        }
    }

    public void OnSunTapped()
    {
        if (isTransitioning || IsNight) return;
        StartCoroutine(TransitionToNight());
    }

    public void OnMoonTapped()
    {
        if (isTransitioning || !IsNight) return;
        StartCoroutine(TransitionToDay());
    }

    private IEnumerator TransitionToNight()
    {
        isTransitioning = true;

        // Squash-bounce the sun
        yield return SquashBounce(sunRT, 0.15f);

        // Animate sun down + colors transition simultaneously
        float dur = 1.2f;
        float elapsed = 0f;
        Vector2 sunStart = sunRT.anchoredPosition;
        Vector2 sunEnd = new Vector2(sunStart.x, offScreenY);
        Vector2 moonStart = new Vector2(moonRT.anchoredPosition.x, offScreenY);
        Vector2 moonEnd = new Vector2(moonRT.anchoredPosition.x, moonRestY);

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);

            // Sun goes down
            if (sunRT != null) sunRT.anchoredPosition = Vector2.Lerp(sunStart, sunEnd, t);

            // Moon comes up (delayed start at 30%)
            float moonT = Mathf.Clamp01((t - 0.3f) / 0.7f);
            moonT = Mathf.SmoothStep(0f, 1f, moonT);
            if (moonRT != null) moonRT.anchoredPosition = Vector2.Lerp(moonStart, moonEnd, moonT);

            // Colors transition
            ApplyColors(t);

            // Sun glow fade out
            if (sunGlow != null) sunGlow.color = new Color(DaySunGlow.r, DaySunGlow.g, DaySunGlow.b, DaySunGlow.a * (1f - t));

            // Stars fade in (synced with transition)
            for (int i = 0; i < starImages.Count; i++)
            {
                if (starImages[i] == null) continue;
                var c = starImages[i].color;
                starImages[i].color = new Color(c.r, c.g, c.b, starMaxAlphas[i] * t);
            }

            yield return null;
        }

        // Settle moon with bounce
        yield return SquashBounce(moonRT, 0.12f);

        IsNight = true;
        isTransitioning = false;
    }

    private IEnumerator TransitionToDay()
    {
        isTransitioning = true;

        // Squash-bounce the moon
        yield return SquashBounce(moonRT, 0.15f);

        float dur = 1.2f;
        float elapsed = 0f;
        Vector2 moonStart = moonRT.anchoredPosition;
        Vector2 moonEnd = new Vector2(moonStart.x, offScreenY);
        Vector2 sunStart = new Vector2(sunRT.anchoredPosition.x, offScreenY);
        Vector2 sunEnd = new Vector2(sunRT.anchoredPosition.x, sunRestY);

        // Capture current star alphas for smooth fade out
        float[] starStartAlpha = new float[starImages.Count];
        for (int i = 0; i < starStartAlpha.Length; i++)
        {
            if (starImages[i] != null)
                starStartAlpha[i] = starImages[i].color.a;
        }

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);

            // Moon goes down
            if (moonRT != null) moonRT.anchoredPosition = Vector2.Lerp(moonStart, moonEnd, t);

            // Sun comes up (delayed start at 30%)
            float sunT = Mathf.Clamp01((t - 0.3f) / 0.7f);
            sunT = Mathf.SmoothStep(0f, 1f, sunT);
            if (sunRT != null) sunRT.anchoredPosition = Vector2.Lerp(sunStart, sunEnd, sunT);

            // Colors transition (reverse: night → day, so use 1-t)
            ApplyColors(1f - t);

            // Sun glow fade in
            if (sunGlow != null) sunGlow.color = new Color(DaySunGlow.r, DaySunGlow.g, DaySunGlow.b, DaySunGlow.a * sunT);

            // Stars fade out (synced with transition)
            for (int i = 0; i < starImages.Count; i++)
            {
                if (starImages[i] == null) continue;
                var c = starImages[i].color;
                starImages[i].color = new Color(c.r, c.g, c.b, starStartAlpha[i] * (1f - t));
            }

            yield return null;
        }

        // Settle sun with bounce
        yield return SquashBounce(sunRT, 0.12f);

        IsNight = false;
        isTransitioning = false;
    }

    private void ApplyColors(float t)
    {
        // t: 0 = day, 1 = night
        if (skyBackground != null) skyBackground.color = Color.Lerp(DaySky, NightSky, t);
        if (hillsLargeLayer != null) hillsLargeLayer.color = Color.Lerp(DayHillsLarge, NightHillsLarge, t);
        if (hillsLayer != null) hillsLayer.color = Color.Lerp(DayHills, NightHills, t);
        if (groundBackLayer != null) groundBackLayer.color = Color.Lerp(DayGroundBack, NightGroundBack, t);
        if (groundFrontLayer != null) groundFrontLayer.color = Color.Lerp(DayGroundFront, NightGroundFront, t);
    }

    private IEnumerator SquashBounce(RectTransform rt, float duration)
    {
        if (rt == null) yield break;
        float half = duration * 0.5f;
        Vector3 squash = new Vector3(1.2f, 0.8f, 1f);
        float elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            rt.localScale = Vector3.Lerp(Vector3.one, squash, elapsed / half);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            rt.localScale = Vector3.Lerp(squash, Vector3.one, Mathf.SmoothStep(0f, 1f, elapsed / half));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    public Color GetCurrentCloudTint()
    {
        return IsNight ? HexColor("#BFCFE0") : HexColor("#F4FBFF");
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
