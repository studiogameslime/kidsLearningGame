using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the coloring/drawing game scene.
/// Supports two interaction modes:
///   - Brush Mode (ages 5+): free drawing with brush strokes, stickers, eraser
///   - Area Fill Mode (ages 2–4): tap-to-fill closed regions
/// Mode is resolved automatically by age or manually via AppSettings.ColoringMode.
/// </summary>
public class ColoringGameController : BaseMiniGame
{
    [Header("Canvas")]
    public DrawingCanvas drawingCanvas;
    public RawImage outlineImage;     // overlay that shows the coloring page outline
    public Image referenceImage;      // small colored sprite preview (gallery upload only)
    public GameObject referenceContainer; // parent GO to show/hide the reference

    [Header("Palette")]
    public Transform colorButtonContainer;
    public Transform brushSizeContainer;
    public GameObject colorButtonPrefab;
    public GameObject brushSizeButtonPrefab;

    [Header("Tool Buttons")]
    public Button eraserButton;
    public Button undoButton;
    public Button clearButton;
    public Button doneButton;         // shown only during journey mode
    public Button saveDrawingButton;  // saves drawing to gallery
    public Image eraserHighlight;     // visual indicator when eraser is active

    [Header("Brushes")]
    public Sprite[] brushIcons;       // Small, Medium, Big brush sprites

    [Header("Stickers")]
    public Transform stickerContainer;
    public Sprite[] stickerSprites;   // legacy — unused, kept for scene compat
    // Category sticker arrays (wired by setup)
    public Sprite[] animalsStickers;
    public Sprite[] lettersStickers;
    public Sprite[] numbersStickers;
    public Sprite[] balloonsStickers;
    public Sprite[] aquariumStickers;
    public Sprite[] carsStickers;
    public Sprite[] foodStickers;
    public Sprite[] artStickers;
    public Sprite[] natureStickers;

    [Header("Settings")]
    public int outlineResolution = 1024;

    // Available colors — bright, kid-friendly
    private static readonly Color[] PaletteColors = new Color[]
    {
        HexColor("#EF4444"), // red
        HexColor("#F97316"), // orange
        HexColor("#FACC15"), // yellow
        HexColor("#22C55E"), // green
        HexColor("#06B6D4"), // cyan
        HexColor("#3B82F6"), // blue
        HexColor("#8B5CF6"), // purple
        HexColor("#EC4899"), // pink
        HexColor("#78716C"), // gray/brown
        HexColor("#1E1E1E"), // black
        HexColor("#FFFFFF"), // white
        HexColor("#92400E"), // brown
    };

    private static readonly int[] BrushSizes = { 12, 24, 40 };

    private Color[] _activePalette = PaletteColors; // defaults, extended in BuildColorPalette
    private int selectedColorIndex = 0;
    private int selectedBrushIndex = 1; // medium default
    private int selectedStickerIndex = -1; // -1 = no sticker
    private bool isEraserActive = false;
    private bool isAreaFillMode = false;
    private List<Image> colorIndicators = new List<Image>();
    private List<Image> brushIndicators = new List<Image>();
    private List<Image> brushTipImages = new List<Image>();
    private List<Image> stickerIndicators = new List<Image>();

    // ── BaseMiniGame Hooks ──────────────────────────────────────

    protected override string GetFallbackGameId() => "coloring";

    protected override void OnGameInit()
    {
        isEndless = true;
        playConfettiOnRoundWin = true;
        playWinSound = false;
        delayBeforeNextRound = 0f;
    }

    protected override void OnRoundSetup()
    {
        // Init drawing canvas
        drawingCanvas.Init();

        // Dismiss tutorial hand on first draw/fill
        drawingCanvas.onFirstDraw = () => DismissTutorial();

        // Determine outline source from context
        Texture2D customTex = GameContext.CustomTexture;

        if (customTex != null)
        {
            // Gallery/selfie import — generate outline from the photo
            SetupOutlineFromTexture(customTex);
            GameContext.CustomTexture = null;
        }
        else
        {
            string categoryKey = GameContext.CurrentSelection != null
                ? GameContext.CurrentSelection.categoryKey
                : "free";

            if (categoryKey != "free")
            {
                SetupOutline();
            }
            else
            {
                outlineImage.gameObject.SetActive(false);
                if (referenceContainer != null)
                    referenceContainer.SetActive(false);
            }
        }

        // Resolve interaction mode (area fill vs brush)
        isAreaFillMode = ResolveAreaFillMode();

        // Build UI and configure canvas based on mode
        BuildColorPalette();

        if (isAreaFillMode)
        {
            ConfigureAreaFillMode();
        }
        else
        {
            ConfigureBrushMode();
        }

        // Done button hidden — save button handles completion during journey
        if (doneButton != null)
            doneButton.gameObject.SetActive(false);

        // Wire save drawing button (also advances journey if active)
        if (saveDrawingButton != null)
        {
            saveDrawingButton.onClick.AddListener(OnSaveDrawing);
            // Ensure save icon appears black — disable color tint transition
            saveDrawingButton.transition = Selectable.Transition.None;
            var btnImg = saveDrawingButton.GetComponent<Image>();
            if (btnImg != null) btnImg.color = Color.black;
        }

        // Position tutorial hand at center of the drawing canvas
        PositionTutorialHand();
    }

