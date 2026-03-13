using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Builds the ColoringGame scene, palette prefabs, and updates coloring data.
/// Run via Tools > Kids Learning Game > Setup Coloring Game.
/// </summary>
public class ColoringGameSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1080, 1920);

    // Colors
    private static readonly Color BgColor       = HexColor("#FFF8F0");  // warm cream
    private static readonly Color TopBarColor   = HexColor("#F8E8D8");  // light peach
    private static readonly Color BottomColor   = HexColor("#FAFAFA");  // near white
    private static readonly Color ToolBtnColor  = HexColor("#F0F0F0");
    private static readonly Color CanvasBorder  = HexColor("#E0D5C8");  // subtle warm border

    // Layout — sized for kids' fingers
    private const int TopBarHeight = 130;
    private const int PaletteHeight = 110;    // single scrollable row of big color circles
    private const int BrushBarHeight = 80;
    private const int CanvasPad = 16;
    private const int RefImageSize = 220;     // colored reference sprite preview (1.5x bigger)
    private const int ColorCircleSize = 86;   // big color circles for kids

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Coloring Game Setup", "Creating prefabs…", 0.1f);
            var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
            var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");

            var colorBtnPrefab = CreateColorButtonPrefab(circleSprite);
            var brushBtnPrefab = CreateBrushSizeButtonPrefab(roundedRect, circleSprite);

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
        if (coloring == null)
        {
            Debug.LogError("Coloring.asset not found. Run Setup Project first.");
            return;
        }

        // Add "Free Draw" as first sub-item if not already present
        bool hasFree = false;
        foreach (var item in coloring.subItems)
        {
            if (item.categoryKey == "free") { hasFree = true; break; }
        }

        if (!hasFree)
        {
            coloring.subItems.Insert(0, new SubItemData
            {
                id = "coloring_free",
                title = "\u05D3\u05E3 \u05D7\u05D3\u05E9", // דף חדש (New Page)
                cardColor = HexColor("#FFD93D"),
                categoryKey = "free",
                targetSceneName = "ColoringGame"
            });
        }

        // Add "Selfie" option if not already present
        bool hasSelfie = false;
        foreach (var item in coloring.subItems)
        {
            if (item.categoryKey == "selfie") { hasSelfie = true; break; }
        }

        if (!hasSelfie)
        {
            var cameraIcon = LoadSprite("Assets/Art/Camera.png");
            int insertIdx = 1;
            coloring.subItems.Insert(insertIdx, new SubItemData
            {
                id = "coloring_selfie",
                title = "\u05E1\u05DC\u05E4\u05D9", // סלפי (Selfie)
                cardColor = HexColor("#F472B6"),
                categoryKey = "selfie",
                targetSceneName = "ColoringGame",
                thumbnail = cameraIcon
            });
        }

        // Assign contentAsset sprites to each animal sub-item
        string[] spriteNames = {
            "Bear", "Bird", "Cat", "Chicken", "Cow", "Dog", "Donkey", "Duck",
            "Elephant", "Fish", "Frog", "Giraffe", "Horse", "Lion", "Monkey",
            "Sheep", "Snake", "Turtle", "Zebra"
        };

        foreach (var item in coloring.subItems)
        {
            if (item.categoryKey == "free" || item.categoryKey == "selfie") continue;

            string name = char.ToUpper(item.categoryKey[0]) + item.categoryKey.Substring(1);

            // Use puzzle Main image for both painting and outline generation
            string mainPath = $"Assets/Art/Animals/{name}/Art/Puzzle/{name} Main.png";
            var sprite = LoadSprite(mainPath);
            if (sprite != null)
            {
                item.contentAsset = sprite;
                item.thumbnail = sprite;
            }
        }

        EditorUtility.SetDirty(coloring);
    }

    // ─────────────────────────────────────────
    //  PREFABS
    // ─────────────────────────────────────────

    /// <summary>Color palette button: circle with a selection ring child.</summary>
    private static GameObject CreateColorButtonPrefab(Sprite circleSprite)
    {
        EnsureFolder("Assets/Prefabs/UI");

        var root = new GameObject("ColorButton");
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(ColorCircleSize, ColorCircleSize);

        var img = root.AddComponent<Image>();
        img.sprite = circleSprite;
        img.color = Color.red; // placeholder, set at runtime
        img.raycastTarget = true;

        root.AddComponent<Button>().targetGraphic = img;

        // Selection ring (hidden by default)
        var ring = new GameObject("Ring");
        ring.transform.SetParent(root.transform, false);
        var ringRT = ring.AddComponent<RectTransform>();
        ringRT.anchorMin = Vector2.zero;
        ringRT.anchorMax = Vector2.one;
        ringRT.offsetMin = new Vector2(-6, -6);
        ringRT.offsetMax = new Vector2(6, 6);
        var ringImg = ring.AddComponent<Image>();
        ringImg.sprite = circleSprite;
        ringImg.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        ringImg.raycastTarget = false;
        ring.SetActive(false);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/UI/ColorButton.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Brush size button: rounded rect with a dot inside showing relative size.</summary>
    private static GameObject CreateBrushSizeButtonPrefab(Sprite roundedRect, Sprite circleSprite)
    {
        EnsureFolder("Assets/Prefabs/UI");

        var root = new GameObject("BrushSizeButton");
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(68, 68);

        var img = root.AddComponent<Image>();
        img.sprite = roundedRect;
        img.type = Image.Type.Sliced;
        img.color = new Color(1f, 1f, 1f, 0.5f);
        img.raycastTarget = true;

        root.AddComponent<Button>().targetGraphic = img;

        // Size indicator dot
        var dot = new GameObject("Dot");
        dot.transform.SetParent(root.transform, false);
        var dotRT = dot.AddComponent<RectTransform>();
        dotRT.anchorMin = new Vector2(0.5f, 0.5f);
        dotRT.anchorMax = new Vector2(0.5f, 0.5f);
        dotRT.sizeDelta = new Vector2(16, 16); // set at runtime
        var dotImg = dot.AddComponent<Image>();
        dotImg.sprite = circleSprite;
        dotImg.color = new Color(0.25f, 0.25f, 0.25f, 1f);
        dotImg.raycastTarget = false;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/UI/BrushSizeButton.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ─────────────────────────────────────────
    //  SCENE
    // ─────────────────────────────────────────

    private static void CreateColoringScene(Sprite roundedRect, Sprite circleSprite,
        GameObject colorBtnPrefab, GameObject brushBtnPrefab)
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgColor;
        cam.orthographic = true;
        var urpType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpType != null) camGO.AddComponent(urpType);

        // EventSystem
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inputType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputType != null) esGO.AddComponent(inputType);
        else esGO.AddComponent<StandaloneInputModule>();

        // Canvas
        var canvasGO = new GameObject("ColoringCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        var bg = CreateStretchImage(canvasGO.transform, "Background", BgColor);
        bg.GetComponent<Image>().raycastTarget = false;

        // Safe area
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        StretchFull(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // ── TOP BAR ──
        var topBar = CreateStretchImage(safeArea.transform, "TopBar", TopBarColor);
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.sizeDelta = new Vector2(0, TopBarHeight);
        topBar.GetComponent<Image>().raycastTarget = false;
        topBar.AddComponent<ThemeHeader>();

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        StretchFull(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = HebrewFixer.Fix("\u05E6\u05D1\u05D9\u05E2\u05D4"); // צביעה
        titleTMP.isRightToLeftText = false;
        titleTMP.fontSize = 48;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button (top-left)
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -15), new Vector2(90, 90));

        // Undo button (top-right area) — return arrow icon
        var undoIcon = LoadSprite("Assets/Art/Icons/return.png");
        var undoGO = CreateIconButton(topBar.transform, "UndoButton", undoIcon,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-120, -18), new Vector2(70, 70));
        undoGO.GetComponent<Image>().color = Color.black;

        // Save button (top-right, before undo/clear) — save icon
        var saveIcon = LoadSprite("Assets/Art/Icons/save.png");
        var saveGO = CreateIconButton(topBar.transform, "SaveButton", saveIcon,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-210, -18), new Vector2(70, 70));
        saveGO.GetComponent<Image>().color = Color.black;

        // Clear button (top-right) — trashcan icon
        var trashIcon = LoadSprite("Assets/Art/Icons/trashcan.png");
        var clearGO = CreateIconButton(topBar.transform, "ClearButton", trashIcon,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -18), new Vector2(70, 70));
        clearGO.GetComponent<Image>().color = Color.black;

        // ── COLOR PALETTE (scrollable horizontal row, ABOVE the canvas) ──
        var paletteBar = new GameObject("PaletteBar");
        paletteBar.transform.SetParent(safeArea.transform, false);
        var paletteBarRT = paletteBar.AddComponent<RectTransform>();
        paletteBarRT.anchorMin = new Vector2(0, 1);
        paletteBarRT.anchorMax = new Vector2(1, 1);
        paletteBarRT.pivot = new Vector2(0.5f, 1);
        paletteBarRT.anchoredPosition = new Vector2(0, -TopBarHeight);
        paletteBarRT.sizeDelta = new Vector2(0, PaletteHeight);

        // Palette background (child, behind everything)
        var paletteBgGO = CreateStretchImage(paletteBar.transform, "PaletteBg", BottomColor);
        paletteBgGO.GetComponent<Image>().raycastTarget = false;

        // Viewport (masks the scrollable content)
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(paletteBar.transform, false);
        var viewportRT = viewport.AddComponent<RectTransform>();
        StretchFull(viewportRT);
        var viewportImg = viewport.AddComponent<Image>();
        viewportImg.color = new Color(1, 1, 1, 0); // transparent but needed for Mask
        viewportImg.raycastTarget = true;
        viewport.AddComponent<RectMask2D>(); // clips children without needing an opaque image

        // Scrollable content with HorizontalLayout
        var colorContainer = new GameObject("Colors");
        colorContainer.transform.SetParent(viewport.transform, false);
        var colorContainerRT = colorContainer.AddComponent<RectTransform>();
        colorContainerRT.anchorMin = new Vector2(0, 0);
        colorContainerRT.anchorMax = new Vector2(0, 1);
        colorContainerRT.pivot = new Vector2(0, 0.5f);
        colorContainerRT.sizeDelta = new Vector2(1200, 0); // initial width, grows with ContentSizeFitter

        var colorLayout = colorContainer.AddComponent<HorizontalLayoutGroup>();
        colorLayout.spacing = 12;
        colorLayout.childAlignment = TextAnchor.MiddleCenter;
        colorLayout.childForceExpandWidth = false;
        colorLayout.childForceExpandHeight = false;
        colorLayout.padding = new RectOffset(16, 16, 8, 8);

        var colorFitter = colorContainer.AddComponent<ContentSizeFitter>();
        colorFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        colorFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        // ScrollRect on the palette bar itself
        var paletteScroll = paletteBar.AddComponent<ScrollRect>();
        paletteScroll.horizontal = true;
        paletteScroll.vertical = false;
        paletteScroll.movementType = ScrollRect.MovementType.Elastic;
        paletteScroll.elasticity = 0.1f;
        paletteScroll.scrollSensitivity = 20f;
        paletteScroll.content = colorContainerRT;
        paletteScroll.viewport = viewportRT;

        // ── BRUSH BAR (under palette, above canvas) ──
        var brushBar = new GameObject("BrushBar");
        brushBar.transform.SetParent(safeArea.transform, false);
        var brushBarRT = brushBar.AddComponent<RectTransform>();
        brushBarRT.anchorMin = new Vector2(0, 1);
        brushBarRT.anchorMax = new Vector2(1, 1);
        brushBarRT.pivot = new Vector2(0.5f, 1);
        brushBarRT.anchoredPosition = new Vector2(0, -(TopBarHeight + PaletteHeight));
        brushBarRT.sizeDelta = new Vector2(0, BrushBarHeight);

        var brushLayout = brushBar.AddComponent<HorizontalLayoutGroup>();
        brushLayout.spacing = 16;
        brushLayout.childAlignment = TextAnchor.MiddleCenter;
        brushLayout.childForceExpandWidth = false;
        brushLayout.childForceExpandHeight = false;
        brushLayout.padding = new RectOffset(24, 24, 4, 4);

        // Eraser button — phone icon
        var phoneIcon = LoadSprite("Assets/Art/Icons/phone.png");
        var eraserGO = new GameObject("EraserButton");
        eraserGO.transform.SetParent(brushBar.transform, false);
        var eraserRT = eraserGO.AddComponent<RectTransform>();
        eraserRT.sizeDelta = new Vector2(56, 56);
        var eraserImg = eraserGO.AddComponent<Image>();
        eraserImg.sprite = phoneIcon;
        eraserImg.preserveAspect = true;
        eraserImg.color = Color.black;
        eraserImg.raycastTarget = true;
        eraserGO.AddComponent<Button>().targetGraphic = eraserImg;
        var eraserHL = new GameObject("EraserHighlight");
        eraserHL.transform.SetParent(eraserGO.transform, false);
        var eraserHLRT = eraserHL.AddComponent<RectTransform>();
        StretchFull(eraserHLRT);
        eraserHLRT.offsetMin = new Vector2(-4, -4);
        eraserHLRT.offsetMax = new Vector2(4, 4);
        var eraserHLImg = eraserHL.AddComponent<Image>();
        eraserHLImg.sprite = roundedRect;
        eraserHLImg.type = Image.Type.Sliced;
        eraserHLImg.color = new Color(0.9f, 0.3f, 0.3f, 0.4f);
        eraserHLImg.raycastTarget = false;
        eraserHL.SetActive(false);

        // Spacer
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(brushBar.transform, false);
        var spacerLE = spacer.AddComponent<LayoutElement>();
        spacerLE.flexibleWidth = 1;

        // Brush size buttons container
        var brushContainer = new GameObject("BrushSizes");
        brushContainer.transform.SetParent(brushBar.transform, false);
        var brushContainerRT = brushContainer.AddComponent<RectTransform>();
        brushContainerRT.sizeDelta = new Vector2(250, BrushBarHeight);
        var brushContainerLayout = brushContainer.AddComponent<HorizontalLayoutGroup>();
        brushContainerLayout.spacing = 14;
        brushContainerLayout.childAlignment = TextAnchor.MiddleCenter;
        brushContainerLayout.childForceExpandWidth = false;
        brushContainerLayout.childForceExpandHeight = false;

        // ── DRAWING AREA (below brush bar, fills remaining space) ──
        int topOffset = TopBarHeight + PaletteHeight + BrushBarHeight;
        var canvasFrame = CreateStretchImage(safeArea.transform, "CanvasFrame", CanvasBorder);
        var canvasFrameRT = canvasFrame.GetComponent<RectTransform>();
        StretchFull(canvasFrameRT);
        canvasFrameRT.offsetMax = new Vector2(-CanvasPad, -(topOffset + 4));
        canvasFrameRT.offsetMin = new Vector2(CanvasPad, 8);
        canvasFrame.GetComponent<Image>().raycastTarget = false;

        // White canvas background
        var canvasBg = CreateStretchImage(canvasFrame.transform, "CanvasWhite", Color.white);
        var canvasBgRT = canvasBg.GetComponent<RectTransform>();
        StretchFull(canvasBgRT);
        canvasBgRT.offsetMin = new Vector2(4, 4);
        canvasBgRT.offsetMax = new Vector2(-4, -4);
        canvasBg.GetComponent<Image>().raycastTarget = false;

        // Drawing layer (RawImage + DrawingCanvas)
        var drawGO = new GameObject("DrawingLayer");
        drawGO.transform.SetParent(canvasBg.transform, false);
        var drawRT = drawGO.AddComponent<RectTransform>();
        StretchFull(drawRT);
        var drawRaw = drawGO.AddComponent<RawImage>();
        drawRaw.color = Color.white;
        var drawCanvas = drawGO.AddComponent<DrawingCanvas>();

        // Outline overlay (for coloring mode, on top of drawing)
        var outlineGO = new GameObject("OutlineOverlay");
        outlineGO.transform.SetParent(canvasBg.transform, false);
        var outlineRT = outlineGO.AddComponent<RectTransform>();
        StretchFull(outlineRT);
        var outlineRaw = outlineGO.AddComponent<RawImage>();
        outlineRaw.color = Color.white;
        outlineRaw.raycastTarget = false;
        outlineGO.SetActive(false);

        // ── REFERENCE IMAGE (colored sprite, TOP-RIGHT overlaid on canvas) ──
        var refBgGO = new GameObject("RefBackground");
        refBgGO.transform.SetParent(safeArea.transform, false);
        var refBgRT = refBgGO.AddComponent<RectTransform>();
        refBgRT.anchorMin = new Vector2(1, 1);
        refBgRT.anchorMax = new Vector2(1, 1);
        refBgRT.pivot = new Vector2(1, 1);
        refBgRT.anchoredPosition = new Vector2(-CanvasPad, -(topOffset + 8));
        refBgRT.sizeDelta = new Vector2(RefImageSize + 16, RefImageSize + 16);
        var refBgImg = refBgGO.AddComponent<Image>();
        refBgImg.sprite = roundedRect;
        refBgImg.type = Image.Type.Sliced;
        refBgImg.color = new Color(1f, 1f, 1f, 0.92f);
        refBgImg.raycastTarget = false;
        refBgGO.SetActive(false);

        // Colored sprite on top
        var refGO = new GameObject("ReferenceSprite");
        refGO.transform.SetParent(refBgGO.transform, false);
        var refRT = refGO.AddComponent<RectTransform>();
        StretchFull(refRT);
        refRT.offsetMin = new Vector2(8, 8);
        refRT.offsetMax = new Vector2(-8, -8);
        var refImg = refGO.AddComponent<Image>();
        refImg.preserveAspect = true;
        refImg.raycastTarget = false;
        refImg.color = Color.white;

        // ── CONTROLLER ──
        var controller = canvasGO.AddComponent<ColoringGameController>();
        controller.drawingCanvas = drawCanvas;
        controller.outlineImage = outlineRaw;
        controller.referenceImage = refImg;
        controller.referenceContainer = refBgGO;
        controller.colorButtonContainer = colorContainerRT;
        controller.brushSizeContainer = brushContainerRT;
        controller.colorButtonPrefab = colorBtnPrefab;
        controller.brushSizeButtonPrefab = brushBtnPrefab;
        controller.eraserButton = eraserGO.GetComponent<Button>();
        controller.undoButton = undoGO.GetComponent<Button>();
        controller.clearButton = clearGO.GetComponent<Button>();
        controller.eraserHighlight = eraserHLImg;
        controller.saveDrawingButton = saveGO.GetComponent<Button>();

        // Wire home button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ColoringGame.unity");
    }

    // ─────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────

    private static GameObject CreateStretchImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        StretchFull(rt);
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static GameObject CreateIconButton(Transform parent, string name, Sprite icon,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        return go;
    }

    private static GameObject CreateIconToolButton(Transform parent, string name, Sprite icon,
        Sprite bg, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 size, bool useAnchors = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        if (useAnchors)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
        }
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.sprite = bg;
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        // Icon child (black tint)
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        var iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.15f, 0.15f);
        iconRT.anchorMax = new Vector2(0.85f, 0.85f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = icon;
        iconImg.preserveAspect = true;
        iconImg.color = Color.black;
        iconImg.raycastTarget = false;

        return go;
    }

    private static GameObject CreateToolButton(Transform parent, string name, string label,
        Sprite bg, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 size, bool useAnchors = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        if (useAnchors)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
        }
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.sprite = bg;
        img.type = Image.Type.Sliced;
        img.color = ToolBtnColor;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        StretchFull(labelRT);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = HexColor("#555555");
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;

        var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (allAssets != null)
        {
            foreach (var asset in allAssets)
                if (asset is Sprite s) return s;
        }
        return null;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
