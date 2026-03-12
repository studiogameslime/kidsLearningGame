using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the BubblePop scene and updates PopTheBubbles.asset data.
/// Run via Tools > Kids Learning Game > Setup Bubble Pop.
/// </summary>
public class BubblePopSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1080, 1920);

    private static readonly Color BgColor     = HexColor("#E1F5FE"); // light blue
    private static readonly Color TopBarColor = HexColor("#29B6F6");

    private const int TopBarHeight   = 130;
    private const int BottomBarHeight = 120;

    private static readonly Color[] AnimalColors = {
        HexColor("#EF9A9A"), HexColor("#F48FB1"), HexColor("#CE93D8"),
        HexColor("#B39DDB"), HexColor("#9FA8DA"), HexColor("#90CAF9"),
        HexColor("#80DEEA"), HexColor("#80CBC4"), HexColor("#A5D6A7"),
        HexColor("#C5E1A5"), HexColor("#E6EE9C"), HexColor("#FFF59D"),
        HexColor("#FFE082"), HexColor("#FFCC80"), HexColor("#FFAB91"),
        HexColor("#BCAAA4"), HexColor("#B0BEC5"), HexColor("#CFD8DC"),
        HexColor("#F8BBD0")
    };

    [MenuItem("Tools/Kids Learning Game/Setup Bubble Pop")]
    public static void RunSetup()
    {
        if (!EditorUtility.DisplayDialog(
            "Bubble Pop Setup",
            "This will create/overwrite:\n• BubblePop scene\n• Update PopTheBubbles.asset with animal data\n\nContinue?",
            "Build", "Cancel"))
            return;

        RunSetupSilent();
        EditorSceneManager.OpenScene("Assets/Scenes/BubblePop.unity");
        EditorUtility.DisplayDialog("Done!", "Bubble Pop built.\nPress Play to test!", "OK");
    }

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Bubble Pop Setup", "Updating data…", 0.2f);
            UpdateGameData();

            EditorUtility.DisplayProgressBar("Bubble Pop Setup", "Building scene…", 0.5f);
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
        var game = AssetDatabase.LoadAssetAtPath<GameItemData>("Assets/Data/Games/PopTheBubbles.asset");
        if (game == null)
        {
            Debug.LogError("PopTheBubbles.asset not found. Run Setup Project first.");
            return;
        }

        game.targetSceneName = "BubblePop";
        game.hasSubItems = false;

        string[] animals = {
            "Bear", "Bird", "Cat", "Chicken", "Cow", "Dog", "Donkey", "Duck",
            "Elephant", "Fish", "Frog", "Giraffe", "Horse", "Lion", "Monkey",
            "Sheep", "Snake", "Turtle", "Zebra"
        };

        if (game.subItems == null)
            game.subItems = new List<SubItemData>();
        game.subItems.Clear();

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

            if (mainSprite == null && thumbSprite == null) continue;

            game.subItems.Add(new SubItemData
            {
                id = $"bubble_{name.ToLower()}",
                title = name,
                cardColor = AnimalColors[i % AnimalColors.Length],
                categoryKey = name.ToLower(),
                targetSceneName = "BubblePop",
                contentAsset = mainSprite != null ? mainSprite : thumbSprite,
                thumbnail = thumbSprite != null ? thumbSprite : mainSprite
            });
        }

        EditorUtility.SetDirty(game);
        Debug.Log($"BubblePop data updated with {game.subItems.Count} animals.");
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
        var canvasGO = new GameObject("BubblePopCanvas");
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
        titleTMP.text = "\u05E4\u05E7\u05E2 \u05D1\u05D5\u05E2\u05D5\u05EA"; // פקע בועות
        titleTMP.isRightToLeftText = true;
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

        // ── TARGET AREA (top of play area) ──
        var targetArea = new GameObject("TargetArea");
        targetArea.transform.SetParent(playArea.transform, false);
        var targetAreaRT = targetArea.AddComponent<RectTransform>();
        targetAreaRT.anchorMin = new Vector2(0.5f, 1);
        targetAreaRT.anchorMax = new Vector2(0.5f, 1);
        targetAreaRT.pivot = new Vector2(0.5f, 1);
        targetAreaRT.anchoredPosition = Vector2.zero;
        targetAreaRT.sizeDelta = new Vector2(260, 180);

        // White rounded rect background
        var targetBg = new GameObject("Background");
        targetBg.transform.SetParent(targetArea.transform, false);
        var targetBgRT = targetBg.AddComponent<RectTransform>();
        targetBgRT.anchorMin = new Vector2(0.5f, 0.5f);
        targetBgRT.anchorMax = new Vector2(0.5f, 0.5f);
        targetBgRT.sizeDelta = new Vector2(260, 160);
        var targetBgImg = targetBg.AddComponent<Image>();
        targetBgImg.color = Color.white;
        if (roundedRect != null)
        {
            targetBgImg.sprite = roundedRect;
            targetBgImg.type = Image.Type.Sliced;
        }
        targetBgImg.raycastTarget = false;

        // "Find:" label (left-aligned)
        var targetLabel = new GameObject("TargetLabel");
        targetLabel.transform.SetParent(targetArea.transform, false);
        var targetLabelRT = targetLabel.AddComponent<RectTransform>();
        targetLabelRT.anchorMin = new Vector2(0, 0);
        targetLabelRT.anchorMax = new Vector2(0.5f, 1);
        targetLabelRT.offsetMin = new Vector2(20, 0);
        targetLabelRT.offsetMax = Vector2.zero;
        var targetLabelTMP = targetLabel.AddComponent<TextMeshProUGUI>();
        targetLabelTMP.text = "Find:";
        targetLabelTMP.fontSize = 30;
        targetLabelTMP.fontStyle = FontStyles.Bold;
        targetLabelTMP.color = HexColor("#333333");
        targetLabelTMP.alignment = TextAlignmentOptions.Left;
        targetLabelTMP.raycastTarget = false;

        // TargetAnimal image (right side)
        var targetAnimal = new GameObject("TargetAnimal");
        targetAnimal.transform.SetParent(targetArea.transform, false);
        var targetAnimalRT = targetAnimal.AddComponent<RectTransform>();
        targetAnimalRT.anchorMin = new Vector2(1, 0.5f);
        targetAnimalRT.anchorMax = new Vector2(1, 0.5f);
        targetAnimalRT.pivot = new Vector2(1, 0.5f);
        targetAnimalRT.anchoredPosition = new Vector2(-20, 0);
        targetAnimalRT.sizeDelta = new Vector2(120, 120);
        var targetAnimalImg = targetAnimal.AddComponent<Image>();
        if (circleSprite != null)
            targetAnimalImg.sprite = circleSprite;
        targetAnimalImg.preserveAspect = true;
        targetAnimalImg.raycastTarget = false;

        // ── BUBBLE CONTAINER (fills rest of play area below target) ──
        var bubbleContainer = new GameObject("BubbleContainer");
        bubbleContainer.transform.SetParent(playArea.transform, false);
        var bubbleContainerRT = bubbleContainer.AddComponent<RectTransform>();
        StretchFull(bubbleContainerRT);
        bubbleContainerRT.offsetMax = new Vector2(0, -180); // below target area

        // ── CONTROLLER ──
        var controller = canvasGO.AddComponent<BubblePopController>();
        controller.playArea = playAreaRT;
        controller.targetColorImage = targetAnimalImg;
        controller.bubbleContainer = bubbleContainerRT;
        controller.circleSprite = circleSprite;
        controller.bubbleSize = 160;
        controller.spawnInterval = 1.2f;
        controller.floatSpeed = 120f;
        controller.correctNeeded = 5;

        // Wire buttons
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/BubblePop.unity");
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
