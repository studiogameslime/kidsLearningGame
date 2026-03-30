using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the ConnectMatch scene — landscape split layout.
/// Left: reference pattern. Right: interactive dot grid.
/// </summary>
public class ConnectMatchSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    private static readonly Color BgColor  = WoodTableBackground.TableBaseColor;
    private static readonly Color BarColor = WoodTableBackground.HeaderColor;
    private static readonly Color PanelBg    = HexColor("#FAFAFA");
    private static readonly Color PanelBorder = HexColor("#E0E0E0");

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Connect Match", "Building scene...", 0.5f);
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

        // ══════════════════════════════════════════
        //  TOP BAR
        // ══════════════════════════════════════════
        var bar = CreateBar(safeGO.transform);

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(bar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(110, 0);
        titleRT.offsetMax = new Vector2(-110, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D7\u05D1\u05E8 \u05D5\u05E6\u05D9\u05D9\u05E8"); // חבר וצייר
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = WoodTableBackground.TitleTextColor;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = Btn(bar.transform, "HomeButton", homeIcon, 16, -12, 76);
        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = BtnRight(bar.transform, "TrophyButton", trophyIcon, -16, -12, 62);

        // ══════════════════════════════════════════
        //  PLAY AREA
        // ══════════════════════════════════════════

        var boardContent = WoodTableBackground.CreateBoardPanel(safeGO.transform, roundedRect,
            0.01f, 0.01f, 0.99f, 1f - (float)TopBarHeight / Ref.y - 0.01f);

        var playGO = new GameObject("PlayArea");
        playGO.transform.SetParent(boardContent, false);
        var playRT = playGO.AddComponent<RectTransform>();
        playRT.anchorMin = new Vector2(0, 0);
        playRT.anchorMax = new Vector2(1, 1);
        playRT.offsetMin = Vector2.zero;
        playRT.offsetMax = Vector2.zero;

        // ── LEFT HALF: Reference pattern ──
        var refPanelGO = new GameObject("ReferencePanel");
        refPanelGO.transform.SetParent(playRT, false);
        var refPanelRT = refPanelGO.AddComponent<RectTransform>();
        refPanelRT.anchorMin = new Vector2(0.01f, 0.04f);
        refPanelRT.anchorMax = new Vector2(0.48f, 0.96f);
        refPanelRT.offsetMin = Vector2.zero;
        refPanelRT.offsetMax = Vector2.zero;
        // Border
        var refBorderGO = new GameObject("Border");
        refBorderGO.transform.SetParent(refPanelGO.transform, false);
        var rbrt = refBorderGO.AddComponent<RectTransform>();
        Full(rbrt);
        rbrt.offsetMin = new Vector2(-3, -3);
        rbrt.offsetMax = new Vector2(3, 3);
        refBorderGO.transform.SetAsFirstSibling();
        var rbimg = refBorderGO.AddComponent<Image>();
        if (roundedRect != null) { rbimg.sprite = roundedRect; rbimg.type = Image.Type.Sliced; }
        rbimg.color = PanelBorder;
        rbimg.raycastTarget = false;
        // Background
        var refBgImg = refPanelGO.AddComponent<Image>();
        if (roundedRect != null) { refBgImg.sprite = roundedRect; refBgImg.type = Image.Type.Sliced; }
        refBgImg.color = PanelBg;
        refBgImg.raycastTarget = false;

        // Reference line container (inside panel)
        var refLineGO = new GameObject("RefLineContainer");
        refLineGO.transform.SetParent(refPanelGO.transform, false);
        var refLineRT = refLineGO.AddComponent<RectTransform>();
        Full(refLineRT);

        // Label
        var refLabelGO = new GameObject("RefLabel");
        refLabelGO.transform.SetParent(refPanelGO.transform, false);
        var refLabelRT = refLabelGO.AddComponent<RectTransform>();
        refLabelRT.anchorMin = new Vector2(0.1f, 0.88f);
        refLabelRT.anchorMax = new Vector2(0.9f, 0.98f);
        refLabelRT.offsetMin = Vector2.zero;
        refLabelRT.offsetMax = Vector2.zero;
        var refLabelTMP = refLabelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(refLabelTMP, "\u05D3\u05D5\u05D2\u05DE\u05D4"); // דוגמה
        refLabelTMP.fontSize = 24;
        refLabelTMP.color = HexColor("#757575");
        refLabelTMP.alignment = TextAlignmentOptions.Center;
        refLabelTMP.raycastTarget = false;

        // ── RIGHT HALF: Interactive grid ──
        var interPanelGO = new GameObject("InteractivePanel");
        interPanelGO.transform.SetParent(playRT, false);
        var interPanelRT = interPanelGO.AddComponent<RectTransform>();
        interPanelRT.anchorMin = new Vector2(0.52f, 0.04f);
        interPanelRT.anchorMax = new Vector2(0.99f, 0.96f);
        interPanelRT.offsetMin = Vector2.zero;
        interPanelRT.offsetMax = Vector2.zero;
        // Border
        var intBorderGO = new GameObject("Border");
        intBorderGO.transform.SetParent(interPanelGO.transform, false);
        var ibrt = intBorderGO.AddComponent<RectTransform>();
        Full(ibrt);
        ibrt.offsetMin = new Vector2(-3, -3);
        ibrt.offsetMax = new Vector2(3, 3);
        intBorderGO.transform.SetAsFirstSibling();
        var ibimg = intBorderGO.AddComponent<Image>();
        if (roundedRect != null) { ibimg.sprite = roundedRect; ibimg.type = Image.Type.Sliced; }
        ibimg.color = PanelBorder;
        ibimg.raycastTarget = false;
        // Background
        var intBgImg = interPanelGO.AddComponent<Image>();
        if (roundedRect != null) { intBgImg.sprite = roundedRect; intBgImg.type = Image.Type.Sliced; }
        intBgImg.color = PanelBg;
        intBgImg.raycastTarget = true; // receive touch

        // ══════════════════════════════════════════
        //  CONTROLLER
        // ══════════════════════════════════════════
        var ctrl = canvasGO.AddComponent<ConnectMatchController>();
        ctrl.referenceArea = refPanelRT;
        ctrl.playArea = interPanelRT;
        ctrl.refLineContainer = refLineRT;
        ctrl.playLineContainer = null; // lines parented to playArea directly
        ctrl.titleText = titleTMP;
        ctrl.dotSprite = circleSprite;
        ctrl.cellSprite = roundedRect;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = roundedRect;
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "connectmatch";

        // Tutorial hand
        TutorialHandHelper.Create(safeGO.transform, TutorialHandHelper.Anim.Tap,
            new Vector2(-100, 0), new Vector2(450, 450), "connectmatch");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ConnectMatch.unity");
    }

    // ── HELPERS ──

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
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
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
