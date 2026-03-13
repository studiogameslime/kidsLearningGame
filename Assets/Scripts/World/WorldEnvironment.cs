using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the World scene environment: layered backgrounds, day/night cycle,
/// sun/moon toggle with animations and color transitions.
/// </summary>
public class WorldEnvironment : MonoBehaviour
{
    [Header("Sky & Background Layers")]
    public Image skyBackground;
    public Image mountainsLayer;
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
    public Image moonGlow;

    [Header("Cloud Layers (static)")]
    public Image cloudLayerBack1;
    public Image cloudLayerBack2;
    public Image cloudLayerFront1;
    public Image cloudLayerFront2;

    // Day colors
    private static readonly Color DaySky = HexColor("#8FD4F5");
    private static readonly Color DayMountains = HexColor("#DCEEF8");
    private static readonly Color DayHillsLarge = HexColor("#B7D7D6");
    private static readonly Color DayHills = HexColor("#9FCBC5");
    private static readonly Color DayGroundBack = HexColor("#8ED36B");
    private static readonly Color DayGroundFront = HexColor("#79C956");
    private static readonly Color DayCloudTint = HexColor("#F4FBFF");
    private static readonly Color DaySunGlow = new Color(1f, 0.95f, 0.6f, 0.25f);

    // Night colors
    private static readonly Color NightSky = HexColor("#345A8A");
    private static readonly Color NightMountains = HexColor("#7D96B2");
    private static readonly Color NightHillsLarge = HexColor("#6E93A0");
    private static readonly Color NightHills = HexColor("#5F8491");
    private static readonly Color NightGroundBack = HexColor("#4F8E4F");
    private static readonly Color NightGroundFront = HexColor("#447A44");
    private static readonly Color NightCloudTint = HexColor("#BFCFE0");
    private static readonly Color NightMoonGlow = new Color(0.85f, 0.9f, 1f, 0.3f);

    public bool IsNight { get; private set; }
    private bool isTransitioning;

    private float sunRestY;   // resting Y for sun (visible)
    private float moonRestY;  // resting Y for moon (visible)
    private float offScreenY; // Y below visible area

    private void Start()
    {
        // Calculate positions based on parent
        sunRestY = sunRT != null ? sunRT.anchoredPosition.y : 0f;
        moonRestY = moonRT != null ? moonRT.anchoredPosition.y : 0f;
        offScreenY = -300f;

        // Start in day mode
        IsNight = false;
        ApplyColors(0f); // t=0 = full day

        // Sun visible, moon hidden below
        if (sunRT != null) sunRT.anchoredPosition = new Vector2(sunRT.anchoredPosition.x, sunRestY);
        if (moonRT != null) moonRT.anchoredPosition = new Vector2(moonRT.anchoredPosition.x, offScreenY);
        if (moonGlow != null) moonGlow.color = new Color(NightMoonGlow.r, NightMoonGlow.g, NightMoonGlow.b, 0f);
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

            // Glow transition
            if (sunGlow != null) sunGlow.color = new Color(DaySunGlow.r, DaySunGlow.g, DaySunGlow.b, DaySunGlow.a * (1f - t));
            if (moonGlow != null) moonGlow.color = new Color(NightMoonGlow.r, NightMoonGlow.g, NightMoonGlow.b, NightMoonGlow.a * moonT);

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

            // Glow transition
            if (moonGlow != null) moonGlow.color = new Color(NightMoonGlow.r, NightMoonGlow.g, NightMoonGlow.b, NightMoonGlow.a * (1f - t));
            if (sunGlow != null) sunGlow.color = new Color(DaySunGlow.r, DaySunGlow.g, DaySunGlow.b, DaySunGlow.a * sunT);

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
        if (mountainsLayer != null) mountainsLayer.color = Color.Lerp(DayMountains, NightMountains, t);
        if (hillsLargeLayer != null) hillsLargeLayer.color = Color.Lerp(DayHillsLarge, NightHillsLarge, t);
        if (hillsLayer != null) hillsLayer.color = Color.Lerp(DayHills, NightHills, t);
        if (groundBackLayer != null) groundBackLayer.color = Color.Lerp(DayGroundBack, NightGroundBack, t);
        if (groundFrontLayer != null) groundFrontLayer.color = Color.Lerp(DayGroundFront, NightGroundFront, t);

        Color cloudTint = Color.Lerp(DayCloudTint, NightCloudTint, t);
        if (cloudLayerBack1 != null) cloudLayerBack1.color = cloudTint;
        if (cloudLayerBack2 != null) cloudLayerBack2.color = cloudTint;
        if (cloudLayerFront1 != null) cloudLayerFront1.color = cloudTint;
        if (cloudLayerFront2 != null) cloudLayerFront2.color = cloudTint;
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
        return IsNight ? NightCloudTint : DayCloudTint;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
