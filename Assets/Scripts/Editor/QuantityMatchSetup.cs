using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the QuantityMatch scene — landscape layout.
/// Top: large target number. Bottom: 4 animal-group tiles.
/// </summary>
public class QuantityMatchSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    private static readonly Color BgColor  = WoodTableBackground.TableBaseColor;
    private static readonly Color BarColor = WoodTableBackground.HeaderColor;
    private static readonly Color NumberClr  = HexColor("#4527A0");

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Quantity Match", "Building scene...", 0.5f);
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

        // ══════════════════════════════════════════
        //  TOP BAR
        // ══════════════════════════════════════════
        var bar = CreateBar(safeGO.transform);

        // Title: התאם כמות
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(bar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(110, 0);
        titleRT.offsetMax = new Vector2(-110, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D4\u05EA\u05D0\u05DD \u05DB\u05DE\u05D5\u05EA \u05DC\u05DE\u05E1\u05E4\u05E8"); // התאם כמות למספר
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = WoodTableBackground.TitleTextColor;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button
        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = Btn(bar.transform, "HomeButton", homeIcon, 16, -12, 76);

        // Trophy button
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

        // ── Number area (top portion: big target number) ──
        var numAreaGO = new GameObject("NumberArea");
        numAreaGO.transform.SetParent(playRT, false);
        var numAreaRT = numAreaGO.AddComponent<RectTransform>();
        numAreaRT.anchorMin = new Vector2(0.30f, 0.58f);
        numAreaRT.anchorMax = new Vector2(0.70f, 0.98f);
        numAreaRT.offsetMin = Vector2.zero;
        numAreaRT.offsetMax = Vector2.zero;

        // Target number text
        var numGO = new GameObject("TargetNumber");
        numGO.transform.SetParent(numAreaRT, false);
        var numRT = numGO.AddComponent<RectTransform>();
        Full(numRT);
        var numTMP = numGO.AddComponent<TextMeshProUGUI>();
        numTMP.text = "?";
        numTMP.fontSize = 140;
        numTMP.fontStyle = FontStyles.Bold;
        numTMP.color = NumberClr;
        numTMP.alignment = TextAlignmentOptions.Center;
        numTMP.raycastTarget = false;

        // ── Tiles area (bottom portion: 4 answer tiles) ──
        var tilesGO = new GameObject("TilesArea");
        tilesGO.transform.SetParent(playRT, false);
        var tilesRT = tilesGO.AddComponent<RectTransform>();
        tilesRT.anchorMin = new Vector2(0.02f, 0.02f);
        tilesRT.anchorMax = new Vector2(0.98f, 0.55f);
        tilesRT.offsetMin = Vector2.zero;
        tilesRT.offsetMax = Vector2.zero;

        // ══════════════════════════════════════════
        //  CONTROLLER
        // ══════════════════════════════════════════
        var ctrl = canvasGO.AddComponent<QuantityMatchController>();
        ctrl.numberArea = numAreaRT;
        ctrl.tilesArea = tilesRT;
        ctrl.targetNumberText = numTMP;
        ctrl.titleText = titleTMP;
        ctrl.cellSprite = roundedRect;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        // Wire trophy / leaderboard
        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = roundedRect;
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "quantitymatch";

        // Tutorial hand
        TutorialHandHelper.Create(safeGO.transform, TutorialHandHelper.Anim.Tap,
            new Vector2(0, -50), new Vector2(450, 450), "quantitymatch");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/QuantityMatch.unity");
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
