using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the FishingGame scene with layered environment:
///   Sky + clouds → sea surface → Elroey on waterline → layered water → fish → sand
/// Landscape 1920x1080.
/// </summary>
public class FishingGameSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    // Colors
    private static readonly Color SkyColor      = HexColor("#A8DEFF");
    private static readonly Color SeaSurface    = HexColor("#8EDCF3");
    private static readonly Color SeaTop        = HexColor("#5EC6E8");
    private static readonly Color SeaMid        = HexColor("#37A9D6");
    private static readonly Color SeaDeep       = HexColor("#2A8CC4");
    private static readonly Color SandColor     = HexColor("#F2D9A0");
    private static readonly Color SandDark      = HexColor("#D4B87A");
    private static readonly Color FoamColor     = new Color(1f, 1f, 1f, 0.25f);
    private static readonly Color TopBarColor   = new Color(0.37f, 0.78f, 0.91f, 0.85f);
    private static readonly Color BubbleColor   = new Color(1f, 1f, 1f, 0.92f);

    // Layout anchors (fraction of screen height from bottom)
    private const float WaterlineAnchor = 0.52f;   // where water surface meets sky
    private const float SeaTopAnchor    = 0.48f;    // lighter water band
    private const float SeaMidAnchor    = 0.30f;    // mid-depth water
    private const float SeaDeepAnchor   = 0.12f;    // deep water ends here
    private const float SandAnchor      = 0.08f;    // sand height

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Fishing Game Setup", "Updating game data…", 0.2f);
            UpdateGameData();
            EditorUtility.DisplayProgressBar("Fishing Game Setup", "Building scene…", 0.5f);
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
        var game = AssetDatabase.LoadAssetAtPath<GameItemData>("Assets/Data/Games/FishingGame.asset");
        if (game == null) return;
        game.targetSceneName = "FishingGame";
        game.hasSubItems = false;
        EditorUtility.SetDirty(game);
    }

    private static void BuildScene()
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");

        // ── Camera ──
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera"; camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SkyColor;
        cam.orthographic = true;
        var urp = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urp != null) camGO.AddComponent(urp);

        // ── EventSystem ──
        var esGO = new GameObject("EventSystem"); esGO.AddComponent<EventSystem>();
        var inp = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inp != null) esGO.AddComponent(inp); else esGO.AddComponent<StandaloneInputModule>();

        // ── Canvas ──
        var canvasGO = new GameObject("FishingUICanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(canvasGO.transform, false);
        Full(safeGO.AddComponent<RectTransform>());
        safeGO.AddComponent<SafeAreaHandler>();

        // ═══════════════════════════════════════════
        //  LAYER 1: SKY
        // ═══════════════════════════════════════════

        var skyGO = StretchImage(safeGO.transform, "skyLayer", SkyColor);
        skyGO.GetComponent<Image>().raycastTarget = false;

        // ═══════════════════════════════════════════
        //  LAYER 2: CLOUDS (from existing world assets)
        // ═══════════════════════════════════════════

        string[] cloudFiles = { "cloud1", "cloud3", "cloud5", "cloud7" };
        float[] cloudX  = { 0.12f, 0.42f, 0.72f, 0.88f };
        float[] cloudY  = { 0.82f, 0.88f, 0.85f, 0.78f };
        float[] cloudW  = { 200f,  160f,  180f,  140f  };

        for (int i = 0; i < cloudFiles.Length; i++)
        {
            var cloudSprite = LoadSprite($"Assets/Art/World/{cloudFiles[i]}.png");
            if (cloudSprite == null) continue;

            var cloudGO = new GameObject($"cloudLayer{i + 1}");
            cloudGO.transform.SetParent(safeGO.transform, false);
            var crt = cloudGO.AddComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(cloudX[i], cloudY[i]);
            crt.sizeDelta = new Vector2(cloudW[i], cloudW[i] * 0.5f);
            var cimg = cloudGO.AddComponent<Image>();
            cimg.sprite = cloudSprite;
            cimg.preserveAspect = true;
            cimg.color = new Color(1f, 1f, 1f, 0.8f);
            cimg.raycastTarget = false;
        }

        // ═══════════════════════════════════════════
        //  LAYER 3: SEA — multiple depth bands
        // ═══════════════════════════════════════════

        // Sea surface (thin bright band at waterline)
        Band(safeGO.transform, "seaSurface", SeaSurface, WaterlineAnchor - 0.02f, WaterlineAnchor + 0.01f);

        // Foam overlay (white semi-transparent strip at waterline)
        Band(safeGO.transform, "foamLayer", FoamColor, WaterlineAnchor - 0.005f, WaterlineAnchor + 0.008f);

        // Sea top (lighter water)
        Band(safeGO.transform, "seaTop", SeaTop, SeaMidAnchor, WaterlineAnchor - 0.02f);

        // Sea mid (main swimming area)
        Band(safeGO.transform, "seaMid", SeaMid, SeaDeepAnchor, SeaMidAnchor);

        // Sea deep (darker bottom water)
        Band(safeGO.transform, "seaDeep", SeaDeep, SandAnchor, SeaDeepAnchor);

        // ═══════════════════════════════════════════
        //  LAYER 4: SAND / SEABED
        // ═══════════════════════════════════════════

        Band(safeGO.transform, "sandLayer", SandColor, 0f, SandAnchor);
        // Darker sand accent line at top of sand
        Band(safeGO.transform, "sandAccent", SandDark, SandAnchor - 0.005f, SandAnchor + 0.005f);

        // ═══════════════════════════════════════════
        //  HEADER (on top of everything)
        // ═══════════════════════════════════════════

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
        HebrewText.SetText(titleTMP, "\u05D3\u05D9\u05D2"); // דיג
        titleTMP.fontSize = 36; titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white; titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = IconBtn(bar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(90, 90));

        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = IconBtn(bar.transform, "TrophyButton", trophyIcon,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(112, -30), new Vector2(70, 70));

        // Progress text
        var progGO = new GameObject("ProgressText");
        progGO.transform.SetParent(bar.transform, false);
        var progRT = progGO.AddComponent<RectTransform>();
        progRT.anchorMin = new Vector2(1, 0.5f); progRT.anchorMax = new Vector2(1, 0.5f);
        progRT.pivot = new Vector2(1, 0.5f);
        progRT.anchoredPosition = new Vector2(-24, 0); progRT.sizeDelta = new Vector2(120, 50);
        var progTMP = progGO.AddComponent<TextMeshProUGUI>();
        progTMP.text = "0/5"; progTMP.fontSize = 30; progTMP.fontStyle = FontStyles.Bold;
        progTMP.color = Color.white; progTMP.alignment = TextAlignmentOptions.Right;
        progTMP.raycastTarget = false;

        // ═══════════════════════════════════════════
        //  ELROEY IN BOAT — sitting ON the waterline
        // ═══════════════════════════════════════════

        var elroeySprite = LoadSprite("Assets/Art/Fishing/Elroey in the boat.png");

        var elroeyGO = new GameObject("Elroey");
        elroeyGO.transform.SetParent(safeGO.transform, false);
        var elroeyRT = elroeyGO.AddComponent<RectTransform>();
        // Anchor at waterline, pivot at boat bottom so hull sits ON the water
        elroeyRT.anchorMin = new Vector2(0.5f, WaterlineAnchor);
        elroeyRT.anchorMax = new Vector2(0.5f, WaterlineAnchor);
        elroeyRT.pivot = new Vector2(0.5f, 0.08f); // very low pivot — hull base
        elroeyRT.anchoredPosition = new Vector2(0, 0); // exactly on waterline
        elroeyRT.sizeDelta = new Vector2(300, 360);
        var elroeyImg = elroeyGO.AddComponent<Image>();
        elroeyImg.sprite = elroeySprite;
        elroeyImg.preserveAspect = true;
        elroeyImg.raycastTarget = false;

        // Rod tip marker (invisible point at end of fishing rod)
        var rodTipGO = new GameObject("RodTip");
        rodTipGO.transform.SetParent(elroeyGO.transform, false);
        var rodTipRT = rodTipGO.AddComponent<RectTransform>();
        rodTipRT.anchorMin = new Vector2(0.82f, 0.92f);
        rodTipRT.anchorMax = new Vector2(0.82f, 0.92f);
        rodTipRT.sizeDelta = Vector2.zero;

        // ═══════════════════════════════════════════
        //  SPEECH BUBBLE — above/right of Elroey
        // ═══════════════════════════════════════════

        var bubbleGO = new GameObject("SpeechBubble");
        bubbleGO.transform.SetParent(safeGO.transform, false);
        var bubbleRT = bubbleGO.AddComponent<RectTransform>();
        bubbleRT.anchorMin = new Vector2(0.5f, WaterlineAnchor);
        bubbleRT.anchorMax = new Vector2(0.5f, WaterlineAnchor);
        bubbleRT.pivot = new Vector2(0f, 0f);
        bubbleRT.anchoredPosition = new Vector2(160, 260);
        bubbleRT.sizeDelta = new Vector2(150, 150);
        var bubbleBgImg = bubbleGO.AddComponent<Image>();
        if (roundedRect != null) { bubbleBgImg.sprite = roundedRect; bubbleBgImg.type = Image.Type.Sliced; }
        bubbleBgImg.color = BubbleColor;
        bubbleBgImg.raycastTarget = false;

        var bubbleFishGO = new GameObject("TargetFish");
        bubbleFishGO.transform.SetParent(bubbleGO.transform, false);
        var bfRT = bubbleFishGO.AddComponent<RectTransform>();
        bfRT.anchorMin = new Vector2(0.1f, 0.1f); bfRT.anchorMax = new Vector2(0.9f, 0.9f);
        bfRT.offsetMin = Vector2.zero; bfRT.offsetMax = Vector2.zero;
        var bubbleFishImg = bubbleFishGO.AddComponent<Image>();
        bubbleFishImg.preserveAspect = true;
        bubbleFishImg.raycastTarget = false;

        // ═══════════════════════════════════════════
        //  FISH SWIM AREA — between waterline and sand
        // ═══════════════════════════════════════════

        var swimGO = new GameObject("fishSwimArea");
        swimGO.transform.SetParent(safeGO.transform, false);
        var swimRT = swimGO.AddComponent<RectTransform>();
        swimRT.anchorMin = new Vector2(0, SandAnchor + 0.02f);
        swimRT.anchorMax = new Vector2(1, WaterlineAnchor - 0.08f);
        swimRT.offsetMin = new Vector2(30, 0);
        swimRT.offsetMax = new Vector2(-30, 0);

        // ═══════════════════════════════════════════
        //  LOAD FISH SPRITES
        // ═══════════════════════════════════════════

        var fishSpriteList = new List<Sprite>();
        var fishIdList = new List<string>();
        string[] fishNames = {
            "Circle-shaped fish", "Diamond-shaped fish", "Heart-shaped fish",
            "Pentagon-shaped fish", "Rectangle-shaped fish", "Square-shaped fish",
            "Star-shaped fish", "Triangle-shaped fish"
        };

        var allFishAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Fishing/Fishes.png");
        var fishDict = new Dictionary<string, Sprite>();
        foreach (var asset in allFishAssets)
            if (asset is Sprite spr) fishDict[spr.name] = spr;

        foreach (string name in fishNames)
            if (fishDict.TryGetValue(name, out Sprite spr))
            { fishSpriteList.Add(spr); fishIdList.Add(name); }

        // ═══════════════════════════════════════════
        //  FISHING LINE
        // ═══════════════════════════════════════════

        var lineGO = new GameObject("FishingLine");
        lineGO.transform.SetParent(safeGO.transform, false);
        lineGO.AddComponent<RectTransform>();
        var fishingLine = lineGO.AddComponent<FishingLine>();
        fishingLine.rodTip = rodTipRT;

        // ═══════════════════════════════════════════
        //  CONTROLLER
        // ═══════════════════════════════════════════

        var ctrl = canvasGO.AddComponent<FishingGameController>();
        ctrl.elroeyRT = elroeyRT;
        ctrl.rodTipRT = rodTipRT;
        ctrl.speechBubbleFish = bubbleFishImg;
        ctrl.swimArea = swimRT;
        ctrl.progressText = progTMP;
        ctrl.fishingLine = fishingLine;
        ctrl.fishSprites = fishSpriteList.ToArray();
        ctrl.fishIds = fishIdList.ToArray();

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = roundedRect;
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "fishing";

        TutorialHandHelper.Create(safeGO.transform, TutorialHandHelper.Anim.Tap,
            new Vector2(0, -200), new Vector2(400, 400), "fishing");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/FishingGame.unity");
    }

    // ─────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────

    /// <summary>Creates a horizontal band spanning full width between two Y anchors.</summary>
    private static GameObject Band(Transform parent, string name, Color color,
        float anchorMinY, float anchorMaxY)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, anchorMinY);
        rt.anchorMax = new Vector2(1, anchorMaxY);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return go;
    }

    private static GameObject IconBtn(Transform p, string name, Sprite icon,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = icon; img.preserveAspect = true; img.color = Color.white;
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
