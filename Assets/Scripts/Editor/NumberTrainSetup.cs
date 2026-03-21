using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the NumberTrain scene — landscape layout.
/// Top/center: train of wagons. Bottom: draggable number options.
/// </summary>
public class NumberTrainSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    private static readonly Color SkyColor   = HexColor("#8FD4F5");
    private static readonly Color GrassBack = HexColor("#8ED36B");
    private static readonly Color GrassFront = HexColor("#7CC95E");
    private static readonly Color BarColor   = HexColor("#43A047");

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Number Train", "Building scene...", 0.5f);
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
        cam.backgroundColor = SkyColor;
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

        // ── Sky background ──
        Layer(root, "Sky", null, 0, 0, 1, 1, SkyColor);

        // ── Grass layers ──
        Layer(root, "GrassBack", null, 0, 0, 1, 0.68f, GrassBack);
        Layer(root, "GrassFront", null, 0, 0, 1, 0.55f, GrassFront);

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

        // Title: רכבת המספרים
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(bar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(110, 0);
        titleRT.offsetMax = new Vector2(-110, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05E8\u05DB\u05D1\u05EA \u05D4\u05DE\u05E1\u05E4\u05E8\u05D9\u05DD"); // רכבת המספרים
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = Btn(bar.transform, "HomeButton", homeIcon, 16, -12, 76);

        // Trophy
        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = BtnRight(bar.transform, "TrophyButton", trophyIcon, -16, -12, 62);

        // ── Cloud system (sky area for spawning) ──
        var skyAreaGO = new GameObject("SkyArea");
        skyAreaGO.transform.SetParent(safeGO.transform, false);
        var skyAreaRT = skyAreaGO.AddComponent<RectTransform>();
        skyAreaRT.anchorMin = new Vector2(0, 0.68f); // above the grass line
        skyAreaRT.anchorMax = new Vector2(1, 1);
        skyAreaRT.offsetMin = Vector2.zero;
        skyAreaRT.offsetMax = new Vector2(0, -TopBarHeight);

        var cloudSystem = canvasGO.AddComponent<WorldCloudSystem>();
        cloudSystem.skyArea = skyAreaRT;
        cloudSystem.worldWidth = 1920f;
        cloudSystem.maxClouds = 6;
        cloudSystem.minSpeed = 10f;
        cloudSystem.maxSpeed = 25f;
        cloudSystem.minScale = 0.5f;
        cloudSystem.maxScale = 1.0f;
        cloudSystem.spawnInterval = 3f;

        // ══════════════════════════════════════════
        //  PLAY AREA
        // ══════════════════════════════════════════
        var playGO = new GameObject("PlayArea");
        playGO.transform.SetParent(safeGO.transform, false);
        var playRT = playGO.AddComponent<RectTransform>();
        playRT.anchorMin = new Vector2(0, 0);
        playRT.anchorMax = new Vector2(1, 1);
        playRT.offsetMin = new Vector2(20, 16);
        playRT.offsetMax = new Vector2(-20, -TopBarHeight);

        // ── Rail track (aligned with wagon wheels) ──
        BuildRailTrack(playRT, 0.42f);

        // ── Train area (higher up, on the grass) ──
        var trainGO = new GameObject("TrainArea");
        trainGO.transform.SetParent(playRT, false);
        var trainRT = trainGO.AddComponent<RectTransform>();
        trainRT.anchorMin = new Vector2(0.02f, 0.44f);
        trainRT.anchorMax = new Vector2(0.98f, 0.82f);
        trainRT.offsetMin = Vector2.zero;
        trainRT.offsetMax = Vector2.zero;

        // ── Options area (on the grass, below track) ──
        var optionsGO = new GameObject("OptionsArea");
        optionsGO.transform.SetParent(playRT, false);
        var optionsRT = optionsGO.AddComponent<RectTransform>();
        optionsRT.anchorMin = new Vector2(0.15f, 0.06f);
        optionsRT.anchorMax = new Vector2(0.85f, 0.34f);
        optionsRT.offsetMin = Vector2.zero;
        optionsRT.offsetMax = Vector2.zero;

        // ══════════════════════════════════════════
        //  CONTROLLER
        // ══════════════════════════════════════════
        var ctrl = canvasGO.AddComponent<NumberTrainController>();
        ctrl.trainArea = trainRT;
        ctrl.optionsArea = optionsRT;
        ctrl.titleText = titleTMP;
        ctrl.cellSprite = roundedRect;
        ctrl.circleSprite = circleSprite;
        Sprite locoSprite = null;
        var locoAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/TrainDriver.png");
        if (locoAssets != null) foreach (var a in locoAssets) if (a is Sprite s) { locoSprite = s; break; }
        ctrl.locomotiveSprite = locoSprite;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = roundedRect;
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "numbertrain";

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/NumberTrain.unity");
    }

    // ── RAIL TRACK ──

    private static readonly Color RailColor = HexColor("#5D4037");    // dark brown
    private static readonly Color SleeperColor = HexColor("#795548"); // lighter brown

    private static void BuildRailTrack(RectTransform parent, float yCenter)
    {
        var trackGO = new GameObject("RailTrack");
        trackGO.transform.SetParent(parent, false);
        var trackRT = trackGO.AddComponent<RectTransform>();
        trackRT.anchorMin = new Vector2(0, yCenter - 0.02f);
        trackRT.anchorMax = new Vector2(1, yCenter + 0.02f);
        trackRT.offsetMin = Vector2.zero;
        trackRT.offsetMax = Vector2.zero;

        // Sleepers (wooden ties across the track)
        int sleeperCount = 30;
        for (int i = 0; i < sleeperCount; i++)
        {
            var sleeperGO = new GameObject($"Sleeper_{i}");
            sleeperGO.transform.SetParent(trackGO.transform, false);
            var srt = sleeperGO.AddComponent<RectTransform>();
            float t = (float)i / (sleeperCount - 1);
            srt.anchorMin = new Vector2(t - 0.008f, 0.1f);
            srt.anchorMax = new Vector2(t + 0.008f, 0.9f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            var simg = sleeperGO.AddComponent<Image>();
            simg.color = SleeperColor;
            simg.raycastTarget = false;
        }

        // Top rail
        var topRailGO = new GameObject("TopRail");
        topRailGO.transform.SetParent(trackGO.transform, false);
        var trRT = topRailGO.AddComponent<RectTransform>();
        trRT.anchorMin = new Vector2(0, 0.65f);
        trRT.anchorMax = new Vector2(1, 0.80f);
        trRT.offsetMin = Vector2.zero;
        trRT.offsetMax = Vector2.zero;
        var trImg = topRailGO.AddComponent<Image>();
        trImg.color = RailColor;
        trImg.raycastTarget = false;

        // Bottom rail
        var botRailGO = new GameObject("BottomRail");
        botRailGO.transform.SetParent(trackGO.transform, false);
        var brRT = botRailGO.AddComponent<RectTransform>();
        brRT.anchorMin = new Vector2(0, 0.20f);
        brRT.anchorMax = new Vector2(1, 0.35f);
        brRT.offsetMin = Vector2.zero;
        brRT.offsetMax = Vector2.zero;
        var brImg = botRailGO.AddComponent<Image>();
        brImg.color = RailColor;
        brImg.raycastTarget = false;
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
