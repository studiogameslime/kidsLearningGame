using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the ColorStudio scene — magical color mixing lab.
/// Dark magical background with cauldron, color circles, and bottle collection.
/// </summary>
public class ColorStudioSetup
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    // Dark magical lab colors
    private static readonly Color BgDark     = HexColor("#1A1028");  // deep purple-black
    private static readonly Color BgMid      = HexColor("#2D1B4E");  // dark purple
    private static readonly Color BgLight    = HexColor("#3D2565");  // medium purple
    private static readonly Color HeaderColor= new Color(0.3f, 0.15f, 0.5f, 0.7f);
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Color Studio Setup", "Building scene…", 0.5f);
            CreateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void CreateScene()
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgDark;
        cam.orthographic = true;
        var urpType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpType != null) camGO.AddComponent(urpType);

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inputType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputType != null) esGO.AddComponent(inputType);
        else esGO.AddComponent<StandaloneInputModule>();

        var canvasGO = new GameObject("ColorStudioCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");

        // ═══ MAGICAL BACKGROUND ═══

        // Deep dark base
        var bgGO = StretchImg(canvasGO.transform, "Background", BgDark);
        bgGO.GetComponent<Image>().raycastTarget = false;

        // Purple gradient overlay (center glow)
        var gradGO = new GameObject("CenterGlow");
        gradGO.transform.SetParent(canvasGO.transform, false);
        var gradRT = gradGO.AddComponent<RectTransform>();
        gradRT.anchorMin = new Vector2(0.2f, 0.2f); gradRT.anchorMax = new Vector2(0.8f, 0.8f);
        gradRT.offsetMin = Vector2.zero; gradRT.offsetMax = Vector2.zero;
        var gradImg = gradGO.AddComponent<Image>();
        if (circleSprite != null) gradImg.sprite = circleSprite;
        gradImg.color = new Color(0.4f, 0.2f, 0.6f, 0.15f);
        gradImg.raycastTarget = false;

        // Subtle star-like dots in background
        for (int i = 0; i < 15; i++)
        {
            var starGO = new GameObject($"Star_{i}");
            starGO.transform.SetParent(canvasGO.transform, false);
            var starRT = starGO.AddComponent<RectTransform>();
            float sx = Random.Range(0.05f, 0.95f);
            float sy = Random.Range(0.15f, 0.90f);
            starRT.anchorMin = starRT.anchorMax = new Vector2(sx, sy);
            float ss = Random.Range(3f, 7f);
            starRT.sizeDelta = new Vector2(ss, ss);
            var starImg = starGO.AddComponent<Image>();
            if (circleSprite != null) starImg.sprite = circleSprite;
            starImg.color = new Color(1f, 1f, 1f, Random.Range(0.1f, 0.3f));
            starImg.raycastTarget = false;
        }

        // ═══ CAULDRON (center) ═══

        // Cauldron body (large dark circle)
        var cauldronGO = new GameObject("Cauldron");
        cauldronGO.transform.SetParent(canvasGO.transform, false);
        var cauldronRT = cauldronGO.AddComponent<RectTransform>();
        cauldronRT.anchorMin = cauldronRT.anchorMax = new Vector2(0.5f, 0.50f);
        cauldronRT.pivot = new Vector2(0.5f, 0.5f);
        cauldronRT.sizeDelta = new Vector2(300, 280);

        // Cauldron outer (dark rim)
        var cauldronOuterImg = cauldronGO.AddComponent<Image>();
        if (circleSprite != null) cauldronOuterImg.sprite = circleSprite;
        cauldronOuterImg.color = new Color(0.15f, 0.1f, 0.2f, 0.95f);
        cauldronOuterImg.raycastTarget = true;

        // Cauldron inner (the liquid)
        var liquidGO = new GameObject("CauldronLiquid");
        liquidGO.transform.SetParent(cauldronGO.transform, false);
        var liquidRT = liquidGO.AddComponent<RectTransform>();
        liquidRT.anchorMin = new Vector2(0.1f, 0.1f); liquidRT.anchorMax = new Vector2(0.9f, 0.85f);
        liquidRT.offsetMin = Vector2.zero; liquidRT.offsetMax = Vector2.zero;
        var liquidImg = liquidGO.AddComponent<Image>();
        if (circleSprite != null) liquidImg.sprite = circleSprite;
        liquidImg.color = new Color(0.2f, 0.15f, 0.25f, 0.5f); // dark empty
        liquidImg.raycastTarget = false;

        // Cauldron rim highlight (top edge)
        var rimHighGO = new GameObject("RimHighlight");
        rimHighGO.transform.SetParent(cauldronGO.transform, false);
        var rimHighRT = rimHighGO.AddComponent<RectTransform>();
        rimHighRT.anchorMin = new Vector2(0.15f, 0.82f); rimHighRT.anchorMax = new Vector2(0.85f, 0.92f);
        rimHighRT.offsetMin = Vector2.zero; rimHighRT.offsetMax = Vector2.zero;
        var rimHighImg = rimHighGO.AddComponent<Image>();
        rimHighImg.color = new Color(0.4f, 0.3f, 0.5f, 0.4f);
        rimHighImg.raycastTarget = false;

        // ═══ BASE COLORS (left side, vertical) ═══

        var baseColorsGO = new GameObject("BaseColorsArea");
        baseColorsGO.transform.SetParent(canvasGO.transform, false);
        var baseColorsRT = baseColorsGO.AddComponent<RectTransform>();
        baseColorsRT.anchorMin = new Vector2(0.03f, 0.15f);
        baseColorsRT.anchorMax = new Vector2(0.18f, 0.88f);
        baseColorsRT.offsetMin = Vector2.zero; baseColorsRT.offsetMax = Vector2.zero;
        var baseVLG = baseColorsGO.AddComponent<VerticalLayoutGroup>();
        baseVLG.spacing = 15; baseVLG.childAlignment = TextAnchor.MiddleCenter;
        baseVLG.childForceExpandWidth = false; baseVLG.childForceExpandHeight = false;
        baseVLG.padding = new RectOffset(5, 5, 10, 10);

        // ═══ POUR BUTTON (below cauldron) ═══

        var pourBtnGO = new GameObject("PourButton");
        pourBtnGO.transform.SetParent(canvasGO.transform, false);
        var pourBtnRT = pourBtnGO.AddComponent<RectTransform>();
        pourBtnRT.anchorMin = pourBtnRT.anchorMax = new Vector2(0.5f, 0.22f);
        pourBtnRT.sizeDelta = new Vector2(200, 55);
        var pourBtnImg = pourBtnGO.AddComponent<Image>();
        if (roundedRect != null) { pourBtnImg.sprite = roundedRect; pourBtnImg.type = Image.Type.Sliced; }
        pourBtnImg.color = new Color(0.5f, 0.3f, 0.8f, 0.9f);
        var pourBtn = pourBtnGO.AddComponent<Button>();
        pourBtn.targetGraphic = pourBtnImg;
        var pourLabelGO = new GameObject("PourLabel");
        pourLabelGO.transform.SetParent(pourBtnGO.transform, false);
        Full(pourLabelGO.AddComponent<RectTransform>());
        var pourLabelTMP = pourLabelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(pourLabelTMP, "\u05E9\u05E4\u05D5\u05DA \u05DC\u05D1\u05E7\u05D1\u05D5\u05E7 \uD83E\uDDEA"); // שפוך לבקבוק 🧪
        pourLabelTMP.fontSize = 22; pourLabelTMP.fontStyle = FontStyles.Bold;
        pourLabelTMP.color = Color.white;
        pourLabelTMP.alignment = TextAlignmentOptions.Center; pourLabelTMP.raycastTarget = false;
        pourBtnGO.SetActive(false);

        // ═══ BOTTLES COLLECTION (bottom) ═══

        var bottlesScrollGO = new GameObject("BottlesScroll");
        bottlesScrollGO.transform.SetParent(canvasGO.transform, false);
        var bottlesScrollRT = bottlesScrollGO.AddComponent<RectTransform>();
        bottlesScrollRT.anchorMin = new Vector2(0.03f, 0.02f);
        bottlesScrollRT.anchorMax = new Vector2(0.97f, 0.17f);
        bottlesScrollRT.offsetMin = Vector2.zero; bottlesScrollRT.offsetMax = Vector2.zero;
        bottlesScrollGO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
        bottlesScrollGO.AddComponent<RectMask2D>();
        var bottlesScroll = bottlesScrollGO.AddComponent<ScrollRect>();
        bottlesScroll.horizontal = true; bottlesScroll.vertical = false;

        var bottlesContentGO = new GameObject("BottlesContent");
        bottlesContentGO.transform.SetParent(bottlesScrollGO.transform, false);
        var bottlesContentRT = bottlesContentGO.AddComponent<RectTransform>();
        bottlesContentRT.anchorMin = new Vector2(0, 0); bottlesContentRT.anchorMax = new Vector2(0, 1);
        bottlesContentRT.pivot = new Vector2(0, 0.5f);
        var bottlesHLG = bottlesContentGO.AddComponent<HorizontalLayoutGroup>();
        bottlesHLG.spacing = 10; bottlesHLG.childAlignment = TextAnchor.MiddleLeft;
        bottlesHLG.padding = new RectOffset(10, 10, 5, 5);
        bottlesHLG.childForceExpandWidth = false; bottlesHLG.childForceExpandHeight = false;
        bottlesContentGO.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        bottlesScroll.content = bottlesContentRT;

        // ═══ FX AREA ═══
        var fxGO = new GameObject("FXArea");
        fxGO.transform.SetParent(canvasGO.transform, false);
        Full(fxGO.AddComponent<RectTransform>());

        // ═══ SAFE AREA + UI ═══

        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        Full(safeArea.AddComponent<RectTransform>());
        safeArea.AddComponent<SafeAreaHandler>();

        var topBar = StretchImg(safeArea.transform, "TopBar", HeaderColor);
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1); topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1); topBarRT.sizeDelta = new Vector2(0, TopBarHeight);
        topBar.GetComponent<Image>().raycastTarget = false;
        topBar.AddComponent<ThemeHeader>();

        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT); titleRT.offsetMin = new Vector2(100, 0); titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05DE\u05E2\u05D1\u05D3\u05EA \u05D4\u05E6\u05D1\u05E2\u05D9\u05DD \u2728"); // מעבדת הצבעים ✨
        titleTMP.fontSize = 34; titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(1f, 0.9f, 1f);
        titleTMP.alignment = TextAlignmentOptions.Center; titleTMP.raycastTarget = false;

        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = IconBtn(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(90, 90));

        // ═══ HISTORY PANEL ═══

        var historyPanelGO = new GameObject("HistoryPanel");
        historyPanelGO.transform.SetParent(canvasGO.transform, false);
        Full(historyPanelGO.AddComponent<RectTransform>());
        historyPanelGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.8f);
        historyPanelGO.GetComponent<Image>().raycastTarget = true;

        var historyContentGO = new GameObject("HistoryContent");
        historyContentGO.transform.SetParent(historyPanelGO.transform, false);
        var historyContentRT = historyContentGO.AddComponent<RectTransform>();
        historyContentRT.anchorMin = new Vector2(0.1f, 0.1f);
        historyContentRT.anchorMax = new Vector2(0.9f, 0.85f);
        historyContentRT.offsetMin = Vector2.zero; historyContentRT.offsetMax = Vector2.zero;
        var hVLG = historyContentGO.AddComponent<VerticalLayoutGroup>();
        hVLG.spacing = 15; hVLG.childAlignment = TextAnchor.MiddleCenter;
        hVLG.childForceExpandWidth = false; hVLG.childForceExpandHeight = false;

        var historyCloseBtnGO = new GameObject("HistoryCloseBtn");
        historyCloseBtnGO.transform.SetParent(historyPanelGO.transform, false);
        var hcRT = historyCloseBtnGO.AddComponent<RectTransform>();
        hcRT.anchorMin = hcRT.anchorMax = new Vector2(0.5f, 0.05f);
        hcRT.sizeDelta = new Vector2(120, 50);
        var hcImg = historyCloseBtnGO.AddComponent<Image>();
        if (roundedRect != null) { hcImg.sprite = roundedRect; hcImg.type = Image.Type.Sliced; }
        hcImg.color = new Color(1f, 1f, 1f, 0.2f);
        var hcBtn = historyCloseBtnGO.AddComponent<Button>();
        hcBtn.targetGraphic = hcImg;
        var hcLabelGO = new GameObject("CloseLabel");
        hcLabelGO.transform.SetParent(historyCloseBtnGO.transform, false);
        Full(hcLabelGO.AddComponent<RectTransform>());
        var hcTMP = hcLabelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(hcTMP, "\u05E1\u05D2\u05D5\u05E8"); // סגור
        hcTMP.fontSize = 22; hcTMP.color = Color.white;
        hcTMP.alignment = TextAlignmentOptions.Center; hcTMP.raycastTarget = false;
        historyPanelGO.SetActive(false);

        // ═══ CONTROLLER ═══

        var controller = canvasGO.AddComponent<ColorStudioController>();
        controller.baseColorsArea = baseColorsRT;
        controller.cauldronArea = cauldronRT;
        controller.cauldronRT = cauldronRT;
        controller.cauldronFillImage = liquidImg;
        controller.bottlesArea = bottlesContentRT;
        controller.backButton = homeGO.GetComponent<Button>();
        controller.pourButton = pourBtn;
        controller.historyPanel = historyPanelGO;
        controller.historyContent = historyContentRT;
        controller.historyCloseButton = hcBtn;
        controller.fxArea = fxGO.GetComponent<RectTransform>();

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ColorStudioScene.unity");
        Debug.Log("[ColorStudioSetup] Scene created");
    }

    // ═══ HELPERS ═══
    private static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; var parts = path.Split('/'); string c = parts[0]; for (int i = 1; i < parts.Length; i++) { string n = c + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(n)) AssetDatabase.CreateFolder(c, parts[i]); c = n; } }
    private static GameObject StretchImg(Transform p, string n, Color c) { var go = new GameObject(n); go.transform.SetParent(p, false); Full(go.AddComponent<RectTransform>()); go.AddComponent<Image>().color = c; return go; }
    private static void Full(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
    private static GameObject IconBtn(Transform p, string n, Sprite i, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 sz) { var go = new GameObject(n); go.transform.SetParent(p, false); var rt = go.AddComponent<RectTransform>(); rt.anchorMin = amin; rt.anchorMax = amax; rt.pivot = amin; rt.sizeDelta = sz; rt.anchoredPosition = pos; var img = go.AddComponent<Image>(); if (i != null) img.sprite = i; img.preserveAspect = true; img.color = Color.white; go.AddComponent<Button>(); return go; }
    private static Color HexColor(string hex) { ColorUtility.TryParseHtmlString(hex, out Color c); return c; }
}
