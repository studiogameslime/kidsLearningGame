using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the SpinPuzzle ("סובב והתאם") scene — landscape layout.
/// Reuses World art assets for background.
/// </summary>
public class SpinPuzzleSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private const string WorldArt = "Assets/Art/World/";

    private static readonly Color SkyColor        = HexColor("#8FD4F5");
    private static readonly Color CloudTint       = Color.white;
    private static readonly Color MountainTint    = new Color(0.78f, 0.88f, 0.95f, 1f);
    private static readonly Color HillsLargeTint  = HexColor("#B7D7D6");
    private static readonly Color GroundBackTint  = HexColor("#8ED36B");
    private static readonly Color GroundFrontTint = HexColor("#79C956");

    private static readonly Color HeaderColor = new Color(0.55f, 0.35f, 0.70f, 0.85f); // purple theme
    private static readonly int HeaderHeight = SetupConstants.HeaderHeight;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Spin Puzzle Setup", "Building scene...", 0.5f);
            var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
            CreateScene(roundedRect);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

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

        // Canvas
        var canvasGO = new GameObject("SpinPuzzleCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        CreateBackground(canvasGO.transform);

        // Safe area
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        StretchFull(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // Header
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
        HebrewText.SetText(titleTMP, "\u05E1\u05D5\u05D1\u05D1 \u05D5\u05D4\u05EA\u05D0\u05DD");
        titleTMP.fontSize = 42;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = CreateIconButton(topBar.transform, "TrophyButton", trophyIcon,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-16, -20), new Vector2(70, 70));

        // Play area
        var playArea = new GameObject("PlayArea");
        playArea.transform.SetParent(safeArea.transform, false);
        var playAreaRT = playArea.AddComponent<RectTransform>();
        StretchFull(playAreaRT);
        playAreaRT.offsetMax = new Vector2(0, -HeaderHeight);
        playAreaRT.offsetMin = Vector2.zero;

        // Controller
        var controller = canvasGO.AddComponent<SpinPuzzleController>();
        controller.playArea = playAreaRT;
        controller.roundedRectSprite = roundedRect;

        // Wire buttons
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        // Leaderboard
        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = roundedRect;
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "spinpuzzle";

        // Tutorial hand
        TutorialHandHelper.Create(safeArea.transform, TutorialHandHelper.Anim.Tap,
            Vector2.zero, new Vector2(450, 450), "spinpuzzle");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SpinPuzzle.unity");
    }

    // ── Background ──

    private static void CreateBackground(Transform parent)
    {
        var sky = CreateStretchImage(parent, "Sky", SkyColor);
        sky.GetComponent<Image>().raycastTarget = false;

        CreateSpriteLayer(parent, "CloudLayerB1", LoadSprite(WorldArt + "cloudLayerB1.png"),
            new Vector2(0, 0.55f), new Vector2(1, 1f), CloudTint);
        CreateSpriteLayer(parent, "CloudLayerB2", LoadSprite(WorldArt + "cloudLayerB2.png"),
            new Vector2(0, 0.50f), new Vector2(1, 0.92f), CloudTint);
        CreateSpriteLayer(parent, "Mountains", LoadSprite(WorldArt + "mountains.png"),
            new Vector2(0, 0.35f), new Vector2(1, 0.70f), MountainTint);
        CreateSpriteLayer(parent, "HillsLarge", LoadSprite(WorldArt + "hillsLarge.png"),
            new Vector2(0, 0.22f), new Vector2(1, 0.55f), HillsLargeTint);
        CreateSpriteLayer(parent, "GroundBack", LoadSprite(WorldArt + "groundLayer1.png"),
            new Vector2(0, 0), new Vector2(1, 0.35f), GroundBackTint);
        CreateSpriteLayer(parent, "GroundFront", LoadSprite(WorldArt + "groundLayer2.png"),
            new Vector2(0, 0), new Vector2(1, 0.20f), GroundFrontTint);
    }

    private static GameObject CreateSpriteLayer(Transform parent, string name, Sprite sprite,
        Vector2 anchorMin, Vector2 anchorMax, Color tint)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = sprite; img.type = Image.Type.Simple;
        img.color = tint; img.preserveAspect = false; img.raycastTarget = false;
        return go;
    }

    // ── Helpers ──

    private static GameObject CreateStretchImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        StretchFull(go.AddComponent<RectTransform>());
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static GameObject CreateIconButton(Transform parent, string name, Sprite icon,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = icon; img.preserveAspect = true; img.color = Color.white; img.raycastTarget = true;
        go.AddComponent<Button>().targetGraphic = img;
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/'); string cur = parts[0];
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