    // ── Mode Resolution ──

    /// <summary>
    /// Determines whether to use Area Fill mode based on settings and profile age.
    /// Free draw mode always uses brush (no boundaries to fill).
    /// </summary>
    private bool ResolveAreaFillMode()
    {
        // Free draw has no outline — area fill makes no sense
        string categoryKey = GameContext.CurrentSelection?.categoryKey ?? "free";
        if (categoryKey == "free") return false;

        var mode = AppSettings.ColoringMode;
        if (mode == ColoringModeOption.AreaFill) return true;
        if (mode == ColoringModeOption.Brush) return false;

        // Auto: default to area fill for all ages
        return true;
    }

    // ── Area Fill Mode ──

    private void ConfigureAreaFillMode()
    {
        // Enable fill mode on canvas with outline boundaries
        drawingCanvas.SetAreaFillMode(true);
        if (outlineImage != null && outlineImage.texture != null)
            drawingCanvas.SetOutlineBoundary(outlineImage.texture);

        // Set initial fill color
        drawingCanvas.SetColor(_activePalette[selectedColorIndex]);

        // Listen for successful fills
        drawingCanvas.onAreaFilled = OnAreaFilled;

        // Hide brush sizes and their section title
        if (brushSizeContainer != null)
        {
            // Hide "מברשות" section title (sibling just before the brush container)
            int idx = brushSizeContainer.GetSiblingIndex();
            if (idx > 0)
                brushSizeContainer.parent.GetChild(idx - 1).gameObject.SetActive(false);

            brushSizeContainer.gameObject.SetActive(false);
        }

        // Hide tool row (parent of eraser/undo/clear)
        if (eraserButton != null)
            eraserButton.transform.parent.gameObject.SetActive(false);
        if (eraserHighlight != null) eraserHighlight.gameObject.SetActive(false);

        // Remove top padding reserved for reference image area
        if (colorButtonContainer != null)
        {
            var panelLayout = colorButtonContainer.parent.GetComponent<VerticalLayoutGroup>();
            if (panelLayout != null)
                panelLayout.padding = new RectOffset(0, 0, 8, 8);
        }

        // Build stickers (available for all ages)
        BuildStickers();
    }

    private void OnAreaFilled(int pixelCount)
    {
        // No voice feedback during coloring — let the child focus
    }

    // ── Brush Mode ──

    private void ConfigureBrushMode()
    {
        drawingCanvas.SetAreaFillMode(false);
        drawingCanvas.SetColor(_activePalette[selectedColorIndex]);
        drawingCanvas.SetBrushSize(BrushSizes[selectedBrushIndex]);

        // Build brush-mode UI
        BuildBrushSizes();
        BuildStickers();

        // Wire tool buttons
        if (eraserButton != null) eraserButton.onClick.AddListener(ToggleEraser);
        if (undoButton != null) undoButton.onClick.AddListener(OnUndo);
        if (clearButton != null) clearButton.onClick.AddListener(OnClear);
    }

    // ── Outline Setup ──

    private void PositionTutorialHand()
    {
        if (TutorialHand == null || drawingCanvas == null) return;

        // Center of the drawing canvas
        Vector2 localPos = TutorialHand.GetLocalCenter(drawingCanvas.GetComponent<RectTransform>());
        TutorialHand.SetPosition(localPos);
    }

