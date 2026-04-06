using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Sand Drawing sandbox controller — kids draw in sand with their finger.
/// Procedurally generates all textures at runtime (no external art needed).
/// Reveals a colorful rainbow layer beneath golden sand with sparkle particles.
/// </summary>
public class SandDrawingController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("UI References")]
    public RawImage sandDisplay;
    public Button backButton;
    public Button resetButton;

    [Header("Brush Settings")]
    public int brushRadius = 36;
    [Range(0f, 1f)]
    public float brushSoftness = 0.5f;
    public int scatterCount = 5;
    public float scatterRadius = 2f;
    public int scatterDotRadius = 2;

    // Texture dimensions
    private const int TexWidth = 1024;
    private const int TexHeight = 576;

    // Runtime textures
    private Texture2D maskTex;
    private Texture2D topTex;
    private Texture2D bottomTex;
    private Texture2D grainTex;
    private Material sandMat;

    // Mask pixel data — RGBA32: R=hue (0-255), G=drawn amount (0-255), B=unused, A=255
    private byte[] maskPixels;
    private bool maskDirty;

    // Rainbow hue that continuously advances while drawing
    private float currentHue; // 0-1, wraps around
    private float hueSpeed = 0.15f; // how fast hue changes per pixel of movement

    // Input state
    private RectTransform displayRT;
    private Vector2? lastDrawPos;
    private bool isDrawing;
    private int activePointerId = -1;

    // Reset shake animation
    private bool isShaking;
    private float shakeTimer;
    private Vector2 shakeBasePos;

    // Sparkle particles
    private RectTransform sparkleContainer;
    private Sprite circleSprite;
    private int sparkleCounter;
    private static readonly Color[] SparkleColors = {
        new Color(1f, 0.85f, 0.2f),    // gold
        new Color(1f, 0.95f, 0.5f),    // light gold
        new Color(1f, 1f, 0.8f),        // cream
        new Color(0.95f, 0.7f, 0.9f),  // pink
        new Color(0.7f, 0.9f, 1f),      // sky blue
        new Color(0.8f, 1f, 0.7f),      // mint
    };

    private void Start()
    {
        displayRT = sandDisplay.GetComponent<RectTransform>();

        GenerateTextures();
        CreateMaterial();

        // Initialize mask to all sand (1.0 = surface visible)
        maskTex = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false);
        maskTex.filterMode = FilterMode.Bilinear;
        maskTex.wrapMode = TextureWrapMode.Clamp;
        maskPixels = new byte[TexWidth * TexHeight * 4]; // RGBA32 = 4 bytes per pixel
        FillMask(255);
        ApplyMask();

        sandMat.SetTexture("_MaskTex", maskTex);
        sandDisplay.material = sandMat;
        sandDisplay.texture = maskTex; // RawImage needs a texture; shader uses _MaskTex
        sandDisplay.color = Color.white;

        // Wire buttons
        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);
        if (resetButton != null)
            resetButton.onClick.AddListener(ResetSand);

        // Disable finger trail in this scene (we draw directly on sand)
        FingerTrail.SetEnabled(false);

        // Create sparkle container (child of sand display, renders on top)
        var sparkleGO = new GameObject("Sparkles");
        sparkleGO.transform.SetParent(sandDisplay.transform, false);
        sparkleContainer = sparkleGO.AddComponent<RectTransform>();
        sparkleContainer.anchorMin = Vector2.zero;
        sparkleContainer.anchorMax = Vector2.one;
        sparkleContainer.offsetMin = Vector2.zero;
        sparkleContainer.offsetMax = Vector2.zero;

        // Create a simple circle sprite for sparkles
        circleSprite = CreateCircleSprite(16);
    }

    private void OnDestroy()
    {
        if (maskTex != null) Destroy(maskTex);
        if (topTex != null) Destroy(topTex);
        if (bottomTex != null) Destroy(bottomTex);
        if (grainTex != null) Destroy(grainTex);
        if (sandMat != null) Destroy(sandMat);
    }

    // ── Texture Generation ──

    private void GenerateTextures()
    {
        // Top sand surface: warm golden beige with fine grain variation
        topTex = new Texture2D(256, 256, TextureFormat.RGB24, false);
        topTex.filterMode = FilterMode.Bilinear;
        topTex.wrapMode = TextureWrapMode.Repeat;
        var topPixels = new Color[256 * 256];
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                float n1 = Mathf.PerlinNoise(x * 0.04f, y * 0.04f);
                float n2 = Mathf.PerlinNoise(x * 0.1f + 100f, y * 0.1f + 100f) * 0.25f;
                float n3 = Mathf.PerlinNoise(x * 0.3f + 50f, y * 0.3f + 50f) * 0.08f; // fine detail
                float n = n1 + n2 + n3;
                // Warm golden sand: base ~(0.90, 0.80, 0.58)
                float r = 0.87f + n * 0.06f;
                float g = 0.78f + n * 0.05f;
                float b = 0.56f + n * 0.04f;
                topPixels[y * 256 + x] = new Color(r, g, b);
            }
        }
        topTex.SetPixels(topPixels);
        topTex.Apply();

        // Bottom (revealed layer): colorful rainbow swirl — hidden treasure under the sand!
        bottomTex = new Texture2D(512, 288, TextureFormat.RGB24, false); // match aspect ratio
        bottomTex.filterMode = FilterMode.Bilinear;
        bottomTex.wrapMode = TextureWrapMode.Clamp; // no tiling — full image
        var bottomPixels = new Color[512 * 288];
        for (int y = 0; y < 288; y++)
        {
            for (int x = 0; x < 512; x++)
            {
                // Rainbow swirl: hue rotates with position + Perlin distortion
                float nx = (float)x / 512f;
                float ny = (float)y / 288f;
                float swirl = Mathf.PerlinNoise(nx * 3f + 10f, ny * 3f + 10f) * 0.3f;
                float hue = (nx * 0.5f + ny * 0.3f + swirl) % 1f;
                // Add pastel softness
                float sat = 0.55f + Mathf.PerlinNoise(nx * 4f, ny * 4f) * 0.2f;
                float val = 0.85f + Mathf.PerlinNoise(nx * 2f + 100f, ny * 2f + 100f) * 0.1f;
                bottomPixels[y * 512 + x] = Color.HSVToRGB(hue, sat, val);
            }
        }
        bottomTex.SetPixels(bottomPixels);
        bottomTex.Apply();

        // Grain texture: multi-octave fine noise for sand grain look
        grainTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        grainTex.filterMode = FilterMode.Point; // sharp grains, not blurred
        grainTex.wrapMode = TextureWrapMode.Repeat;
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                float n1 = Mathf.PerlinNoise(x * 0.5f + 300f, y * 0.5f + 300f);
                float n2 = Mathf.PerlinNoise(x * 1.2f + 500f, y * 1.2f + 500f) * 0.4f;
                float n = (n1 + n2) * 0.7f;
                grainTex.SetPixel(x, y, new Color(n, n, n));
            }
        }
        grainTex.Apply();
    }

    private void CreateMaterial()
    {
        var shader = Shader.Find("UI/SandSurface");
        if (shader == null)
        {
            Debug.LogError("[SandDrawing] UI/SandSurface shader not found!");
            return;
        }

        sandMat = new Material(shader);
        sandMat.SetTexture("_TopTex", topTex);
        sandMat.SetTexture("_GrainTex", grainTex);
        sandMat.SetFloat("_TopTiling", 4f);
        sandMat.SetFloat("_EdgeWidth", 0.04f);
        sandMat.SetFloat("_EdgeBrightness", 1.8f);
        sandMat.SetFloat("_GrainStrength", 0.08f);
        sandMat.SetFloat("_Saturation", 0.7f);
        sandMat.SetFloat("_Brightness", 0.95f);
    }

    // ── Mask Operations ──

    private void FillMask(byte value)
    {
        for (int i = 0; i < maskPixels.Length; i += 4)
        {
            maskPixels[i]     = 0;     // R = hue (irrelevant when not drawn)
            maskPixels[i + 1] = 0;     // G = drawn amount (0 = sand visible)
            maskPixels[i + 2] = 0;     // B = unused
            maskPixels[i + 3] = 255;   // A
        }
    }

    private void ApplyMask()
    {
        maskTex.LoadRawTextureData(maskPixels);
        maskTex.Apply();
        maskDirty = false;
    }

    // Sand refill rate: how fast cleared sand fills back (0-255 units per second)
    private const float RefillRate = 30f; // ~8.5 seconds to fully refill
    private float refillAccumulator;

    private void LateUpdate()
    {
        // Gradually refill sand — cleared areas slowly cover back up
        refillAccumulator += RefillRate * Time.deltaTime;
        if (refillAccumulator >= 1f)
        {
            int step = (int)refillAccumulator;
            refillAccumulator -= step;
            bool anyChanged = false;

            for (int i = 0; i < maskPixels.Length; i += 4)
            {
                byte drawn = maskPixels[i + 1]; // G = drawn amount
                if (drawn > 0)
                {
                    int newVal = Mathf.Max(0, drawn - step);
                    maskPixels[i + 1] = (byte)newVal; // fade drawn back to 0 (sand returns)
                    anyChanged = true;
                }
            }
            if (anyChanged) maskDirty = true;
        }

        if (maskDirty)
            ApplyMask();

        // Screen shake animation for reset
        if (isShaking)
        {
            shakeTimer -= Time.deltaTime;
            if (shakeTimer <= 0f)
            {
                isShaking = false;
                displayRT.anchoredPosition = shakeBasePos;
            }
            else
            {
                float intensity = shakeTimer * 18f;
                float offsetX = Mathf.Sin(shakeTimer * 55f) * intensity;
                float offsetY = Mathf.Cos(shakeTimer * 45f) * intensity * 0.6f;
                displayRT.anchoredPosition = shakeBasePos + new Vector2(offsetX, offsetY);
            }
        }
    }

    // ── Drawing ──

    /// <summary>
    /// Paint a soft-falloff circle into the mask at the given texture coordinate.
    /// Paints rainbow into the mask. R=hue, G=drawn amount (with soft falloff).
    /// </summary>
    private void PaintMaskCircle(Vector2 center)
    {
        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        int r = brushRadius;
        float rSq = r * r;
        float innerR = r * (1f - brushSoftness);
        float innerRSq = innerR * innerR;
        byte hueByte = (byte)(Mathf.Repeat(currentHue, 1f) * 255f);

        for (int dy = -r; dy <= r; dy++)
        {
            int py = cy + dy;
            if (py < 0 || py >= TexHeight) continue;

            for (int dx = -r; dx <= r; dx++)
            {
                int px = cx + dx;
                if (px < 0 || px >= TexWidth) continue;

                float distSq = dx * dx + dy * dy;
                if (distSq > rSq) continue;

                int idx = (py * TexWidth + px) * 4; // RGBA32

                if (distSq <= innerRSq)
                {
                    // Full draw: set hue and drawn=255
                    maskPixels[idx] = hueByte;       // R = hue
                    maskPixels[idx + 1] = 255;       // G = fully drawn
                }
                else
                {
                    // Soft edge: partial draw
                    float dist = Mathf.Sqrt(distSq);
                    float t = 1f - (dist - innerR) / (r - innerR);
                    t = t * t;
                    byte drawnTarget = (byte)(t * 255f);
                    byte currentDrawn = maskPixels[idx + 1];
                    if (drawnTarget > currentDrawn)
                    {
                        maskPixels[idx] = hueByte;           // R = hue
                        maskPixels[idx + 1] = drawnTarget;   // G = drawn amount
                    }
                }
            }
        }

        maskDirty = true;
    }

    /// <summary>
    /// Scatter tiny dots around the brush position to simulate sand grains
    /// flying off from the groove edges. Critical for realistic sand feel.
    /// </summary>
    private void ScatterGrains(Vector2 center)
    {
        for (int i = 0; i < scatterCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = brushRadius + Random.Range(2f, brushRadius * scatterRadius);
            float sx = center.x + Mathf.Cos(angle) * dist;
            float sy = center.y + Mathf.Sin(angle) * dist;

            int dotR = Random.Range(1, scatterDotRadius + 1);

            int cx = Mathf.RoundToInt(sx);
            int cy = Mathf.RoundToInt(sy);

            for (int dy = -dotR; dy <= dotR; dy++)
            {
                int py = cy + dy;
                if (py < 0 || py >= TexHeight) continue;
                for (int dx = -dotR; dx <= dotR; dx++)
                {
                    int px = cx + dx;
                    if (px < 0 || px >= TexWidth) continue;
                    if (dx * dx + dy * dy > dotR * dotR) continue;

                    int idx = (py * TexWidth + px) * 4; // RGBA32
                    byte currentDrawn = maskPixels[idx + 1];
                    byte target = (byte)Random.Range(80, 180);
                    if (target > currentDrawn)
                    {
                        byte hueByte = (byte)(Mathf.Repeat(currentHue + Random.Range(-0.05f, 0.05f), 1f) * 255f);
                        maskPixels[idx] = hueByte;       // R = hue
                        maskPixels[idx + 1] = target;    // G = drawn
                    }
                }
            }
        }
    }

    /// <summary>
    /// Interpolate brush circles between two positions for smooth continuous strokes.
    /// Same pattern as DrawingCanvas.DrawLine().
    /// </summary>
    private void DrawLine(Vector2 from, Vector2 to)
    {
        float dist = Vector2.Distance(from, to);
        float step = Mathf.Max(1f, brushRadius * 0.3f);
        int steps = Mathf.CeilToInt(dist / step);

        // Advance hue based on distance traveled
        float hueAdvance = dist * hueSpeed / TexWidth;

        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0 : (float)i / steps;
            Vector2 pos = Vector2.Lerp(from, to, t);
            currentHue += hueAdvance / Mathf.Max(1, steps);
            PaintMaskCircle(pos);
        }

        ScatterGrains(to);
    }

    // ── Input Handling ──

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isDrawing) return;
        activePointerId = eventData.pointerId;

        Vector2 texPos;
        if (!ScreenToTexturePos(eventData.position, out texPos)) return;

        isDrawing = true;
        lastDrawPos = null;

        PaintMaskCircle(texPos);
        ScatterGrains(texPos);
        lastDrawPos = texPos;
        maskDirty = true;
        SpawnSparkles(eventData.position, 2);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDrawing) return;
        if (eventData.pointerId != activePointerId) return;

        Vector2 texPos;
        if (!ScreenToTexturePos(eventData.position, out texPos)) return;

        if (lastDrawPos.HasValue)
            DrawLine(lastDrawPos.Value, texPos);
        else
        {
            PaintMaskCircle(texPos);
            ScatterGrains(texPos);
        }

        lastDrawPos = texPos;
        maskDirty = true;
        SpawnSparkles(eventData.position, 2);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;

        isDrawing = false;
        lastDrawPos = null;
        activePointerId = -1;
    }

    // ── Coordinate Conversion ──

    private bool ScreenToTexturePos(Vector2 screenPos, out Vector2 texturePos)
    {
        texturePos = Vector2.zero;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            displayRT, screenPos, null, out Vector2 localPoint))
            return false;

        Rect rect = displayRT.rect;
        float normalizedX = (localPoint.x - rect.x) / rect.width;
        float normalizedY = (localPoint.y - rect.y) / rect.height;

        if (normalizedX < 0 || normalizedX > 1 || normalizedY < 0 || normalizedY > 1)
            return false;

        texturePos = new Vector2(normalizedX * TexWidth, normalizedY * TexHeight);
        return true;
    }

    // ── Actions ──

    /// <summary>
    /// Reset all sand — fill mask back to 1.0 (surface) and play screen shake.
    /// </summary>
    public void ResetSand()
    {
        FillMask(255);
        maskDirty = true;

        // Clear all sparkles
        if (sparkleContainer != null)
            for (int i = sparkleContainer.childCount - 1; i >= 0; i--)
                Destroy(sparkleContainer.GetChild(i).gameObject);

        // Start shake animation
        isShaking = true;
        shakeTimer = 0.35f;
        shakeBasePos = displayRT.anchoredPosition;
    }

    private void OnBackPressed()
    {
        FingerTrail.SetEnabled(true);
        NavigationManager.GoToWorld();
    }

    // ── Sparkle Particles ──

    /// <summary>Spawn sparkle particles at a screen position while drawing.</summary>
    private void SpawnSparkles(Vector2 screenPos, int count = 3)
    {
        if (sparkleContainer == null || circleSprite == null) return;

        for (int i = 0; i < count; i++)
        {
            sparkleCounter++;
            var go = new GameObject($"Sparkle_{sparkleCounter}");
            go.transform.SetParent(sparkleContainer, false);
            var rt = go.AddComponent<RectTransform>();

            // Convert screen pos to local pos in sparkle container
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                sparkleContainer, screenPos, null, out localPos);

            rt.anchoredPosition = localPos + new Vector2(
                Random.Range(-brushRadius * 1.5f, brushRadius * 1.5f),
                Random.Range(-brushRadius * 1.5f, brushRadius * 1.5f));
            float size = Random.Range(8f, 22f);
            rt.sizeDelta = new Vector2(size, size);

            var img = go.AddComponent<Image>();
            img.sprite = circleSprite;
            img.color = SparkleColors[Random.Range(0, SparkleColors.Length)];
            img.raycastTarget = false;

            StartCoroutine(AnimateSparkle(rt, img));
        }
    }

    private IEnumerator AnimateSparkle(RectTransform rt, Image img)
    {
        if (rt == null) yield break;

        Vector2 startPos = rt.anchoredPosition;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float speed = Random.Range(60f, 150f);
        Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        float spinSpeed = Random.Range(-360f, 360f);
        float lifetime = Random.Range(0.4f, 0.8f);
        float elapsed = 0f;
        Color startColor = img.color;
        float startScale = rt.localScale.x;

        // Start with a quick pop-in
        rt.localScale = Vector3.zero;
        float popDur = 0.08f;
        float popT = 0f;
        while (popT < popDur)
        {
            popT += Time.deltaTime;
            float s = Mathf.Lerp(0f, 1.2f, popT / popDur);
            if (rt == null) yield break;
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;

        // Float upward and fade
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            if (rt == null) yield break;

            float progress = elapsed / lifetime;
            velocity.y += 80f * Time.deltaTime; // float upward
            rt.anchoredPosition += velocity * Time.deltaTime;
            rt.Rotate(0, 0, spinSpeed * Time.deltaTime);

            // Fade and shrink
            float fade = 1f - progress * progress;
            img.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * fade);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, progress);

            yield return null;
        }

        if (rt != null) Destroy(rt.gameObject);
    }

    private Sprite CreateCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = center - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float a = Mathf.Clamp01((radius - dist) / 1.5f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
