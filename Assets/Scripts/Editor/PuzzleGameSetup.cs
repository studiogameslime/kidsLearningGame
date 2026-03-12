using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Builds the PuzzleGame scene and updates Puzzle.asset with animal sub-items.
/// Run via Tools > Kids Learning Game > Setup Puzzle Game.
/// </summary>
public class PuzzleGameSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1080, 1920);

    // Colors
    private static readonly Color BgColor      = HexColor("#F5EDE3");
    private static readonly Color TopBarColor  = HexColor("#E8D5C0");

    // Layout
    private const int TopBarHeight = 130;

    // Animal colors for cards
    private static readonly Color[] AnimalColors = {
        HexColor("#EF9A9A"), HexColor("#F48FB1"), HexColor("#CE93D8"),
        HexColor("#B39DDB"), HexColor("#9FA8DA"), HexColor("#90CAF9"),
        HexColor("#80DEEA"), HexColor("#80CBC4"), HexColor("#A5D6A7"),
        HexColor("#C5E1A5"), HexColor("#E6EE9C"), HexColor("#FFF59D"),
        HexColor("#FFE082"), HexColor("#FFCC80"), HexColor("#FFAB91"),
        HexColor("#BCAAA4"), HexColor("#B0BEC5"), HexColor("#CFD8DC"),
        HexColor("#F8BBD0")
    };

    [MenuItem("Tools/Kids Learning Game/Setup Puzzle Game")]
    public static void RunSetup()
    {
        if (!EditorUtility.DisplayDialog(
            "Puzzle Game Setup",
            "This will create/overwrite:\n• PuzzleGame scene\n• Update Puzzle.asset with animal sub-items\n\nContinue?",
            "Build", "Cancel"))
            return;

        RunSetupSilent();
        EditorSceneManager.OpenScene("Assets/Scenes/PuzzleGame.unity");
        EditorUtility.DisplayDialog("Done!", "Puzzle Game built.\nPress Play to test!", "OK");
    }

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Puzzle Game Setup", "Updating puzzle data…", 0.2f);
            UpdatePuzzleData();

            EditorUtility.DisplayProgressBar("Puzzle Game Setup", "Building scene…", 0.5f);
            var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
            CreatePuzzleScene(roundedRect);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ─────────────────────────────────────────
    //  UPDATE PUZZLE DATA
    // ─────────────────────────────────────────

    private static void UpdatePuzzleData()
    {
        var puzzle = AssetDatabase.LoadAssetAtPath<GameItemData>("Assets/Data/Games/Puzzle.asset");
        if (puzzle == null)
        {
            Debug.LogError("Puzzle.asset not found. Run Setup Project first.");
            return;
        }

        puzzle.hasSubItems = true;
        puzzle.selectionScreenTitle = "\u05D1\u05D7\u05E8\u05D5 \u05E4\u05D0\u05D6\u05DC"; // בחרו פאזל
        puzzle.targetSceneName = "PuzzleGame";

        string[] animals = {
            "Bear", "Bird", "Cat", "Chicken", "Cow", "Dog", "Donkey", "Duck",
            "Elephant", "Fish", "Frog", "Giraffe", "Horse", "Lion", "Monkey",
            "Sheep", "Snake", "Turtle", "Zebra"
        };

        if (puzzle.subItems == null)
            puzzle.subItems = new List<SubItemData>();

        puzzle.subItems.Clear();

        // Gallery import option — first item, outlined style
        puzzle.subItems.Add(new SubItemData
        {
            id = "puzzle_gallery",
            title = "\u05DE\u05D4\u05D2\u05DC\u05E8\u05D9\u05D4", // מהגלריה
            cardColor = new Color(0.85f, 0.75f, 0.95f),
            categoryKey = "gallery",
            targetSceneName = "PuzzleGame",
            contentAsset = null,
            thumbnail = null
        });

        for (int i = 0; i < animals.Length; i++)
        {
            string name = animals[i];
            string mainPath = $"Assets/Art/Animals/{name}/Art/Puzzle/{name} Main.png";
            var mainSprite = LoadSprite(mainPath);

            Sprite thumbSprite = null;
            string[] thumbPaths = {
                $"Assets/Art/Animals/{name}/Art/{name}Sprite.png",
                $"Assets/Art/Animals/{name}/Art/{name}.png"
            };
            foreach (var tp in thumbPaths)
            {
                thumbSprite = LoadSprite(tp);
                if (thumbSprite != null) break;
            }

            if (mainSprite == null)
            {
                Debug.LogWarning($"Puzzle main image not found: {mainPath}");
                continue;
            }

            puzzle.subItems.Add(new SubItemData
            {
                id = $"puzzle_{name.ToLower()}",
                title = name,
                cardColor = AnimalColors[i % AnimalColors.Length],
                categoryKey = name.ToLower(),
                targetSceneName = "PuzzleGame",
                contentAsset = mainSprite,
                thumbnail = thumbSprite != null ? thumbSprite : mainSprite
            });
        }

        EditorUtility.SetDirty(puzzle);
        Debug.Log($"Puzzle data updated with {puzzle.subItems.Count} animals.");
    }

    // ─────────────────────────────────────────
    //  SCENE
    // ─────────────────────────────────────────

    private static void CreatePuzzleScene(Sprite roundedRect)
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
        var canvasGO = new GameObject("PuzzleCanvas");
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
        titleTMP.text = "\u05E4\u05D0\u05D6\u05DC"; // פאזל
        titleTMP.isRightToLeftText = true;
        titleTMP.fontSize = 48;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(16, -15), new Vector2(90, 90));

        // ── PUZZLE AREA (fills space below top bar) ──
        var puzzleArea = new GameObject("PuzzleArea");
        puzzleArea.transform.SetParent(safeArea.transform, false);
        var puzzleAreaRT = puzzleArea.AddComponent<RectTransform>();
        StretchFull(puzzleAreaRT);
        puzzleAreaRT.offsetMax = new Vector2(0, -TopBarHeight);
        puzzleAreaRT.offsetMin = Vector2.zero;

        // ── REFERENCE IMAGE (big, centered, faded) ──
        var refGO = new GameObject("ReferenceImage");
        refGO.transform.SetParent(puzzleArea.transform, false);
        var refRT = refGO.AddComponent<RectTransform>();
        refRT.anchorMin = new Vector2(0.5f, 0.5f);
        refRT.anchorMax = new Vector2(0.5f, 0.5f);
        refRT.sizeDelta = new Vector2(500, 500); // resized by controller at runtime
        var refRaw = refGO.AddComponent<RawImage>();
        refRaw.color = new Color(1f, 1f, 1f, 0.3f);
        refRaw.raycastTarget = false;

        // ── PIECE TRAY (bottom portion, just for sizing reference) ──
        var tray = new GameObject("PieceTray");
        tray.transform.SetParent(puzzleArea.transform, false);
        var trayRT = tray.AddComponent<RectTransform>();
        trayRT.anchorMin = new Vector2(0, 0);
        trayRT.anchorMax = new Vector2(1, 0.25f);
        trayRT.offsetMin = Vector2.zero;
        trayRT.offsetMax = Vector2.zero;

        // ── CONTROLLER ──
        var controller = canvasGO.AddComponent<PuzzleGameController>();
        controller.puzzleArea = puzzleAreaRT;
        controller.referenceImage = refRaw;
        controller.pieceTray = trayRT;
        controller.referenceAlpha = 0.3f;

        // Wire home button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/PuzzleGame.unity");
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