    private void SetupOutline()
    {
        Sprite pageSprite = null;

        // Load pre-built coloring page from contentAsset
        if (GameContext.CurrentSelection != null)
            pageSprite = GameContext.CurrentSelection.contentAsset;

        if (pageSprite != null && pageSprite.texture != null)
        {
            // Strip white/light background → transparent, keep dark lines only
            var transparentOutline = MakeOutlineTransparent(pageSprite.texture);
            outlineImage.texture = transparentOutline;
            outlineImage.gameObject.SetActive(true);
        }
        else
        {
            if (pageSprite == null)
                Debug.LogWarning($"Could not load coloring page sprite. Key: {GameContext.CurrentSelection?.categoryKey}");
            outlineImage.gameObject.SetActive(false);
        }

        // No reference image for pre-built coloring pages
        if (referenceContainer != null)
            referenceContainer.SetActive(false);
    }

    /// <summary>
    /// Converts a coloring page texture so that light/white pixels become transparent
    /// and dark line pixels are preserved. This allows the drawing layer to show through.
    /// </summary>
    private Texture2D MakeOutlineTransparent(Texture sourceTex)
    {
        // Blit to a readable texture at outline resolution
        int w = outlineResolution;
        int h = outlineResolution;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(sourceTex, rt);
        RenderTexture.active = rt;
        var readable = new Texture2D(w, h, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        readable.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        Color[] pixels = readable.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            float lum = (c.r + c.g + c.b) * 0.333f;

            if (c.a < 0.1f)
            {
                // Already transparent
                pixels[i] = Color.clear;
            }
            else if (lum > 0.7f)
            {
                // Light/white pixel → make transparent
                pixels[i] = Color.clear;
            }
            else if (lum > 0.4f)
            {
                // Mid-tone → semi-transparent for anti-aliased edges
                float alpha = 1f - ((lum - 0.4f) / 0.3f);
                pixels[i] = new Color(c.r, c.g, c.b, alpha);
            }
            // else: dark pixel — keep as-is (outline line)
        }

        readable.SetPixels(pixels);
        readable.Apply();
        readable.filterMode = FilterMode.Bilinear;
        return readable;
    }

    private void SetupOutlineFromTexture(Texture2D tex)
    {
        var sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        var outlineTex = OutlineGenerator.Generate(
            sprite, outlineResolution, outlineResolution, 0.12f, 2);

        if (outlineTex != null)
        {
            outlineImage.texture = outlineTex;
            outlineImage.gameObject.SetActive(true);
        }
        else
        {
            outlineImage.gameObject.SetActive(false);
        }

        if (referenceImage != null)
        {
            referenceImage.sprite = sprite;
            if (referenceContainer != null)
                referenceContainer.SetActive(true);
        }
    }

    // ── Palette ──

    private void BuildColorPalette()
    {
        if (colorButtonPrefab == null || colorButtonContainer == null) return;

        // Use default palette colors
        _activePalette = PaletteColors;

        for (int i = 0; i < _activePalette.Length; i++)
        {
            var go = Instantiate(colorButtonPrefab, colorButtonContainer);
            var btn = go.GetComponent<Button>();
            var img = go.GetComponent<Image>();
            int index = i; // capture

            img.color = _activePalette[i];

            // Selection ring (child image)
            var ring = go.transform.Find("Ring");
            Image ringImg = ring != null ? ring.GetComponent<Image>() : null;
            if (ringImg != null) colorIndicators.Add(ringImg);

            btn.onClick.AddListener(() => SelectColor(index));
        }

        UpdateColorSelection();
    }

    private void BuildBrushSizes()
    {
        if (brushSizeButtonPrefab == null || brushSizeContainer == null) return;

        for (int i = 0; i < BrushSizes.Length; i++)
        {
            var go = Instantiate(brushSizeButtonPrefab, brushSizeContainer);
            var btn = go.GetComponent<Button>();
            int index = i;

            // Set brush icon sprite if available
            var icon = go.transform.Find("Icon");
            if (icon != null && brushIcons != null && i < brushIcons.Length)
            {
                var iconImg = icon.GetComponent<Image>();
                if (iconImg != null) iconImg.sprite = brushIcons[i];

                // Find tip color indicator and scale per brush size
                var tip = icon.Find("Tip");
                if (tip != null)
                {
                    var tipRT = tip.GetComponent<RectTransform>();
                    if (tipRT != null)
                    {
                        // Small=0, Medium=1, Large=2 → progressively bigger circle
                        float s = 0.05f + i * 0.03f;  // 0.05, 0.08, 0.11
                        float cy = 0.88f; // center Y near top
                        tipRT.anchorMin = new Vector2(0.5f - s, cy - s);
                        tipRT.anchorMax = new Vector2(0.5f + s, cy + s);
                    }
                    var tipImg = tip.GetComponent<Image>();
                    if (tipImg != null) brushTipImages.Add(tipImg);
                }
            }

            var bgImg = go.GetComponent<Image>();
            if (bgImg != null) brushIndicators.Add(bgImg);

            btn.onClick.AddListener(() => SelectBrushSize(index));
        }

        UpdateBrushSelection();
        UpdateBrushTipColors();
    }

