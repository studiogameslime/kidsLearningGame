using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the LetterTracing scene in LANDSCAPE orientation.
///
/// Layout:
///   Header (100px) — home left, title center, trophy right
///   Left ~65%  — tracing canvas (RawImage + guide overlay)
///   Right ~35% — reference letter, color palette, progress dots, next button
/// </summary>
public class LetterTracingSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    private static readonly Color BgColor     = HexColor("#FFF5EE");
    private static readonly Color TopBarColor = HexColor("#F8E8D8");
    private static readonly Color PanelColor  = HexColor("#FDF6EF");
    private static readonly Color CanvasBg    = Color.white;
    private static readonly Color CanvasBorder = HexColor("#E0D5C8");

    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;
    private const float LeftRatio = 0.62f;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Letter Tracing Setup", "Updating game data…", 0.2f);
            UpdateGameData();

            EditorUtility.DisplayProgressBar("Letter Tracing Setup", "Building scene…", 0.5f);
            BuildScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void UpdateGameData()
    {
        var game = AssetDatabase.LoadAssetAtPath<GameItemData>("Assets/Data/Games/LetterTracing.asset");
        if (game == null) return;
        game.targetSceneName = "LetterTracing";
        game.hasSubItems = false;
        EditorUtility.SetDirty(game);
    }

    private static void BuildScene()
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");

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
        var canvasGO = new GameObject("TracingUICanvas");
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
        //  HEADER
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
        Full(titleRT); titleRT.offsetMin = new Vector2(100, 0); titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05DB\u05EA\u05D9\u05D1\u05EA \u05D0\u05D5\u05EA\u05D9\u05D5\u05EA"); // כתיבת אותיות
        titleTMP.fontSize = 36; titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white; titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = IconBtn(bar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(90, 90));

        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = IconBtn(bar.transform, "TrophyButton", trophyIcon,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(112, -30), new Vector2(70, 70));

        // ═══════════════════════════════════
        //  CONTENT
        // ═══════════════════════════════════

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(safeGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        Full(contentRT); contentRT.offsetMax = new Vector2(0, -TopBarHeight);

        // ═══════════════════════════════════
        //  LEFT — TRACING CANVAS
        // ═══════════════════════════════════

        var leftGO = new GameObject("LeftPanel");
        leftGO.transform.SetParent(contentGO.transform, false);
        var leftRT = leftGO.AddComponent<RectTransform>();
        leftRT.anchorMin = Vector2.zero; leftRT.anchorMax = new Vector2(LeftRatio, 1);
        leftRT.offsetMin = Vector2.zero; leftRT.offsetMax = Vector2.zero;

        // Canvas frame with border
        var frameGO = StretchImage(leftGO.transform, "CanvasFrame", CanvasBorder);
        var frameRT = frameGO.GetComponent<RectTransform>();
        Full(frameRT); frameRT.offsetMin = new Vector2(12, 12); frameRT.offsetMax = new Vector2(-12, -12);
        frameGO.GetComponent<Image>().raycastTarget = false;

        // White canvas background
        var canvasBgGO = StretchImage(frameGO.transform, "CanvasWhite", CanvasBg);
        var canvasBgRT = canvasBgGO.GetComponent<RectTransform>();
        Full(canvasBgRT); canvasBgRT.offsetMin = new Vector2(3, 3); canvasBgRT.offsetMax = new Vector2(-3, -3);
        canvasBgGO.GetComponent<Image>().raycastTarget = false;

        // Guide overlay (for dotted guide points — sits behind tracing layer)
        var guideGO = new GameObject("GuideOverlay");
        guideGO.transform.SetParent(canvasBgGO.transform, false);
        var guideRT = guideGO.AddComponent<RectTransform>();
        Full(guideRT);

        // Tracing layer (RawImage + TracingCanvas)
        var traceGO = new GameObject("TracingLayer");
        traceGO.transform.SetParent(canvasBgGO.transform, false);
        Full(traceGO.AddComponent<RectTransform>());
        var traceRaw = traceGO.AddComponent<RawImage>();
        traceRaw.color = Color.white;
        var tracingCanvas = traceGO.AddComponent<TracingCanvas>();
        tracingCanvas.SetGuideParent(guideRT);

        // ═══════════════════════════════════
        //  RIGHT — INFO PANEL
        // ═══════════════════════════════════

        var rightGO = StretchImage(contentGO.transform, "RightPanel", PanelColor);
        var rightRT = rightGO.GetComponent<RectTransform>();
        rightRT.anchorMin = new Vector2(LeftRatio, 0); rightRT.anchorMax = new Vector2(1, 1);
        rightRT.offsetMin = Vector2.zero; rightRT.offsetMax = Vector2.zero;
        rightGO.GetComponent<Image>().raycastTarget = false;

        var panelGO = new GameObject("PanelLayout");
        panelGO.transform.SetParent(rightGO.transform, false);
        var panelRT = panelGO.AddComponent<RectTransform>();
        Full(panelRT); panelRT.offsetMin = new Vector2(8, 16); panelRT.offsetMax = new Vector2(-8, -16);
        var panelVL = panelGO.AddComponent<VerticalLayoutGroup>();
        panelVL.spacing = 12;
        panelVL.childAlignment = TextAnchor.UpperCenter;
        panelVL.childForceExpandWidth = true;
        panelVL.childForceExpandHeight = false;
        panelVL.padding = new RectOffset(8, 8, 8, 8);

        // Large reference letter
        var letterGO = new GameObject("LetterDisplay");
        letterGO.transform.SetParent(panelGO.transform, false);
        letterGO.AddComponent<RectTransform>();
        letterGO.AddComponent<LayoutElement>().preferredHeight = 280;
        var letterTMP = letterGO.AddComponent<TextMeshProUGUI>();
        letterTMP.text = "\u05D0"; // א placeholder
        letterTMP.fontSize = 220;
        letterTMP.fontStyle = FontStyles.Bold;
        letterTMP.color = new Color(0.2f, 0.2f, 0.2f, 0.15f);
        letterTMP.alignment = TextAlignmentOptions.Center;
        letterTMP.enableAutoSizing = false;
        letterTMP.raycastTarget = false;

        // Letter name
        var nameGO = new GameObject("LetterName");
        nameGO.transform.SetParent(panelGO.transform, false);
        nameGO.AddComponent<RectTransform>();
        nameGO.AddComponent<LayoutElement>().preferredHeight = 36;
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.fontSize = 26;
        nameTMP.color = new Color(0.4f, 0.3f, 0.2f);
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.raycastTarget = false;

        // Progress dots
        var dotsGO = new GameObject("ProgressDots");
        dotsGO.transform.SetParent(panelGO.transform, false);
        dotsGO.AddComponent<RectTransform>();
        dotsGO.AddComponent<LayoutElement>().preferredHeight = 36;
        var dotsHL = dotsGO.AddComponent<HorizontalLayoutGroup>();
        dotsHL.spacing = 12;
        dotsHL.childAlignment = TextAnchor.MiddleCenter;
        dotsHL.childForceExpandWidth = false;
        dotsHL.childForceExpandHeight = false;

        // Color palette
        var paletteTitleGO = new GameObject("PaletteTitle");
        paletteTitleGO.transform.SetParent(panelGO.transform, false);
        paletteTitleGO.AddComponent<RectTransform>();
        paletteTitleGO.AddComponent<LayoutElement>().preferredHeight = 28;
        var paletteTitleTMP = paletteTitleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(paletteTitleTMP, "\u05E6\u05D1\u05E2"); // צבע
        paletteTitleTMP.fontSize = 20; paletteTitleTMP.fontStyle = FontStyles.Bold;
        paletteTitleTMP.color = new Color(0.45f, 0.35f, 0.25f);
        paletteTitleTMP.alignment = TextAlignmentOptions.Center;
        paletteTitleTMP.raycastTarget = false;

        var paletteGO = new GameObject("ColorPalette");
        paletteGO.transform.SetParent(panelGO.transform, false);
        paletteGO.AddComponent<RectTransform>();
        paletteGO.AddComponent<LayoutElement>().preferredHeight = 80;
        var paletteHL = paletteGO.AddComponent<HorizontalLayoutGroup>();
        paletteHL.spacing = 12;
        paletteHL.childAlignment = TextAnchor.MiddleCenter;
        paletteHL.childForceExpandWidth = false;
        paletteHL.childForceExpandHeight = false;

        // Next button (hidden by default)
        var nextBtnGO = new GameObject("NextButton");
        nextBtnGO.transform.SetParent(panelGO.transform, false);
        nextBtnGO.AddComponent<RectTransform>();
        nextBtnGO.AddComponent<LayoutElement>().preferredHeight = 60;
        var nextBtnImg = nextBtnGO.AddComponent<Image>();
        if (roundedRect != null) { nextBtnImg.sprite = roundedRect; nextBtnImg.type = Image.Type.Sliced; }
        nextBtnImg.color = HexColor("#4CAF50");
        nextBtnImg.raycastTarget = true;
        var nextBtn = nextBtnGO.AddComponent<Button>();
        nextBtn.targetGraphic = nextBtnImg;

        var nextLbl = new GameObject("Label");
        nextLbl.transform.SetParent(nextBtnGO.transform, false);
        Full(nextLbl.AddComponent<RectTransform>());
        var nextTMP = nextLbl.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(nextTMP, "\u05D4\u05D1\u05D0"); // הבא
        nextTMP.fontSize = 28; nextTMP.fontStyle = FontStyles.Bold;
        nextTMP.color = Color.white; nextTMP.alignment = TextAlignmentOptions.Center;
        nextTMP.raycastTarget = false;
        nextBtnGO.SetActive(false);

        // ═══════════════════════════════════
        //  LOAD COLOR BUTTON PREFAB
        // ═══════════════════════════════════

        var colorBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ColorButton.prefab");

        // ═══════════════════════════════════
        //  CONTROLLER
        // ═══════════════════════════════════

        var ctrl = canvasGO.AddComponent<LetterTracingController>();
        ctrl.tracingCanvas = tracingCanvas;
        ctrl.letterDisplay = letterTMP;
        ctrl.letterNameText = nameTMP;
        ctrl.colorButtonContainer = paletteGO.transform;
        ctrl.colorButtonPrefab = colorBtnPrefab;
        ctrl.progressDotsContainer = dotsGO.transform;
        ctrl.nextButton = nextBtn;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = roundedRect;
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "lettertracing";

        // Tutorial hand
        TutorialHandHelper.Create(safeGO.transform, TutorialHandHelper.Anim.SlideDown,
            new Vector2(0, 0), new Vector2(400, 400), "lettertracing");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/LetterTracing.unity");
    }

    // ─────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────

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
