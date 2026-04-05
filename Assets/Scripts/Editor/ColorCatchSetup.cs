using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the ColorCatch scene — falling items catching game with colored basket.
/// </summary>
public class ColorCatchSetup
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    // Wood table colors (same as ColorSort / Memory)
    private static readonly Color TableBaseColor    = HexColor("#5C3D2E");
    private static readonly Color BoardWoodA        = HexColor("#8B6B4A");
    private static readonly Color BoardWoodB        = HexColor("#7E6042");
    private static readonly Color PlankSepColor     = HexColor("#5A4030");
    private static readonly Color BoardEdgeColor    = HexColor("#6B4D38");
    private static readonly Color BoardInnerRim     = HexColor("#A08060");
    private static readonly Color HeaderColor       = new Color(0.35f, 0.22f, 0.12f, 0.75f);
    private static readonly int TopBarHeight  = SetupConstants.HeaderHeight;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Color Catch Setup", "Building scene…", 0.6f);
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
        cam.backgroundColor = TableBaseColor;
        cam.orthographic = true;
        var urpType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpType != null) camGO.AddComponent(urpType);

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inputType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputType != null) esGO.AddComponent(inputType);
        else esGO.AddComponent<StandaloneInputModule>();

        var canvasGO = new GameObject("ColorCatchCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");

        // ═══ WOOD TABLE BACKGROUND ═══

        // Base dark brown table
        var bgGO = StretchImg(canvasGO.transform, "Background", TableBaseColor);
        bgGO.GetComponent<Image>().raycastTarget = false;

        // Board panel (wood plank surface)
        var boardGO = new GameObject("BoardPanel");
        boardGO.transform.SetParent(canvasGO.transform, false);
        var boardRT = boardGO.AddComponent<RectTransform>();
        boardRT.anchorMin = new Vector2(0.005f, 0.01f);
        boardRT.anchorMax = new Vector2(0.995f, 0.90f);
        boardRT.offsetMin = Vector2.zero;
        boardRT.offsetMax = Vector2.zero;

        var boardImg = boardGO.AddComponent<Image>();
        if (roundedRect != null) { boardImg.sprite = roundedRect; boardImg.type = Image.Type.Sliced; }
        boardImg.color = BoardEdgeColor;
        boardImg.raycastTarget = false;
        boardGO.AddComponent<Shadow>().effectColor = new Color(0.12f, 0.06f, 0.02f, 0.5f);

        // Inner rim
        var rimGO = new GameObject("InnerRim");
        rimGO.transform.SetParent(boardGO.transform, false);
        var rimRT = rimGO.AddComponent<RectTransform>();
        Full(rimRT);
        rimRT.offsetMin = new Vector2(2, 2);
        rimRT.offsetMax = new Vector2(-2, -2);
        var rimImg = rimGO.AddComponent<Image>();
        if (roundedRect != null) { rimImg.sprite = roundedRect; rimImg.type = Image.Type.Sliced; }
        rimImg.color = BoardInnerRim;
        rimImg.raycastTarget = false;

        // Wood planks
        var woodGO = new GameObject("WoodSurface");
        woodGO.transform.SetParent(boardGO.transform, false);
        var woodRT = woodGO.AddComponent<RectTransform>();
        Full(woodRT);
        woodRT.offsetMin = new Vector2(3, 3);
        woodRT.offsetMax = new Vector2(-3, -3);
        CreateWoodPlanks(woodGO.transform, roundedRect);

        // ═══ PLAY AREA ═══

        var playAreaGO = new GameObject("PlayArea");
        playAreaGO.transform.SetParent(canvasGO.transform, false);
        var playAreaRT = playAreaGO.AddComponent<RectTransform>();
        playAreaRT.anchorMin = new Vector2(0.02f, 0.02f);
        playAreaRT.anchorMax = new Vector2(0.98f, 0.88f);
        playAreaRT.offsetMin = Vector2.zero;
        playAreaRT.offsetMax = Vector2.zero;

        // ═══ PROGRESS BAR ═══

        var progressBG = new GameObject("ProgressBarBG");
        progressBG.transform.SetParent(canvasGO.transform, false);
        var progBGRT = progressBG.AddComponent<RectTransform>();
        progBGRT.anchorMin = new Vector2(0.3f, 0.82f);
        progBGRT.anchorMax = new Vector2(0.7f, 0.85f);
        progBGRT.offsetMin = Vector2.zero;
        progBGRT.offsetMax = Vector2.zero;
        var progBGImg = progressBG.AddComponent<Image>();
        if (roundedRect != null) { progBGImg.sprite = roundedRect; progBGImg.type = Image.Type.Sliced; }
        progBGImg.color = new Color(0.2f, 0.12f, 0.06f, 0.6f);
        progBGImg.raycastTarget = false;

        // Progress fill
        var progressFillGO = new GameObject("ProgressFill");
        progressFillGO.transform.SetParent(progressBG.transform, false);
        var progFillRT = progressFillGO.AddComponent<RectTransform>();
        Full(progFillRT);
        progFillRT.offsetMin = new Vector2(4, 4);
        progFillRT.offsetMax = new Vector2(-4, -4);
        var progFillImg = progressFillGO.AddComponent<Image>();
        if (roundedRect != null) { progFillImg.sprite = roundedRect; progFillImg.type = Image.Type.Sliced; }
        progFillImg.color = HexColor("#66BB6A");
        progFillImg.type = Image.Type.Filled;
        progFillImg.fillMethod = Image.FillMethod.Horizontal;
        progFillImg.fillAmount = 0f;
        progFillImg.raycastTarget = false;

        // Progress text
        var progressTextGO = new GameObject("ProgressText");
        progressTextGO.transform.SetParent(progressBG.transform, false);
        var progTextRT = progressTextGO.AddComponent<RectTransform>();
        Full(progTextRT);
        var progTextTMP = progressTextGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(progTextTMP, "0/0");
        progTextTMP.fontSize = 24;
        progTextTMP.fontStyle = FontStyles.Bold;
        progTextTMP.color = Color.white;
        progTextTMP.alignment = TextAlignmentOptions.Center;
        progTextTMP.raycastTarget = false;

        // ═══ SAFE AREA + UI ═══

        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        Full(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        var topBar = StretchImg(safeArea.transform, "TopBar", HeaderColor);
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.sizeDelta = new Vector2(0, TopBarHeight);
        topBar.GetComponent<Image>().raycastTarget = false;
        topBar.AddComponent<ThemeHeader>();

        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05EA\u05E4\u05D5\u05E1 \u05E6\u05D1\u05E2\u05D9\u05DD"); // תפוס צבעים
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(1f, 0.96f, 0.88f);
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = IconBtn(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = IconBtn(topBar.transform, "TrophyButton", trophyIcon,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-16, 0), new Vector2(70, 70));

        // ═══ CONTROLLER ═══

        var controller = canvasGO.AddComponent<ColorCatchController>();
        controller.playArea = playAreaRT;
        controller.titleText = titleTMP;
        controller.progressFill = progFillImg;
        controller.progressText = progTextTMP;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = LoadSprite("Assets/UI/Sprites/RoundedRect.png");
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "colorcatch";

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ColorCatch.unity");
        Debug.Log("[ColorCatchSetup] Scene created: Assets/Scenes/ColorCatch.unity");
    }

    // ═══ HELPERS ═══

    private static void CreateWoodPlanks(Transform parent, Sprite roundedRect)
    {
        int plankCount = 8;
        for (int i = 0; i < plankCount; i++)
        {
            float yMin = (float)i / plankCount;
            float yMax = (float)(i + 1) / plankCount;

            var plankGO = new GameObject($"Plank_{i}");
            plankGO.transform.SetParent(parent, false);
            var prt = plankGO.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0, yMin);
            prt.anchorMax = new Vector2(1, yMax);
            prt.offsetMin = new Vector2(0, i > 0 ? 1 : 0);
            prt.offsetMax = Vector2.zero;
            var pimg = plankGO.AddComponent<Image>();
            pimg.color = (i % 2 == 0) ? BoardWoodA : BoardWoodB;
            pimg.raycastTarget = false;
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static GameObject StretchImg(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Full(go.AddComponent<RectTransform>());
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static void Full(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject IconBtn(Transform parent, string name, Sprite icon,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        if (icon != null) img.sprite = icon;
        img.preserveAspect = true;
        img.color = Color.white;
        go.AddComponent<Button>();
        return go;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
