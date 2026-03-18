using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the MazeGame scene.
/// Run via Tools > Kids Learning Game > Setup Maze Game.
/// </summary>
public class MazeGameSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1080, 1920);

    private static readonly Color BgColor     = HexColor("#FFF8E1"); // warm cream
    private static readonly Color TopBarColor = HexColor("#FFD54F"); // yellow
    private static readonly Color GoalColor   = HexColor("#FFD54F"); // golden yellow
    private static readonly Color RestartColor = HexColor("#66BB6A"); // green

    private const int TopBarHeight   = 130;
    private const int BottomBarHeight = 120;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Maze Game Setup", "Building scene…", 0.5f);
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
        var canvasGO = new GameObject("MazeCanvas");
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
        topBar.AddComponent<ThemeHeader>();

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        StretchFull(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05DE\u05D1\u05D5\u05DA"); // מבוך
        titleTMP.fontSize = 48;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button (top-left)
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(16, -20), new Vector2(90, 90));

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

        // MazeContainer
        var mazeContainerGO = new GameObject("MazeContainer");
        mazeContainerGO.transform.SetParent(playArea.transform, false);
        var mazeContainerRT = mazeContainerGO.AddComponent<RectTransform>();
        StretchFull(mazeContainerRT);
        mazeContainerRT.offsetMin = new Vector2(30, 30);
        mazeContainerRT.offsetMax = new Vector2(-30, -30);

        // TrailContainer (same size/position, layered above maze cells)
        var trailContainerGO = new GameObject("TrailContainer");
        trailContainerGO.transform.SetParent(playArea.transform, false);
        var trailContainerRT = trailContainerGO.AddComponent<RectTransform>();
        StretchFull(trailContainerRT);
        trailContainerRT.offsetMin = new Vector2(30, 30);
        trailContainerRT.offsetMax = new Vector2(-30, -30);

        // Player
        var playerGO = new GameObject("Player");
        playerGO.transform.SetParent(playArea.transform, false);
        var playerRT = playerGO.AddComponent<RectTransform>();
        playerRT.sizeDelta = new Vector2(60, 60);
        var playerImage = playerGO.AddComponent<Image>();
        playerImage.preserveAspect = true;
        playerImage.color = new Color(1, 1, 1, 0); // starts invisible
        playerImage.raycastTarget = false;

        // Goal
        var goalGO = new GameObject("Goal");
        goalGO.transform.SetParent(playArea.transform, false);
        var goalRT = goalGO.AddComponent<RectTransform>();
        goalRT.sizeDelta = new Vector2(50, 50);
        var goalImage = goalGO.AddComponent<Image>();
        if (circleSprite != null) goalImage.sprite = circleSprite;
        goalImage.color = GoalColor;
        goalImage.raycastTarget = false;

        // DragArea (full-area transparent Image for drag input)
        var dragAreaGO = new GameObject("DragArea");
        dragAreaGO.transform.SetParent(playArea.transform, false);
        var dragAreaRT = dragAreaGO.AddComponent<RectTransform>();
        StretchFull(dragAreaRT);
        var dragAreaImg = dragAreaGO.AddComponent<Image>();
        dragAreaImg.color = new Color(1, 1, 1, 0); // transparent
        dragAreaImg.raycastTarget = true;
        var dragHandler = dragAreaGO.AddComponent<MazeDragHandler>();

        // ── CONTROLLER ──
        var controller = canvasGO.AddComponent<MazeController>();
        controller.mazeContainer = mazeContainerRT;
        controller.playerRT = playerRT;
        controller.playerImage = playerImage;
        controller.goalRT = goalRT;
        controller.goalImage = goalImage;
        controller.trailContainer = trailContainerRT;
        controller.circleSprite = circleSprite;

        // Wire drag handler to controller
        dragHandler.controller = controller;

        // Wire buttons
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MazeGame.unity");
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
