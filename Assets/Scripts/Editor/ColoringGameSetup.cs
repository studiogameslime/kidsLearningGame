using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the ColoringGame scene in LANDSCAPE orientation.
///
/// Layout:
///   Header (80px) — home left, title center, save + done right
///   Left ~64%  — drawing canvas with outline overlay
///   Right ~36% — fixed tool panel (no scroll):
///       1. Reference image (hidden by default)
///       2. Action icons:  eraser | undo | clear
///       3. צבעים:         6-column color grid
///       4. מברשות:        3 brush icons
///       5. מדבקות:        sticker grid (all sliced sprites, no scroll)
/// </summary>
public class ColoringGameSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    // Colors
    private static readonly Color BgColor       = HexColor("#FFF8F0");
    private static readonly Color TopBarColor   = HexColor("#F8E8D8");
    private static readonly Color PanelColor    = HexColor("#FDF6EF");
    private static readonly Color CanvasBorder  = HexColor("#E0D5C8");
    private static readonly Color ActionBtnBg   = HexColor("#EEEEEE");
    private static readonly Color SectionTitleColor = HexColor("#7A6A5A");
    private static readonly Color ToolRowBg     = HexColor("#F5EDE4");

    // Layout
    private static readonly int TopBarHeight    = SetupConstants.HeaderHeight;
    private const int CanvasPad       = 8;
    private const int RefImageSize    = 260;
    private const int ColorCircleSize = 90;
    private const int BrushBtnSize    = 90;
    private const int StickerSize     = 90;
    private const int ToolBtnSize     = 112;
    private const int SectionTitleFontSize = 18;
    private const int PanelPadH       = 4;
    private const int ToolRowHeight   = 124;

    private const float LeftRatio = 0.52f;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Coloring Game Setup", "Creating prefabs…", 0.1f);
            var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
            var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");

            var colorBtnPrefab = CreateColorButtonPrefab(circleSprite);
            var brushBtnPrefab = CreateBrushButtonPrefab(roundedRect, circleSprite);

            EditorUtility.DisplayProgressBar("Coloring Game Setup", "Updating coloring data…", 0.3f);
            UpdateColoringData();

            EditorUtility.DisplayProgressBar("Coloring Game Setup", "Building scene…", 0.5f);
            CreateColoringScene(roundedRect, circleSprite, colorBtnPrefab, brushBtnPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ─────────────────────────────────────────
    //  UPDATE COLORING DATA
    // ─────────────────────────────────────────

    private static void UpdateColoringData()
    {
        var coloring = AssetDatabase.LoadAssetAtPath<GameItemData>("Assets/Data/Games/Coloring.asset");
        if (coloring == null) { Debug.LogError("Coloring.asset not found."); return; }

        // Keep only special items (free, selfie, gallery) — remove animal entries
        coloring.subItems.RemoveAll(item =>
            item.categoryKey != "free" &&
            item.categoryKey != "selfie" &&
            item.categoryKey != "gallery");

        if (!HasKey(coloring, "free"))
            coloring.subItems.Insert(0, new SubItemData {
                id = "coloring_free", title = "\u05D3\u05E3 \u05D7\u05D3\u05E9",
                cardColor = HexColor("#FFD93D"), categoryKey = "free", targetSceneName = "ColoringGame"
            });

        if (!HasKey(coloring, "selfie"))
            coloring.subItems.Insert(1, new SubItemData {
                id = "coloring_selfie", title = "\u05E1\u05DC\u05E4\u05D9",
                cardColor = HexColor("#F472B6"), categoryKey = "selfie",
                targetSceneName = "ColoringGame", thumbnail = LoadSprite("Assets/Art/Camera.png")
            });

        // Load painting pages from Art/PaintingPages/ into contentPages array
        var paintingPages = new List<Sprite>();
        var guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/PaintingPages" });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                paintingPages.Add(sprite);
        }
        // Sort by filename for consistent order (1.png, 2.png, ...)
        paintingPages.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        coloring.contentPages = paintingPages.ToArray();

        EditorUtility.SetDirty(coloring);
        Debug.Log($"Coloring data updated: {coloring.subItems.Count} special items, {coloring.contentPages.Length} painting pages.");
    }

    private static bool HasKey(GameItemData g, string key)
    {
        foreach (var i in g.subItems) if (i.categoryKey == key) return true;
        return false;
    }

    // ─────────────────────────────────────────
    //  PREFABS
    // ─────────────────────────────────────────

    private static GameObject CreateColorButtonPrefab(Sprite circleSprite)
    {
        EnsureFolder("Assets/Prefabs/UI");
        var root = new GameObject("ColorButton");
        root.AddComponent<RectTransform>().sizeDelta = new Vector2(ColorCircleSize, ColorCircleSize);
        var img = root.AddComponent<Image>();
        img.sprite = circleSprite; img.color = Color.red; img.raycastTarget = true;
        root.AddComponent<Button>().targetGraphic = img;

        var ring = new GameObject("Ring");
        ring.transform.SetParent(root.transform, false);
        var ringRT = ring.AddComponent<RectTransform>();
        ringRT.anchorMin = Vector2.zero; ringRT.anchorMax = Vector2.one;
        ringRT.offsetMin = new Vector2(-5, -5); ringRT.offsetMax = new Vector2(5, 5);
        var ri = ring.AddComponent<Image>();
        ri.sprite = circleSprite; ri.color = new Color(0.3f, 0.3f, 0.3f, 0.5f); ri.raycastTarget = false;
        ring.SetActive(false);

        var p = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/UI/ColorButton.prefab");
        Object.DestroyImmediate(root); return p;
    }

    private static GameObject CreateBrushButtonPrefab(Sprite roundedRect, Sprite circleSprite)
    {
        EnsureFolder("Assets/Prefabs/UI");
        var root = new GameObject("BrushSizeButton");
        root.AddComponent<RectTransform>().sizeDelta = new Vector2(BrushBtnSize, BrushBtnSize);
        var img = root.AddComponent<Image>();
        img.sprite = roundedRect; img.type = Image.Type.Sliced;
        img.color = new Color(1f, 1f, 1f, 0.5f); img.raycastTarget = true;
        root.AddComponent<Button>().targetGraphic = img;

        var icon = new GameObject("Icon");
        icon.transform.SetParent(root.transform, false);
        var iconRT = icon.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.08f, 0.08f);
        iconRT.anchorMax = new Vector2(0.92f, 0.92f);
        iconRT.offsetMin = Vector2.zero; iconRT.offsetMax = Vector2.zero;
        var ii = icon.AddComponent<Image>();
        ii.preserveAspect = true; ii.color = Color.white; ii.raycastTarget = false;

        // Tip color indicator — small paint mark at the brush bristles (top)
        var tip = new GameObject("Tip");
        tip.transform.SetParent(icon.transform, false);
        var tipRT = tip.AddComponent<RectTransform>();
        tipRT.anchorMin = new Vector2(0.35f, 0.82f);
        tipRT.anchorMax = new Vector2(0.65f, 0.97f);
        tipRT.offsetMin = Vector2.zero;
        tipRT.offsetMax = Vector2.zero;
        var tipImg = tip.AddComponent<Image>();
        tipImg.sprite = circleSprite;
        tipImg.color = Color.red; // updated at runtime
        tipImg.raycastTarget = false;

        var p = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/UI/BrushSizeButton.prefab");
        Object.DestroyImmediate(root); return p;
    }

    // ─────────────────────────────────────────
    //  SCENE
    // ─────────────────────────────────────────

    private static void CreateColoringScene(Sprite roundedRect, Sprite circleSprite,
        GameObject colorBtnPrefab, GameObject brushBtnPrefab)
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera ──
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera"; camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = BgColor; cam.orthographic = true;
        var urp = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urp != null) camGO.AddComponent(urp);

        // ── EventSystem ──
        var esGO = new GameObject("EventSystem"); esGO.AddComponent<EventSystem>();
        var inp = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inp != null) esGO.AddComponent(inp); else esGO.AddComponent<StandaloneInputModule>();

        // ── Canvas ──
        var canvasGO = new GameObject("ColoringCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref; scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var bg = StretchImage(canvasGO.transform, "Background", BgColor);
        bg.GetComponent<Image>().raycastTarget = false;

        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(canvasGO.transform, false);
        Full(safeGO.AddComponent<RectTransform>());
        safeGO.AddComponent<SafeAreaHandler>();

        // ═══════════════════════════════════
        //  HEADER (80px)
        //  [Home]   [Title]   [Save] [Done]
        // ═══════════════════════════════════

        var bar = StretchImage(safeGO.transform, "TopBar", TopBarColor);
        var barRT = bar.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1); barRT.anchorMax = new Vector2(1, 1);
        barRT.pivot = new Vector2(0.5f, 1); barRT.sizeDelta = new Vector2(0, TopBarHeight);
        bar.GetComponent<Image>().raycastTarget = false;
        bar.AddComponent<ThemeHeader>();

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(bar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT); titleRT.offsetMin = new Vector2(100, 0); titleRT.offsetMax = new Vector2(-160, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05E6\u05D1\u05D9\u05E2\u05D4");
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold; titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center; titleTMP.raycastTarget = false;

        // Home button (top-left)
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = IconBtn(bar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(90, 90));

        // Trophy button (next to home)
        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = IconBtn(bar.transform, "TrophyButton", trophyIcon,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(112, -30), new Vector2(70, 70));

        // Save button (top-right) — green tinted
        var saveIcon = LoadSprite("Assets/Art/Icons/save.png");
        var saveGO = new GameObject("SaveButton");
        saveGO.transform.SetParent(bar.transform, false);
        var saveRT = saveGO.AddComponent<RectTransform>();
        saveRT.anchorMin = new Vector2(1, 1); saveRT.anchorMax = new Vector2(1, 1);
        saveRT.pivot = new Vector2(1, 1);
        saveRT.anchoredPosition = new Vector2(-16, -8); saveRT.sizeDelta = new Vector2(64, 64);
        var saveBgImg = saveGO.AddComponent<Image>();
        saveBgImg.sprite = roundedRect; saveBgImg.type = Image.Type.Sliced;
        saveBgImg.color = HexColor("#A5D6A7"); saveBgImg.raycastTarget = true;
        saveGO.AddComponent<Button>().targetGraphic = saveBgImg;
        var saveIconGO = new GameObject("Icon");
        saveIconGO.transform.SetParent(saveGO.transform, false);
        var saveIconRT = saveIconGO.AddComponent<RectTransform>();
        saveIconRT.anchorMin = new Vector2(0.15f, 0.15f); saveIconRT.anchorMax = new Vector2(0.85f, 0.85f);
        saveIconRT.offsetMin = Vector2.zero; saveIconRT.offsetMax = Vector2.zero;
        var saveIconImg = saveIconGO.AddComponent<Image>();
        saveIconImg.sprite = saveIcon; saveIconImg.preserveAspect = true;
        saveIconImg.color = Color.white; saveIconImg.raycastTarget = false;

        // Done button (journey only, next to save)
        var doneGO = new GameObject("DoneButton");
        doneGO.transform.SetParent(bar.transform, false);
        var doneRT = doneGO.AddComponent<RectTransform>();
        doneRT.anchorMin = new Vector2(1, 1); doneRT.anchorMax = new Vector2(1, 1);
        doneRT.pivot = new Vector2(1, 1);
        doneRT.anchoredPosition = new Vector2(-88, -8); doneRT.sizeDelta = new Vector2(64, 64);
        var doneImg = doneGO.AddComponent<Image>();
        doneImg.sprite = roundedRect; doneImg.type = Image.Type.Sliced;
        doneImg.color = HexColor("#4CAF50"); doneImg.raycastTarget = true;
        doneGO.AddComponent<Button>().targetGraphic = doneImg;
        var doneLbl = new GameObject("Label");
        doneLbl.transform.SetParent(doneGO.transform, false);
        Full(doneLbl.AddComponent<RectTransform>());
        var doneTMP = doneLbl.AddComponent<TextMeshProUGUI>();
        doneTMP.text = "\u2714"; doneTMP.fontSize = 30;
        doneTMP.color = Color.white; doneTMP.alignment = TextAlignmentOptions.Center;
        doneTMP.raycastTarget = false;
        doneGO.SetActive(false);

        // ═══════════════════════════════════
        //  CONTENT — below header
        // ═══════════════════════════════════

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(safeGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        Full(contentRT); contentRT.offsetMax = new Vector2(0, -TopBarHeight);

        // ═══════════════════════════════════
        //  LEFT — DRAWING CANVAS (64%)
        // ═══════════════════════════════════

        var leftGO = new GameObject("LeftPanel");
        leftGO.transform.SetParent(contentGO.transform, false);
        var leftRT = leftGO.AddComponent<RectTransform>();
        leftRT.anchorMin = Vector2.zero; leftRT.anchorMax = new Vector2(LeftRatio, 1);
        leftRT.offsetMin = Vector2.zero; leftRT.offsetMax = Vector2.zero;

        var canvasFrame = StretchImage(leftGO.transform, "CanvasFrame", CanvasBorder);
        var cfRT = canvasFrame.GetComponent<RectTransform>();
        Full(cfRT); cfRT.offsetMin = new Vector2(CanvasPad, CanvasPad); cfRT.offsetMax = new Vector2(-CanvasPad, -CanvasPad);
        canvasFrame.GetComponent<Image>().raycastTarget = false;

        var canvasBg = StretchImage(canvasFrame.transform, "CanvasWhite", Color.white);
        var cbRT = canvasBg.GetComponent<RectTransform>();
        Full(cbRT); cbRT.offsetMin = new Vector2(3, 3); cbRT.offsetMax = new Vector2(-3, -3);
        canvasBg.GetComponent<Image>().raycastTarget = false;

        var drawGO = new GameObject("DrawingLayer");
        drawGO.transform.SetParent(canvasBg.transform, false);
        Full(drawGO.AddComponent<RectTransform>());
        drawGO.AddComponent<RawImage>().color = Color.white;
        var drawCanvas = drawGO.AddComponent<DrawingCanvas>();

        var outlineGO = new GameObject("OutlineOverlay");
        outlineGO.transform.SetParent(canvasBg.transform, false);
        Full(outlineGO.AddComponent<RectTransform>());
        var outlineRaw = outlineGO.AddComponent<RawImage>();
        outlineRaw.color = Color.white; outlineRaw.raycastTarget = false;
        outlineGO.SetActive(false);

        // ═══════════════════════════════════
        //  RIGHT — TOOL PANEL (36%)
        //
        //  NO SCROLL. Strict vertical order:
        //   1. Reference image (hidden by default)
        //   2. Action icons: eraser | undo | clear
        //   3. צבעים — color grid (6/row)
        //   4. מברשות — brush icons
        //   5. מדבקות — sticker grid (5/row, no scroll)
        // ═══════════════════════════════════

        var rightGO = StretchImage(contentGO.transform, "RightPanel", PanelColor);
        var rightRT = rightGO.GetComponent<RectTransform>();
        rightRT.anchorMin = new Vector2(LeftRatio, 0); rightRT.anchorMax = new Vector2(1, 1);
        rightRT.offsetMin = Vector2.zero; rightRT.offsetMax = Vector2.zero;
        rightGO.GetComponent<Image>().raycastTarget = false;

        // Main vertical layout — NO scroll
        var panelGO = new GameObject("PanelLayout");
        panelGO.transform.SetParent(rightGO.transform, false);
        var panelRT = panelGO.AddComponent<RectTransform>();
        Full(panelRT);
        panelRT.offsetMin = new Vector2(PanelPadH, 4);
        panelRT.offsetMax = new Vector2(-PanelPadH, -4);
        var panelVL = panelGO.AddComponent<VerticalLayoutGroup>();
        panelVL.spacing = 4;
        // Top area height: ref image + padding (~30% of panel)
        int topAreaHeight = RefImageSize + 24;
        panelVL.childAlignment = TextAnchor.UpperCenter;
        panelVL.childForceExpandWidth = true;
        panelVL.childForceExpandHeight = false;
        panelVL.padding = new RectOffset(0, 0, topAreaHeight, 8);

        // ── 1. Reference Image (outside layout, anchored top-left of right panel) ──
        var refBgGO = new GameObject("RefBackground");
        refBgGO.transform.SetParent(rightGO.transform, false);
        var refBgRT = refBgGO.AddComponent<RectTransform>();
        refBgRT.anchorMin = new Vector2(0, 1);
        refBgRT.anchorMax = new Vector2(0, 1);
        refBgRT.pivot = new Vector2(0, 1);
        refBgRT.anchoredPosition = new Vector2(8, -8);
        refBgRT.sizeDelta = new Vector2(RefImageSize + 16, RefImageSize + 16);
        var refBgImg = refBgGO.AddComponent<Image>();
        refBgImg.sprite = roundedRect; refBgImg.type = Image.Type.Sliced;
        refBgImg.color = new Color(1f, 1f, 1f, 0.95f); refBgImg.raycastTarget = false;
        refBgGO.SetActive(false);

        var refGO = new GameObject("ReferenceSprite");
        refGO.transform.SetParent(refBgGO.transform, false);
        var refRT = refGO.AddComponent<RectTransform>();
        Full(refRT); refRT.offsetMin = new Vector2(8, 8); refRT.offsetMax = new Vector2(-8, -8);
        var refImg = refGO.AddComponent<Image>();
        refImg.preserveAspect = true; refImg.raycastTarget = false; refImg.color = Color.white;

        // ── 2. Action Icons (outside layout, to the right of reference image) ──
        var toolRowGO = new GameObject("ToolRow");
        toolRowGO.transform.SetParent(rightGO.transform, false);
        var toolRowRT = toolRowGO.AddComponent<RectTransform>();
        toolRowRT.anchorMin = new Vector2(0, 1);
        toolRowRT.anchorMax = new Vector2(1, 1);
        toolRowRT.pivot = new Vector2(0, 1);
        // Position: starts after reference image, vertically centered with it
        float toolRowX = RefImageSize + 32;
        toolRowRT.anchoredPosition = new Vector2(toolRowX, -8);
        toolRowRT.sizeDelta = new Vector2(-(toolRowX + 8), ToolRowHeight);
        var toolRowBgImg = toolRowGO.AddComponent<Image>();
        toolRowBgImg.sprite = roundedRect; toolRowBgImg.type = Image.Type.Sliced;
        toolRowBgImg.color = ToolRowBg; toolRowBgImg.raycastTarget = false;

        var toolRowLayout = toolRowGO.AddComponent<HorizontalLayoutGroup>();
        toolRowLayout.spacing = 16;
        toolRowLayout.childAlignment = TextAnchor.MiddleCenter;
        toolRowLayout.childForceExpandWidth = false;
        toolRowLayout.childForceExpandHeight = false;
        toolRowLayout.padding = new RectOffset(8, 8, 4, 4);

        var phoneIcon = LoadSprite("Assets/Art/Icons/phone.png");
        var eraserGO = ToolButton(toolRowGO.transform, "EraserButton", phoneIcon, roundedRect);

        var eraserHL = new GameObject("EraserHighlight");
        eraserHL.transform.SetParent(eraserGO.transform, false);
        var eraserHLRT = eraserHL.AddComponent<RectTransform>();
        Full(eraserHLRT); eraserHLRT.offsetMin = new Vector2(-3, -3); eraserHLRT.offsetMax = new Vector2(3, 3);
        var eraserHLImg = eraserHL.AddComponent<Image>();
        eraserHLImg.sprite = roundedRect; eraserHLImg.type = Image.Type.Sliced;
        eraserHLImg.color = new Color(0.9f, 0.3f, 0.3f, 0.4f); eraserHLImg.raycastTarget = false;
        eraserHL.SetActive(false);

        var undoIcon = LoadSprite("Assets/Art/Icons/return.png");
        var undoGO = ToolButton(toolRowGO.transform, "UndoButton", undoIcon, roundedRect);

        var trashIcon = LoadSprite("Assets/Art/Icons/trashcan.png");
        var clearGO = ToolButton(toolRowGO.transform, "ClearButton", trashIcon, roundedRect);

        // ── 3. צבעים (Colors) ──
        SectionTitle(panelGO.transform, "\u05E6\u05D1\u05E2\u05D9\u05DD");

        var paletteGO = new GameObject("ColorPalette");
        paletteGO.transform.SetParent(panelGO.transform, false);
        paletteGO.AddComponent<RectTransform>();
        var paletteLE = paletteGO.AddComponent<LayoutElement>();
        paletteLE.preferredHeight = ColorCircleSize * 2 + 8 + 4;
        paletteLE.flexibleWidth = 1;

        var paletteGrid = paletteGO.AddComponent<GridLayoutGroup>();
        paletteGrid.cellSize = new Vector2(ColorCircleSize, ColorCircleSize);
        paletteGrid.spacing = new Vector2(8, 8);
        paletteGrid.childAlignment = TextAnchor.MiddleCenter;
        paletteGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        paletteGrid.constraintCount = 9;
        paletteGrid.padding = new RectOffset(0, 0, 0, 0);

        // ── 4. מברשות (Brushes) ──
        SectionTitle(panelGO.transform, "\u05DE\u05D1\u05E8\u05E9\u05D5\u05EA");

        var brushGO = new GameObject("BrushSizes");
        brushGO.transform.SetParent(panelGO.transform, false);
        brushGO.AddComponent<RectTransform>();
        var brushLE = brushGO.AddComponent<LayoutElement>();
        brushLE.preferredHeight = BrushBtnSize + 8;
        brushLE.flexibleWidth = 1;

        var brushLayout = brushGO.AddComponent<HorizontalLayoutGroup>();
        brushLayout.spacing = 20;
        brushLayout.childAlignment = TextAnchor.MiddleCenter;
        brushLayout.childForceExpandWidth = false;
        brushLayout.childForceExpandHeight = false;
        brushLayout.padding = new RectOffset(10, 10, 0, 0);

        // ── 5. מדבקות (Stickers) — NO scroll, grid fills remaining space ──
        SectionTitle(panelGO.transform, "\u05DE\u05D3\u05D1\u05E7\u05D5\u05EA");

        var stickerContentGO = new GameObject("StickerGrid");
        stickerContentGO.transform.SetParent(panelGO.transform, false);
        stickerContentGO.AddComponent<RectTransform>();
        var stickerContentLE = stickerContentGO.AddComponent<LayoutElement>();
        stickerContentLE.flexibleHeight = 1; // fill remaining space
        stickerContentLE.flexibleWidth = 1;

        var stickerGrid = stickerContentGO.AddComponent<GridLayoutGroup>();
        stickerGrid.cellSize = new Vector2(StickerSize, StickerSize);
        stickerGrid.spacing = new Vector2(8, 8);
        stickerGrid.childAlignment = TextAnchor.UpperCenter;
        stickerGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        stickerGrid.constraintCount = 9;
        stickerGrid.padding = new RectOffset(0, 0, 0, 0);

        // ═══════════════════════════════════
        //  LOAD ASSETS
        // ═══════════════════════════════════

        var brushSmall  = LoadSprite("Assets/Art/Brushes/Small Brush.png");
        var brushMedium = LoadSprite("Assets/Art/Brushes/Medium Brush.png");
        var brushBig    = LoadSprite("Assets/Art/Brushes/Big Brush.png");

        // Load all sliced sprites from sticker sprite sheet
        var stickerSprites = new List<Sprite>();
        var allStickerAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Stickers/Sticker.png");
        if (allStickerAssets != null)
        {
            foreach (var asset in allStickerAssets)
            {
                if (asset is Sprite spr)
                    stickerSprites.Add(spr);
            }
        }
        // Sort by name for consistent order (Sticker_0, Sticker_1, ..., Sticker_15)
        stickerSprites.Sort((a, b) =>
        {
            int numA = 0, numB = 0;
            var partsA = a.name.Split('_');
            var partsB = b.name.Split('_');
            if (partsA.Length > 1) int.TryParse(partsA[partsA.Length - 1], out numA);
            if (partsB.Length > 1) int.TryParse(partsB[partsB.Length - 1], out numB);
            return numA.CompareTo(numB);
        });

        // ═══════════════════════════════════
        //  CONTROLLER
        // ═══════════════════════════════════

        var ctrl = canvasGO.AddComponent<ColoringGameController>();
        ctrl.drawingCanvas = drawCanvas;
        ctrl.outlineImage = outlineRaw;
        ctrl.referenceImage = refImg;
        ctrl.referenceContainer = refBgGO;
        ctrl.colorButtonContainer = paletteGO.transform;
        ctrl.brushSizeContainer = brushGO.transform;
        ctrl.colorButtonPrefab = colorBtnPrefab;
        ctrl.brushSizeButtonPrefab = brushBtnPrefab;
        ctrl.eraserButton = eraserGO.GetComponent<Button>();
        ctrl.undoButton = undoGO.GetComponent<Button>();
        ctrl.clearButton = clearGO.GetComponent<Button>();
        ctrl.eraserHighlight = eraserHLImg;
        ctrl.saveDrawingButton = saveGO.GetComponent<Button>();
        ctrl.doneButton = doneGO.GetComponent<Button>();
        ctrl.brushIcons = new Sprite[] { brushSmall, brushMedium, brushBig };
        ctrl.stickerContainer = stickerContentGO.transform;
        ctrl.stickerSprites = stickerSprites.ToArray();

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = LoadSprite("Assets/UI/Sprites/RoundedRect.png");
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "coloring";

        // Tutorial hand
        TutorialHandHelper.Create(safeGO.transform, TutorialHandHelper.Anim.Tap,
            new Vector2(0, -50), new Vector2(450, 450), "coloring");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ColoringGame.unity");
    }

    // ─────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────

    private static void SectionTitle(Transform parent, string hebrewText)
    {
        var go = new GameObject("SectionTitle");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = SectionTitleFontSize + 8;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(tmp, hebrewText);
        tmp.fontSize = SectionTitleFontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = SectionTitleColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    /// <summary>Tool button for the top row (eraser, undo, clear).</summary>
    private static GameObject ToolButton(Transform parent, string name, Sprite icon, Sprite bg)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(ToolBtnSize, ToolBtnSize);
        var img = go.AddComponent<Image>();
        img.sprite = bg; img.type = Image.Type.Sliced;
        img.color = ActionBtnBg; img.raycastTarget = true;
        go.AddComponent<Button>().targetGraphic = img;

        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        var iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.15f, 0.15f); iconRT.anchorMax = new Vector2(0.85f, 0.85f);
        iconRT.offsetMin = Vector2.zero; iconRT.offsetMax = Vector2.zero;
        var ii = iconGO.AddComponent<Image>();
        ii.sprite = icon; ii.preserveAspect = true;
        ii.color = new Color(0.2f, 0.2f, 0.2f, 1f); ii.raycastTarget = false;
        return go;
    }

    private static GameObject IconBtn(Transform p, string name, Sprite icon,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = icon; img.preserveAspect = true; img.color = Color.white; img.raycastTarget = true;
        go.AddComponent<Button>().targetGraphic = img;
        return go;
    }

    private static GameObject StretchImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Full(go.AddComponent<RectTransform>());
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static void Full(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    private static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        if (all != null) foreach (var o in all) if (o is Sprite sp) return sp;
        return null;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c); return c;
    }
}
