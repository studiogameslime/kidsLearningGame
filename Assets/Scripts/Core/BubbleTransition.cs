using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Full-screen bubble transition overlay. Bubbles fly inward to cover the screen,
/// a solid backdrop guarantees no scene bleed-through during the swap,
/// then bubbles fly outward to reveal the new scene.
/// Uses the child's profile color. Singleton, DontDestroyOnLoad.
/// </summary>
public class BubbleTransition : MonoBehaviour
{
    public static BubbleTransition Instance { get; private set; }

    private const int BubbleCount = 150;
    private const float CoverDuration = 0.55f;
    private const float RevealDuration = 0.55f;
    private const float OvershootPause = 0.08f;
    private const int GridCols = 12;
    private const int GridRows = 14;

    private Canvas transitionCanvas;
    private RectTransform canvasRT;
    private Image backdropImage;          // solid full-screen color behind bubbles
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
        CreateBackdrop();
        CreateBubblePool();
        HideAll();
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
        // Generate a hard circle texture (opaque interior, sharp edge)
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
                // Sharper edge — 3px antialiasing band
                float alpha = Mathf.Clamp01((radius - dist) * 1.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void CreateBackdrop()
    {
        // Full-screen solid color image behind all bubbles — guarantees no scene bleed-through
        var bdGO = new GameObject("Backdrop");
        bdGO.transform.SetParent(transitionCanvas.transform, false);
        bdGO.transform.SetAsFirstSibling(); // behind bubbles

        var bdRT = bdGO.AddComponent<RectTransform>();
        bdRT.anchorMin = Vector2.zero;
        bdRT.anchorMax = Vector2.one;
        bdRT.offsetMin = Vector2.zero;
        bdRT.offsetMax = Vector2.zero;

        backdropImage = bdGO.AddComponent<Image>();
        backdropImage.raycastTarget = false;
        backdropImage.color = new Color(0, 0, 0, 0); // start invisible
        bdGO.SetActive(false);
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

    private void HideAll()
    {
        foreach (var b in bubbles)
            b.go.SetActive(false);
        if (backdropImage != null)
            backdropImage.gameObject.SetActive(false);
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

        // Show backdrop (starts transparent, will fade in during cover)
        backdropImage.gameObject.SetActive(true);
        Color backdropColor = palette[0];
        backdropImage.color = new Color(backdropColor.r, backdropColor.g, backdropColor.b, 0f);

        // Phase 1: Cover (bubbles fly inward + backdrop fades in)
        yield return AnimateCover(CoverDuration);

        // Ensure full coverage — backdrop opaque, bubbles at center
        backdropImage.color = new Color(backdropColor.r, backdropColor.g, backdropColor.b, 1f);
        foreach (var b in bubbles)
        {
            b.rt.anchoredPosition = b.centerPos;
            b.rt.localScale = Vector3.one;
        }

        // Brief pause at full coverage
        yield return new WaitForSeconds(OvershootPause);

        // Load scene — completely hidden behind opaque backdrop + bubbles
        var asyncOp = SceneManager.LoadSceneAsync(sceneName);
        while (asyncOp != null && !asyncOp.isDone)
            yield return null;

        // Let new scene initialize
        yield return null;
        yield return null;

        // Phase 2: Reveal — exact mirror of cover (bubbles separate outward from packed state)
        yield return AnimateReveal(RevealDuration);

        HideAll();
        isTransitioning = false;
    }

    // ─────────────────────────────────────────────
    //  BUBBLE SETUP
    // ─────────────────────────────────────────────

    private void SetupBubbles(Color[] palette)
    {
        float screenW = canvasRT.rect.width;
        float screenH = canvasRT.rect.height;
        if (screenW <= 0) screenW = 1080f;
        if (screenH <= 0) screenH = 1920f;

        float halfW = screenW * 0.5f;
        float halfH = screenH * 0.5f;
        float spawnMargin = 200f;

        // Grid-based placement for first GridCols*GridRows bubbles, remainder random
        int gridCount = Mathf.Min(GridCols * GridRows, bubbles.Count);

        for (int i = 0; i < bubbles.Count; i++)
        {
            var b = bubbles[i];
            b.go.SetActive(true);

            // Size distribution: bigger bubbles for better coverage
            float sizeRoll = Random.value;
            float diameter;
            if (sizeRoll < 0.35f)
                diameter = Random.Range(100f, 150f);   // small
            else if (sizeRoll < 0.75f)
                diameter = Random.Range(160f, 240f);    // medium
            else
                diameter = Random.Range(250f, 380f);    // large

            b.rt.sizeDelta = new Vector2(diameter, diameter);

            // Color from palette — fully opaque bubbles
            Color bubbleColor = palette[Random.Range(0, palette.Length)];
            float alpha = Random.Range(0.85f, 1.0f);
            b.img.color = new Color(bubbleColor.r, bubbleColor.g, bubbleColor.b, alpha);

            // Shine
            b.shine.color = new Color(1f, 1f, 1f, Random.Range(0.15f, 0.35f));

            // Target position: grid-based for coverage, with jitter
            float cx, cy;
            if (i < gridCount)
            {
                int col = i % GridCols;
                int row = i / GridCols;
                // Map to full screen with overlap at edges
                float normX = (col + 0.5f) / GridCols;
                float normY = (row + 0.5f) / GridRows;
                float jitterX = Random.Range(-0.5f / GridCols, 0.5f / GridCols);
                float jitterY = Random.Range(-0.5f / GridRows, 0.5f / GridRows);
                cx = Mathf.Lerp(-halfW * 1.05f, halfW * 1.05f, normX + jitterX);
                cy = Mathf.Lerp(-halfH * 1.05f, halfH * 1.05f, normY + jitterY);
            }
            else
            {
                // Extra bubbles: random positions for additional coverage
                cx = Random.Range(-halfW * 1.0f, halfW * 1.0f);
                cy = Random.Range(-halfH * 1.0f, halfH * 1.0f);
            }
            b.centerPos = new Vector2(cx, cy);

            // Spawn position: outside the screen
            float angle = Mathf.Atan2(cy, cx);
            float outDist = Mathf.Sqrt(halfW * halfW + halfH * halfH) + spawnMargin + diameter;
            angle += Random.Range(-0.3f, 0.3f);
            b.offScreenPos = new Vector2(
                Mathf.Cos(angle) * outDist,
                Mathf.Sin(angle) * outDist
            );

            // Timing variation
            b.delay = Random.Range(0f, 0.12f);
            b.speed = Random.Range(0.9f, 1.1f);
            b.wobblePhase = Random.Range(0f, Mathf.PI * 2f);
            b.wobbleAmp = Random.Range(10f, 30f);
        }
    }

    // ─────────────────────────────────────────────
    //  ANIMATION
    // ─────────────────────────────────────────────

    /// <summary>Cover phase: bubbles fly inward from off-screen to packed center positions.</summary>
    private IEnumerator AnimateCover(float duration)
    {
        float elapsed = 0f;
        float totalDuration = duration + 0.15f; // account for max delay

        Color bdColor = backdropImage.color;

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float overallT = Mathf.Clamp01(elapsed / duration);

            // Fade backdrop in: transparent → opaque over t=0.3..0.8
            float bdAlpha = Mathf.Clamp01((overallT - 0.3f) / 0.5f);
            backdropImage.color = new Color(bdColor.r, bdColor.g, bdColor.b, bdAlpha);

            foreach (var b in bubbles)
            {
                float adjustedTime = (elapsed - b.delay) * b.speed;
                float t = Mathf.Clamp01(adjustedTime / duration);

                // Smooth step easing
                float eased = t * t * (3f - 2f * t);

                Vector2 pos = Vector2.Lerp(b.offScreenPos, b.centerPos, eased);

                // Wobble perpendicular to movement
                if (t > 0f && t < 1f)
                {
                    Vector2 dir = (b.centerPos - b.offScreenPos).normalized;
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    float wobble = Mathf.Sin(t * Mathf.PI * 3f + b.wobblePhase) * b.wobbleAmp * (1f - t);
                    pos += perp * wobble;
                }

                b.rt.anchoredPosition = pos;

                // Subtle scale pulse during movement
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.08f;
                b.rt.localScale = Vector3.one * scale;
            }

            yield return null;
        }

        // Snap to final covered state
        foreach (var b in bubbles)
        {
            b.rt.anchoredPosition = b.centerPos;
            b.rt.localScale = Vector3.one;
        }
        backdropImage.color = new Color(bdColor.r, bdColor.g, bdColor.b, 1f);
    }

    /// <summary>
    /// Reveal phase: exact mirror of cover. Bubbles start packed together
    /// then smoothly separate outward — same smooth-step easing reversed,
    /// same wobble, with a small pop-scale at the beginning.
    /// </summary>
    private IEnumerator AnimateReveal(float duration)
    {
        float elapsed = 0f;
        float totalDuration = duration + 0.15f;

        Color bdColor = backdropImage.color;

        // Ensure all bubbles start from their packed center positions
        foreach (var b in bubbles)
        {
            b.rt.anchoredPosition = b.centerPos;
            b.rt.localScale = Vector3.one;
        }

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float overallT = Mathf.Clamp01(elapsed / duration);

            // Fade backdrop out: opaque → transparent over t=0.2..0.8
            // Backdrop stays opaque initially while bubbles begin separating,
            // then fades as gaps appear
            float bdAlpha = 1f - Mathf.Clamp01((overallT - 0.2f) / 0.6f);
            backdropImage.color = new Color(bdColor.r, bdColor.g, bdColor.b, bdAlpha);

            foreach (var b in bubbles)
            {
                float adjustedTime = (elapsed - b.delay) * b.speed;
                float t = Mathf.Clamp01(adjustedTime / duration);

                // Mirror of cover easing: reverse smooth step
                // At t=0 bubble is at center, at t=1 bubble is off-screen
                // Use (1 - smoothstep(1-t)) to mirror the cover's deceleration into acceleration
                float reverseT = 1f - t;
                float eased = 1f - (reverseT * reverseT * (3f - 2f * reverseT));

                Vector2 pos = Vector2.Lerp(b.centerPos, b.offScreenPos, eased);

                // Same wobble as cover but mirrored — strongest at start, fading out
                if (t > 0f && t < 1f)
                {
                    Vector2 dir = (b.offScreenPos - b.centerPos).normalized;
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    float wobble = Mathf.Sin((1f - t) * Mathf.PI * 3f + b.wobblePhase) * b.wobbleAmp * t;
                    pos += perp * wobble;
                }

                b.rt.anchoredPosition = pos;

                // Pop scale: small burst at start of reveal, then settles
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.10f;
                b.rt.localScale = Vector3.one * scale;
            }

            yield return null;
        }

        // Snap to final off-screen state
        foreach (var b in bubbles)
        {
            b.rt.anchoredPosition = b.offScreenPos;
            b.rt.localScale = Vector3.one;
        }
        backdropImage.color = new Color(bdColor.r, bdColor.g, bdColor.b, 0f);
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
            baseColor,                                                              // base
            Color.HSVToRGB(h, Mathf.Max(s - 0.15f, 0f), Mathf.Min(v + 0.15f, 1f)), // lighter
            Color.HSVToRGB(h, Mathf.Min(s + 0.10f, 1f), Mathf.Max(v - 0.12f, 0f)), // darker
            Color.HSVToRGB(h, Mathf.Max(s - 0.30f, 0f), Mathf.Min(v + 0.25f, 1f)), // very pale
            Color.HSVToRGB(h, s, v),                                                // base again (weighted)
        };
    }
}
