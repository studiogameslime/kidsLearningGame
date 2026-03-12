using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the ConnectTheDots scene and updates FillTheDots.asset data.
/// Run via Tools > Kids Learning Game > Setup Connect The Dots.
/// </summary>
public class ConnectTheDotsSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1080, 1920);

    private static readonly Color BgColor     = HexColor("#E8F5E9"); // soft green
    private static readonly Color TopBarColor = HexColor("#A5D6A7"); // green bar
    private static readonly Color DotColor    = HexColor("#BDBDBD");
    private static readonly Color LineColor   = HexColor("#4DB6AC"); // teal line

    private const int TopBarHeight  = 130;
    private const int BottomBarHeight = 120;
    private const float DotSize     = 80f;

    [MenuItem("Tools/Kids Learning Game/Setup Connect The Dots")]
    public static void RunSetup()
    {
        if (!EditorUtility.DisplayDialog(
            "Connect The Dots Setup",
            "This will create/overwrite:\n• ConnectTheDots scene\n• Update FillTheDots.asset\n\nContinue?",
            "Build", "Cancel"))
            return;

        RunSetupSilent();
        EditorSceneManager.OpenScene("Assets/Scenes/ConnectTheDots.unity");
        EditorUtility.DisplayDialog("Done!", "Connect The Dots built.\nPress Play to test!", "OK");
    }

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

    // ─────────────────────────────────────────
    //  DATA
    // ─────────────────────────────────────────

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
        Debug.Log("ConnectTheDots data updated (shape-based, no sub-items).");
    }

    // ─────────────────────────────────────────
    //  SCENE
    // ─────────────────────────────────────────

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
        cam.backgroundColor = BgColor;
        cam.orthographic = true;
        var urpType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpType != null) camGO.AddComponent(urpType);

        // EventSystem
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inputType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputType != null) esGO.AddComponent(inputType);
        else esGO.AddComponent<StandaloneInputModule>();

        // Canvas
        var canvasGO = new GameObject("DotsCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        var bg = CreateStretchImage(canvasGO.transform, "Background", BgColor);
        bg.GetComponent<Image>().raycastTarget = false;

        // Safe area
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        StretchFull(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // ── TOP BAR ──
        var topBar = CreateStretchImage(safeArea.transform, "TopBar", TopBarColor);
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.sizeDelta = new Vector2(0, TopBarHeight);
        topBar.GetComponent<Image>().raycastTarget = false;

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        StretchFull(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "Connect the Dots";
        titleTMP.fontSize = 48;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button (top-left)
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(16, -15), new Vector2(90, 90));

        // ── BOTTOM BAR ──
        var bottomBar = CreateStretchImage(safeArea.transform, "BottomBar", new Color(1, 1, 1, 0));
        var bottomBarRT = bottomBar.GetComponent<RectTransform>();
        bottomBarRT.anchorMin = new Vector2(0, 0);
        bottomBarRT.anchorMax = new Vector2(1, 0);
        bottomBarRT.pivot = new Vector2(0.5f, 0);
        bottomBarRT.sizeDelta = new Vector2(0, BottomBarHeight);
        bottomBar.GetComponent<Image>().raycastTarget = false;

        // ── PLAY AREA (between top and bottom bars) ──
        var playArea = new GameObject("PlayArea");
        playArea.transform.SetParent(safeArea.transform, false);
        var playAreaRT = playArea.AddComponent<RectTransform>();
        StretchFull(playAreaRT);
        playAreaRT.offsetMax = new Vector2(0, -TopBarHeight);
        playAreaRT.offsetMin = new Vector2(0, BottomBarHeight);

        // Line container (behind dots)
        var lineContainer = new GameObject("LineContainer");
        lineContainer.transform.SetParent(playArea.transform, false);
        var lineContRT = lineContainer.AddComponent<RectTransform>();
        StretchFull(lineContRT);

        // Shape name text (centered, shown after completion)
        var shapeNameGO = new GameObject("ShapeNameText");
        shapeNameGO.transform.SetParent(playArea.transform, false);
        var shapeNameRT = shapeNameGO.AddComponent<RectTransform>();
        shapeNameRT.anchorMin = new Vector2(0.1f, 0.4f);
        shapeNameRT.anchorMax = new Vector2(0.9f, 0.6f);
        shapeNameRT.offsetMin = Vector2.zero;
        shapeNameRT.offsetMax = Vector2.zero;
        var shapeNameTMP = shapeNameGO.AddComponent<TextMeshProUGUI>();
        shapeNameTMP.text = "";
        shapeNameTMP.fontSize = 96;
        shapeNameTMP.fontStyle = FontStyles.Bold;
        shapeNameTMP.color = new Color(1, 1, 1, 0);
        shapeNameTMP.alignment = TextAlignmentOptions.Center;
        shapeNameTMP.raycastTarget = false;
        shapeNameTMP.enableAutoSizing = true;
        shapeNameTMP.fontSizeMin = 48;
        shapeNameTMP.fontSizeMax = 96;
        shapeNameGO.SetActive(false);

        // ── DOT PREFAB ──
        var dotPrefab = CreateDotPrefab(circleSprite, roundedRect);

        // ── CONTROLLER ──
        var controller = canvasGO.AddComponent<ConnectTheDotsController>();
        controller.playArea = playAreaRT;
        controller.lineContainer = lineContRT;
        controller.shapeNameText = shapeNameTMP;
        controller.dotPrefab = dotPrefab;
        controller.dotSize = DotSize;
        controller.lineWidth = 8f;
        controller.lineColor = LineColor;

        // Wire buttons
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ConnectTheDots.unity");
    }

    // ─────────────────────────────────────────
    //  DOT PREFAB
    // ─────────────────────────────────────────

    private static GameObject CreateDotPrefab(Sprite circleSprite, Sprite roundedRect)
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/UI");

        var root = new GameObject("DotPoint");
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(DotSize, DotSize);

        // Pulse ring (behind dot)
        var ringGO = new GameObject("Ring");
        ringGO.transform.SetParent(root.transform, false);
        var ringRT = ringGO.AddComponent<RectTransform>();
        ringRT.anchorMin = Vector2.zero;
        ringRT.anchorMax = Vector2.one;
        ringRT.offsetMin = new Vector2(-12, -12);
        ringRT.offsetMax = new Vector2(12, 12);
        var ringImg = ringGO.AddComponent<Image>();
        if (circleSprite != null) ringImg.sprite = circleSprite;
        ringImg.color = new Color(1f, 1f, 1f, 0.4f);
        ringImg.raycastTarget = false;

        // Dot circle
        var dotGO = new GameObject("Dot");
        dotGO.transform.SetParent(root.transform, false);
        var dotRT = dotGO.AddComponent<RectTransform>();
        StretchFull(dotRT);
        var dotImg = dotGO.AddComponent<Image>();
        if (circleSprite != null) dotImg.sprite = circleSprite;
        dotImg.color = DotColor;
        dotImg.raycastTarget = true;

        // Number text
        var numGO = new GameObject("Number");
        numGO.transform.SetParent(root.transform, false);
        var numRT = numGO.AddComponent<RectTransform>();
        StretchFull(numRT);
        var numTMP = numGO.AddComponent<TextMeshProUGUI>();
        numTMP.text = "1";
        numTMP.fontSize = 32;
        numTMP.fontStyle = FontStyles.Bold;
        numTMP.color = Color.white;
        numTMP.alignment = TextAlignmentOptions.Center;
        numTMP.raycastTarget = false;

        // DotPoint component
        var dotPoint = root.AddComponent<DotPoint>();
        dotPoint.dotImage = dotImg;
        dotPoint.ringImage = ringImg;
        dotPoint.numberText = numTMP;

        string prefabPath = "Assets/Prefabs/UI/DotPoint.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ─────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────

    private static GameObject CreateStretchImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        StretchFull(rt);
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static GameObject CreateIconButton(Transform parent, string name, Sprite icon,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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
        {
            foreach (var asset in allAssets)
                if (asset is Sprite s) return s;
        }
        return null;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
