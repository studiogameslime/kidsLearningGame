using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the ParentDashboard scene. Portrait layout.
/// Parental gate (math question) → Dashboard with 4 tabs.
/// </summary>
public class ParentDashboardSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1080, 1920);

    // Palette
    private static readonly Color BgColor = HexColor("#F0F2F5");
    private static readonly Color HeaderBg = HexColor("#2C3E50");
    private static readonly Color CardColor = HexColor("#FFFFFF");
    private static readonly Color Primary = HexColor("#3498DB");
    private static readonly Color TabInactive = HexColor("#95A5A6");
    private static readonly Color GateBg = HexColor("#2C3E50");
    private static readonly Color AnswerBg = HexColor("#ECF0F1");

    private const int HeaderHeight = 120;
    private const int TabBarHeight = 56;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Parent Dashboard", "Building scene...", 0.5f);
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

        // ── Camera ──
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

        // ── EventSystem ──
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inp = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inp != null) esGO.AddComponent(inp);
        else esGO.AddComponent<StandaloneInputModule>();

        // ── Canvas ──
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        Full(bgRT);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = BgColor;
        bgImg.raycastTarget = false;

        // SafeArea
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeGO.AddComponent<RectTransform>();
        Full(safeRT);
        safeGO.AddComponent<SafeAreaHandler>();

        // ═══════════════════════════════════════════════════════════
        //  PARENTAL GATE
        // ═══════════════════════════════════════════════════════════

        var gateGO = new GameObject("GatePanel");
        gateGO.transform.SetParent(safeGO.transform, false);
        var gateRT = gateGO.AddComponent<RectTransform>();
        Full(gateRT);
        var gateImg = gateGO.AddComponent<Image>();
        gateImg.color = GateBg;

        // Gate title
        var gateTitleGO = MakeTextGO(gateGO.transform, "GateTitle",
            HebrewFixer.Fix("\u05D0\u05D6\u05D5\u05E8 \u05D4\u05D5\u05E8\u05D9\u05DD"), // אזור הורים
            36, Color.white, TextAlignmentOptions.Center);
        var gateTitleRT = gateTitleGO.GetComponent<RectTransform>();
        gateTitleRT.anchorMin = new Vector2(0, 0.7f);
        gateTitleRT.anchorMax = new Vector2(1, 0.8f);
        gateTitleRT.offsetMin = Vector2.zero;
        gateTitleRT.offsetMax = Vector2.zero;

        // Gate subtitle
        var gateSubGO = MakeTextGO(gateGO.transform, "GateSubtitle",
            HebrewFixer.Fix("\u05E4\u05EA\u05E8\u05D5 \u05D0\u05EA \u05D4\u05EA\u05E8\u05D2\u05D9\u05DC"), // פתרו את התרגיל
            22, new Color(1, 1, 1, 0.7f), TextAlignmentOptions.Center);
        var gateSubRT = gateSubGO.GetComponent<RectTransform>();
        gateSubRT.anchorMin = new Vector2(0, 0.63f);
        gateSubRT.anchorMax = new Vector2(1, 0.70f);
        gateSubRT.offsetMin = Vector2.zero;
        gateSubRT.offsetMax = Vector2.zero;

        // Question text
        var questionGO = MakeTextGO(gateGO.transform, "QuestionText", "? = 5 + 3",
            48, Color.white, TextAlignmentOptions.Center);
        var questionRT = questionGO.GetComponent<RectTransform>();
        questionRT.anchorMin = new Vector2(0.1f, 0.50f);
        questionRT.anchorMax = new Vector2(0.9f, 0.63f);
        questionRT.offsetMin = Vector2.zero;
        questionRT.offsetMax = Vector2.zero;
        var questionTMP = questionGO.GetComponent<TextMeshProUGUI>();
        questionTMP.fontStyle = FontStyles.Bold;

        // Answer buttons (2x2 grid)
        var answerButtons = new Button[4];
        var answerLabels = new TextMeshProUGUI[4];
        float btnW = 200, btnH = 80, gap = 20;
        float gridW = btnW * 2 + gap;
        float gridH = btnH * 2 + gap;
        float startX = -gridW / 2f + btnW / 2f;
        float startY = gridH / 2f - btnH / 2f;

        var answersContainer = new GameObject("Answers");
        answersContainer.transform.SetParent(gateGO.transform, false);
        var answersRT = answersContainer.AddComponent<RectTransform>();
        answersRT.anchorMin = new Vector2(0.5f, 0.30f);
        answersRT.anchorMax = new Vector2(0.5f, 0.30f);
        answersRT.pivot = new Vector2(0.5f, 0.5f);
        answersRT.sizeDelta = new Vector2(gridW, gridH);

        for (int i = 0; i < 4; i++)
        {
            int col = i % 2;
            int row = i / 2;
            float x = startX + col * (btnW + gap);
            float y = startY - row * (btnH + gap);

            var btnGO = new GameObject($"Answer_{i}");
            btnGO.transform.SetParent(answersContainer.transform, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchoredPosition = new Vector2(x, y);
            btnRT.sizeDelta = new Vector2(btnW, btnH);

            var btnBg = btnGO.AddComponent<Image>();
            btnBg.sprite = roundedRect;
            btnBg.type = Image.Type.Sliced;
            btnBg.color = AnswerBg;
            btnBg.raycastTarget = true;

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnBg;

            var labelGO = MakeTextGO(btnGO.transform, "Label", "0", 28, HexColor("#2C3E50"), TextAlignmentOptions.Center);
            Full(labelGO.GetComponent<RectTransform>());

            answerButtons[i] = btn;
            answerLabels[i] = labelGO.GetComponent<TextMeshProUGUI>();
        }

        // Gate back button
        var gateBackGO = MakeTextGO(gateGO.transform, "GateBack",
            HebrewFixer.Fix("\u05D7\u05D6\u05E8\u05D4"), // חזרה
            20, new Color(1, 1, 1, 0.5f), TextAlignmentOptions.Center);
        var gateBackRT = gateBackGO.GetComponent<RectTransform>();
        gateBackRT.anchorMin = new Vector2(0.3f, 0.15f);
        gateBackRT.anchorMax = new Vector2(0.7f, 0.20f);
        gateBackRT.offsetMin = Vector2.zero;
        gateBackRT.offsetMax = Vector2.zero;
        var gateBackBtn = gateBackGO.AddComponent<Button>();
        gateBackBtn.targetGraphic = gateBackGO.GetComponent<TextMeshProUGUI>();

        // ═══════════════════════════════════════════════════════════
        //  DASHBOARD PANEL
        // ═══════════════════════════════════════════════════════════

        var dashGO = new GameObject("DashboardPanel");
        dashGO.transform.SetParent(safeGO.transform, false);
        var dashRT = dashGO.AddComponent<RectTransform>();
        Full(dashRT);

        // ── Header ──
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(dashGO.transform, false);
        var headerRT = headerGO.AddComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 1);
        headerRT.anchorMax = new Vector2(1, 1);
        headerRT.pivot = new Vector2(0.5f, 1);
        headerRT.sizeDelta = new Vector2(0, HeaderHeight);
        var headerImg = headerGO.AddComponent<Image>();
        headerImg.color = HeaderBg;

        // Back button
        var backGO = MakeTextGO(headerGO.transform, "BackButton", "\u2190", 32, Color.white, TextAlignmentOptions.Center);
        var backRT = backGO.GetComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0, 0.5f);
        backRT.anchorMax = new Vector2(0, 0.5f);
        backRT.pivot = new Vector2(0, 0.5f);
        backRT.anchoredPosition = new Vector2(16, 0);
        backRT.sizeDelta = new Vector2(64, 64);
        var backBtn = backGO.AddComponent<Button>();
        backBtn.targetGraphic = backGO.GetComponent<TextMeshProUGUI>();

        // Header title
        var headerTitleGO = MakeTextGO(headerGO.transform, "HeaderTitle",
            HebrewFixer.Fix("\u05D0\u05D6\u05D5\u05E8 \u05D4\u05D5\u05E8\u05D9\u05DD"), // אזור הורים
            24, Color.white, TextAlignmentOptions.Center);
        var headerTitleRT = headerTitleGO.GetComponent<RectTransform>();
        headerTitleRT.anchorMin = new Vector2(0.15f, 0.55f);
        headerTitleRT.anchorMax = new Vector2(0.85f, 0.95f);
        headerTitleRT.offsetMin = Vector2.zero;
        headerTitleRT.offsetMax = Vector2.zero;
        headerTitleGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Profile name + age
        var nameGO = MakeTextGO(headerGO.transform, "ProfileName", "---",
            18, new Color(1, 1, 1, 0.8f), TextAlignmentOptions.Center);
        var nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0.15f, 0.05f);
        nameRT.anchorMax = new Vector2(0.55f, 0.45f);
        nameRT.offsetMin = Vector2.zero;
        nameRT.offsetMax = Vector2.zero;

        var ageGO = MakeTextGO(headerGO.transform, "AgeText", "",
            16, new Color(1, 1, 1, 0.6f), TextAlignmentOptions.Center);
        var ageRT = ageGO.GetComponent<RectTransform>();
        ageRT.anchorMin = new Vector2(0.55f, 0.05f);
        ageRT.anchorMax = new Vector2(0.85f, 0.45f);
        ageRT.offsetMin = Vector2.zero;
        ageRT.offsetMax = Vector2.zero;

        // ── Tab Bar ──
        var tabBarGO = new GameObject("TabBar");
        tabBarGO.transform.SetParent(dashGO.transform, false);
        var tabBarRT = tabBarGO.AddComponent<RectTransform>();
        tabBarRT.anchorMin = new Vector2(0, 1);
        tabBarRT.anchorMax = new Vector2(1, 1);
        tabBarRT.pivot = new Vector2(0.5f, 1);
        tabBarRT.anchoredPosition = new Vector2(0, -HeaderHeight);
        tabBarRT.sizeDelta = new Vector2(0, TabBarHeight);
        var tabBarImg = tabBarGO.AddComponent<Image>();
        tabBarImg.color = CardColor;
        tabBarGO.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.04f);

        var tabBarLayout = tabBarGO.AddComponent<HorizontalLayoutGroup>();
        tabBarLayout.childForceExpandWidth = true;
        tabBarLayout.childForceExpandHeight = true;
        tabBarLayout.spacing = 0;

        string[] tabNames = {
            "\u05E1\u05E7\u05D9\u05E8\u05D4",    // סקירה
            "\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD", // משחקים
            "\u05EA\u05D7\u05D5\u05DE\u05D9\u05DD", // תחומים
            "\u05DE\u05D2\u05DE\u05D5\u05EA"         // מגמות
        };

        var tabButtons = new Button[4];
        var tabIndicators = new Image[4];

        for (int i = 0; i < 4; i++)
        {
            var tabGO = new GameObject($"Tab_{i}");
            tabGO.transform.SetParent(tabBarGO.transform, false);

            var tabLabel = MakeTextGO(tabGO.transform, "Label",
                HebrewFixer.Fix(tabNames[i]), 15, TabInactive, TextAlignmentOptions.Center);
            Full(tabLabel.GetComponent<RectTransform>());
            tabLabel.GetComponent<RectTransform>().offsetMax = new Vector2(0, -4);

            // Indicator line at bottom
            var indGO = new GameObject("Indicator");
            indGO.transform.SetParent(tabGO.transform, false);
            var indRT = indGO.AddComponent<RectTransform>();
            indRT.anchorMin = new Vector2(0.2f, 0);
            indRT.anchorMax = new Vector2(0.8f, 0);
            indRT.pivot = new Vector2(0.5f, 0);
            indRT.sizeDelta = new Vector2(0, 3);
            var indImg = indGO.AddComponent<Image>();
            indImg.color = Color.clear;

            tabButtons[i] = tabGO.AddComponent<Button>();
            tabButtons[i].transition = Selectable.Transition.None;
            tabIndicators[i] = indImg;
        }

        // ── Tab Content ScrollViews ──
        float contentTop = HeaderHeight + TabBarHeight;

        var tabContents = new RectTransform[4];
        var scrollViews = new GameObject[4];

        for (int i = 0; i < 4; i++)
        {
            var svGO = new GameObject($"ScrollView_{i}");
            svGO.transform.SetParent(dashGO.transform, false);
            var svRT = svGO.AddComponent<RectTransform>();
            svRT.anchorMin = Vector2.zero;
            svRT.anchorMax = Vector2.one;
            svRT.offsetMin = Vector2.zero;
            svRT.offsetMax = new Vector2(0, -contentTop);

            var svImg = svGO.AddComponent<Image>();
            svImg.color = BgColor;
            svImg.raycastTarget = true;

            var sv = svGO.AddComponent<ScrollRect>();
            sv.horizontal = false;
            sv.vertical = true;
            sv.movementType = ScrollRect.MovementType.Elastic;

            // Mask
            svGO.AddComponent<Mask>().showMaskGraphic = true;

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(svGO.transform, false);
            var contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.offsetMin = Vector2.zero;
            contentRT.offsetMax = Vector2.zero;

            var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 16;
            contentLayout.padding = new RectOffset(20, 20, 16, 20);
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;

            var contentFitter = contentGO.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sv.content = contentRT;
            tabContents[i] = contentRT;
            scrollViews[i] = svGO;
            svGO.SetActive(false); // all hidden initially
        }

        // ═══════════════════════════════════════════════════════════
        //  CONTROLLER WIRING
        // ═══════════════════════════════════════════════════════════

        var ctrl = canvasGO.AddComponent<ParentDashboardController>();
        ctrl.gatePanel = gateRT;
        ctrl.questionText = questionTMP;
        ctrl.answerButtons = answerButtons;
        ctrl.answerLabels = answerLabels;
        ctrl.dashboardPanel = dashRT;
        ctrl.headerNameText = nameGO.GetComponent<TextMeshProUGUI>();
        ctrl.headerAgeText = ageGO.GetComponent<TextMeshProUGUI>();
        ctrl.backButton = backBtn;
        ctrl.tabButtons = tabButtons;
        ctrl.tabIndicators = tabIndicators;
        ctrl.tabContents = tabContents;
        ctrl.roundedRect = roundedRect;

        // Wire gate back button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            gateBackBtn.onClick, ctrl.OnBackPressed);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ParentDashboard.unity");
    }

    // ── Helpers ──

    private static GameObject MakeTextGO(Transform parent, string name, string text,
        int fontSize, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.isRightToLeftText = false;
        tmp.raycastTarget = false;
        return go;
    }

    private static void Full(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
