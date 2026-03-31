using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Reusable visual-feedback utilities for UI elements.
/// All methods are static — no MonoBehaviour instance needed except Shake
/// (which requires a coroutine host).
/// </summary>
public static class UIEffects
{
    // ── Sparkles ─────────────────────────────────────────────────────

    private static Sprite _cachedStar;

    private static readonly Color[] SparkleColors = new[]
    {
        new Color(1f, 0.84f, 0f),       // gold
        new Color(1f, 0.4f, 0.6f),      // pink
        new Color(0.4f, 0.8f, 1f),      // sky blue
        new Color(0.5f, 1f, 0.5f),      // mint green
        new Color(0.7f, 0.5f, 1f),      // purple
        new Color(1f, 0.6f, 0.2f),      // orange
        new Color(1f, 0.95f, 0.3f),     // yellow
        Color.white,
        new Color(1f, 0.3f, 0.3f),      // red
        new Color(0.3f, 0.9f, 0.8f),    // teal
    };

    /// <summary>
    /// Spawn sparkle star particles around a UI element.
    /// Stars fly outward, rotate, scale down, and fade out, then self-destruct.
    /// </summary>
    /// <param name="target">The RectTransform to sparkle around.</param>
    /// <param name="count">Number of sparkle particles (default 8).</param>
    public static void SpawnSparkles(RectTransform target, int count = 12)
    {
        if (target == null) return;

        Canvas rootCanvas = target.GetComponentInParent<Canvas>();
        if (rootCanvas == null) return;

        // Create sparkles on the root canvas, on top of everything
        var runner = new GameObject("_SparkleRunner").AddComponent<SparkleRunner>();
        runner.transform.SetParent(rootCanvas.transform, false);
        runner.transform.SetAsLastSibling();

        // Convert target center to root canvas space
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, worldCenter);
        RectTransform canvasRT = rootCanvas.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRT, screenPos, null, out Vector2 localCenter);

        for (int i = 0; i < count; i++)
        {
            GameObject star = CreateStarObject(runner.transform, localCenter, i);
            runner.StartCoroutine(AnimateSparkle(star.GetComponent<RectTransform>(),
                                                  star.GetComponent<CanvasGroup>()));
        }

        Object.Destroy(runner.gameObject, 1f);
    }

    private static GameObject CreateStarObject(Transform parent, Vector2 center, int index)
    {
        var go = new GameObject($"Sparkle_{index}");
        go.transform.SetParent(parent, false);

        float size = Random.Range(18f, 32f);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center;
        rt.sizeDelta = new Vector2(size, size);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        var cg = go.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;

        // Colored circle particle
        var img = go.AddComponent<Image>();
        img.color = SparkleColors[index % SparkleColors.Length];
        img.raycastTarget = false;
        if (_cachedStar == null)
        {
            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
                if (s.name == "star") { _cachedStar = s; break; }
        }
        if (_cachedStar != null) img.sprite = _cachedStar;

        return go;
    }

    private static IEnumerator AnimateSparkle(RectTransform rt, CanvasGroup cg)
    {
        float duration = Random.Range(0.45f, 0.7f);
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(60f, 140f);
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 startPos = rt.anchoredPosition;
        float startScale = rt.localScale.x;
        float spinSpeed = Random.Range(180f, 400f) * (Random.value > 0.5f ? 1f : -1f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Ease-out movement
            float eased = 1f - (1f - t) * (1f - t);
            rt.anchoredPosition = startPos + direction * distance * eased;

            // Spin
            rt.Rotate(0, 0, spinSpeed * Time.deltaTime);

            // Scale down in second half
            float scale = t < 0.4f ? startScale : Mathf.Lerp(startScale, 0f, (t - 0.4f) / 0.6f);
            rt.localScale = Vector3.one * scale;

            // Fade out in second half
            cg.alpha = t < 0.3f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.3f) / 0.7f);

            yield return null;
        }

        Object.Destroy(rt.gameObject);
    }

    /// <summary>Tiny MonoBehaviour to host sparkle coroutines, auto-destroyed.</summary>
    private class SparkleRunner : MonoBehaviour { }

    // ── Shake ────────────────────────────────────────────────────────

    /// <summary>
    /// Rapidly shake a UI element left/right then return to its original position.
    /// Requires a MonoBehaviour host to run the coroutine.
    /// </summary>
    /// <param name="host">Any active MonoBehaviour (typically the game controller).</param>
    /// <param name="target">The RectTransform to shake.</param>
    /// <param name="intensity">Max pixel offset (default 10).</param>
    /// <param name="duration">Total shake time in seconds (default 0.3).</param>
    public static void Shake(MonoBehaviour host, RectTransform target,
                             float intensity = 10f, float duration = 0.4f)
    {
        if (host == null || target == null) return;

        #if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
        #endif

        host.StartCoroutine(ShakeCoroutine(host, target, intensity, duration));
    }

    /// <summary>
    /// Cute "no-no" animation: gentle wobble rotation + brief red tint + slight shrink,
    /// then bounces back. Friendly, not scary for kids.
    /// </summary>
    private static IEnumerator ShakeCoroutine(MonoBehaviour host, RectTransform target,
        float intensity, float duration)
    {
        Vector2 originalPos = target.anchoredPosition;
        Quaternion originalRot = target.localRotation;

        // Try to tint the element briefly
        var img = target.GetComponent<Image>();
        Color originalColor = img != null ? img.color : Color.white;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float decay = 1f - t;

            // Gentle wobble rotation (like shaking head "no-no")
            float wobble = Mathf.Sin(t * Mathf.PI * 6f) * 8f * decay;
            target.localRotation = originalRot * Quaternion.Euler(0, 0, wobble);

            // Soft horizontal sway
            float sway = Mathf.Sin(t * Mathf.PI * 6f) * intensity * 0.5f * decay;
            target.anchoredPosition = originalPos + new Vector2(sway, 0);

            // Brief rosy tint in first half
            if (img != null)
            {
                float tintStrength = t < 0.5f ? (1f - t * 2f) * 0.3f : 0f;
                img.color = Color.Lerp(originalColor,
                    new Color(1f, 0.6f, 0.6f, originalColor.a), tintStrength);
            }

            yield return null;
        }

        // Restore everything
        target.anchoredPosition = originalPos;
        target.localRotation = originalRot;
        if (img != null) img.color = originalColor;
    }
}