    private void SelectColor(int index)
    {
        selectedColorIndex = index;
        selectedStickerIndex = -1;
        isEraserActive = false;
        drawingCanvas.SetColor(_activePalette[index]);
        drawingCanvas.SetEraser(false);
        drawingCanvas.SetStickerMode(null);
        UpdateColorSelection();
        UpdateStickerSelection();
        UpdateEraserVisual();
        UpdateBrushTipColors();
    }

    private void SelectBrushSize(int index)
    {
        selectedBrushIndex = index;
        selectedStickerIndex = -1;
        drawingCanvas.SetBrushSize(BrushSizes[index]);
        drawingCanvas.SetStickerMode(null);
        UpdateBrushSelection();
        UpdateStickerSelection();
    }

    private void ToggleEraser()
    {
        isEraserActive = !isEraserActive;
        selectedStickerIndex = -1;
        drawingCanvas.SetEraser(isEraserActive);
        drawingCanvas.SetStickerMode(null);
        UpdateEraserVisual();
        UpdateColorSelection();
        UpdateStickerSelection();
        UpdateBrushTipColors();
    }

    private void OnUndo()
    {
        drawingCanvas.Undo();
    }

    private void OnClear()
    {
        drawingCanvas.Clear();
    }

    // ── Visual Feedback ──

    private void UpdateColorSelection()
    {
        for (int i = 0; i < colorIndicators.Count; i++)
        {
            bool selected = !isEraserActive && i == selectedColorIndex;
            colorIndicators[i].gameObject.SetActive(selected);
        }
    }

    private void UpdateBrushSelection()
    {
        for (int i = 0; i < brushIndicators.Count; i++)
        {
            bool selected = i == selectedBrushIndex;
            brushIndicators[i].color = selected
                ? new Color(0.85f, 0.85f, 0.85f, 1f)
                : new Color(1f, 1f, 1f, 0.5f);
        }
    }

    private void UpdateBrushTipColors()
    {
        Color tipColor = isEraserActive ? Color.white : _activePalette[selectedColorIndex];
        foreach (var tip in brushTipImages)
            tip.color = tipColor;
    }

    private void UpdateEraserVisual()
    {
        if (eraserHighlight != null)
            eraserHighlight.gameObject.SetActive(isEraserActive);
    }

    // ── Stickers ──

    // Flat list of collected sprites built from category arrays
    private readonly List<Sprite> _collectedSprites = new List<Sprite>();

    private void BuildStickers()
    {
        if (stickerContainer == null) return;

        _collectedSprites.Clear();
        var profile = ProfileManager.ActiveProfile;
        if (profile != null && profile.journey != null && profile.journey.collectedStickerIds != null)
        {
            foreach (var id in profile.journey.collectedStickerIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                Sprite spr = StickerSpriteBank.GetSprite(id);
                if (spr != null)
                    _collectedSprites.Add(spr);
                else
                    Debug.Log($"[ColoringStickers] No sprite for '{id}'");
            }
            Debug.Log($"[ColoringStickers] {_collectedSprites.Count} sprites found from {profile.journey.collectedStickerIds.Count} IDs");
        }

        if (_collectedSprites.Count == 0) return;

        for (int i = 0; i < _collectedSprites.Count; i++)
        {
            var go = new GameObject($"Sticker_{i}");
            go.transform.SetParent(stickerContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(58, 58);

            var img = go.AddComponent<Image>();
            img.sprite = _collectedSprites[i];
            img.preserveAspect = true;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            // Selection highlight (hidden by default)
            var ring = new GameObject("Ring");
            ring.transform.SetParent(go.transform, false);
            var ringRT = ring.AddComponent<RectTransform>();
            ringRT.anchorMin = Vector2.zero;
            ringRT.anchorMax = Vector2.one;
            ringRT.offsetMin = new Vector2(-4, -4);
            ringRT.offsetMax = new Vector2(4, 4);
            var ringImg = ring.AddComponent<Image>();
            ringImg.color = new Color(0.3f, 0.7f, 1f, 0.4f);
            ringImg.raycastTarget = false;
            ring.SetActive(false);
            stickerIndicators.Add(ringImg);

            int index = i;
            btn.onClick.AddListener(() => SelectSticker(index));
        }
    }

    private void SelectSticker(int index)
    {
        if (index < 0 || index >= _collectedSprites.Count) return;
        selectedStickerIndex = index;
        isEraserActive = false;
        drawingCanvas.SetEraser(false);
        drawingCanvas.SetStickerMode(_collectedSprites[index]);
        UpdateStickerSelection();
        UpdateColorSelection();
        UpdateEraserVisual();
    }

    private void UpdateStickerSelection()
    {
        for (int i = 0; i < stickerIndicators.Count; i++)
        {
            bool selected = i == selectedStickerIndex;
            stickerIndicators[i].gameObject.SetActive(selected);
        }
    }

    /// <summary>Called by Save Drawing button — captures and saves the drawing.</summary>
    public void OnSaveDrawing()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null || drawingCanvas == null) return;

