using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the coloring/drawing game scene.
/// Reads GameContext to determine mode (free draw vs coloring) and which animal to use.
/// </summary>
public class ColoringGameController : MonoBehaviour
{
    [Header("Canvas")]
    public DrawingCanvas drawingCanvas;
    public RawImage outlineImage;     // overlay that shows the animal outline (coloring mode only)
    public Image referenceImage;      // small colored sprite preview (coloring mode only)
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
    public Image eraserHighlight;     // visual indicator when eraser is active

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

    private int selectedColorIndex = 0;
    private int selectedBrushIndex = 1; // medium default
    private bool isEraserActive = false;
    private List<Image> colorIndicators = new List<Image>();
    private List<Image> brushIndicators = new List<Image>();

    private void Start()
    {
        // Init drawing canvas
        drawingCanvas.Init();
        drawingCanvas.SetColor(PaletteColors[selectedColorIndex]);
        drawingCanvas.SetBrushSize(BrushSizes[selectedBrushIndex]);

        // Determine mode from context
        Texture2D customTex = GameContext.CustomTexture;

        if (customTex != null)
        {
            // Selfie or gallery import — generate outlines from the photo
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

        // Build palette UI
        BuildColorPalette();
        BuildBrushSizes();

        // Wire tool buttons
        if (eraserButton != null) eraserButton.onClick.AddListener(ToggleEraser);
        if (undoButton != null) undoButton.onClick.AddListener(OnUndo);
        if (clearButton != null) clearButton.onClick.AddListener(OnClear);
    }

    private void SetupOutline()
    {
        Sprite animalSprite = null;

        // Load from the contentAsset assigned on the sub-item data
        if (GameContext.CurrentSelection != null)
            animalSprite = GameContext.CurrentSelection.contentAsset;

        if (animalSprite != null)
        {
            var outlineTex = OutlineGenerator.Generate(
                animalSprite, outlineResolution, outlineResolution, 0.12f, 2);

            if (outlineTex != null)
            {
                outlineImage.texture = outlineTex;
                outlineImage.gameObject.SetActive(true);
            }
            else
            {
                outlineImage.gameObject.SetActive(false);
            }

            // Show colored reference thumbnail
            if (referenceImage != null)
            {
                referenceImage.sprite = animalSprite;
                if (referenceContainer != null)
                    referenceContainer.SetActive(true);
            }
        }
        else
        {
            Debug.LogWarning($"Could not load animal sprite for coloring. Key: {GameContext.CurrentSelection?.categoryKey}");
            outlineImage.gameObject.SetActive(false);
            if (referenceContainer != null)
                referenceContainer.SetActive(false);
        }
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

        for (int i = 0; i < PaletteColors.Length; i++)
        {
            var go = Instantiate(colorButtonPrefab, colorButtonContainer);
            var btn = go.GetComponent<Button>();
            var img = go.GetComponent<Image>();
            int index = i; // capture

            img.color = PaletteColors[i];

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

            // Size dot inside the button
            var dot = go.transform.Find("Dot");
            if (dot != null)
            {
                var dotRT = dot.GetComponent<RectTransform>();
                float dotSize = 10 + BrushSizes[i] * 0.6f;
                dotRT.sizeDelta = new Vector2(dotSize, dotSize);
            }

            var bgImg = go.GetComponent<Image>();
            if (bgImg != null) brushIndicators.Add(bgImg);

            btn.onClick.AddListener(() => SelectBrushSize(index));
        }

        UpdateBrushSelection();
    }

    private void SelectColor(int index)
    {
        selectedColorIndex = index;
        isEraserActive = false;
        drawingCanvas.SetColor(PaletteColors[index]);
        drawingCanvas.SetEraser(false);
        UpdateColorSelection();
        UpdateEraserVisual();
    }

    private void SelectBrushSize(int index)
    {
        selectedBrushIndex = index;
        drawingCanvas.SetBrushSize(BrushSizes[index]);
        UpdateBrushSelection();
    }

    private void ToggleEraser()
    {
        isEraserActive = !isEraserActive;
        drawingCanvas.SetEraser(isEraserActive);
        UpdateEraserVisual();
        UpdateColorSelection();
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

    private void UpdateEraserVisual()
    {
        if (eraserHighlight != null)
            eraserHighlight.gameObject.SetActive(isEraserActive);
    }

    /// <summary>Called by Home button.</summary>
    public void OnHomePressed()
    {
        NavigationManager.GoToMainMenu();
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
