using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a "new sticker!" popup animation when a sticker is awarded.
/// The sticker pops in (scale 0→1 with bounce), slight wiggle, sparkles,
/// then flies to the corner and disappears. ~1.5 seconds total.
/// </summary>
public static class StickerPopup
{
    private static bool _isShowing;

    public static IEnumerator Show(string stickerId)
    {
        if (_isShowing) yield break;

        Sprite sprite = StickerSpriteBank.GetSprite(stickerId);
        if (sprite == null) yield break;

        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) yield break;

        _isShowing = true;

        // Root container
        var rootGO = new GameObject("StickerPopup");
        rootGO.transform.SetParent(canvas.transform, false);
        rootGO.transform.SetAsLastSibling();
        var rootRT = rootGO.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero; rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero; rootRT.offsetMax = Vector2.zero;

        // Sticker image (centered)
        var stickerGO = new GameObject("Sticker");
        stickerGO.transform.SetParent(rootGO.transform, false);
        var stickerRT = stickerGO.AddComponent<RectTransform>();
        stickerRT.anchorMin = new Vector2(0.5f, 0.5f);
        stickerRT.anchorMax = new Vector2(0.5f, 0.5f);
        stickerRT.sizeDelta = new Vector2(200, 200);
        stickerRT.localScale = Vector3.zero;

        var stickerImg = stickerGO.AddComponent<Image>();
        stickerImg.sprite = sprite;
        stickerImg.preserveAspect = true;
        stickerImg.raycastTarget = false;

        // Glow behind
        var glowGO = new GameObject("Glow");
        glowGO.transform.SetParent(stickerGO.transform, false);
        glowGO.transform.SetAsFirstSibling();
        var glowRT = glowGO.AddComponent<RectTransform>();
        glowRT.anchorMin = new Vector2(-0.3f, -0.3f);
        glowRT.anchorMax = new Vector2(1.3f, 1.3f);
        glowRT.offsetMin = Vector2.zero;
        glowRT.offsetMax = Vector2.zero;
        var glowImg = glowGO.AddComponent<Image>();
        glowImg.color = new Color(1f, 0.95f, 0.5f, 0.3f);
        glowImg.raycastTarget = false;

        // ── Phase 1: Pop in with elastic bounce (0.4s) ──
        float t = 0f;
        float popDur = 0.4f;
        while (t < popDur)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / popDur);
            float scale = EaseOutElastic(p);
            stickerRT.localScale = Vector3.one * scale;
            stickerRT.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(p * Mathf.PI * 3f) * 8f * (1f - p));
            yield return null;
        }
        if (rootGO == null) { _isShowing = false; yield break; }
        stickerRT.localScale = Vector3.one;
        stickerRT.localRotation = Quaternion.identity;

        SoundLibrary.PlayStickerCollected();

        // ── Sparkle particles ──
        SpawnSparkles(rootGO.transform, stickerRT);

        // ── Phase 2: Hold with gentle pulse (0.6s) ──
        t = 0f;
        float holdDur = 0.6f;
        while (t < holdDur)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            float p = t / holdDur;
            float pulse = 1f + Mathf.Sin(p * Mathf.PI * 2f) * 0.05f;
            stickerRT.localScale = Vector3.one * pulse;
            float glowAlpha = 0.3f + Mathf.Sin(p * Mathf.PI * 3f) * 0.1f;
            glowImg.color = new Color(1f, 0.95f, 0.5f, glowAlpha);
            yield return null;
        }

        // ── Phase 3: Fly to bottom-left using anchors (aspect-ratio safe) ──
        if (rootGO == null) { _isShowing = false; yield break; }
        // Switch to anchor-based positioning for the fly-out
        stickerRT.anchorMin = new Vector2(0.5f, 0.5f);
        stickerRT.anchorMax = new Vector2(0.5f, 0.5f);
        Vector2 startPos = stickerRT.anchoredPosition;
        // Target: 10% from left, 10% from bottom (safe on all aspect ratios)
        var canvasRT = canvas.GetComponent<RectTransform>();
        float canvasW = canvasRT != null ? canvasRT.rect.width : 1920f;
        float canvasH = canvasRT != null ? canvasRT.rect.height : 1080f;
        Vector2 endPos = new Vector2(-canvasW * 0.35f, -canvasH * 0.35f);

        t = 0f;
        float flyDur = 0.5f;
        while (t < flyDur)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / flyDur);
            float ease = p * p;
            stickerRT.anchoredPosition = Vector2.Lerp(startPos, endPos, ease);
            float scale = Mathf.Lerp(1f, 0.15f, ease);
            stickerRT.localScale = Vector3.one * scale;
            stickerImg.color = new Color(1, 1, 1, 1f - ease * 0.5f);
            glowImg.color = new Color(1f, 0.95f, 0.5f, 0.3f * (1f - ease));
            yield return null;
        }

        if (rootGO != null) Object.Destroy(rootGO);
        _isShowing = false;
    }

    private static void SpawnSparkles(Transform parent, RectTransform center)
    {
        for (int i = 0; i < 8; i++)
        {
            var go = new GameObject("Sparkle");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(12, 12);
            rt.anchoredPosition = center.anchoredPosition;

            var img = go.AddComponent<Image>();
            img.color = i % 2 == 0
                ? new Color(1f, 0.9f, 0.3f, 0.9f)
                : new Color(1f, 1f, 1f, 0.8f);
            img.raycastTarget = false;

            float angle = i * 45f * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            var mono = go.AddComponent<SparkleParticle>();
            mono.velocity = dir * Random.Range(300f, 500f);
            mono.lifetime = Random.Range(0.4f, 0.7f);
        }
    }

    private static float EaseOutElastic(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        float p = 0.35f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - p / 4f) * (2f * Mathf.PI) / p) + 1f;
    }
}

/// <summary>Simple self-destroying sparkle particle.</summary>
public class SparkleParticle : MonoBehaviour
{
    public Vector2 velocity;
    public float lifetime;
    private float _elapsed;
    private RectTransform _rt;
    private Image _img;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _img = GetComponent<Image>();
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        if (_elapsed >= lifetime) { Destroy(gameObject); return; }

        float p = _elapsed / lifetime;
        _rt.anchoredPosition += velocity * Time.deltaTime;
        velocity *= 0.92f;
        _rt.localScale = Vector3.one * (1f - p * 0.8f);
        var c = _img.color;
        _img.color = new Color(c.r, c.g, c.b, c.a * (1f - p));
    }
}
