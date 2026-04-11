using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shows a polished "new sticker!" popup when a sticker is awarded.
/// Design: dim background → circular glow → sticker bounces in → "מדבקה חדשה!" label → fade out.
/// Auto-timeout at 4 seconds to prevent stuck state.
/// </summary>
public static class StickerPopup
{
    private static bool _isShowing;
    private static float _showStartTime;
    private const float MaxDuration = 4f;

    public static IEnumerator Show(string stickerId)
    {
        if (_isShowing)
        {
            // Safety: if stuck for too long, force reset
            if (Time.realtimeSinceStartup - _showStartTime > MaxDuration)
                _isShowing = false;
            else
                yield break;
        }

        Sprite sprite = StickerSpriteBank.GetSprite(stickerId);
        if (sprite == null) yield break;

        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) yield break;

        _isShowing = true;
        _showStartTime = Time.realtimeSinceStartup;

        // ── Root overlay ──
        var rootGO = new GameObject("StickerPopup");
        rootGO.transform.SetParent(canvas.transform, false);
        rootGO.transform.SetAsLastSibling();
        var rootRT = rootGO.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero; rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero; rootRT.offsetMax = Vector2.zero;

        // Dim background (semi-transparent, makes popup pop out)
        var dimImg = rootGO.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0);
        dimImg.raycastTarget = true; // block touches during popup

        // ── Center container ──
        var containerGO = new GameObject("Container");
        containerGO.transform.SetParent(rootGO.transform, false);
        var containerRT = containerGO.AddComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0.5f);
        containerRT.anchorMax = new Vector2(0.5f, 0.5f);
        containerRT.sizeDelta = new Vector2(280, 340);
        containerRT.localScale = Vector3.zero;

        // Card background (rounded, soft white)
        var cardBg = containerGO.AddComponent<Image>();
        var roundedRect = Resources.Load<Sprite>("UI/RoundedRect");
        if (roundedRect == null)
        {
            // Fallback: try other paths
            var all = Resources.LoadAll<Sprite>("");
            foreach (var s in all)
                if (s.name == "RoundedRect") { roundedRect = s; break; }
        }
        if (roundedRect != null) { cardBg.sprite = roundedRect; cardBg.type = Image.Type.Sliced; }
        cardBg.color = new Color(1f, 1f, 1f, 0.95f);
        cardBg.raycastTarget = false;

        // Card shadow
        var cardShadow = containerGO.AddComponent<Shadow>();
        cardShadow.effectColor = new Color(0, 0, 0, 0.25f);
        cardShadow.effectDistance = new Vector2(0, -4);

        // ── Circular glow behind sticker ──
        var glowGO = new GameObject("Glow");
        glowGO.transform.SetParent(containerGO.transform, false);
        var glowRT = glowGO.AddComponent<RectTransform>();
        glowRT.anchorMin = new Vector2(0.5f, 0.55f);
        glowRT.anchorMax = new Vector2(0.5f, 0.55f);
        glowRT.sizeDelta = new Vector2(220, 220);
        var glowImg = glowGO.AddComponent<Image>();
        var circleSprite = Resources.Load<Sprite>("Circle");
        if (circleSprite != null) glowImg.sprite = circleSprite;
        glowImg.color = new Color(1f, 0.95f, 0.5f, 0.25f);
        glowImg.raycastTarget = false;

        // ── Sticker image ──
        var stickerGO = new GameObject("Sticker");
        stickerGO.transform.SetParent(containerGO.transform, false);
        var stickerRT = stickerGO.AddComponent<RectTransform>();
        stickerRT.anchorMin = new Vector2(0.5f, 0.55f);
        stickerRT.anchorMax = new Vector2(0.5f, 0.55f);
        stickerRT.sizeDelta = new Vector2(180, 180);
        var stickerImg = stickerGO.AddComponent<Image>();
        stickerImg.sprite = sprite;
        stickerImg.preserveAspect = true;
        stickerImg.raycastTarget = false;

        // ── "מדבקה חדשה!" label at bottom of card ──
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(containerGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0, 0);
        labelRT.anchorMax = new Vector2(1, 0);
        labelRT.pivot = new Vector2(0.5f, 0);
        labelRT.anchoredPosition = new Vector2(0, 16);
        labelRT.sizeDelta = new Vector2(0, 50);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(labelTMP, "\u05DE\u05D3\u05D1\u05E7\u05D4 \u05D7\u05D3\u05E9\u05D4!"); // מדבקה חדשה!
        labelTMP.fontSize = 32;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.color = new Color(0.3f, 0.3f, 0.35f);
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.raycastTarget = false;

        // ═══════════════════════════════
        //  ANIMATION
        // ═══════════════════════════════

        // ── Phase 1: Dim in + card pop (0.4s) ──
        float t = 0f;
        while (t < 0.4f)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.4f);
            dimImg.color = new Color(0, 0, 0, p * 0.35f);
            containerRT.localScale = Vector3.one * EaseOutBack(p);
            yield return null;
        }
        if (rootGO == null) { _isShowing = false; yield break; }
        containerRT.localScale = Vector3.one;

        SoundLibrary.PlayStickerCollected();

        // Sparkles burst
        SpawnSparkles(containerGO.transform, stickerRT);

        // ── Phase 2: Hold with gentle pulse (1.0s) ──
        t = 0f;
        while (t < 1.0f)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            float p = t / 1.0f;
            float pulse = 1f + Mathf.Sin(p * Mathf.PI * 2f) * 0.03f;
            containerRT.localScale = Vector3.one * pulse;
            // Glow breathe
            float ga = 0.25f + Mathf.Sin(p * Mathf.PI * 3f) * 0.1f;
            glowImg.color = new Color(1f, 0.95f, 0.5f, ga);
            yield return null;
        }

        // ── Phase 3: Shrink + fade out (0.4s) ──
        if (rootGO == null) { _isShowing = false; yield break; }
        t = 0f;
        while (t < 0.4f)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.4f);
            float ease = p * p;
            containerRT.localScale = Vector3.one * Mathf.Lerp(1f, 0f, ease);
            dimImg.color = new Color(0, 0, 0, 0.35f * (1f - ease));
            yield return null;
        }

        if (rootGO != null) Object.Destroy(rootGO);
        _isShowing = false;
    }

    // ── Achievement variant ──

    private static readonly Color BronzeColor = new Color(0.8f, 0.5f, 0.2f);
    private static readonly Color SilverColor = new Color(0.75f, 0.75f, 0.78f);
    private static readonly Color GoldColor   = new Color(1f, 0.84f, 0f);

    public static IEnumerator ShowAchievement(string achievementId)
    {
        if (_isShowing) yield break;
        if (string.IsNullOrEmpty(achievementId)) yield break;

        string tier = "bronze";
        if (achievementId.EndsWith("_gold")) tier = "gold";
        else if (achievementId.EndsWith("_silver")) tier = "silver";

        string withoutPrefix = achievementId.Substring("achievement_".Length);
        string gameId = withoutPrefix.Substring(0, withoutPrefix.LastIndexOf('_'));

        Sprite thumbnail = null;
        var db = Resources.Load<GameDatabase>("GameDatabase");
        if (db != null)
        {
            foreach (var game in db.games)
                if (game != null && game.id == gameId && game.thumbnail != null)
                { thumbnail = game.thumbnail; break; }
        }
        if (thumbnail == null) yield break;

        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) yield break;

        _isShowing = true;
        _showStartTime = Time.realtimeSinceStartup;

        Color frameColor = tier == "gold" ? GoldColor : tier == "silver" ? SilverColor : BronzeColor;
        string tierName = tier == "gold" ? "\u05D6\u05D4\u05D1" : tier == "silver" ? "\u05DB\u05E1\u05E3" : "\u05D0\u05E8\u05D3"; // זהב/כסף/ארד

        // Root
        var rootGO = new GameObject("AchievementPopup");
        rootGO.transform.SetParent(canvas.transform, false);
        rootGO.transform.SetAsLastSibling();
        var rootRT = rootGO.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero; rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero; rootRT.offsetMax = Vector2.zero;

        var dimImg = rootGO.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0);
        dimImg.raycastTarget = true;

        // Card
        var containerGO = new GameObject("Container");
        containerGO.transform.SetParent(rootGO.transform, false);
        var containerRT = containerGO.AddComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0.5f);
        containerRT.anchorMax = new Vector2(0.5f, 0.5f);
        containerRT.sizeDelta = new Vector2(300, 360);
        containerRT.localScale = Vector3.zero;

        var cardBg = containerGO.AddComponent<Image>();
        cardBg.color = new Color(frameColor.r, frameColor.g, frameColor.b, 0.15f);
        cardBg.raycastTarget = false;
        containerGO.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.3f);

        // Frame border
        var frameGO = new GameObject("Frame");
        frameGO.transform.SetParent(containerGO.transform, false);
        var frameRT = frameGO.AddComponent<RectTransform>();
        frameRT.anchorMin = Vector2.zero; frameRT.anchorMax = Vector2.one;
        frameRT.offsetMin = new Vector2(4, 4); frameRT.offsetMax = new Vector2(-4, -4);
        var frameImg = frameGO.AddComponent<Image>();
        frameImg.color = Color.white;
        frameImg.raycastTarget = false;

        // Thumbnail
        var imgGO = new GameObject("Thumbnail");
        imgGO.transform.SetParent(frameGO.transform, false);
        var imgRT = imgGO.AddComponent<RectTransform>();
        imgRT.anchorMin = new Vector2(0.05f, 0.22f); imgRT.anchorMax = new Vector2(0.95f, 0.95f);
        imgRT.offsetMin = Vector2.zero; imgRT.offsetMax = Vector2.zero;
        var img = imgGO.AddComponent<Image>();
        img.sprite = thumbnail;
        img.preserveAspect = true;
        img.raycastTarget = false;

        // Label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(frameGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0, 0); labelRT.anchorMax = new Vector2(1, 0.22f);
        labelRT.offsetMin = Vector2.zero; labelRT.offsetMax = Vector2.zero;
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        string gameName = ParentDashboardViewModel.GetGameName(gameId);
        HebrewText.SetText(labelTMP, $"\u05DE\u05D3\u05DC\u05D9\u05D9\u05EA {tierName}!"); // מדליית זהב/כסף/ארד!
        labelTMP.fontSize = 28;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.color = frameColor;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.raycastTarget = false;

        // Top color bar
        var barGO = new GameObject("Bar");
        barGO.transform.SetParent(containerGO.transform, false);
        var barRT = barGO.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1); barRT.anchorMax = new Vector2(1, 1);
        barRT.pivot = new Vector2(0.5f, 1); barRT.sizeDelta = new Vector2(0, 6);
        barGO.AddComponent<Image>().color = frameColor;

        // Animation
        float t = 0f;
        while (t < 0.4f)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.4f);
            dimImg.color = new Color(0, 0, 0, p * 0.35f);
            containerRT.localScale = Vector3.one * EaseOutBack(p);
            yield return null;
        }
        if (rootGO == null) { _isShowing = false; yield break; }
        containerRT.localScale = Vector3.one;

        SoundLibrary.PlayStickerCollected();
        SpawnSparkles(containerGO.transform, containerRT);

        t = 0f;
        while (t < 1.2f)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            float p = t / 1.2f;
            float shimmer = 0.9f + Mathf.Sin(p * Mathf.PI * 4f) * 0.1f;
            frameImg.color = new Color(shimmer, shimmer, shimmer);
            yield return null;
        }

        if (rootGO == null) { _isShowing = false; yield break; }
        t = 0f;
        while (t < 0.4f)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.4f);
            containerRT.localScale = Vector3.one * (1f - p * p);
            dimImg.color = new Color(0, 0, 0, 0.35f * (1f - p));
            yield return null;
        }

        if (rootGO != null) Object.Destroy(rootGO);
        _isShowing = false;
    }

    // ── Helpers ──

    private static void SpawnSparkles(Transform parent, RectTransform center)
    {
        for (int i = 0; i < 10; i++)
        {
            var go = new GameObject("Sparkle");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(10, 10);
            rt.anchoredPosition = center.anchoredPosition;

            var img = go.AddComponent<Image>();
            var circle = Resources.Load<Sprite>("Circle");
            if (circle != null) img.sprite = circle;
            img.color = i % 3 == 0
                ? new Color(1f, 0.85f, 0.2f, 0.9f)  // gold
                : i % 3 == 1
                    ? new Color(1f, 1f, 1f, 0.8f)    // white
                    : new Color(0.9f, 0.5f, 1f, 0.7f); // purple
            img.raycastTarget = false;

            float angle = i * 36f * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            var mono = go.AddComponent<SparkleParticle>();
            mono.velocity = dir * Random.Range(250f, 450f);
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

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
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
