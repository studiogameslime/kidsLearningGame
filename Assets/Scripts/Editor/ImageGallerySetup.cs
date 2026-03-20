using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the ImageGallery scene — landscape layout.
/// Scrollable gallery with child drawings section + parent images section.
/// "Start" button at bottom to confirm selection.
/// </summary>
public class ImageGallerySetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    private static readonly Color BgColor      = HexColor("#F5F0EB");
    private static readonly Color BarColor     = HexColor("#8D6E63"); // warm brown
    private static readonly Color SectionTitle = HexColor("#5D4037");
    private static readonly Color EmptyMsg     = HexColor("#9E9E9E");
    private static readonly Color StartBtnBg   = HexColor("#66BB6A");
    private static readonly Color StartBtnDis  = HexColor("#BDBDBD");

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Image Gallery", "Building scene...", 0.5f);
            BuildScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static void BuildScene()
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");

        // Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgColor;
        cam.orthographic = true;
        var urp = System.Type.GetType(
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urp != null) camGO.AddComponent(urp);

        // EventSystem
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inp = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inp != null) esGO.AddComponent(inp);
        else esGO.AddComponent<StandaloneInputModule>();

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        var root = canvasGO.transform;

        Layer(root, "Background", null, 0, 0, 1, 1, BgColor);

        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(root, false);
        var safeRT = safeGO.AddComponent<RectTransform>();
        Full(safeRT);
        safeGO.AddComponent<SafeAreaHandler>();

        // ── Top Bar ──
        var bar = CreateBar(safeGO.transform);

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(bar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(110, 0);
        titleRT.offsetMax = new Vector2(-110, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D1\u05D7\u05E8\u05D5 \u05EA\u05DE\u05D5\u05E0\u05D4"); // בחרו תמונה
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = Btn(bar.transform, "HomeButton", homeIcon, 16, -12, 76);

        // ── Play Area ──
        var playGO = new GameObject("PlayArea");
        playGO.transform.SetParent(safeGO.transform, false);
        var playRT = playGO.AddComponent<RectTransform>();
        playRT.anchorMin = new Vector2(0, 0);
        playRT.anchorMax = new Vector2(1, 1);
        playRT.offsetMin = new Vector2(20, 16);
        playRT.offsetMax = new Vector2(-20, -TopBarHeight);

        // ── Scroll View ──
        var scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(playRT, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0.02f, 0.12f);
        scrollRT.anchorMax = new Vector2(0.98f, 0.98f);
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = Vector2.zero;
        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollGO.AddComponent<Image>().color = Color.white;
        scrollGO.AddComponent<RectMask2D>(); // RectMask2D doesn't need a visible Image

        // Content container inside scroll
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(scrollGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.sizeDelta = new Vector2(0, 0);
        var contentVLG = contentGO.AddComponent<VerticalLayoutGroup>();
        contentVLG.spacing = 16;
        contentVLG.padding = new RectOffset(16, 16, 16, 16);
        contentVLG.childAlignment = TextAnchor.UpperCenter;
        contentVLG.childControlWidth = true;
        contentVLG.childControlHeight = true;
        contentVLG.childForceExpandWidth = true;
        contentVLG.childForceExpandHeight = false;
        var contentCSF = contentGO.AddComponent<ContentSizeFitter>();
        contentCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRT;

        // ── Section 1: Drawings Title ──
        var drawTitleGO = new GameObject("DrawingsTitle");
        drawTitleGO.transform.SetParent(contentRT, false);
        var dtLE = drawTitleGO.AddComponent<LayoutElement>();
        dtLE.preferredHeight = 50;
        var drawTitleTMP = drawTitleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(drawTitleTMP, "\u05D4\u05E6\u05D9\u05D5\u05E8\u05D9\u05DD \u05E9\u05DC\u05D9"); // הציורים שלי
        drawTitleTMP.fontSize = 28;
        drawTitleTMP.fontStyle = FontStyles.Bold;
        drawTitleTMP.color = SectionTitle;
        drawTitleTMP.alignment = TextAlignmentOptions.Right;
        drawTitleTMP.raycastTarget = false;

        // Drawings Grid
        var drawGridGO = new GameObject("DrawingsGrid");
        drawGridGO.transform.SetParent(contentRT, false);
        var drawGridRT = drawGridGO.AddComponent<RectTransform>();
        var drawGridGLG = drawGridGO.AddComponent<GridLayoutGroup>();
        drawGridGLG.cellSize = new Vector2(280, 280);
        drawGridGLG.spacing = new Vector2(20, 20);
        drawGridGLG.startCorner = GridLayoutGroup.Corner.UpperRight; // RTL
        drawGridGLG.childAlignment = TextAnchor.UpperCenter;
        var drawGridCSF = drawGridGO.AddComponent<ContentSizeFitter>();
        drawGridCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── Section 2: Parent Images Title ──
        var parentTitleGO = new GameObject("ParentTitle");
        parentTitleGO.transform.SetParent(contentRT, false);
        var ptLE = parentTitleGO.AddComponent<LayoutElement>();
        ptLE.preferredHeight = 50;
        var parentTitleTMP = parentTitleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(parentTitleTMP, "\u05EA\u05DE\u05D5\u05E0\u05D5\u05EA \u05DE\u05D4\u05D4\u05D5\u05E8\u05D9\u05DD"); // תמונות מההורים
        parentTitleTMP.fontSize = 28;
        parentTitleTMP.fontStyle = FontStyles.Bold;
        parentTitleTMP.color = SectionTitle;
        parentTitleTMP.alignment = TextAlignmentOptions.Right;
        parentTitleTMP.raycastTarget = false;

        // Parent Grid
        var parentGridGO = new GameObject("ParentGrid");
        parentGridGO.transform.SetParent(contentRT, false);
        var parentGridRT = parentGridGO.AddComponent<RectTransform>();
        var parentGridGLG = parentGridGO.AddComponent<GridLayoutGroup>();
        parentGridGLG.cellSize = new Vector2(280, 280);
        parentGridGLG.spacing = new Vector2(20, 20);
        parentGridGLG.startCorner = GridLayoutGroup.Corner.UpperRight;
        parentGridGLG.childAlignment = TextAnchor.UpperCenter;
        var parentGridCSF = parentGridGO.AddComponent<ContentSizeFitter>();
        parentGridCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Parent empty message
        var parentEmptyGO = new GameObject("ParentEmptyMessage");
        parentEmptyGO.transform.SetParent(contentRT, false);
        var peLE = parentEmptyGO.AddComponent<LayoutElement>();
        peLE.preferredHeight = 60;
        var parentEmptyTMP = parentEmptyGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(parentEmptyTMP,
            "\u05D4\u05D4\u05D5\u05E8\u05D9\u05DD \u05D9\u05DB\u05D5\u05DC\u05D9\u05DD \u05DC\u05D4\u05D5\u05E1\u05D9\u05E3 \u05DC\u05DA \u05EA\u05DE\u05D5\u05E0\u05D5\u05EA \u05D3\u05E8\u05DA \u05E4\u05D0\u05E0\u05DC \u05D4\u05D4\u05D5\u05E8\u05D9\u05DD");
        // ההורים יכולים להוסיף לך תמונות דרך פאנל ההורים
        parentEmptyTMP.fontSize = 22;
        parentEmptyTMP.color = EmptyMsg;
        parentEmptyTMP.alignment = TextAlignmentOptions.Center;
        parentEmptyTMP.raycastTarget = false;
        parentEmptyGO.SetActive(false);

        // ── Global empty state (both sections empty) ──
        var emptyGO = new GameObject("EmptyState");
        emptyGO.transform.SetParent(playRT, false);
        var emptyRT = emptyGO.AddComponent<RectTransform>();
        emptyRT.anchorMin = new Vector2(0.1f, 0.3f);
        emptyRT.anchorMax = new Vector2(0.9f, 0.7f);
        emptyRT.offsetMin = Vector2.zero;
        emptyRT.offsetMax = Vector2.zero;
        var emptyTMP = emptyGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(emptyTMP,
            "\u05D0\u05D9\u05DF \u05E2\u05D3\u05D9\u05D9\u05DF \u05EA\u05DE\u05D5\u05E0\u05D5\u05EA. \u05E6\u05D9\u05D9\u05E8\u05D5 \u05E6\u05D9\u05D5\u05E8 \u05D0\u05D5 \u05D1\u05E7\u05E9\u05D5 \u05DE\u05D4\u05D4\u05D5\u05E8\u05D9\u05DD \u05DC\u05D4\u05D5\u05E1\u05D9\u05E3 \u05EA\u05DE\u05D5\u05E0\u05D5\u05EA");
        // אין עדיין תמונות. ציירו ציור או בקשו מההורים להוסיף תמונות
        emptyTMP.fontSize = 26;
        emptyTMP.color = EmptyMsg;
        emptyTMP.alignment = TextAlignmentOptions.Center;
        emptyTMP.raycastTarget = false;
        emptyGO.SetActive(false);

        // ── Start Button (bottom) ──
        var startGO = new GameObject("StartButton");
        startGO.transform.SetParent(playRT, false);
        var startRT = startGO.AddComponent<RectTransform>();
        startRT.anchorMin = new Vector2(0.30f, 0.01f);
        startRT.anchorMax = new Vector2(0.70f, 0.10f);
        startRT.offsetMin = Vector2.zero;
        startRT.offsetMax = Vector2.zero;
        var startBgImg = startGO.AddComponent<Image>();
        if (roundedRect != null) { startBgImg.sprite = roundedRect; startBgImg.type = Image.Type.Sliced; }
        startBgImg.color = StartBtnBg;
        startBgImg.raycastTarget = true;
        var startBtn = startGO.AddComponent<Button>();
        startBtn.targetGraphic = startBgImg;

        var startTextGO = new GameObject("Text");
        startTextGO.transform.SetParent(startGO.transform, false);
        var stRT = startTextGO.AddComponent<RectTransform>();
        Full(stRT);
        var startTMP = startTextGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(startTMP, "\u05D4\u05EA\u05D7\u05DC"); // התחל
        startTMP.fontSize = 32;
        startTMP.fontStyle = FontStyles.Bold;
        startTMP.color = Color.white;
        startTMP.alignment = TextAlignmentOptions.Center;
        startTMP.raycastTarget = false;

        // ── Controller ──
        var ctrl = canvasGO.AddComponent<ImageGalleryController>();
        ctrl.contentContainer = contentRT;
        ctrl.homeButton = homeGO.GetComponent<Button>();
        ctrl.startButton = startBtn;
        ctrl.startButtonText = startTMP;
        ctrl.emptyStateText = emptyGO;
        ctrl.drawingsSectionTitle = drawTitleTMP;
        ctrl.drawingsGrid = drawGridRT;
        ctrl.parentSectionTitle = parentTitleTMP;
        ctrl.parentGrid = parentGridRT;
        ctrl.parentEmptyMessage = parentEmptyGO;
        ctrl.roundedRectSprite = roundedRect;

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ImageGallery.unity");
    }

    // ── HELPERS ──

    private static GameObject Layer(Transform p, string name, Sprite spr,
        float x0, float y0, float x1, float y1, Color c)
    {
        var go = new GameObject(name); go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        if (spr != null) img.sprite = spr;
        img.type = Image.Type.Simple; img.color = c; img.raycastTarget = false;
        return go;
    }

    private static GameObject CreateBar(Transform parent)
    {
        var bar = new GameObject("TopBar"); bar.transform.SetParent(parent, false);
        var barRT = bar.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1); barRT.anchorMax = new Vector2(1, 1);
        barRT.pivot = new Vector2(0.5f, 1); barRT.sizeDelta = new Vector2(0, TopBarHeight);
        var barImg = bar.AddComponent<Image>(); barImg.color = BarColor; barImg.raycastTarget = false;
        bar.AddComponent<ThemeHeader>();
        return bar;
    }

    private static GameObject Btn(Transform p, string name, Sprite icon, float x, float y, float sz)
    {
        var go = new GameObject(name); go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(sz, sz);
        var img = go.AddComponent<Image>(); img.sprite = icon; img.preserveAspect = true;
        img.color = Color.white; img.raycastTarget = true;
        go.AddComponent<Button>().targetGraphic = img; return go;
    }

    private static void Full(RectTransform rt)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/'); string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        { string next = cur + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]); cur = next; }
    }

    private static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (s != null) return s;
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        if (all != null) foreach (var o in all) if (o is Sprite sp) return sp;
        return null;
    }

    private static Color HexColor(string hex)
    { ColorUtility.TryParseHtmlString(hex, out Color c); return c; }
}
