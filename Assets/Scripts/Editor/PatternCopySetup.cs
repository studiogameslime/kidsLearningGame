using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the PatternCopy scene — portrait layout.
/// Side-by-side: source pattern (LEFT), player grid (RIGHT).
/// No check button — auto-wins when pattern matches.
/// </summary>
public class PatternCopySetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    // Wood table theme
    private static readonly Color BgColor  = WoodTableBackground.TableBaseColor;
    private static readonly Color BarColor = WoodTableBackground.HeaderColor;
    private static readonly Color GridBgColor     = HexColor("#FFFFFF");
    private static readonly Color GridBorderColor = HexColor("#E0E0E0");
    private static readonly Color LabelColor      = HexColor("#5D4037");
    private static readonly Color ScoreColor      = HexColor("#424242");

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Pattern Copy", "Building scene...", 0.5f);
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
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        var root = canvasGO.transform;

        // ── Wood Background ──
        WoodTableBackground.CreateBackground(root);

        // ── Safe Area ──
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(root, false);
        var safeRT = safeGO.AddComponent<RectTransform>();
        Full(safeRT);
        safeGO.AddComponent<SafeAreaHandler>();

        // ── Top Bar ──
        var bar = CreateBar(safeGO.transform);

        // Title: העתק את הדפוס
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(bar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D4\u05E2\u05EA\u05E7\u05EA \u05E6\u05D5\u05E8\u05D4"); // העתקת צורה
        titleTMP.fontSize = 38;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = WoodTableBackground.TitleTextColor;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = Btn(bar.transform, "HomeButton", homeIcon, 16, -20, 90);

        // Trophy button
        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = BtnRight(bar.transform, "TrophyButton", trophyIcon, -16, -20, 70);

        // ══════════════════════════════════════════
        //  PLAY AREA
        // ══════════════════════════════════════════

        var boardContent = WoodTableBackground.CreateBoardPanel(safeGO.transform, roundedRect,
            0.01f, 0.01f, 0.99f, 0.92f);

        var playGO = new GameObject("PlayArea");
        playGO.transform.SetParent(boardContent, false);
        var playRT = playGO.AddComponent<RectTransform>();
        playRT.anchorMin = new Vector2(0, 0);
        playRT.anchorMax = new Vector2(1, 1);
        playRT.offsetMin = Vector2.zero;
        playRT.offsetMax = Vector2.zero;

        // ══════════════════════════════════════════
        //  SIDE-BY-SIDE GRIDS (source LEFT, player RIGHT)
        //  Hebrew RTL: "דוגמה" on right visually = source on LEFT in code
        // ══════════════════════════════════════════

        // ── Source Label (above left grid) ──
        var srcLabelGO = new GameObject("SourceLabel");
        srcLabelGO.transform.SetParent(playRT, false);
        var srcLabelRT = srcLabelGO.AddComponent<RectTransform>();
        srcLabelRT.anchorMin = new Vector2(0, 0.92f);
        srcLabelRT.anchorMax = new Vector2(0.48f, 0.98f);
        srcLabelRT.offsetMin = Vector2.zero;
        srcLabelRT.offsetMax = Vector2.zero;
        var srcLabelTMP = srcLabelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(srcLabelTMP, "\u05D3\u05D5\u05D2\u05DE\u05D4"); // דוגמה (Example)
        srcLabelTMP.fontSize = 28;
        srcLabelTMP.fontStyle = FontStyles.Bold;
        srcLabelTMP.color = LabelColor;
        srcLabelTMP.alignment = TextAlignmentOptions.Center;
        srcLabelTMP.raycastTarget = false;

        // ── Source Grid (LEFT half, large square) ──
        var srcGridGO = new GameObject("SourceGrid");
        srcGridGO.transform.SetParent(playRT, false);
        var srcGridRT = srcGridGO.AddComponent<RectTransform>();
        srcGridRT.anchorMin = new Vector2(0, 0.10f);
        srcGridRT.anchorMax = new Vector2(0.48f, 0.91f);
        srcGridRT.offsetMin = Vector2.zero;
        srcGridRT.offsetMax = Vector2.zero;
        var srcGridImg = srcGridGO.AddComponent<Image>();
        if (roundedRect != null) { srcGridImg.sprite = roundedRect; srcGridImg.type = Image.Type.Sliced; }
        srcGridImg.color = GridBgColor;
        srcGridImg.raycastTarget = false;

        // Source border
        var srcBorderGO = new GameObject("Border");
        srcBorderGO.transform.SetParent(srcGridGO.transform, false);
        var srcBorderRT = srcBorderGO.AddComponent<RectTransform>();
        Full(srcBorderRT);
        srcBorderRT.offsetMin = new Vector2(-3, -3);
        srcBorderRT.offsetMax = new Vector2(3, 3);
        srcBorderGO.transform.SetAsFirstSibling();
        var srcBorderImg = srcBorderGO.AddComponent<Image>();
        if (roundedRect != null) { srcBorderImg.sprite = roundedRect; srcBorderImg.type = Image.Type.Sliced; }
        srcBorderImg.color = GridBorderColor;
        srcBorderImg.raycastTarget = false;

        // ── Player Label (above right grid) ──
        var plrLabelGO = new GameObject("PlayerLabel");
        plrLabelGO.transform.SetParent(playRT, false);
        var plrLabelRT = plrLabelGO.AddComponent<RectTransform>();
        plrLabelRT.anchorMin = new Vector2(0.52f, 0.92f);
        plrLabelRT.anchorMax = new Vector2(1, 0.98f);
        plrLabelRT.offsetMin = Vector2.zero;
        plrLabelRT.offsetMax = Vector2.zero;
        var plrLabelTMP = plrLabelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(plrLabelTMP, "\u05D4\u05DC\u05D5\u05D7 \u05E9\u05DC\u05D9"); // הלוח שלי (My Board)
        plrLabelTMP.fontSize = 28;
        plrLabelTMP.fontStyle = FontStyles.Bold;
        plrLabelTMP.color = LabelColor;
        plrLabelTMP.alignment = TextAlignmentOptions.Center;
        plrLabelTMP.raycastTarget = false;

        // ── Player Grid (RIGHT half, large square) ──
        var plrGridGO = new GameObject("PlayerGrid");
        plrGridGO.transform.SetParent(playRT, false);
        var plrGridRT = plrGridGO.AddComponent<RectTransform>();
        plrGridRT.anchorMin = new Vector2(0.52f, 0.10f);
        plrGridRT.anchorMax = new Vector2(1, 0.91f);
        plrGridRT.offsetMin = Vector2.zero;
        plrGridRT.offsetMax = Vector2.zero;
        var plrGridImg = plrGridGO.AddComponent<Image>();
        if (roundedRect != null) { plrGridImg.sprite = roundedRect; plrGridImg.type = Image.Type.Sliced; }
        plrGridImg.color = GridBgColor;
        plrGridImg.raycastTarget = false;

        // Player border
        var plrBorderGO = new GameObject("Border");
        plrBorderGO.transform.SetParent(plrGridGO.transform, false);
        var plrBorderRT = plrBorderGO.AddComponent<RectTransform>();
        Full(plrBorderRT);
        plrBorderRT.offsetMin = new Vector2(-3, -3);
        plrBorderRT.offsetMax = new Vector2(3, 3);
        plrBorderGO.transform.SetAsFirstSibling();
        var plrBorderImg = plrBorderGO.AddComponent<Image>();
        if (roundedRect != null) { plrBorderImg.sprite = roundedRect; plrBorderImg.type = Image.Type.Sliced; }
        plrBorderImg.color = GridBorderColor;
        plrBorderImg.raycastTarget = false;

        // ── Score Text (bottom center) ──
        var scoreGO = new GameObject("ScoreText");
        scoreGO.transform.SetParent(playRT, false);
        var scoreRT = scoreGO.AddComponent<RectTransform>();
        scoreRT.anchorMin = new Vector2(0.2f, 0.01f);
        scoreRT.anchorMax = new Vector2(0.8f, 0.09f);
        scoreRT.offsetMin = Vector2.zero;
        scoreRT.offsetMax = Vector2.zero;
        var scoreTMP = scoreGO.AddComponent<TextMeshProUGUI>();
        scoreTMP.text = "0/0";
        scoreTMP.fontSize = 36;
        scoreTMP.color = ScoreColor;
        scoreTMP.alignment = TextAlignmentOptions.Center;
        scoreTMP.raycastTarget = false;

        // ══════════════════════════════════════════
        //  CONTROLLER
        // ══════════════════════════════════════════

        var ctrl = canvasGO.AddComponent<PatternCopyController>();
        ctrl.sourceGridParent = srcGridRT;
        ctrl.playerGridParent = plrGridRT;
        ctrl.playArea = playRT;
        ctrl.titleText = titleTMP;
        ctrl.scoreText = scoreTMP;
        ctrl.cellSprite = roundedRect;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        // Wire trophy / leaderboard
        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = roundedRect;
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "patterncopy";

        // Tutorial hand
        TutorialHandHelper.Create(safeGO.transform, TutorialHandHelper.Anim.Tap,
            new Vector2(150, -50), new Vector2(450, 450), "patterncopy");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/PatternCopy.unity");
    }

    // ── HELPERS ──────────────────────────────────────────────────

    private static GameObject Layer(Transform p, string name, Sprite spr,
        float x0, float y0, float x1, float y1, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        if (spr != null) img.sprite = spr;
        img.type = Image.Type.Simple;
        img.color = c;
        img.raycastTarget = false;
        return go;
    }

    private static GameObject CreateBar(Transform parent)
    {
        var bar = new GameObject("TopBar");
        bar.transform.SetParent(parent, false);
        var barRT = bar.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1);
        barRT.anchorMax = new Vector2(1, 1);
        barRT.pivot = new Vector2(0.5f, 1);
        barRT.sizeDelta = new Vector2(0, TopBarHeight);
        var barImg = bar.AddComponent<Image>();
        barImg.color = BarColor;
        barImg.raycastTarget = false;
        bar.AddComponent<ThemeHeader>();
        return bar;
    }

    private static GameObject Btn(Transform p, string name, Sprite icon, float x, float y, float sz)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(sz, sz);
        var img = go.AddComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = true;
        go.AddComponent<Button>().targetGraphic = img;
        return go;
    }

    private static GameObject BtnRight(Transform p, string name, Sprite icon, float x, float y, float sz)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(sz, sz);
        var img = go.AddComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = true;
        go.AddComponent<Button>().targetGraphic = img;
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

    private static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        if (all != null)
            foreach (var o in all)
                if (o is Sprite sp) return sp;
        return null;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
