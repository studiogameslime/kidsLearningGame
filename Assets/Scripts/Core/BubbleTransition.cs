using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Full-screen bubble transition overlay. Bubbles fly inward to cover the screen,
/// the scene switches, then bubbles fly outward to reveal the new scene.
/// Uses the child's profile color. Singleton, DontDestroyOnLoad.
/// </summary>
public class BubbleTransition : MonoBehaviour
{
    public static BubbleTransition Instance { get; private set; }

    private const int BubbleCount = 60;
    private const float CoverDuration = 0.55f;
    private const float RevealDuration = 0.50f;
    private const float OvershootPause = 0.08f;

    private Canvas transitionCanvas;
    private RectTransform canvasRT;
    private List<BubbleData> bubbles = new List<BubbleData>();
    private bool isTransitioning;

    // Shared circle sprite for all bubbles
    private Sprite circleSprite;

    private class BubbleData
    {
        public GameObject go;
        public RectTransform rt;
        public Image img;
        public Image shine;
        public Vector2 offScreenPos;
        public Vector2 centerPos;
        public float delay;
        public float speed;     // individual speed multiplier
        public float wobblePhase;
        public float wobbleAmp;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("BubbleTransition");
        go.AddComponent<BubbleTransition>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateCanvas();
        CreateCircleSprite();
        CreateBubblePool();
        HideAllBubbles();
    }

    private void CreateCanvas()
    {
        var canvasGO = new GameObject("BubbleTransitionCanvas");
        canvasGO.transform.SetParent(transform, false);

        transitionCanvas = canvasGO.AddComponent<Canvas>();
        transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        transitionCanvas.sortingOrder = 999; // above everything

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRT = canvasGO.GetComponent<RectTransform>();

        // No GraphicRaycaster — bubbles should not block input
    }

