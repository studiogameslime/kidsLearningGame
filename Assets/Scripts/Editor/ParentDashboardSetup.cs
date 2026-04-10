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
    private static readonly Color BgColor = HexColor("#0F1923");
    private static readonly Color HeaderBg = HexColor("#2C3E50");
    private static readonly Color CardColor = HexColor("#1E2A3A");
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
        var gearSprite = UISheetHelper.GearIcon;

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
        gateTitleRT.anchorMin = new Vector2(0.1f, 0.82f);
        gateTitleRT.anchorMax = new Vector2(0.9f, 0.94f);
        gateTitleRT.offsetMin = gateTitleRT.offsetMax = Vector2.zero;
        gateTitleGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Question text
        var questionGO = MakeTMP(gateGO.transform, "QuestionText", "? = 5 + 3", 72, Color.white);
        var questionRT = questionGO.GetComponent<RectTransform>();
        questionRT.anchorMin = new Vector2(0.1f, 0.70f);
        questionRT.anchorMax = new Vector2(0.9f, 0.85f);
        questionRT.offsetMin = questionRT.offsetMax = Vector2.zero;
        var questionTMP = questionGO.GetComponent<TextMeshProUGUI>();
        questionTMP.fontStyle = FontStyles.Bold;

        // Answer input field + submit button
        var inputAreaGO = new GameObject("InputArea");
        inputAreaGO.transform.SetParent(gateGO.transform, false);
        var inputAreaRT = inputAreaGO.AddComponent<RectTransform>();
        inputAreaRT.anchorMin = new Vector2(0.2f, 0.55f);
        inputAreaRT.anchorMax = new Vector2(0.8f, 0.69f);
        inputAreaRT.offsetMin = Vector2.zero;
        inputAreaRT.offsetMax = Vector2.zero;

        // Input field background
        var inputGO = new GameObject("AnswerInput");
        inputGO.transform.SetParent(inputAreaGO.transform, false);
        var inputRT = inputGO.AddComponent<RectTransform>();
        inputRT.anchorMin = new Vector2(0f, 0f);
        inputRT.anchorMax = new Vector2(0.65f, 1f);
        inputRT.offsetMin = Vector2.zero;
        inputRT.offsetMax = Vector2.zero;
        var inputBg = inputGO.AddComponent<Image>();
        inputBg.sprite = roundedRect;
        inputBg.type = Image.Type.Sliced;
        inputBg.color = Color.white;

        // TMP Input Field
        var inputField = inputGO.AddComponent<TMP_InputField>();
        inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        inputField.characterLimit = 4;

        // Text area
        var textAreaGO = new GameObject("TextArea");
        textAreaGO.transform.SetParent(inputGO.transform, false);
        var textAreaRT = textAreaGO.AddComponent<RectTransform>();
        Full(textAreaRT);
        textAreaRT.offsetMin = new Vector2(20, 5);
        textAreaRT.offsetMax = new Vector2(-20, -5);
        textAreaGO.AddComponent<RectMask2D>();

        // Input text
        var inputTextGO = MakeTMP(textAreaGO.transform, "Text", "", 44, HexColor("#2C3E50"));
        var inputTextRT = inputTextGO.GetComponent<RectTransform>();
        Full(inputTextRT);
        var inputTextTMP = inputTextGO.GetComponent<TextMeshProUGUI>();
        inputTextTMP.alignment = TextAlignmentOptions.Center;
        inputField.textComponent = inputTextTMP;
        inputField.textViewport = textAreaRT;

        // Placeholder
        var placeholderGO = MakeTMP(textAreaGO.transform, "Placeholder",
            "\u05D4\u05E7\u05DC\u05D3 \u05EA\u05E9\u05D5\u05D1\u05D4", 36, new Color(0.6f, 0.6f, 0.6f)); // הקלד תשובה
        var placeholderRT = placeholderGO.GetComponent<RectTransform>();
        Full(placeholderRT);
        placeholderGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        inputField.placeholder = placeholderGO.GetComponent<TextMeshProUGUI>();

        // Submit button
        var submitGO = new GameObject("SubmitButton");
        submitGO.transform.SetParent(inputAreaGO.transform, false);
        var submitRT = submitGO.AddComponent<RectTransform>();
        submitRT.anchorMin = new Vector2(0.7f, 0f);
        submitRT.anchorMax = new Vector2(1f, 1f);
        submitRT.offsetMin = Vector2.zero;
        submitRT.offsetMax = Vector2.zero;
        var submitBg = submitGO.AddComponent<Image>();
        submitBg.sprite = roundedRect;
        submitBg.type = Image.Type.Sliced;
        submitBg.color = HexColor("#4CAF50");
        var submitBtn = submitGO.AddComponent<Button>();
        submitBtn.targetGraphic = submitBg;
        var submitLabelGO = MakeTMP(submitGO.transform, "Label",
            "\u05D0\u05D9\u05E9\u05D5\u05E8", 36, Color.white); // אישור
        Full(submitLabelGO.GetComponent<RectTransform>());
        submitLabelGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Error text (hidden by default)
        var errorGO = MakeTMP(gateGO.transform, "ErrorText",
            "\u05EA\u05E9\u05D5\u05D1\u05D4 \u05DC\u05D0 \u05E0\u05DB\u05D5\u05E0\u05D4", 28, new Color(1f, 0.4f, 0.4f)); // תשובה לא נכונה
        var errorRT = errorGO.GetComponent<RectTransform>();
        errorRT.anchorMin = new Vector2(0.2f, 0.30f);
        errorRT.anchorMax = new Vector2(0.8f, 0.38f);
        errorRT.offsetMin = Vector2.zero;
        errorRT.offsetMax = Vector2.zero;
        errorGO.SetActive(false);

        // Gate home button (top-left)
        var homeIcon = UISheetHelper.HomeIcon;
        var gateBackGO = new GameObject("GateHomeButton");
        gateBackGO.transform.SetParent(gateGO.transform, false);
        var gateBackRT = gateBackGO.AddComponent<RectTransform>();
        gateBackRT.anchorMin = new Vector2(0, 1);
        gateBackRT.anchorMax = new Vector2(0, 1);
        gateBackRT.pivot = new Vector2(0, 1);
        gateBackRT.anchoredPosition = new Vector2(16, -16);
        gateBackRT.sizeDelta = new Vector2(76, 76);
        var gateBackImg = gateBackGO.AddComponent<Image>();
        gateBackImg.sprite = homeIcon;
        gateBackImg.preserveAspect = true;
        gateBackImg.color = Color.white;
        gateBackImg.raycastTarget = true;
        var gateBackBtn = gateBackGO.AddComponent<Button>();
        gateBackBtn.targetGraphic = gateBackImg;

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

        // Back button — standard icon button (matches MainMenu/SelectionMenu pattern)
        var backGO = new GameObject("BackButton");
        backGO.transform.SetParent(headerGO.transform, false);
        var backRT = backGO.AddComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0, 0.5f);
        backRT.anchorMax = new Vector2(0, 0.5f);
        backRT.pivot = new Vector2(0, 0.5f);
        backRT.anchoredPosition = new Vector2(24, 0);
        backRT.sizeDelta = new Vector2(90, 90);

        var backImg = backGO.AddComponent<Image>();
        backImg.sprite = homeIcon;
        backImg.preserveAspect = true;
        backImg.color = Color.white;
        backImg.raycastTarget = true;

        var backBtn = backGO.AddComponent<Button>();
        backBtn.targetGraphic = backImg;

        // Center info column (title + name/age row)
        var infoCenterGO = new GameObject("InfoCenter");
        infoCenterGO.transform.SetParent(headerGO.transform, false);
        var infoCenterLE = infoCenterGO.AddComponent<LayoutElement>();
        infoCenterLE.flexibleWidth = 1;
        var infoCenterLayout = infoCenterGO.AddComponent<VerticalLayoutGroup>();
        infoCenterLayout.spacing = 3;
        infoCenterLayout.childAlignment = TextAnchor.MiddleCenter;
        infoCenterLayout.childForceExpandWidth = true;
        infoCenterLayout.childForceExpandHeight = false;
        infoCenterLayout.childControlHeight = true;
        infoCenterLayout.padding = new RectOffset(2, 2, 15, 10);

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(infoCenterGO.transform, false);
        titleGO.AddComponent<RectTransform>();
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D0\u05D6\u05D5\u05E8 \u05D4\u05D5\u05E8\u05D9\u05DD"); // אזור הורים
        titleTMP.fontSize = 34;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;
        titleTMP.enableWordWrapping = false;
        titleTMP.overflowMode = TextOverflowModes.Overflow;
        titleGO.AddComponent<LayoutElement>().preferredHeight = 43;

        // Subtitle row: name | age | sessions
        var subRowGO = new GameObject("SubRow");
        subRowGO.transform.SetParent(infoCenterGO.transform, false);
        subRowGO.AddComponent<RectTransform>();
        var subRowLayout = subRowGO.AddComponent<HorizontalLayoutGroup>();
        subRowLayout.spacing = 10;
        subRowLayout.childAlignment = TextAnchor.MiddleCenter;
        subRowLayout.childForceExpandWidth = false;
        subRowLayout.childForceExpandHeight = true;
        subRowLayout.childControlWidth = false;
        subRowLayout.childControlHeight = false;
        subRowGO.AddComponent<LayoutElement>().preferredHeight = 38;

        // RTL order: rightmost child is first in hierarchy when using MiddleCenter + ContentSizeFitter
        // Visual order (right to left): Name • Age • Sessions
        var nameGO2 = MakeHeaderSubText(subRowGO.transform, "Name", "---");
        MakeHeaderSeparator(subRowGO.transform);
        var ageGO2 = MakeHeaderSubText(subRowGO.transform, "Age", "");
        MakeHeaderSeparator(subRowGO.transform);
        var sessionsGO = MakeHeaderSubText(subRowGO.transform, "Sessions", "");

        // (Trophy button removed)

        // Settings gear button — right side of header
        var gearGO = new GameObject("SettingsButton");
        gearGO.transform.SetParent(headerGO.transform, false);
        var gearRT = gearGO.AddComponent<RectTransform>();
        gearRT.anchorMin = new Vector2(1, 0.5f);
        gearRT.anchorMax = new Vector2(1, 0.5f);
        gearRT.pivot = new Vector2(1, 0.5f);
        gearRT.anchoredPosition = new Vector2(-24, 0);
        gearRT.sizeDelta = new Vector2(60, 60);

        var gearImg = gearGO.AddComponent<Image>();
        gearImg.sprite = gearSprite;
        gearImg.preserveAspect = true;
        gearImg.color = Color.white;
        gearImg.raycastTarget = true;

        var gearBtn = gearGO.AddComponent<Button>();
        gearBtn.targetGraphic = gearImg;

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
            contentLayout.spacing = 21;
            contentLayout.padding = new RectOffset(25, 25, 20, 150); // extra bottom for banner ad
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
        ctrl.answerInput = inputField;
        ctrl.submitButton = submitBtn;
        ctrl.errorText = errorGO.GetComponent<TextMeshProUGUI>();
        ctrl.answerButtons = new Button[0];
        ctrl.answerLabels = new TextMeshProUGUI[0];
        ctrl.dashboardPanel = dashRT;
        ctrl.headerNameText = nameGO2.GetComponent<TextMeshProUGUI>();
        ctrl.headerAgeText = ageGO2.GetComponent<TextMeshProUGUI>();
        ctrl.headerSessionsText = sessionsGO.GetComponent<TextMeshProUGUI>();
        ctrl.backButton = backBtn;
        ctrl.trophyButton = null;
        ctrl.trophySprite = trophySprite;
        ctrl.settingsButton = gearBtn;
        ctrl.gearSprite = gearSprite;
        ctrl.tabButtons = new Button[0];
        ctrl.tabIndicators = new Image[0];
        ctrl.tabContents = tabContents;
        ctrl.roundedRect = roundedRect;
        ctrl.circleSprite = circleSprite;
        ctrl.gameDatabase = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/Games/GameDatabase.asset");

        // UI Kit sprites
        ctrl.uiCardBlue = LoadSpriteFromSheet("Assets/Art/UI/UI_2.png", "UI_2_0");
        ctrl.uiCardPurple = LoadSpriteFromSheet("Assets/Art/UI/UI_2.png", "UI_2_2");
        ctrl.uiToggleGreen = LoadSpriteFromSheet("Assets/Art/UI/UI_2.png", "UI_2_14");
        ctrl.uiToggleRed = LoadSpriteFromSheet("Assets/Art/UI/UI_2.png", "UI_2_15");
        ctrl.uiBtnRounded = LoadSpriteFromSheet("Assets/Art/UI/UI_1.png", "UI_1_41");
        ctrl.uiBtnRoundedAlt = LoadSpriteFromSheet("Assets/Art/UI/UI_1.png", "UI_1_42");
        ctrl.uiPlus = LoadSpriteFromSheet("Assets/Art/UI/UI_1.png", "UI_1_16");
        ctrl.uiMinus = LoadSpriteFromSheet("Assets/Art/UI/UI_1.png", "UI_1_30");
        ctrl.uiBarBlue = LoadSpriteFromSheet("Assets/Art/UI/UI_1.png", "UI_1_33");
        ctrl.uiBarGreen = LoadSpriteFromSheet("Assets/Art/UI/UI_1.png", "UI_1_34");
        ctrl.uiBarYellow = LoadSpriteFromSheet("Assets/Art/UI/UI_1.png", "UI_1_35");
        ctrl.uiSectionBg = LoadSpriteFromSheet("Assets/Art/UI/UI_1.png", "UI_1_49");
        ctrl.uiCheckIcon = LoadSpriteFromSheet("Assets/Art/UI/UI_1.png", "UI_1_5");
        ctrl.uiBarGray = LoadSpriteFromSheet("Assets/Art/UI/UI_1.png", "UI_1_44");
        ctrl.uiPlaceholder = LoadSpriteFromSheet("Assets/Art/UI/UI_2.png", "UI_2_6");
        ctrl.uiShareIcon = LoadSpriteFromSheet("Assets/Art/UI/UI_1.png", "UI_1_69");

        // Wire gate back button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            gateBackBtn.onClick, ctrl.OnBackPressed);

        // Ads — only in parent dashboard (behind parental gate)
        canvasGO.AddComponent<BannerAdManager>();
        canvasGO.AddComponent<InterstitialAdManager>();

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
        tmp.fontSize = 22;
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
        tmp.fontSize = 20;
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

    private static Sprite LoadSpriteFromSheet(string sheetPath, string spriteName)
    {
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
        if (allAssets != null)
            foreach (var asset in allAssets)
                if (asset is Sprite spr && spr.name == spriteName) return spr;
        return null;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
