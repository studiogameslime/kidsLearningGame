using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the ConnectTheDots scene in LANDSCAPE with a night sky theme.
/// Child connects glowing star-dots to form constellations.
/// </summary>
public class ConnectTheDotsSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    // Night sky colors — deep blue, child-friendly (not too dark)
    private static readonly Color NightSkyTop = HexColor("#0D1B3E");
    private static readonly Color NightSkyBottom = HexColor("#1A2D5A");
    private static readonly Color HorizonGlowColor = HexColor("#2A4080");

    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;
    private const float DotSize = 80f;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Connect The Dots Setup", "Updating data…", 0.2f);
            UpdateGameData();

            EditorUtility.DisplayProgressBar("Connect The Dots Setup", "Building scene…", 0.5f);
            var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
            var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");
            CreateScene(roundedRect, circleSprite);

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
        var game = AssetDatabase.LoadAssetAtPath<GameItemData>("Assets/Data/Games/FillTheDots.asset");
        if (game == null)
        {
            Debug.LogError("FillTheDots.asset not found. Run Setup Project first.");
            return;
        }

        game.targetSceneName = "ConnectTheDots";
        game.hasSubItems = false;

        if (game.subItems == null)
            game.subItems = new List<SubItemData>();
        game.subItems.Clear();

        EditorUtility.SetDirty(game);
    }

    private static void CreateScene(Sprite roundedRect, Sprite circleSprite)
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = NightSkyTop;
        cam.orthographic = true;
        var urpType = System.Type.GetType(
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpType != null) camGO.AddComponent(urpType);

        // EventSystem
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inputType = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputType != null) esGO.AddComponent(inputType);
        else esGO.AddComponent<StandaloneInputModule>();

        // Canvas (landscape)
        var canvasGO = new GameObject("DotsCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── NIGHT SKY BACKGROUND ──

        // Deep sky (full background)
        var skyGO = StretchImg(canvasGO.transform, "NightSky", NightSkyTop);
        skyGO.GetComponent<Image>().raycastTarget = false;
        var skyImg = skyGO.GetComponent<Image>();

        // Star layer (behind everything interactive — decorative twinkling stars)
        var starLayerGO = new GameObject("StarLayer");
        starLayerGO.transform.SetParent(canvasGO.transform, false);
        var starLayerRT = starLayerGO.AddComponent<RectTransform>();
        Full(starLayerRT);

        // Moon (top-right, decorative)
        var moonSprite = LoadSprite("Assets/Art/World/moonFull.png");
        if (moonSprite != null)
        {
            var moonGO = new GameObject("Moon");
            moonGO.transform.SetParent(canvasGO.transform, false);
            var moonRT = moonGO.AddComponent<RectTransform>();
            moonRT.anchorMin = new Vector2(1, 1);
            moonRT.anchorMax = new Vector2(1, 1);
            moonRT.pivot = new Vector2(1, 1);
            moonRT.sizeDelta = new Vector2(130, 130);
            moonRT.anchoredPosition = new Vector2(-45, -100);
            var moonImg = moonGO.AddComponent<Image>();
            moonImg.sprite = moonSprite;
            moonImg.preserveAspect = true;
            moonImg.raycastTarget = false;
            moonImg.color = Color.white;
        }

        // Safe area
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        Full(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // ── TOP BAR (slim, semi-transparent dark) ──
        var topBar = StretchImg(safeArea.transform, "TopBar", HexColor("#1A2D5A"));
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.sizeDelta = new Vector2(0, TopBarHeight);
        topBar.GetComponent<Image>().raycastTarget = false;
        topBar.GetComponent<Image>().color = new Color(0.1f, 0.15f, 0.3f, 0.7f);
        topBar.AddComponent<ThemeHeader>();

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D7\u05D1\u05E8 \u05D0\u05EA \u05D4\u05E0\u05E7\u05D5\u05D3\u05D5\u05EA");
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(1f, 0.95f, 0.8f, 1f); // warm white
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button
        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = IconBtn(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(16, -20), new Vector2(90, 90));

        // Trophy button (top-right)
        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = IconBtn(topBar.transform, "TrophyButton", trophyIcon,
            new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-16, -20), new Vector2(70, 70));

        // ── PLAY AREA ──
        var playArea = new GameObject("PlayArea");
        playArea.transform.SetParent(safeArea.transform, false);
        var playAreaRT = playArea.AddComponent<RectTransform>();
        Full(playAreaRT);
        playAreaRT.offsetMin = new Vector2(40, 20);
        playAreaRT.offsetMax = new Vector2(-40, -TopBarHeight);

        // Line container (behind dots)
        var lineContainer = new GameObject("LineContainer");
        lineContainer.transform.SetParent(playArea.transform, false);
        var lineContRT = lineContainer.AddComponent<RectTransform>();
        Full(lineContRT);

        // Reveal image (centered, for animal sprite reveal on completion)
        var revealGO = new GameObject("RevealImage");
        revealGO.transform.SetParent(playArea.transform, false);
        var revealRT = revealGO.AddComponent<RectTransform>();
        revealRT.anchorMin = new Vector2(0.5f, 0.5f);
        revealRT.anchorMax = new Vector2(0.5f, 0.5f);
        revealRT.pivot = new Vector2(0.5f, 0.5f);
        revealRT.anchoredPosition = Vector2.zero;
        revealRT.sizeDelta = new Vector2(350, 350);
        var revealImg = revealGO.AddComponent<Image>();
        revealImg.preserveAspect = true;
        revealImg.raycastTarget = false;
        revealImg.color = new Color(1, 1, 1, 0);
        revealGO.SetActive(false);

        // Shape name text
        var shapeNameGO = new GameObject("ShapeNameText");
        shapeNameGO.transform.SetParent(playArea.transform, false);
        var shapeNameRT = shapeNameGO.AddComponent<RectTransform>();
        shapeNameRT.anchorMin = new Vector2(0.25f, 0.02f);
        shapeNameRT.anchorMax = new Vector2(0.75f, 0.18f);
        shapeNameRT.offsetMin = Vector2.zero;
        shapeNameRT.offsetMax = Vector2.zero;
        var shapeNameTMP = shapeNameGO.AddComponent<TextMeshProUGUI>();
        shapeNameTMP.text = "";
        shapeNameTMP.fontSize = 72;
        shapeNameTMP.fontStyle = FontStyles.Bold;
        shapeNameTMP.color = new Color(1, 1, 1, 0);
        shapeNameTMP.alignment = TextAlignmentOptions.Center;
        shapeNameTMP.raycastTarget = false;
        shapeNameTMP.enableAutoSizing = true;
        shapeNameTMP.fontSizeMin = 40;
        shapeNameTMP.fontSizeMax = 72;
        shapeNameGO.SetActive(false);

        // ── DOT PREFAB (star-like) ──
        var dotPrefab = CreateDotPrefab(circleSprite);

        // ── CONTROLLER ──
        var controller = canvasGO.AddComponent<ConnectTheDotsController>();
        controller.playArea = playAreaRT;
        controller.lineContainer = lineContRT;
        controller.shapeNameText = shapeNameTMP;
        controller.revealImage = revealImg;
        controller.skyImage = skyImg;

        controller.starLayer = starLayerRT;
        controller.dotPrefab = dotPrefab;
        controller.dotSize = DotSize;
        controller.lineWidth = 10f;
        controller.lineColor = new Color(1f, 0.95f, 0.75f, 0.85f);

        // Wire buttons
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        // Wire trophy / leaderboard
        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = LoadSprite("Assets/UI/Sprites/RoundedRect.png");
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "fillthedots";

        // Tutorial hand
        TutorialHandHelper.Create(safeArea.transform, TutorialHandHelper.Anim.Tap,
            new Vector2(-50, 50), new Vector2(450, 450), "fillthedots");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ConnectTheDots.unity");
    }

    // ── DOT PREFAB (star-like appearance) ──

    private static GameObject CreateDotPrefab(Sprite circleSprite)
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/UI");

        var root = new GameObject("DotPoint");
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(DotSize, DotSize);

        // Outer glow (larger, warm golden glow)
        var glowGO = new GameObject("Glow");
        glowGO.transform.SetParent(root.transform, false);
        var glowRT = glowGO.AddComponent<RectTransform>();
        glowRT.anchorMin = Vector2.zero;
        glowRT.anchorMax = Vector2.one;
        glowRT.offsetMin = new Vector2(-12, -12);
        glowRT.offsetMax = new Vector2(12, 12);
        var glowImg = glowGO.AddComponent<Image>();
        if (circleSprite != null) glowImg.sprite = circleSprite;
        glowImg.color = new Color(1f, 0.92f, 0.5f, 0f);
        glowImg.raycastTarget = false;

        // Pulse ring
        var ringGO = new GameObject("Ring");
        ringGO.transform.SetParent(root.transform, false);
        var ringRT = ringGO.AddComponent<RectTransform>();
        ringRT.anchorMin = Vector2.zero;
        ringRT.anchorMax = Vector2.one;
        ringRT.offsetMin = new Vector2(-8, -8);
        ringRT.offsetMax = new Vector2(8, 8);
        var ringImg = ringGO.AddComponent<Image>();
        if (circleSprite != null) ringImg.sprite = circleSprite;
        ringImg.color = new Color(1f, 0.95f, 0.7f, 0.4f);
        ringImg.raycastTarget = false;

        // Dot circle (white star core)
        var dotGO = new GameObject("Dot");
        dotGO.transform.SetParent(root.transform, false);
        var dotRT = dotGO.AddComponent<RectTransform>();
        Full(dotRT);
        var dotImg = dotGO.AddComponent<Image>();
        if (circleSprite != null) dotImg.sprite = circleSprite;
        dotImg.color = new Color(0.8f, 0.85f, 0.95f, 0.35f); // dim star for future dots
        dotImg.raycastTarget = true;

        // Number text
        var numGO = new GameObject("Number");
        numGO.transform.SetParent(root.transform, false);
        var numRT = numGO.AddComponent<RectTransform>();
        Full(numRT);
        var numTMP = numGO.AddComponent<TextMeshProUGUI>();
        numTMP.text = "1";
        numTMP.fontSize = 32;
        numTMP.fontStyle = FontStyles.Bold;
        numTMP.color = new Color(1f, 1f, 1f, 0.9f);
        numTMP.alignment = TextAlignmentOptions.Center;
        numTMP.raycastTarget = false;
        numTMP.outlineWidth = 0.35f;
        numTMP.outlineColor = new Color(0.05f, 0.05f, 0.15f, 0.9f);

        // DotPoint component
        var dotPoint = root.AddComponent<DotPoint>();
        dotPoint.dotImage = dotImg;
        dotPoint.ringImage = ringImg;
        dotPoint.glowImage = glowImg;
        dotPoint.numberText = numTMP;

        string prefabPath = "Assets/Prefabs/UI/DotPoint.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ── HELPERS ──

    private static GameObject StretchImg(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Full(go.AddComponent<RectTransform>());
        go.AddComponent<Image>().color = color;
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

    private static void Full(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (allAssets != null)
            foreach (var asset in allAssets)
                if (asset is Sprite s) return s;
        return null;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
