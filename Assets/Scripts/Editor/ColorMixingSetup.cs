using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the ColorMixing scene for landscape orientation (1920×1080).
/// Art-studio aesthetic: canvas texture background, watercolor accents,
/// wooden painter's palette, and glass containers for mixing.
///
/// Layout (top to bottom):
///   Header (80px) — home button + title
///   Target area — large paint blob with glow + label
///   Containers — two small containers + plus → arrow → large mixing container
///   Painter palette — wooden kidney-shaped palette with 4 paint blobs
///
/// Run via Tools > Kids Learning Game > Setup Color Mixing.
/// </summary>
public class ColorMixingSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    // ── Colors ──────────────────────────────────────────────────────
    // Background — soft pastel blue watercolor paper
    private static readonly Color BgBase       = HexColor("#D6E6F0"); // soft pastel blue
    private static readonly Color CanvasCenter = HexColor("#E4EFF7"); // lighter blue center
    private static readonly Color CanvasEdge   = HexColor("#B8CFDF"); // darker blue edge
    private static readonly Color CanvasStitch = new Color(0.60f, 0.72f, 0.80f, 0.10f); // faint blue-grey threads

    // Header
    private static readonly Color HeaderColor  = new Color(0.82f, 0.72f, 0.86f, 0.80f);
    private static readonly Color AccentPurple = HexColor("#7B1FA2");

    // Palette wood
    private static readonly Color WoodBase     = HexColor("#C4A882"); // warm light wood
    private static readonly Color WoodDark     = HexColor("#A88B68"); // darker wood accent
    private static readonly Color WoodLight    = HexColor("#D9C5A8"); // wood highlight
    private static readonly Color WoodGrain    = new Color(0.62f, 0.50f, 0.38f, 0.12f);
    private static readonly Color WoodRim      = HexColor("#8B7355"); // palette edge
    private static readonly Color ThumbHole    = new Color(0.78f, 0.85f, 0.90f, 0.90f); // hole shows blue bg through

    // Paint wells (small depressions on palette)
    private static readonly Color WellColor    = new Color(0.58f, 0.48f, 0.38f, 0.18f);

    // Containers — stronger visibility against blue background
    private static readonly Color ContainerGlass  = new Color(0.92f, 0.96f, 1.00f, 0.55f); // brighter translucent glass
    private static readonly Color ContainerRim    = new Color(0.50f, 0.58f, 0.70f, 0.75f); // darker glass rim
    private static readonly Color ContainerEdge   = new Color(0.42f, 0.50f, 0.62f, 0.40f); // dark glass edge/outline
    private static readonly Color ContainerShadow = new Color(0.20f, 0.25f, 0.35f, 0.18f); // stronger shadow
    private static readonly Color SymbolColor    = new Color(0.55f, 0.45f, 0.65f, 0.55f);

    // Watercolor accents (very faint)
    private static readonly Color WcRed    = new Color(0.95f, 0.35f, 0.30f, 0.07f);
    private static readonly Color WcBlue   = new Color(0.35f, 0.50f, 0.95f, 0.06f);
    private static readonly Color WcYellow = new Color(1.00f, 0.85f, 0.25f, 0.07f);
    private static readonly Color WcGreen  = new Color(0.40f, 0.82f, 0.45f, 0.05f);
    private static readonly Color WcPink   = new Color(0.95f, 0.60f, 0.68f, 0.06f);
    private static readonly Color WcPurple = new Color(0.68f, 0.48f, 0.82f, 0.05f);
    private static readonly Color WcOrange = new Color(1.00f, 0.65f, 0.20f, 0.05f);

    private const int HeaderHeight = 80;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Color Mixing Setup", "Building scene…", 0.5f);
            var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
            var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");
            CreateScene(roundedRect, circleSprite);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ═════════════════════════════════════════
    //  SCENE
    // ═════════════════════════════════════════

    private static void CreateScene(Sprite roundedRect, Sprite circleSprite)
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgBase;
        cam.orthographic = true;
        var urpType = System.Type.GetType(
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpType != null) camGO.AddComponent(urpType);

        // EventSystem
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inputType = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputType != null) esGO.AddComponent(inputType);
        else esGO.AddComponent<StandaloneInputModule>();

        // Canvas — LANDSCAPE
        var canvasGO = new GameObject("ColorMixingCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ═══════════════════════════════════
        //  BACKGROUND — Canvas texture
        // ═══════════════════════════════════

        CreateCanvasBackground(canvasGO.transform, roundedRect, circleSprite);

        // Safe area
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        StretchFull(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // ═══════════════════════════════════
        //  HEADER (80px)
        // ═══════════════════════════════════

        var topBar = CreateStretchImage(safeArea.transform, "TopBar", HeaderColor);
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.sizeDelta = new Vector2(0, HeaderHeight);
        topBar.GetComponent<Image>().raycastTarget = false;
        topBar.AddComponent<ThemeHeader>();

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        StretchFull(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = HebrewFixer.Fix("\u05E2\u05E8\u05D1\u05D5\u05D1 \u05E6\u05D1\u05E2\u05D9\u05DD");
        titleTMP.isRightToLeftText = false;
        titleTMP.fontSize = 42;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(16, -8), new Vector2(64, 64));

        // ═══════════════════════════════════
        //  PLAY AREA
        // ═══════════════════════════════════

        var playArea = new GameObject("PlayArea");
        playArea.transform.SetParent(safeArea.transform, false);
        var playAreaRT = playArea.AddComponent<RectTransform>();
        StretchFull(playAreaRT);
        playAreaRT.offsetMax = new Vector2(0, -HeaderHeight);
        playAreaRT.offsetMin = Vector2.zero;

        // ── TARGET COLOR AREA ────────────────────────────

        var targetLabel = new GameObject("TargetLabel");
        targetLabel.transform.SetParent(playArea.transform, false);
        var targetLabelRT = targetLabel.AddComponent<RectTransform>();
        targetLabelRT.anchorMin = new Vector2(0.3f, 0.84f);
        targetLabelRT.anchorMax = new Vector2(0.7f, 0.97f);
        targetLabelRT.offsetMin = Vector2.zero;
        targetLabelRT.offsetMax = Vector2.zero;
        var labelTMP = targetLabel.AddComponent<TextMeshProUGUI>();
        labelTMP.text = HebrewFixer.Fix("!\u05E6\u05E8\u05D5 \u05D0\u05EA \u05D4\u05E6\u05D1\u05E2 \u05D4\u05D6\u05D4");
        labelTMP.isRightToLeftText = false;
        labelTMP.fontSize = 34;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.color = AccentPurple;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.raycastTarget = false;

        // Outer glow
        var targetOuterGlow = new GameObject("TargetOuterGlow");
        targetOuterGlow.transform.SetParent(playArea.transform, false);
        var togRT = targetOuterGlow.AddComponent<RectTransform>();
        togRT.anchorMin = new Vector2(0.5f, 0.84f);
        togRT.anchorMax = new Vector2(0.5f, 0.84f);
        togRT.pivot = new Vector2(0.5f, 0.5f);
        togRT.anchoredPosition = new Vector2(0, -90);
        togRT.sizeDelta = new Vector2(200, 200);
        var togImg = targetOuterGlow.AddComponent<Image>();
        if (circleSprite != null) togImg.sprite = circleSprite;
        togImg.color = new Color(1f, 1f, 1f, 0.12f);
        togImg.raycastTarget = false;

        // Inner glow
        var targetGlow = new GameObject("TargetGlow");
        targetGlow.transform.SetParent(playArea.transform, false);
        var tgRT = targetGlow.AddComponent<RectTransform>();
        tgRT.anchorMin = new Vector2(0.5f, 0.84f);
        tgRT.anchorMax = new Vector2(0.5f, 0.84f);
        tgRT.pivot = new Vector2(0.5f, 0.5f);
        tgRT.anchoredPosition = new Vector2(0, -90);
        tgRT.sizeDelta = new Vector2(165, 165);
        var tgImg = targetGlow.AddComponent<Image>();
        if (circleSprite != null) tgImg.sprite = circleSprite;
        tgImg.color = new Color(1f, 1f, 1f, 0.22f);
        tgImg.raycastTarget = false;

        // Target circle
        var targetGO = new GameObject("TargetCircle");
        targetGO.transform.SetParent(targetGlow.transform, false);
        var targetRT = targetGO.AddComponent<RectTransform>();
        targetRT.anchorMin = new Vector2(0.5f, 0.5f);
        targetRT.anchorMax = new Vector2(0.5f, 0.5f);
        targetRT.sizeDelta = new Vector2(130, 130);
        var targetImg = targetGO.AddComponent<Image>();
        if (circleSprite != null) targetImg.sprite = circleSprite;
        targetImg.color = Color.white;
        targetImg.raycastTarget = false;

        CreateShine(targetGO.transform, circleSprite, 0.30f);

        // Target shadow
        var targetShadow = new GameObject("TargetShadow");
        targetShadow.transform.SetParent(targetGlow.transform, false);
        var tsRT = targetShadow.AddComponent<RectTransform>();
        tsRT.anchorMin = new Vector2(0.5f, 0.5f);
        tsRT.anchorMax = new Vector2(0.5f, 0.5f);
        tsRT.anchoredPosition = new Vector2(3, -4);
        tsRT.sizeDelta = new Vector2(130, 130);
        var tsImg = targetShadow.AddComponent<Image>();
        if (circleSprite != null) tsImg.sprite = circleSprite;
        tsImg.color = new Color(0f, 0f, 0f, 0.08f);
        tsImg.raycastTarget = false;
        targetShadow.transform.SetAsFirstSibling();

        // ── CONTAINER MIXING AREA ────────────────────────

        var containersArea = new GameObject("ContainersArea");
        containersArea.transform.SetParent(playArea.transform, false);
        var containersAreaRT = containersArea.AddComponent<RectTransform>();
        containersAreaRT.anchorMin = new Vector2(0.5f, 0.38f);
        containersAreaRT.anchorMax = new Vector2(0.5f, 0.38f);
        containersAreaRT.sizeDelta = new Vector2(750, 400);

        // Small containers — glass beakers with fill area
        float smallW = 95f, smallH = 130f;
        float containerSpacing = 155f;

        var containerLeftGO = CreateContainer(containersArea.transform, "ContainerLeft",
            roundedRect, circleSprite, smallW, smallH, new Vector2(-containerSpacing, 55));
        var containerRightGO = CreateContainer(containersArea.transform, "ContainerRight",
            roundedRect, circleSprite, smallW, smallH, new Vector2(containerSpacing, 55));

        var containerLeftBodyImg = containerLeftGO.transform.Find("Body").GetComponent<Image>();
        var containerLeftFillImg = containerLeftGO.transform.Find("Body/Fill").GetComponent<Image>();
        var containerRightBodyImg = containerRightGO.transform.Find("Body").GetComponent<Image>();
        var containerRightFillImg = containerRightGO.transform.Find("Body/Fill").GetComponent<Image>();

        // Plus sign between small containers
        var plusGO = new GameObject("PlusSign");
        plusGO.transform.SetParent(containersArea.transform, false);
        var plusRT = plusGO.AddComponent<RectTransform>();
        plusRT.anchorMin = new Vector2(0.5f, 0.5f);
        plusRT.anchorMax = new Vector2(0.5f, 0.5f);
        plusRT.anchoredPosition = new Vector2(0, 55);
        plusRT.sizeDelta = new Vector2(60, 60);
        var plusTMP = plusGO.AddComponent<TextMeshProUGUI>();
        plusTMP.text = "+";
        plusTMP.fontSize = 52;
        plusTMP.fontStyle = FontStyles.Bold;
        plusTMP.color = SymbolColor;
        plusTMP.alignment = TextAlignmentOptions.Center;
        plusTMP.raycastTarget = false;

        // Down arrow
        var arrowGO = new GameObject("ArrowDown");
        arrowGO.transform.SetParent(containersArea.transform, false);
        var arrowRT = arrowGO.AddComponent<RectTransform>();
        arrowRT.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRT.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRT.anchoredPosition = new Vector2(0, -15);
        arrowRT.sizeDelta = new Vector2(60, 40);
        var arrowTMP = arrowGO.AddComponent<TextMeshProUGUI>();
        arrowTMP.text = "\u25BC";
        arrowTMP.fontSize = 32;
        arrowTMP.color = SymbolColor;
        arrowTMP.alignment = TextAlignmentOptions.Center;
        arrowTMP.raycastTarget = false;

        // Large mixing container — glow ring behind, then body+fill
        float mixW = 140f, mixH = 180f;

        // Glow ring (behind mixing container)
        var mixGlow = new GameObject("MixContainerGlow");
        mixGlow.transform.SetParent(containersArea.transform, false);
        var mgRT = mixGlow.AddComponent<RectTransform>();
        mgRT.anchorMin = new Vector2(0.5f, 0.5f);
        mgRT.anchorMax = new Vector2(0.5f, 0.5f);
        mgRT.anchoredPosition = new Vector2(0, -115);
        mgRT.sizeDelta = new Vector2(mixW + 40f, mixH + 40f);
        var mgImg = mixGlow.AddComponent<Image>();
        if (circleSprite != null) mgImg.sprite = circleSprite;
        mgImg.color = new Color(1f, 1f, 1f, 0f);
        mgImg.raycastTarget = false;

        var mixContainerGO = CreateContainer(containersArea.transform, "MixContainer",
            roundedRect, circleSprite, mixW, mixH, new Vector2(0, -115));

        var mixContainerBodyImg = mixContainerGO.transform.Find("Body").GetComponent<Image>();
        var mixContainerFillImg = mixContainerGO.transform.Find("Body/Fill").GetComponent<Image>();

        // ═══════════════════════════════════
        //  PAINTER'S PALETTE
        // ═══════════════════════════════════

        CreatePainterPalette(playArea.transform, circleSprite, roundedRect);

        // Color palette buttons container (positioned over the palette surface)
        var palette = new GameObject("ColorPalette");
        palette.transform.SetParent(playArea.transform, false);
        var paletteRT = palette.AddComponent<RectTransform>();
        paletteRT.anchorMin = new Vector2(0.5f, 0.0f);
        paletteRT.anchorMax = new Vector2(0.5f, 0.0f);
        paletteRT.pivot = new Vector2(0.5f, 0);
        paletteRT.anchoredPosition = new Vector2(30, 25);
        paletteRT.sizeDelta = new Vector2(1000, 140);

        // ═══════════════════════════════════
        //  CONTROLLER
        // ═══════════════════════════════════

        var controller = canvasGO.AddComponent<ColorMixingController>();
        controller.playArea = playAreaRT;
        controller.targetColorCircle = targetImg;
        controller.targetGlowImage = tgImg;
        controller.targetOuterGlowImage = togImg;
        controller.containerLeftBody = containerLeftBodyImg;
        controller.containerLeftFill = containerLeftFillImg;
        controller.containerRightBody = containerRightBodyImg;
        controller.containerRightFill = containerRightFillImg;
        controller.mixContainerBody = mixContainerBodyImg;
        controller.mixContainerFill = mixContainerFillImg;
        controller.mixContainerGlow = mgImg;
        controller.colorPalette = paletteRT;
        controller.circleSprite = circleSprite;
        controller.roundedRectSprite = roundedRect;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ColorMixing.unity");
    }

    // ═════════════════════════════════════════
    //  CANVAS BACKGROUND
    // ═════════════════════════════════════════

    private static void CreateCanvasBackground(Transform parent, Sprite roundedRect, Sprite circleSprite)
    {
        // Layer 1: Base warm color
        var bg = CreateStretchImage(parent, "BgBase", BgBase);
        bg.GetComponent<Image>().raycastTarget = false;

        // Layer 2: Lighter center (vignette inverse — bright center, darker edges)
        var center = new GameObject("BgCenter");
        center.transform.SetParent(parent, false);
        var centerRT = center.AddComponent<RectTransform>();
        centerRT.anchorMin = new Vector2(0.15f, 0.10f);
        centerRT.anchorMax = new Vector2(0.85f, 0.90f);
        centerRT.offsetMin = Vector2.zero;
        centerRT.offsetMax = Vector2.zero;
        var centerImg = center.AddComponent<Image>();
        if (circleSprite != null) centerImg.sprite = circleSprite;
        centerImg.color = new Color(CanvasCenter.r, CanvasCenter.g, CanvasCenter.b, 0.5f);
        centerImg.raycastTarget = false;

        // Layer 3: Edge darkening strips (vignette)
        CreateVignetteEdge(parent, "VigLeft",   0.00f, 0.00f, 0.08f, 1.00f, CanvasEdge, 0.35f);
        CreateVignetteEdge(parent, "VigRight",  0.92f, 0.00f, 1.00f, 1.00f, CanvasEdge, 0.35f);
        CreateVignetteEdge(parent, "VigTop",    0.00f, 0.92f, 1.00f, 1.00f, CanvasEdge, 0.25f);
        CreateVignetteEdge(parent, "VigBottom", 0.00f, 0.00f, 1.00f, 0.06f, CanvasEdge, 0.30f);

        // Layer 4: Canvas weave texture — subtle horizontal + vertical lines
        for (int i = 0; i < 12; i++)
        {
            float y = 0.06f + i * 0.08f;
            CreateCanvasThread(parent, circleSprite, true, y);
        }
        for (int i = 0; i < 18; i++)
        {
            float x = 0.04f + i * 0.055f;
            CreateCanvasThread(parent, circleSprite, false, x);
        }

        // Layer 5: Watercolor stains
        CreateWatercolorBlob(parent, circleSprite, 0.04f, 0.80f, 240f, 180f, WcRed,    -15f);
        CreateWatercolorBlob(parent, circleSprite, 0.94f, 0.75f, 200f, 220f, WcBlue,    10f);
        CreateWatercolorBlob(parent, circleSprite, 0.08f, 0.10f, 220f, 170f, WcYellow,  18f);
        CreateWatercolorBlob(parent, circleSprite, 0.92f, 0.12f, 180f, 200f, WcGreen,  -22f);
        CreateWatercolorBlob(parent, circleSprite, 0.48f, 0.93f, 160f, 130f, WcPurple,   5f);
        CreateWatercolorBlob(parent, circleSprite, 0.75f, 0.88f, 140f, 160f, WcOrange,  28f);

        CreateWatercolorBlob(parent, circleSprite, 0.06f, 0.76f, 140f, 110f, WcPink,    35f);
        CreateWatercolorBlob(parent, circleSprite, 0.90f, 0.72f, 110f, 140f, WcPurple, -12f);
        CreateWatercolorBlob(parent, circleSprite, 0.12f, 0.14f, 130f, 100f, WcOrange,  40f);
        CreateWatercolorBlob(parent, circleSprite, 0.88f, 0.16f, 100f, 120f, WcRed,    -30f);
        CreateWatercolorBlob(parent, circleSprite, 0.25f, 0.06f, 150f, 100f, WcBlue,     8f);
        CreateWatercolorBlob(parent, circleSprite, 0.70f, 0.05f, 120f, 90f,  WcYellow,  -5f);

        CreateWatercolorBlob(parent, circleSprite, 0.18f, 0.72f, 40f, 55f, WcRed,    45f);
        CreateWatercolorBlob(parent, circleSprite, 0.82f, 0.65f, 35f, 50f, WcBlue,  -20f);
        CreateWatercolorBlob(parent, circleSprite, 0.30f, 0.20f, 45f, 35f, WcGreen,  60f);
        CreateWatercolorBlob(parent, circleSprite, 0.72f, 0.22f, 38f, 48f, WcPink,  -40f);
    }

    private static void CreateVignetteEdge(Transform parent, string name,
        float aMinX, float aMinY, float aMaxX, float aMaxY, Color color, float alpha)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(aMinX, aMinY);
        rt.anchorMax = new Vector2(aMaxX, aMaxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(color.r, color.g, color.b, alpha);
        img.raycastTarget = false;
    }

    private static void CreateCanvasThread(Transform parent, Sprite circleSprite,
        bool horizontal, float pos)
    {
        var go = new GameObject("Thread");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        if (horizontal)
        {
            rt.anchorMin = new Vector2(0, pos);
            rt.anchorMax = new Vector2(1, pos);
            rt.sizeDelta = new Vector2(0, 1.5f);
        }
        else
        {
            rt.anchorMin = new Vector2(pos, 0);
            rt.anchorMax = new Vector2(pos, 1);
            rt.sizeDelta = new Vector2(1.5f, 0);
        }
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = CanvasStitch;
        img.raycastTarget = false;
    }

    // ═════════════════════════════════════════
    //  PAINTER'S PALETTE
    // ═════════════════════════════════════════

    private static void CreatePainterPalette(Transform parent, Sprite circleSprite, Sprite roundedRect)
    {
        var palette = new GameObject("PainterPalette");
        palette.transform.SetParent(parent, false);
        var palRT = palette.AddComponent<RectTransform>();
        palRT.anchorMin = new Vector2(0.5f, 0f);
        palRT.anchorMax = new Vector2(0.5f, 0f);
        palRT.pivot = new Vector2(0.5f, 0f);
        palRT.anchoredPosition = new Vector2(0, -30);
        palRT.sizeDelta = new Vector2(1100, 280);

        // Drop shadow
        CreatePaletteBlob(palette.transform, circleSprite,
            0, -12, 1000, 200, new Color(0, 0, 0, 0.10f), -3f);
        CreatePaletteBlob(palette.transform, circleSprite,
            60, -8, 400, 160, new Color(0, 0, 0, 0.06f), 5f);

        // Main wood body
        CreatePaletteBlob(palette.transform, circleSprite,
            20, 20, 900, 200, WoodBase, -2f);
        CreatePaletteBlob(palette.transform, circleSprite,
            250, 15, 500, 210, WoodBase, 3f);
        CreatePaletteBlob(palette.transform, circleSprite,
            -250, 25, 420, 180, WoodBase, -5f);
        CreatePaletteBlob(palette.transform, circleSprite,
            50, 60, 700, 160, WoodBase, 1f);
        CreatePaletteBlob(palette.transform, circleSprite,
            0, -5, 800, 170, WoodBase, -1f);

        // Wood grain lines
        CreatePaletteBlob(palette.transform, circleSprite,
            -80, 80, 750, 20, WoodGrain, -4f);
        CreatePaletteBlob(palette.transform, circleSprite,
            40, 50, 680, 18, WoodGrain, 2f);
        CreatePaletteBlob(palette.transform, circleSprite,
            -20, 25, 720, 16, WoodGrain, -1f);
        CreatePaletteBlob(palette.transform, circleSprite,
            100, 100, 600, 14, WoodGrain, 5f);
        CreatePaletteBlob(palette.transform, circleSprite,
            -60, -5, 650, 15, WoodGrain, -3f);

        // Highlight
        CreatePaletteBlob(palette.transform, circleSprite,
            30, 70, 700, 120, new Color(WoodLight.r, WoodLight.g, WoodLight.b, 0.35f), -2f);
        CreatePaletteBlob(palette.transform, circleSprite,
            200, 80, 350, 100, new Color(WoodLight.r, WoodLight.g, WoodLight.b, 0.20f), 4f);

        // Rim / edge darkening
        CreatePaletteBlob(palette.transform, circleSprite,
            20, -10, 880, 50, new Color(WoodRim.r, WoodRim.g, WoodRim.b, 0.20f), -2f);
        CreatePaletteBlob(palette.transform, circleSprite,
            -380, 30, 180, 150, new Color(WoodRim.r, WoodRim.g, WoodRim.b, 0.15f), -8f);
        CreatePaletteBlob(palette.transform, circleSprite,
            440, 25, 160, 160, new Color(WoodRim.r, WoodRim.g, WoodRim.b, 0.15f), 6f);

        // Thumb hole
        CreatePaletteBlob(palette.transform, circleSprite,
            -310, 45, 85, 95, new Color(WoodRim.r, WoodRim.g, WoodRim.b, 0.45f), -5f);
        CreatePaletteBlob(palette.transform, circleSprite,
            -310, 48, 70, 80, ThumbHole, -5f);
        CreatePaletteBlob(palette.transform, circleSprite,
            -315, 58, 35, 40, new Color(1f, 1f, 1f, 0.15f), -5f);

        // Paint wells
        float[] wellXPositions = { -232f, -57f, 118f, 293f };
        foreach (float wx in wellXPositions)
        {
            CreatePaletteBlob(palette.transform, circleSprite,
                wx, 60, 105, 100, WellColor, 0);
            CreatePaletteBlob(palette.transform, circleSprite,
                wx, 72, 80, 70, new Color(1f, 1f, 1f, 0.08f), 0);
        }

        // Dried paint smudges
        CreatePaletteBlob(palette.transform, circleSprite,
            -260, 40, 50, 40, new Color(0.90f, 0.18f, 0.18f, 0.08f), 15f);
        CreatePaletteBlob(palette.transform, circleSprite,
            -30, 95, 45, 35, new Color(0.20f, 0.40f, 0.90f, 0.06f), -10f);
        CreatePaletteBlob(palette.transform, circleSprite,
            150, 40, 40, 50, new Color(1.0f, 0.88f, 0.12f, 0.07f), 25f);
        CreatePaletteBlob(palette.transform, circleSprite,
            320, 90, 35, 30, new Color(0.92f, 0.92f, 0.90f, 0.10f), -8f);
    }

    private static void CreatePaletteBlob(Transform parent, Sprite circleSprite,
        float x, float y, float w, float h, Color color, float rotation)
    {
        var go = new GameObject("PBlob");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0);
        rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        if (rotation != 0) rt.localEulerAngles = new Vector3(0, 0, rotation);
        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = color;
        img.raycastTarget = false;
    }

    // ═════════════════════════════════════════
    //  CONTAINER (glass beaker with fill)
    // ═════════════════════════════════════════

    /// <summary>
    /// Creates a glass jar container with visible outline, shadow, top opening,
    /// and a Fill child whose anchorMax.y is animated at runtime to show liquid level.
    /// Hierarchy: Container (pivot) > Shadow, Outline, Body > Fill, EdgeLeft, EdgeRight, Shine, Rim, Opening
    /// </summary>
    private static GameObject CreateContainer(Transform parent, string name,
        Sprite roundedRect, Sprite circleSprite, float w, float h, Vector2 pos)
    {
        // Outer pivot (controller tilts this for pouring)
        var container = new GameObject(name);
        container.transform.SetParent(parent, false);
        var cRT = container.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0.5f, 0.5f);
        cRT.anchorMax = new Vector2(0.5f, 0.5f);
        cRT.anchoredPosition = pos;
        cRT.sizeDelta = new Vector2(w, h);

        // Drop shadow (larger, more visible)
        var shadow = new GameObject("Shadow");
        shadow.transform.SetParent(container.transform, false);
        var shRT = shadow.AddComponent<RectTransform>();
        shRT.anchorMin = new Vector2(0.05f, -0.06f);
        shRT.anchorMax = new Vector2(0.95f, 0.12f);
        shRT.offsetMin = Vector2.zero;
        shRT.offsetMax = Vector2.zero;
        var shImg = shadow.AddComponent<Image>();
        if (circleSprite != null) shImg.sprite = circleSprite;
        shImg.color = ContainerShadow;
        shImg.raycastTarget = false;

        // Outline (slightly larger than body, provides dark border)
        var outline = new GameObject("Outline");
        outline.transform.SetParent(container.transform, false);
        var olRT = outline.AddComponent<RectTransform>();
        olRT.anchorMin = new Vector2(-0.04f, -0.02f);
        olRT.anchorMax = new Vector2(1.04f, 1.02f);
        olRT.offsetMin = Vector2.zero;
        olRT.offsetMax = Vector2.zero;
        var olImg = outline.AddComponent<Image>();
        if (roundedRect != null) olImg.sprite = roundedRect;
        olImg.type = Image.Type.Sliced;
        olImg.color = ContainerEdge;
        olImg.raycastTarget = false;

        // Glass body
        var body = new GameObject("Body");
        body.transform.SetParent(container.transform, false);
        var bodyRT = body.AddComponent<RectTransform>();
        StretchFull(bodyRT);
        var bodyImg = body.AddComponent<Image>();
        if (roundedRect != null) bodyImg.sprite = roundedRect;
        bodyImg.type = Image.Type.Sliced;
        bodyImg.color = ContainerGlass;
        bodyImg.raycastTarget = false;

        // Fill area (child of body, anchors control height)
        var fill = new GameObject("Fill");
        fill.transform.SetParent(body.transform, false);
        var fillRT = fill.AddComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0.08f, 0.08f);
        fillRT.anchorMax = new Vector2(0.92f, 0.08f); // starts empty (0 height)
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        if (roundedRect != null) fillImg.sprite = roundedRect;
        fillImg.type = Image.Type.Sliced;
        fillImg.color = new Color(1, 1, 1, 0); // invisible until filled
        fillImg.raycastTarget = false;

        // Dark edge strips (left and right side shading for glass depth)
        var edgeL = new GameObject("EdgeLeft");
        edgeL.transform.SetParent(body.transform, false);
        var elRT = edgeL.AddComponent<RectTransform>();
        elRT.anchorMin = new Vector2(-0.01f, 0.05f);
        elRT.anchorMax = new Vector2(0.10f, 0.95f);
        elRT.offsetMin = Vector2.zero;
        elRT.offsetMax = Vector2.zero;
        var elImg = edgeL.AddComponent<Image>();
        elImg.color = new Color(ContainerEdge.r, ContainerEdge.g, ContainerEdge.b, 0.22f);
        elImg.raycastTarget = false;

        var edgeR = new GameObject("EdgeRight");
        edgeR.transform.SetParent(body.transform, false);
        var erRT = edgeR.AddComponent<RectTransform>();
        erRT.anchorMin = new Vector2(0.90f, 0.05f);
        erRT.anchorMax = new Vector2(1.01f, 0.95f);
        erRT.offsetMin = Vector2.zero;
        erRT.offsetMax = Vector2.zero;
        var erImg = edgeR.AddComponent<Image>();
        erImg.color = new Color(ContainerEdge.r, ContainerEdge.g, ContainerEdge.b, 0.22f);
        erImg.raycastTarget = false;

        // Glass shine highlight (top-left reflection)
        var shine = new GameObject("Shine");
        shine.transform.SetParent(body.transform, false);
        var shineRT = shine.AddComponent<RectTransform>();
        shineRT.anchorMin = new Vector2(0.12f, 0.50f);
        shineRT.anchorMax = new Vector2(0.32f, 0.88f);
        shineRT.offsetMin = Vector2.zero;
        shineRT.offsetMax = Vector2.zero;
        var shineImg = shine.AddComponent<Image>();
        if (circleSprite != null) shineImg.sprite = circleSprite;
        shineImg.color = new Color(1f, 1f, 1f, 0.32f);
        shineImg.raycastTarget = false;

        // Rim / top edge (thick, visible top of jar)
        var rim = new GameObject("Rim");
        rim.transform.SetParent(body.transform, false);
        var rimRT = rim.AddComponent<RectTransform>();
        rimRT.anchorMin = new Vector2(0.02f, 0.91f);
        rimRT.anchorMax = new Vector2(0.98f, 1.04f);
        rimRT.offsetMin = Vector2.zero;
        rimRT.offsetMax = Vector2.zero;
        var rimImg = rim.AddComponent<Image>();
        if (roundedRect != null) rimImg.sprite = roundedRect;
        rimImg.type = Image.Type.Sliced;
        rimImg.color = ContainerRim;
        rimImg.raycastTarget = false;

        // Top opening (darker inset at the very top to show jar is open)
        var opening = new GameObject("Opening");
        opening.transform.SetParent(body.transform, false);
        var opRT = opening.AddComponent<RectTransform>();
        opRT.anchorMin = new Vector2(0.12f, 0.95f);
        opRT.anchorMax = new Vector2(0.88f, 1.00f);
        opRT.offsetMin = Vector2.zero;
        opRT.offsetMax = Vector2.zero;
        var opImg = opening.AddComponent<Image>();
        if (roundedRect != null) opImg.sprite = roundedRect;
        opImg.type = Image.Type.Sliced;
        opImg.color = new Color(0.35f, 0.42f, 0.55f, 0.25f); // dark opening interior
        opImg.raycastTarget = false;

        return container;
    }

    // ═════════════════════════════════════════
    //  COMPONENT HELPERS
    // ═════════════════════════════════════════

    private static void CreateShine(Transform parent, Sprite circleSprite, float alpha)
    {
        var go = new GameObject("Shine");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.18f, 0.58f);
        rt.anchorMax = new Vector2(0.48f, 0.88f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = new Color(1f, 1f, 1f, alpha);
        img.raycastTarget = false;
    }

    private static void CreateWatercolorBlob(Transform parent, Sprite circleSprite,
        float anchorX, float anchorY, float w, float h, Color color, float rotation)
    {
        var go = new GameObject("WcBlob");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchorX, anchorY);
        rt.anchorMax = new Vector2(anchorX, anchorY);
        rt.sizeDelta = new Vector2(w, h);
        if (rotation != 0) rt.localEulerAngles = new Vector3(0, 0, rotation);
        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = color;
        img.raycastTarget = false;
    }

    // ═════════════════════════════════════════
    //  GENERIC HELPERS
    // ═════════════════════════════════════════

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
