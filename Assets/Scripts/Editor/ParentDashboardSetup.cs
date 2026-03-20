using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the ParentDashboard scene. Portrait layout.
/// Parental gate (math question) → Dashboard with header + tabs + scrollable content.
/// </summary>
public class ParentDashboardSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    // Palette
    private static readonly Color BgColor = HexColor("#F0F2F5");
    private static readonly Color HeaderBg = HexColor("#2C3E50");
    private static readonly Color CardColor = HexColor("#FFFFFF");
    private static readonly Color Primary = HexColor("#3498DB");
    private static readonly Color GateBg = HexColor("#2C3E50");
    private static readonly Color AnswerBg = HexColor("#ECF0F1");

    private static readonly int HeaderHeight = SetupConstants.HeaderHeight;

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
        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");
        var trophySprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Icons/trophy.png");

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
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        Full(bgGO.AddComponent<RectTransform>());
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = BgColor;
        bgImg.raycastTarget = false;

        // SafeArea
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(canvasGO.transform, false);
        Full(safeGO.AddComponent<RectTransform>());
        safeGO.AddComponent<SafeAreaHandler>();

        // ═══════════════════════════════════════════════════════════
        //  PARENTAL GATE
        // ═══════════════════════════════════════════════════════════

        var gateGO = new GameObject("GatePanel");
        gateGO.transform.SetParent(safeGO.transform, false);
        var gateRT = gateGO.AddComponent<RectTransform>();
        Full(gateRT);
        gateGO.AddComponent<Image>().color = GateBg;

        // Gate title
        var gateTitleGO = MakeTMP(gateGO.transform, "GateTitle",
            "\u05D0\u05D6\u05D5\u05E8 \u05D4\u05D5\u05E8\u05D9\u05DD", 52, Color.white);
        var gateTitleRT = gateTitleGO.GetComponent<RectTransform>();
        gateTitleRT.anchorMin = new Vector2(0.1f, 0.72f);
        gateTitleRT.anchorMax = new Vector2(0.9f, 0.84f);
        gateTitleRT.offsetMin = gateTitleRT.offsetMax = Vector2.zero;
        gateTitleGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Gate subtitle
        var gateSubGO = MakeTMP(gateGO.transform, "GateSubtitle",
            "\u05E4\u05EA\u05E8\u05D5 \u05D0\u05EA \u05D4\u05EA\u05E8\u05D2\u05D9\u05DC", 32, new Color(1, 1, 1, 0.7f));
        var gateSubRT = gateSubGO.GetComponent<RectTransform>();
        gateSubRT.anchorMin = new Vector2(0.1f, 0.62f);
        gateSubRT.anchorMax = new Vector2(0.9f, 0.72f);
        gateSubRT.offsetMin = gateSubRT.offsetMax = Vector2.zero;

        // Question text
        var questionGO = MakeTMP(gateGO.transform, "QuestionText", "? = 5 + 3", 72, Color.white);
        var questionRT = questionGO.GetComponent<RectTransform>();
        questionRT.anchorMin = new Vector2(0.1f, 0.48f);
        questionRT.anchorMax = new Vector2(0.9f, 0.62f);
        questionRT.offsetMin = questionRT.offsetMax = Vector2.zero;
        var questionTMP = questionGO.GetComponent<TextMeshProUGUI>();
        questionTMP.fontStyle = FontStyles.Bold;

        // Answer buttons (2x2 grid — larger for landscape)
        var answerButtons = new Button[4];
        var answerLabels = new TextMeshProUGUI[4];
        float btnW = 320, btnH = 100, gap = 30;
        float gridW = btnW * 2 + gap;
        float gridH = btnH * 2 + gap;

        var answersGO = new GameObject("Answers");
        answersGO.transform.SetParent(gateGO.transform, false);
        var answersRT = answersGO.AddComponent<RectTransform>();
        answersRT.anchorMin = new Vector2(0.5f, 0.22f);
        answersRT.anchorMax = new Vector2(0.5f, 0.22f);
        answersRT.pivot = new Vector2(0.5f, 0.5f);
        answersRT.sizeDelta = new Vector2(gridW, gridH);

        for (int i = 0; i < 4; i++)
        {
            int col = i % 2;
            int row = i / 2;
            float x = (-gridW / 2f + btnW / 2f) + col * (btnW + gap);
            float y = (gridH / 2f - btnH / 2f) - row * (btnH + gap);

            var btnGO = new GameObject($"Answer_{i}");
            btnGO.transform.SetParent(answersGO.transform, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchoredPosition = new Vector2(x, y);
            btnRT.sizeDelta = new Vector2(btnW, btnH);

            var btnBg = btnGO.AddComponent<Image>();
            btnBg.sprite = roundedRect;
            btnBg.type = Image.Type.Sliced;
            btnBg.color = AnswerBg;

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnBg;

            var labelGO = MakeTMP(btnGO.transform, "Label", "0", 40, HexColor("#2C3E50"));
            Full(labelGO.GetComponent<RectTransform>());

            answerButtons[i] = btn;
            answerLabels[i] = labelGO.GetComponent<TextMeshProUGUI>();
        }

        // Gate back button
        var gateBackGO = MakeTMP(gateGO.transform, "GateBack",
            "\u05D7\u05D6\u05E8\u05D4", 28, new Color(1, 1, 1, 0.5f));
        var gateBackRT = gateBackGO.GetComponent<RectTransform>();
        gateBackRT.anchorMin = new Vector2(0.3f, 0.06f);
        gateBackRT.anchorMax = new Vector2(0.7f, 0.14f);
        gateBackRT.offsetMin = gateBackRT.offsetMax = Vector2.zero;
        var gateBackBtn = gateBackGO.AddComponent<Button>();
        gateBackBtn.targetGraphic = gateBackGO.GetComponent<TextMeshProUGUI>();

        // ═══════════════════════════════════════════════════════════
        //  DASHBOARD PANEL
        // ═══════════════════════════════════════════════════════════

        var dashGO = new GameObject("DashboardPanel");
        dashGO.transform.SetParent(safeGO.transform, false);
        var dashRT = dashGO.AddComponent<RectTransform>();
        Full(dashRT);
        var dashBg = dashGO.AddComponent<Image>();
        dashBg.color = BgColor;
        dashBg.raycastTarget = false;

        // ── Header (dark blue, 130px) ──
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(dashGO.transform, false);
        var headerRT = headerGO.AddComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 1);
        headerRT.anchorMax = new Vector2(1, 1);
        headerRT.pivot = new Vector2(0.5f, 1);
        headerRT.sizeDelta = new Vector2(0, HeaderHeight);
        headerGO.AddComponent<Image>().color = HeaderBg;

        // Header uses HorizontalLayoutGroup for clean alignment
        var headerLayout = headerGO.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 0;
        headerLayout.padding = new RectOffset(16, 16, 0, 0);
        headerLayout.childAlignment = TextAnchor.MiddleRight;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = false;

        // Back button — pill-shaped with "← חזרה" label
        var backGO = new GameObject("BackButton");
        backGO.transform.SetParent(headerGO.transform, false);
        var backLE = backGO.AddComponent<LayoutElement>();
        backLE.preferredWidth = 100;
        backLE.preferredHeight = 40;
        var backImg = backGO.AddComponent<Image>();
        if (roundedRect != null) backImg.sprite = roundedRect;
        backImg.type = Image.Type.Sliced;
        backImg.color = new Color(1, 1, 1, 0.15f);
        var backBtn = backGO.AddComponent<Button>();
        backBtn.targetGraphic = backImg;
        var backColors = backBtn.colors;
        backColors.highlightedColor = new Color(1, 1, 1, 0.25f);
        backColors.pressedColor = new Color(1, 1, 1, 0.35f);
        backBtn.colors = backColors;

        // Back button label
        var backLabelGO = new GameObject("Label");
        backLabelGO.transform.SetParent(backGO.transform, false);
        var backLabelRT = backLabelGO.AddComponent<RectTransform>();
        backLabelRT.anchorMin = Vector2.zero;
        backLabelRT.anchorMax = Vector2.one;
        backLabelRT.offsetMin = Vector2.zero;
        backLabelRT.offsetMax = Vector2.zero;
        var backTMP = backLabelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(backTMP, "\u05D7\u05D6\u05E8\u05D4 \u2190"); // חזרה ←
        backTMP.fontSize = 16;
        backTMP.color = Color.white;
        backTMP.alignment = TextAlignmentOptions.Center;
        backTMP.enableWordWrapping = false;
        backTMP.raycastTarget = false;

        // Center info column (title + name/age row)
        var infoCenterGO = new GameObject("InfoCenter");
        infoCenterGO.transform.SetParent(headerGO.transform, false);
        var infoCenterLE = infoCenterGO.AddComponent<LayoutElement>();
        infoCenterLE.flexibleWidth = 1;
        var infoCenterLayout = infoCenterGO.AddComponent<VerticalLayoutGroup>();
        infoCenterLayout.spacing = 2;
        infoCenterLayout.childAlignment = TextAnchor.MiddleCenter;
        infoCenterLayout.childForceExpandWidth = true;
        infoCenterLayout.childForceExpandHeight = false;
        infoCenterLayout.childControlHeight = true;
        infoCenterLayout.padding = new RectOffset(0, 0, 12, 8);

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(infoCenterGO.transform, false);
        titleGO.AddComponent<RectTransform>();
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D0\u05D6\u05D5\u05E8 \u05D4\u05D5\u05E8\u05D9\u05DD"); // אזור הורים
        titleTMP.fontSize = 24;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;
        titleTMP.enableWordWrapping = false;
        titleTMP.overflowMode = TextOverflowModes.Overflow;
        titleGO.AddComponent<LayoutElement>().preferredHeight = 32;

        // Subtitle row: name | age | sessions
        var subRowGO = new GameObject("SubRow");
        subRowGO.transform.SetParent(infoCenterGO.transform, false);
        subRowGO.AddComponent<RectTransform>();
        var subRowLayout = subRowGO.AddComponent<HorizontalLayoutGroup>();
        subRowLayout.spacing = 8;
        subRowLayout.childAlignment = TextAnchor.MiddleCenter;
        subRowLayout.childForceExpandWidth = false;
        subRowLayout.childForceExpandHeight = true;
        subRowLayout.childControlWidth = false;
        subRowLayout.childControlHeight = false;
        subRowGO.AddComponent<LayoutElement>().preferredHeight = 28;

        // RTL order: rightmost child is first in hierarchy when using MiddleCenter + ContentSizeFitter
        // Visual order (right to left): Name • Age • Sessions
        var nameGO2 = MakeHeaderSubText(subRowGO.transform, "Name", "---");
        MakeHeaderSeparator(subRowGO.transform);
        var ageGO2 = MakeHeaderSubText(subRowGO.transform, "Age", "");
        MakeHeaderSeparator(subRowGO.transform);
        var sessionsGO = MakeHeaderSubText(subRowGO.transform, "Sessions", "");

        // Trophy button (right side, balances back button)
        var trophyGO = new GameObject("TrophyButton");
        trophyGO.transform.SetParent(headerGO.transform, false);
        var trophyLE = trophyGO.AddComponent<LayoutElement>();
        trophyLE.preferredWidth = 100;
        trophyLE.preferredHeight = 40;
        var trophyBgImg = trophyGO.AddComponent<Image>();
        if (roundedRect != null) trophyBgImg.sprite = roundedRect;
        trophyBgImg.type = Image.Type.Sliced;
        trophyBgImg.color = new Color(1, 1, 1, 0.15f);
        var trophyBtn = trophyGO.AddComponent<Button>();
        trophyBtn.targetGraphic = trophyBgImg;
        var trophyColors = trophyBtn.colors;
        trophyColors.highlightedColor = new Color(1, 1, 1, 0.25f);
        trophyColors.pressedColor = new Color(1, 1, 1, 0.35f);
        trophyBtn.colors = trophyColors;

        // Trophy button inner layout (icon + text)
        var trophyInnerLayout = trophyGO.AddComponent<HorizontalLayoutGroup>();
        trophyInnerLayout.spacing = 6;
        trophyInnerLayout.padding = new RectOffset(10, 10, 6, 6);
        trophyInnerLayout.childAlignment = TextAnchor.MiddleCenter;
        trophyInnerLayout.childForceExpandWidth = false;
        trophyInnerLayout.childForceExpandHeight = false;
        trophyInnerLayout.childControlWidth = false;
        trophyInnerLayout.childControlHeight = false;

        // Trophy icon
        if (trophySprite != null)
        {
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(trophyGO.transform, false);
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.sizeDelta = new Vector2(24, 24);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = trophySprite;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
        }

        // Trophy label
        var trophyLabelGO = new GameObject("Label");
        trophyLabelGO.transform.SetParent(trophyGO.transform, false);
        var trophyLabelRT = trophyLabelGO.AddComponent<RectTransform>();
        trophyLabelRT.sizeDelta = new Vector2(50, 28);
        var trophyLabelTMP = trophyLabelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(trophyLabelTMP, "\u05D2\u05D1\u05D9\u05E2"); // גביע
        trophyLabelTMP.fontSize = 14;
        trophyLabelTMP.color = Color.white;
        trophyLabelTMP.alignment = TextAlignmentOptions.Center;
        trophyLabelTMP.enableWordWrapping = false;
        trophyLabelTMP.raycastTarget = false;

        // ── Tab Content ScrollViews ──
        float contentTop = HeaderHeight;
        var tabContents = new RectTransform[4];

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
            svGO.AddComponent<Mask>().showMaskGraphic = true;

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

            contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sv.content = contentRT;
            tabContents[i] = contentRT;
            svGO.SetActive(false);
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
        ctrl.headerNameText = nameGO2.GetComponent<TextMeshProUGUI>();
        ctrl.headerAgeText = ageGO2.GetComponent<TextMeshProUGUI>();
        ctrl.headerSessionsText = sessionsGO.GetComponent<TextMeshProUGUI>();
        ctrl.backButton = backBtn;
        ctrl.trophyButton = trophyBtn;
        ctrl.trophySprite = trophySprite;
        ctrl.tabButtons = new Button[0];
        ctrl.tabIndicators = new Image[0];
        ctrl.tabContents = tabContents;
        ctrl.roundedRect = roundedRect;
        ctrl.circleSprite = circleSprite;
        ctrl.gameDatabase = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/Games/GameDatabase.asset");

        // Wire gate back button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            gateBackBtn.onClick, ctrl.OnBackPressed);

        // Ads — only in parent dashboard (behind parental gate)
        canvasGO.AddComponent<BannerAdManager>();
        canvasGO.AddComponent<RewardedAdManager>();

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ParentDashboard.unity");
    }

    // ── Helpers ──

    private static GameObject MakeHeaderSubText(Transform parent, string name, string text)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0.5f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(tmp, text);
        tmp.fontSize = 16;
        tmp.color = new Color(1, 1, 1, 0.75f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        var csf = go.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return go;
    }

    private static void MakeHeaderSeparator(Transform parent)
    {
        var go = new GameObject("Separator");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "\u2022"; // •
        tmp.fontSize = 14;
        tmp.color = new Color(1, 1, 1, 0.5f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        var csf = go.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static GameObject MakeTMP(Transform parent, string name, string text,
        int fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(tmp, text);
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
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