    private void CreateCircleSprite()
    {
        // Generate a soft circle texture
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color32[size * size];
        float center = size * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((radius - dist) * 2f); // soft edge
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void CreateBubblePool()
    {
        for (int i = 0; i < BubbleCount; i++)
        {
            var go = new GameObject($"Bubble_{i}");
            go.transform.SetParent(transitionCanvas.transform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<Image>();
            img.sprite = circleSprite;
            img.raycastTarget = false;

            // Shine highlight (upper-left area of bubble)
            var shineGO = new GameObject("Shine");
            shineGO.transform.SetParent(go.transform, false);
            var shineRT = shineGO.AddComponent<RectTransform>();
            shineRT.anchorMin = new Vector2(0.15f, 0.55f);
            shineRT.anchorMax = new Vector2(0.50f, 0.88f);
            shineRT.offsetMin = Vector2.zero;
            shineRT.offsetMax = Vector2.zero;
            var shineImg = shineGO.AddComponent<Image>();
            shineImg.sprite = circleSprite;
            shineImg.raycastTarget = false;

            var bd = new BubbleData
            {
                go = go,
                rt = rt,
                img = img,
                shine = shineImg
            };
            bubbles.Add(bd);
        }
    }

    private void HideAllBubbles()
    {
        foreach (var b in bubbles)
            b.go.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────

    /// <summary>
    /// Play bubble transition and load the target scene.
    /// Call this instead of SceneManager.LoadScene().
    /// </summary>
    public static void LoadScene(string sceneName)
    {
        if (Instance == null || Instance.isTransitioning)
        {
            // Fallback: direct load
            SceneManager.LoadScene(sceneName);
            return;
        }
        Instance.StartCoroutine(Instance.TransitionCoroutine(sceneName));
    }

    /// <summary>Check if currently mid-transition.</summary>
    public static bool IsActive => Instance != null && Instance.isTransitioning;

    // ─────────────────────────────────────────────
    //  TRANSITION COROUTINE
    // ─────────────────────────────────────────────

    private IEnumerator TransitionCoroutine(string sceneName)
    {
        isTransitioning = true;

        // Get profile color
        Color baseColor = GetProfileColor();

        // Generate palette
        Color[] palette = GeneratePalette(baseColor);

        // Setup bubble positions & visuals
        SetupBubbles(palette);

        // Phase 1: Cover (bubbles fly inward)
        yield return AnimateBubbles(true, CoverDuration);

        // Brief pause at full coverage
        yield return new WaitForSeconds(OvershootPause);

        // Load scene
        var asyncOp = SceneManager.LoadSceneAsync(sceneName);
        while (asyncOp != null && !asyncOp.isDone)
            yield return null;

        // Small frame to let scene initialize
        yield return null;

        // Phase 2: Reveal (bubbles fly outward)
        yield return AnimateBubbles(false, RevealDuration);

        HideAllBubbles();
        isTransitioning = false;
    }

    // ─────────────────────────────────────────────
    //  BUBBLE SETUP
    // ─────────────────────────────────────────────

    private void SetupBubbles(Color[] palette)
    {
        float screenW = canvasRT.rect.width;
        float screenH = canvasRT.rect.height;
        if (screenW <= 0) screenW = 1920f;
        if (screenH <= 0) screenH = 1080f;

        float halfW = screenW * 0.5f;
        float halfH = screenH * 0.5f;
        float spawnMargin = 150f; // extra distance beyond screen edge

        for (int i = 0; i < bubbles.Count; i++)
        {
            var b = bubbles[i];
            b.go.SetActive(true);

            // Size distribution: 60% small, 30% medium, 10% large
            float sizeRoll = Random.value;
            float diameter;
            if (sizeRoll < 0.60f)
                diameter = Random.Range(60f, 110f);   // small
            else if (sizeRoll < 0.90f)
                diameter = Random.Range(120f, 190f);   // medium
            else
                diameter = Random.Range(200f, 300f);   // large

            b.rt.sizeDelta = new Vector2(diameter, diameter);

            // Color from palette
            Color bubbleColor = palette[Random.Range(0, palette.Length)];
            // Vary alpha slightly
            float alpha = Random.Range(0.55f, 0.85f);
            b.img.color = new Color(bubbleColor.r, bubbleColor.g, bubbleColor.b, alpha);

            // Shine
            b.shine.color = new Color(1f, 1f, 1f, Random.Range(0.2f, 0.45f));

            // Target: random position near center that ensures coverage
            // Use a grid-like distribution with jitter for even coverage
            float gridX = (i % 8) / 7f;   // 0..1
            float gridY = (i / 8) / 7f;   // 0..1 (roughly)
            float jitterX = Random.Range(-0.08f, 0.08f);
            float jitterY = Random.Range(-0.08f, 0.08f);
            float cx = Mathf.Lerp(-halfW * 0.85f, halfW * 0.85f, gridX + jitterX);
            float cy = Mathf.Lerp(-halfH * 0.85f, halfH * 0.85f, Mathf.Clamp01(gridY + jitterY));
            b.centerPos = new Vector2(cx, cy);

            // Spawn position: outside the screen from a random side
            float angle = Mathf.Atan2(cy, cx); // direction from center
            // Push outward from center along that angle, beyond the screen
            float outDist = Mathf.Sqrt(halfW * halfW + halfH * halfH) + spawnMargin + diameter;
            // Add some randomness to the angle so they don't all converge perfectly
            angle += Random.Range(-0.3f, 0.3f);
            b.offScreenPos = new Vector2(
                Mathf.Cos(angle) * outDist,
                Mathf.Sin(angle) * outDist
            );

            // Timing variation
            b.delay = Random.Range(0f, 0.15f);
            b.speed = Random.Range(0.85f, 1.15f);
            b.wobblePhase = Random.Range(0f, Mathf.PI * 2f);
            b.wobbleAmp = Random.Range(8f, 25f);
        }
    }

    // ─────────────────────────────────────────────
    //  ANIMATION
    // ─────────────────────────────────────────────

    private IEnumerator AnimateBubbles(bool inward, float duration)
    {
        float elapsed = 0f;
        float totalDuration = duration + 0.15f; // account for max delay

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            foreach (var b in bubbles)
            {
                float adjustedTime = (elapsed - b.delay) * b.speed;
                float t = Mathf.Clamp01(adjustedTime / duration);

                // Ease: smooth step for inward, slight overshoot for outward
                float eased;
                if (inward)
                    eased = t * t * (3f - 2f * t); // smooth step
                else
                    eased = 1f - Mathf.Pow(1f - t, 2.5f); // ease out

                Vector2 from, to;
                if (inward)
                {
                    from = b.offScreenPos;
                    to = b.centerPos;
                }
                else
                {
                    from = b.centerPos;
                    to = b.offScreenPos;
                }

                Vector2 pos = Vector2.Lerp(from, to, eased);

                // Add wobble perpendicular to movement direction
                if (t > 0f && t < 1f)
                {
                    Vector2 dir = (to - from).normalized;
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    float wobble = Mathf.Sin(t * Mathf.PI * 3f + b.wobblePhase) * b.wobbleAmp * (1f - t);
                    pos += perp * wobble;
                }

                b.rt.anchoredPosition = pos;

                // Subtle scale pulse
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.08f;
                b.rt.localScale = Vector3.one * scale;
            }

            yield return null;
        }

        // Ensure final positions
        foreach (var b in bubbles)
        {
            b.rt.anchoredPosition = inward ? b.centerPos : b.offScreenPos;
            b.rt.localScale = Vector3.one;
        }
    }

    // ─────────────────────────────────────────────
    //  COLOR
    // ─────────────────────────────────────────────

    private Color GetProfileColor()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
            return profile.AvatarColor;

        // Fallback soft blue
        ColorUtility.TryParseHtmlString("#90CAF9", out Color c);
        return c;
    }

    private Color[] GeneratePalette(Color baseColor)
    {
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);

        return new Color[]
        {
            baseColor,                                                          // base
            Color.HSVToRGB(h, Mathf.Max(s - 0.15f, 0f), Mathf.Min(v + 0.15f, 1f)), // lighter
            Color.HSVToRGB(h, Mathf.Min(s + 0.10f, 1f), Mathf.Max(v - 0.12f, 0f)), // darker
            Color.HSVToRGB(h, Mathf.Max(s - 0.30f, 0f), Mathf.Min(v + 0.25f, 1f)), // very pale
            Color.HSVToRGB(h, s, v),                                             // base again (weighted)
        };
    }
}
