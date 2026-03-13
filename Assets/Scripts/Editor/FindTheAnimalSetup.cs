using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the FindTheAnimal scene and updates FindTheObject.asset data.
/// Run via Tools > Kids Learning Game > Setup Find The Animal.
/// </summary>
public class FindTheAnimalSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1080, 1920);

    private static readonly Color BgColor     = HexColor("#FFF3E0"); // warm orange-ish
    private static readonly Color TopBarColor = HexColor("#FFB74D"); // orange bar

    private const int TopBarHeight     = 130;
    private const int BottomBarHeight  = 120;
    private const int TargetAreaHeight = 200;

    private static readonly Color[] AnimalColors = {
        HexColor("#EF9A9A"), HexColor("#F48FB1"), HexColor("#CE93D8"),
        HexColor("#B39DDB"), HexColor("#9FA8DA"), HexColor("#90CAF9"),
        HexColor("#80DEEA"), HexColor("#80CBC4"), HexColor("#A5D6A7"),
        HexColor("#C5E1A5"), HexColor("#E6EE9C"), HexColor("#FFF59D"),
        HexColor("#FFE082"), HexColor("#FFCC80"), HexColor("#FFAB91"),
        HexColor("#BCAAA4"), HexColor("#B0BEC5"), HexColor("#CFD8DC"),
        HexColor("#F8BBD0")
    };

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Find The Animal Setup", "Updating data…", 0.2f);
            UpdateGameData();

            EditorUtility.DisplayProgressBar("Find The Animal Setup", "Building scene…", 0.5f);
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
        var game = AssetDatabase.LoadAssetAtPath<GameItemData>("Assets/Data/Games/FindTheObject.asset");
        if (game == null)
        {
            Debug.LogError("FindTheObject.asset not found. Run Setup Project first.");
            return;
        }

        game.targetSceneName = "FindTheAnimal";
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
                id = $"find_{name.ToLower()}",
                title = name,
                cardColor = AnimalColors[i % AnimalColors.Length],
                categoryKey = name.ToLower(),
                targetSceneName = "FindTheAnimal",
                contentAsset = mainSprite != null ? mainSprite : thumbSprite,
                thumbnail = thumbSprite != null ? thumbSprite : mainSprite
            });
        }

        EditorUtility.SetDirty(game);
        Debug.Log($"FindTheAnimal data updated with {game.subItems.Count} animals.");
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
        var canvasGO = new GameObject("FindAnimalCanvas");
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
        titleTMP.text = HebrewFixer.Fix("\u05DE\u05E6\u05D0 \u05D0\u05EA \u05D4\u05D7\u05D9\u05D4"); // מצא את החיה
        titleTMP.isRightToLeftText = false;
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

        // ── TARGET AREA (top of play area, height 200) ──
        var targetArea = new GameObject("TargetArea");
        targetArea.transform.SetParent(playArea.transform, false);
        var targetAreaRT = targetArea.AddComponent<RectTransform>();
        targetAreaRT.anchorMin = new Vector2(0, 1);
        targetAreaRT.anchorMax = new Vector2(1, 1);
        targetAreaRT.pivot = new Vector2(0.5f, 1);
        targetAreaRT.sizeDelta = new Vector2(0, TargetAreaHeight);

        // White rounded rect background for target
        var targetBgGO = new GameObject("TargetBackground");
        targetBgGO.transform.SetParent(targetArea.transform, false);
        var targetBgRT = targetBgGO.AddComponent<RectTransform>();
        targetBgRT.anchorMin = new Vector2(0.5f, 0.5f);
        targetBgRT.anchorMax = new Vector2(0.5f, 0.5f);
        targetBgRT.sizeDelta = new Vector2(280, 180);
        var targetBgImg = targetBgGO.AddComponent<Image>();
        targetBgImg.color = Color.white;
        if (roundedRect != null)
        {
            targetBgImg.sprite = roundedRect;
            targetBgImg.type = Image.Type.Sliced;
        }
        targetBgImg.raycastTarget = false;

        // Target image (left side of background)
        var targetImgGO = new GameObject("TargetImage");
        targetImgGO.transform.SetParent(targetBgGO.transform, false);
        var targetImgRT = targetImgGO.AddComponent<RectTransform>();
        targetImgRT.anchorMin = new Vector2(0, 0.5f);
        targetImgRT.anchorMax = new Vector2(0, 0.5f);
        targetImgRT.pivot = new Vector2(0, 0.5f);
        targetImgRT.anchoredPosition = new Vector2(15, 0);
        targetImgRT.sizeDelta = new Vector2(130, 130);
        var targetImg = targetImgGO.AddComponent<Image>();
        targetImg.preserveAspect = true;
        targetImg.color = Color.white;
        targetImg.raycastTarget = false;

        // Remaining count text (right side of background)
        var countGO = new GameObject("RemainingCount");
        countGO.transform.SetParent(targetBgGO.transform, false);
        var countRT = countGO.AddComponent<RectTransform>();
        countRT.anchorMin = new Vector2(1, 0.5f);
        countRT.anchorMax = new Vector2(1, 0.5f);
        countRT.pivot = new Vector2(1, 0.5f);
        countRT.anchoredPosition = new Vector2(-20, 0);
        countRT.sizeDelta = new Vector2(100, 130);
        var countTMP = countGO.AddComponent<TextMeshProUGUI>();
        countTMP.text = "0";
        countTMP.fontSize = 64;
        countTMP.fontStyle = FontStyles.Bold;
        countTMP.color = HexColor("#FF7043");
        countTMP.alignment = TextAlignmentOptions.Center;
        countTMP.raycastTarget = false;

        // ── ANIMAL AREA (fills remaining play area below target area) ──
        var animalArea = new GameObject("AnimalArea");
        animalArea.transform.SetParent(playArea.transform, false);
        var animalAreaRT = animalArea.AddComponent<RectTransform>();
        StretchFull(animalAreaRT);
        animalAreaRT.offsetMax = new Vector2(0, -TargetAreaHeight);

        // ── CONTROLLER ──
        var controller = canvasGO.AddComponent<FindTheAnimalController>();
        controller.playArea = animalAreaRT;
        controller.targetImage = targetImg;
        controller.remainingText = countTMP;
        controller.animalSize = 280f;

        // Wire buttons
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/FindTheAnimal.unity");
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
