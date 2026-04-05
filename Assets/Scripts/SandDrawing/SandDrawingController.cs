using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Sand Drawing sandbox controller — kids draw in sand with their finger.
/// Procedurally generates all textures at runtime (no external art needed).
/// Uses a mask texture to reveal a dark "wet sand" layer beneath light surface sand.
/// </summary>
public class SandDrawingController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("UI References")]
    public RawImage sandDisplay;
    public Button backButton;
    public Button resetButton;

    [Header("Brush Settings")]
    public int brushRadius = 28;
    [Range(0f, 1f)]
    public float brushSoftness = 0.45f;
    public int scatterCount = 4;
    public float scatterRadius = 2.5f;
    public int scatterDotRadius = 3;

    // Texture dimensions
    private const int TexWidth = 1024;
    private const int TexHeight = 576;

    // Runtime textures
    private Texture2D maskTex;
    private Texture2D topTex;
    private Texture2D bottomTex;
    private Texture2D grainTex;
    private Material sandMat;

    // Mask pixel data (R8 — single channel)
    private byte[] maskPixels;
    private bool maskDirty;

    // Input state
    private RectTransform displayRT;
    private Vector2? lastDrawPos;
    private bool isDrawing;
    private int activePointerId = -1;

    // Reset shake animation
    private bool isShaking;
    private float shakeTimer;
    private Vector2 shakeBasePos;

    private void Start()
    {
        displayRT = sandDisplay.GetComponent<RectTransform>();

        GenerateTextures();
        CreateMaterial();

        // Initialize mask to all sand (1.0 = surface visible)
        maskTex = new Texture2D(TexWidth, TexHeight, TextureFormat.R8, false);
        maskTex.filterMode = FilterMode.Bilinear;
        maskTex.wrapMode = TextureWrapMode.Clamp;
        maskPixels = new byte[TexWidth * TexHeight];
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
        // Top sand: warm beige with Perlin noise variation
        topTex = new Texture2D(256, 256, TextureFormat.RGB24, false);
        topTex.filterMode = FilterMode.Bilinear;
        topTex.wrapMode = TextureWrapMode.Repeat;
        var topPixels = new Color[256 * 256];
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                float n1 = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
                float n2 = Mathf.PerlinNoise(x * 0.12f + 100f, y * 0.12f + 100f) * 0.3f;
                float n = n1 + n2;
                // Warm beige: base ~(0.86, 0.76, 0.58) with noise variation
                float r = 0.82f + n * 0.08f;
                float g = 0.72f + n * 0.07f;
                float b = 0.54f + n * 0.06f;
                topPixels[y * 256 + x] = new Color(r, g, b);
            }
        }
        topTex.SetPixels(topPixels);
        topTex.Apply();

        // Bottom sand: dark wet brown
        bottomTex = new Texture2D(256, 256, TextureFormat.RGB24, false);
        bottomTex.filterMode = FilterMode.Bilinear;
        bottomTex.wrapMode = TextureWrapMode.Repeat;
        var bottomPixels = new Color[256 * 256];
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                float n1 = Mathf.PerlinNoise(x * 0.04f + 50f, y * 0.04f + 50f);
                float n2 = Mathf.PerlinNoise(x * 0.15f + 200f, y * 0.15f + 200f) * 0.2f;
                float n = n1 + n2;
                // Dark wet sand: base ~(0.48, 0.38, 0.24)
                float r = 0.44f + n * 0.08f;
                float g = 0.34f + n * 0.07f;
                float b = 0.20f + n * 0.06f;
                bottomPixels[y * 256 + x] = new Color(r, g, b);
            }
        }
        bottomTex.SetPixels(bottomPixels);
        bottomTex.Apply();

        // Grain texture: high-frequency Perlin noise
        grainTex = new Texture2D(256, 256, TextureFormat.R8, false);
        grainTex.filterMode = FilterMode.Bilinear;
        grainTex.wrapMode = TextureWrapMode.Repeat;
        var grainBytes = new byte[256 * 256];
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.25f + 300f, y * 0.25f + 300f);
                grainBytes[y * 256 + x] = (byte)(n * 255);
            }
        }
        grainTex.SetRawTextureData(grainBytes);
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
        sandMat.SetTexture("_BottomTex", bottomTex);
        sandMat.SetTexture("_GrainTex", grainTex);
        sandMat.SetFloat("_TopTiling", 3f);
        sandMat.SetFloat("_BottomTiling", 2.5f);
        sandMat.SetFloat("_EdgeWidth", 0.06f);
        sandMat.SetFloat("_EdgeBrightness", 1.2f);
        sandMat.SetFloat("_GrainStrength", 0.12f);
        sandMat.SetFloat("_GrooveDepth", 0.3f);
    }

    // ── Mask Operations ──

    private void FillMask(byte value)
    {
        for (int i = 0; i < maskPixels.Length; i++)
            maskPixels[i] = value;
    }

    private void ApplyMask()
    {
        maskTex.LoadRawTextureData(maskPixels);
        maskTex.Apply();
        maskDirty = false;
    }

    private void LateUpdate()
    {
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
    /// Sets mask to 0 (groove) with soft edges controlled by brushSoftness.
    /// </summary>
    private void PaintMaskCircle(Vector2 center)
    {
        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        int r = brushRadius;
        float rSq = r * r;
        float innerR = r * (1f - brushSoftness);
        float innerRSq = innerR * innerR;

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

                int idx = py * TexWidth + px;
                byte current = maskPixels[idx];

                if (distSq <= innerRSq)
                {
                    // Hard center: fully grooved
                    maskPixels[idx] = 0;
                }
                else
                {
                    // Soft falloff zone
                    float dist = Mathf.Sqrt(distSq);
                    float t = (dist - innerR) / (r - innerR); // 0 at inner edge, 1 at outer edge
                    t = t * t; // quadratic falloff for organic feel
                    byte target = (byte)(t * 255);
                    // Only make it darker (lower), never lighter
                    if (target < current)
                        maskPixels[idx] = target;
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

                    int idx = py * TexWidth + px;
                    byte current = maskPixels[idx];
                    // Partially reveal groove at scatter point
                    byte target = (byte)Random.Range(40, 140);
                    if (target < current)
                        maskPixels[idx] = target;
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

        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0 : (float)i / steps;
            Vector2 pos = Vector2.Lerp(from, to, t);
            PaintMaskCircle(pos);
        }

        // Scatter grains at the endpoint for natural look
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
}
