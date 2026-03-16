using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the TowerBuilder scene for landscape orientation (1920×1080).
/// Uses the project's layered landscape assets (clouds, mountains, hills, ground).
///
/// Layout:
///   Header (80px) — home button + Hebrew title "בנה את המגדל"
///   PlayArea — reference tower (left), build area (right), palette (bottom)
///
/// Run via Tools > Kids Learning Game > Setup Tower Builder.
/// </summary>
public class TowerBuilderSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private const string WorldArt = "Assets/Art/World/";

    // ── Colors ──────────────────────────────────────────────────────
    private static readonly Color SkyColor       = HexColor("#8FD4F5");
    private static readonly Color CloudTint      = Color.white;
    private static readonly Color MountainTint   = new Color(0.78f, 0.88f, 0.95f, 1f);
    private static readonly Color HillsLargeTint = HexColor("#B7D7D6");
    private static readonly Color GroundBackTint = HexColor("#8ED36B");
    private static readonly Color GroundFrontTint = HexColor("#79C956");

    // Header
    private static readonly Color HeaderColor = new Color(0.30f, 0.65f, 0.85f, 0.80f);

    private const int HeaderHeight = 130;

    [MenuItem("Tools/Kids Learning Game/Setup Tower Builder")]
    public static void ShowWindow()
    {
        RunSetupSilent();
        Debug.Log("Tower Builder scene created!");
    }

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Tower Builder Setup", "Building scene…", 0.3f);
            var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
            CreateScene(roundedRect);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ═════════════════════════════════════════
    //  SCENE
    // ═════════════════════════════════════════

    private static void CreateScene(Sprite roundedRect)
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SkyColor;
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

        // Canvas — LANDSCAPE
        var canvasGO = new GameObject("TowerBuilderCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ═══════════════════════════════════
        //  BACKGROUND — Layered landscape from project assets
        // ═══════════════════════════════════

        CreateBackground(canvasGO.transform);

        // Safe area
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        StretchFull(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // ═══════════════════════════════════
        //  HEADER (80px)
        // ═══════════════════════════════════

        var topBar = CreateStretchImage(safeArea.transform, "TopBar", HeaderColor);
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.sizeDelta = new Vector2(0, HeaderHeight);
        topBar.GetComponent<Image>().raycastTarget = false;
        topBar.AddComponent<ThemeHeader>();

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        StretchFull(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = HebrewFixer.Fix("\u05D1\u05E0\u05D4 \u05D0\u05EA \u05D4\u05DE\u05D2\u05D3\u05DC");
        titleTMP.isRightToLeftText = true;
        titleTMP.fontSize = 42;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(16, -20), new Vector2(90, 90));

        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = CreateIconButton(topBar.transform, "TrophyButton", trophyIcon,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-16, -20), new Vector2(70, 70));

        // ═══════════════════════════════════
        //  PLAY AREA
        // ═══════════════════════════════════

        var playArea = new GameObject("PlayArea");
        playArea.transform.SetParent(safeArea.transform, false);
        var playAreaRT = playArea.AddComponent<RectTransform>();
        StretchFull(playAreaRT);
        playAreaRT.offsetMax = new Vector2(0, -HeaderHeight);
        playAreaRT.offsetMin = Vector2.zero;

        // ═══════════════════════════════════
        //  CONTROLLER + SPRITE LOADING
        // ═══════════════════════════════════

        var controller = canvasGO.AddComponent<TowerBuilderController>();
        controller.playArea = playAreaRT;
        controller.roundedRectSprite = roundedRect;

        // Load brick sprites from TowerLevels
        EditorUtility.DisplayProgressBar("Tower Builder Setup", "Loading brick sprites…", 0.6f);
        LoadBrickSprites(controller);

        // Wire home button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = roundedRect;
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "towerbuilder";

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/TowerBuilder.unity");
    }

    // ═════════════════════════════════════════
    //  BACKGROUND — layered landscape assets
    // ═════════════════════════════════════════

    private static void CreateBackground(Transform parent)
    {
        // Load landscape sprites
        var cloudLayerB1Spr  = LoadSprite(WorldArt + "cloudLayerB1.png");
        var cloudLayerB2Spr  = LoadSprite(WorldArt + "cloudLayerB2.png");
        var mountainsSpr     = LoadSprite(WorldArt + "mountains.png");
        var hillsLargeSpr    = LoadSprite(WorldArt + "hillsLarge.png");
        var groundLayer1Spr  = LoadSprite(WorldArt + "groundLayer1.png");
        var groundLayer2Spr  = LoadSprite(WorldArt + "groundLayer2.png");

        // 1. SKY — solid base color (full screen)
        var sky = CreateStretchImage(parent, "Sky", SkyColor);
        sky.GetComponent<Image>().raycastTarget = false;

        // 2. CLOUDS — back layer (upper portion, stretches full width)
        CreateSpriteLayer(parent, "CloudLayerB1", cloudLayerB1Spr,
            new Vector2(0, 0.55f), new Vector2(1, 1f), CloudTint);

        // 3. CLOUDS — front layer (slightly lower, overlapping)
        CreateSpriteLayer(parent, "CloudLayerB2", cloudLayerB2Spr,
            new Vector2(0, 0.50f), new Vector2(1, 0.92f), CloudTint);

        // 4. MOUNTAINS — behind hills, overlapping clouds at bottom
        CreateSpriteLayer(parent, "Mountains", mountainsSpr,
            new Vector2(0, 0.35f), new Vector2(1, 0.70f), MountainTint);

        // 5. HILLS — overlapping mountains, above ground
        CreateSpriteLayer(parent, "HillsLarge", hillsLargeSpr,
            new Vector2(0, 0.22f), new Vector2(1, 0.55f), HillsLargeTint);

        // 6. GROUND back layer — overlapping hills at bottom
        CreateSpriteLayer(parent, "GroundBack", groundLayer1Spr,
            new Vector2(0, 0), new Vector2(1, 0.35f), GroundBackTint);

        // 7. GROUND front layer — slightly shorter, brighter grass
        CreateSpriteLayer(parent, "GroundFront", groundLayer2Spr,
            new Vector2(0, 0), new Vector2(1, 0.20f), GroundFrontTint);
    }

    private static GameObject CreateSpriteLayer(Transform parent, string name, Sprite sprite,
        Vector2 anchorMin, Vector2 anchorMax, Color tint)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.color = tint;
        img.preserveAspect = false;
        img.raycastTarget = false;
        return go;
    }

    // ═════════════════════════════════════════
    //  BRICK SPRITE LOADING
    // ═════════════════════════════════════════

    private static void LoadBrickSprites(TowerBuilderController controller)
    {
        var keys = TowerLevels.GetAllSpriteKeys();
        controller.spriteKeys = new List<string>();
        controller.spriteValues = new List<Sprite>();

        foreach (string key in keys)
        {
            // key format: "Red/brick_medium_4"
            string path = "Assets/Art/Bricks/" + key + ".png";
            Sprite sprite = LoadSprite(path);
            if (sprite != null)
            {
                controller.spriteKeys.Add(key);
                controller.spriteValues.Add(sprite);
            }
            else
            {
                Debug.LogWarning("TowerBuilderSetup: Could not load sprite at " + path);
            }
        }

        Debug.Log($"TowerBuilderSetup: Loaded {controller.spriteKeys.Count}/{keys.Count} brick sprites");
    }

    // ═════════════════════════════════════════
    //  GENERIC HELPERS
    // ═════════════════════════════════════════

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
