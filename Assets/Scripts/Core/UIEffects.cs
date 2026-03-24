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

    private static readonly Color[] SparkleColors = new[]
    {
        new Color(1f, 0.84f, 0f),       // gold
        new Color(1f, 0.95f, 0.4f),     // light yellow
        Color.white,
        new Color(1f, 0.7f, 0.2f),      // orange-gold
    };

    /// <summary>
    /// Spawn sparkle star particles around a UI element.
    /// Stars fly outward, rotate, scale down, and fade out, then self-destruct.
    /// </summary>
    /// <param name="target">The RectTransform to sparkle around.</param>
    /// <param name="count">Number of sparkle particles (default 8).</param>
    public static void SpawnSparkles(RectTransform target, int count = 8)
    {
        if (target == null) return;

        Canvas rootCanvas = target.GetComponentInParent<Canvas>();
        if (rootCanvas == null) return;

        // We need a coroutine runner — use a temporary hidden object
        var runner = new GameObject("_SparkleRunner").AddComponent<SparkleRunner>();
        runner.transform.SetParent(rootCanvas.transform, false);

        for (int i = 0; i < count; i++)
        {
            GameObject star = CreateStarObject(target, i, count);
            runner.StartCoroutine(AnimateSparkle(star.GetComponent<RectTransform>(),
                                                  star.GetComponent<CanvasGroup>(),
                                                  target));
        }

        // Self-destruct runner after all animations finish
        Object.Destroy(runner.gameObject, 1f);
    }

    private static GameObject CreateStarObject(RectTransform target, int index, int total)
    {
        Transform parent = target.parent != null ? target.parent : target;

        var go = new GameObject($"Sparkle_{index}");
        go.transform.SetParent(parent, false);

        // RectTransform — start at target center
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = target.anchorMin;
        rt.anchorMax = target.anchorMax;
        rt.anchoredPosition = target.anchoredPosition;
        rt.sizeDelta = new Vector2(30f, 30f);
        rt.localScale = Vector3.one * Random.Range(0.6f, 1.2f);
        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        // CanvasGroup for fading
        var cg = go.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;

        // Star text
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "\u2605"; // ★
        tmp.fontSize = 36;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = SparkleColors[index % SparkleColors.Length];
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;

        return go;
    }

    private static IEnumerator AnimateSparkle(RectTransform rt, CanvasGroup cg, RectTransform target)
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
                             float intensity = 10f, float duration = 0.3f)
    {
        if (host == null || target == null) return;

        #if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
        #endif

        host.StartCoroutine(ShakeCoroutine(target, intensity, duration));
    }

    private static IEnumerator ShakeCoroutine(RectTransform target, float intensity, float duration)
    {
        Vector2 originalPos = target.anchoredPosition;
        float elapsed = 0f;
        float shakeInterval = 0.03f; // time between position changes
        float timer = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            timer += Time.deltaTime;

            if (timer >= shakeInterval)
            {
                timer = 0f;
                float decay = 1f - (elapsed / duration); // decays from 1 → 0
                float offsetX = Random.Range(-1f, 1f) * intensity * decay;
                float offsetY = Random.Range(-0.3f, 0.3f) * intensity * decay; // mostly horizontal
                target.anchoredPosition = originalPos + new Vector2(offsetX, offsetY);
            }

            yield return null;
        }

        // Snap back to original position
        target.anchoredPosition = originalPos;
    }
}