        byte[] png = CompositeDrawing();
        if (png == null) return;

        string folder = ProfileManager.Instance.GetProfileFolder(profile.id);
        string drawingsDir = Path.Combine(folder, "drawings");
        if (!Directory.Exists(drawingsDir))
            Directory.CreateDirectory(drawingsDir);

        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string fileName = $"drawing_{timestamp}.png";
        string fullPath = Path.Combine(drawingsDir, fileName);
        File.WriteAllBytes(fullPath, png);

        string animalId = GameContext.CurrentSelection != null
            ? GameContext.CurrentSelection.categoryKey
            : "free";

        var drawing = new SavedDrawing
        {
            imagePath = Path.Combine("profiles", profile.id, "drawings", fileName),
            animalId = animalId,
            createdAt = timestamp
        };
        profile.savedDrawings.Add(drawing);
        ProfileManager.Instance.Save();

        SoundLibrary.PlayGreatPainting();

        // Brief visual feedback — disable button momentarily
        if (saveDrawingButton != null)
        {
            saveDrawingButton.interactable = false;
            StartCoroutine(ReEnableSaveButton());
        }
    }

    private System.Collections.IEnumerator ReEnableSaveButton()
    {
        yield return new WaitForSeconds(1.5f);
        if (saveDrawingButton != null) saveDrawingButton.interactable = true;
    }

    /// <summary>Called by Done button during journey mode.</summary>
    public void OnDonePressed()
    {
        Stats?.RecordCorrect();
        CompleteRound(); // BaseMiniGame handles confetti
    }

    /// <summary>Called by Home button.</summary>
    public void OnHomePressed()
    {
        ExitGame();
    }

    /// <summary>
    /// Composites the drawing layer with the outline overlay into a single PNG.
    /// The outline is drawn on top of the painting so the saved image looks complete.
    /// </summary>
    private byte[] CompositeDrawing()
    {
        var drawPixels = drawingCanvas.GetPixels();
        if (drawPixels == null) return drawingCanvas.EncodeToPNG();

        var size = drawingCanvas.GetTextureSize();
        if (size.x == 0) return drawingCanvas.EncodeToPNG();

        // If no outline is active, just save the drawing as-is
        if (outlineImage == null || !outlineImage.gameObject.activeSelf || outlineImage.texture == null)
            return drawingCanvas.EncodeToPNG();

        // Read the outline texture
        var outlineTex = outlineImage.texture as Texture2D;
        if (outlineTex == null) return drawingCanvas.EncodeToPNG();

        // Resize outline to match drawing if needed
        Color[] outlinePixels;
        if (outlineTex.width == size.x && outlineTex.height == size.y)
        {
            outlinePixels = outlineTex.GetPixels();
        }
        else
        {
            // Create a resized copy
            var resized = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
            var rt = RenderTexture.GetTemporary(size.x, size.y);
            Graphics.Blit(outlineTex, rt);
            RenderTexture.active = rt;
            resized.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            resized.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            outlinePixels = resized.GetPixels();
            Destroy(resized);
        }

        // Composite: drawing as base, outline on top (alpha blend)
        for (int i = 0; i < drawPixels.Length && i < outlinePixels.Length; i++)
        {
            Color src = outlinePixels[i]; // outline (foreground)
            if (src.a <= 0f) continue;

            Color dst = drawPixels[i]; // drawing (background)
            float outA = src.a;
            float invA = 1f - outA;
            drawPixels[i] = new Color(
                src.r * outA + dst.r * invA,
                src.g * outA + dst.g * invA,
                src.b * outA + dst.b * invA,
                Mathf.Max(dst.a, outA)
            );
        }

        var result = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
        result.SetPixels(drawPixels);
        result.Apply();
        byte[] pngData = result.EncodeToPNG();
        Destroy(result);
        return pngData;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
