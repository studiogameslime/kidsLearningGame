using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the CountingGame scene in LANDSCAPE with a sky+grass outdoor theme.
/// Children count animals on screen and tap the correct number.
/// </summary>
public class CountingGameSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    // Outdoor theme colors
    private static readonly Color SkyColor = HexColor("#87CEEB");
    private static readonly Color SkyBottomColor = HexColor("#B8E4F0");
    private static readonly Color GrassColor = HexColor("#7EC850");
    private static readonly Color GrassDarkColor = HexColor("#5DAA35");
    private static readonly Color HeaderColor = new Color(0.1f, 0.4f, 0.2f, 0.65f);

    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

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
            EditorUtility.DisplayProgressBar("Counting Game Setup", "Updating data\u2026", 0.2f);
            UpdateGameData();

            EditorUtility.DisplayProgressBar("Counting Game Setup", "Building scene\u2026", 0.5f);
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

    // ── DATA ──

    private static void UpdateGameData()
    {
        var game = AssetDatabase.LoadAssetAtPath<GameItemData>("Assets/Data/Games/FindTheCount.asset");
        if (game == null)
        {
            Debug.LogError("FindTheCount.asset not found. Run Setup Project first.");
            return;
        }

        game.targetSceneName = "CountingGame";
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
                id = $"count_{name.ToLower()}",
                title = name,
                cardColor = AnimalColors[i % AnimalColors.Length],
                categoryKey = name.ToLower(),
                targetSceneName = "CountingGame",
                contentAsset = mainSprite != null ? mainSprite : thumbSprite,
                thumbnail = thumbSprite != null ? thumbSprite : mainSprite
            });
        }

        EditorUtility.SetDirty(game);
        Debug.Log($"CountingGame data updated with {game.subItems.Count} animals.");
    }

    // ── SCENE ──

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

        // Canvas (landscape)
        var canvasGO = new GameObject("CountingCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── SKY BACKGROUND ──
        var skyGO = StretchImg(canvasGO.transform, "Sky", SkyColor);
        skyGO.GetComponent<Image>().raycastTarget = false;

        // Sky gradient overlay (lighter at bottom horizon)
        var skyGradGO = new GameObject("SkyGradient");
        skyGradGO.transform.SetParent(canvasGO.transform, false);
        var skyGradRT = skyGradGO.AddComponent<RectTransform>();
        Full(skyGradRT);
        // Position in upper half
        skyGradRT.anchorMin = new Vector2(0, 0.35f);
        skyGradRT.anchorMax = new Vector2(1, 1);
        skyGradRT.offsetMin = Vector2.zero;
        skyGradRT.offsetMax = Vector2.zero;
        var skyGradImg = skyGradGO.AddComponent<Image>();
        skyGradImg.color = new Color(1, 1, 1, 0.12f);
        skyGradImg.raycastTarget = false;

        // ── GRASS ──
        var grassGO = new GameObject("Grass");
        grassGO.transform.SetParent(canvasGO.transform, false);
        var grassRT = grassGO.AddComponent<RectTransform>();
        grassRT.anchorMin = new Vector2(0, 0);
        grassRT.anchorMax = new Vector2(1, 0.35f);
        grassRT.offsetMin = Vector2.zero;
        grassRT.offsetMax = Vector2.zero;
        var grassImg = grassGO.AddComponent<Image>();
        grassImg.color = GrassColor;
        grassImg.raycastTarget = false;

        // Grass top edge (darker strip)
        var grassTopGO = new GameObject("GrassEdge");
        grassTopGO.transform.SetParent(grassGO.transform, false);
        var grassTopRT = grassTopGO.AddComponent<RectTransform>();
        grassTopRT.anchorMin = new Vector2(0, 1);
        grassTopRT.anchorMax = new Vector2(1, 1);
        grassTopRT.pivot = new Vector2(0.5f, 1);
        grassTopRT.sizeDelta = new Vector2(0, 12);
        var grassTopImg = grassTopGO.AddComponent<Image>();
        grassTopImg.color = GrassDarkColor;
        grassTopImg.raycastTarget = false;

        // ── DECORATIVE CLOUDS ──
        CreateClouds(canvasGO.transform);

        // ── SUN (top-right decorative) ──
        var sunSprite = LoadSprite("Assets/Art/World/sun.png");
        if (sunSprite != null)
        {
            var sunGO = new GameObject("Sun");
            sunGO.transform.SetParent(canvasGO.transform, false);
            var sunRT = sunGO.AddComponent<RectTransform>();
            sunRT.anchorMin = new Vector2(1, 1);
            sunRT.anchorMax = new Vector2(1, 1);
            sunRT.pivot = new Vector2(1, 1);
            sunRT.sizeDelta = new Vector2(120, 120);
            sunRT.anchoredPosition = new Vector2(-40, -20);
            var sunImg = sunGO.AddComponent<Image>();
            sunImg.sprite = sunSprite;
            sunImg.preserveAspect = true;
            sunImg.raycastTarget = false;
            sunImg.color = new Color(1, 1, 0.9f, 0.85f);
        }

        // Safe area
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        Full(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // ── TOP BAR (slim, semi-transparent) ──
        var topBar = StretchImg(safeArea.transform, "TopBar", HeaderColor);
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.sizeDelta = new Vector2(0, TopBarHeight);
        topBar.GetComponent<Image>().raycastTarget = false;
        topBar.AddComponent<ThemeHeader>();

        // Question text (center of header)
        var questionGO = new GameObject("QuestionText");
        questionGO.transform.SetParent(topBar.transform, false);
        var questionRT = questionGO.AddComponent<RectTransform>();
        Full(questionRT);
        questionRT.offsetMin = new Vector2(100, 0);
        questionRT.offsetMax = new Vector2(-100, 0);
        var questionTMP = questionGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(questionTMP, "\u05DB\u05DE\u05D4 \u05D7\u05D9\u05D5\u05EA \u05D9\u05E9?"); // כמה חיות יש?
        questionTMP.fontSize = 36;
        questionTMP.fontStyle = FontStyles.Bold;
        questionTMP.color = Color.white;
        questionTMP.alignment = TextAlignmentOptions.Center;
        questionTMP.raycastTarget = false;

        // Small animal icon (right of question area)
        var iconGO = new GameObject("QuestionAnimalIcon");
        iconGO.transform.SetParent(topBar.transform, false);
        var iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(1, 0.5f);
        iconRT.anchorMax = new Vector2(1, 0.5f);
        iconRT.pivot = new Vector2(1, 0.5f);
        iconRT.sizeDelta = new Vector2(60, 60);
        iconRT.anchoredPosition = new Vector2(-110, 0);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        iconImg.color = Color.white;
        iconGO.SetActive(false); // controller will show it

        // Home button (top-left)
        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = IconBtn(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        // Trophy button (top-right)
        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = IconBtn(topBar.transform, "TrophyButton", trophyIcon,
            new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-16, -20), new Vector2(70, 70));

        // ── COUNTING NUMBER (large, top center, hidden until counting animation) ──
        var countNumGO = new GameObject("CountNumberText");
        countNumGO.transform.SetParent(safeArea.transform, false);
        var countNumRT = countNumGO.AddComponent<RectTransform>();
        countNumRT.anchorMin = new Vector2(0.3f, 0.78f);
        countNumRT.anchorMax = new Vector2(0.7f, 0.92f);
        countNumRT.offsetMin = Vector2.zero;
        countNumRT.offsetMax = Vector2.zero;
        var countNumTMP = countNumGO.AddComponent<TextMeshProUGUI>();
        countNumTMP.text = "";
        countNumTMP.fontSize = 120;
        countNumTMP.fontStyle = FontStyles.Bold;
        countNumTMP.color = Color.white;
        countNumTMP.alignment = TextAlignmentOptions.Center;
        countNumTMP.raycastTarget = false;
        countNumTMP.outlineWidth = 0.4f;
        countNumTMP.outlineColor = new Color(0, 0, 0, 0.3f);
        countNumGO.SetActive(false);

        // ── ANIMAL AREA (center of screen, above grass) ──
        var animalArea = new GameObject("AnimalArea");
        animalArea.transform.SetParent(safeArea.transform, false);
        var animalAreaRT = animalArea.AddComponent<RectTransform>();
        // Between header and grass+button area
        animalAreaRT.anchorMin = new Vector2(0.05f, 0.28f);
        animalAreaRT.anchorMax = new Vector2(0.95f, 0.90f);
        animalAreaRT.offsetMin = Vector2.zero;
        animalAreaRT.offsetMax = Vector2.zero;

        // ── BUTTON AREA (bottom, on the grass) ──
        var buttonArea = new GameObject("ButtonArea");
        buttonArea.transform.SetParent(safeArea.transform, false);
        var buttonAreaRT = buttonArea.AddComponent<RectTransform>();
        buttonAreaRT.anchorMin = new Vector2(0.15f, 0.03f);
        buttonAreaRT.anchorMax = new Vector2(0.85f, 0.22f);
        buttonAreaRT.offsetMin = Vector2.zero;
        buttonAreaRT.offsetMax = Vector2.zero;

        // ── CONTROLLER ──
        var controller = canvasGO.AddComponent<CountingGameController>();
        controller.animalArea = animalAreaRT;
        controller.buttonArea = buttonAreaRT;
        controller.questionText = questionTMP;
        controller.questionAnimalIcon = iconImg;
        controller.countNumberText = countNumTMP;
        controller.animalSize = 240f;
        controller.buttonSize = 160f;
        controller.circleSprite = circleSprite;

        // Wire home button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        // Wire trophy / leaderboard
        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = LoadSprite("Assets/UI/Sprites/RoundedRect.png");
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "findthecount";

        // Tutorial hand
        TutorialHandHelper.Create(safeArea.transform, TutorialHandHelper.Anim.Tap,
            new Vector2(0, -50), new Vector2(450, 450), "findthecount");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/CountingGame.unity");
    }

    // ── DECORATIVE CLOUDS ──

    private static void CreateClouds(Transform parent)
    {
        // Simple white ellipse clouds in the sky area
        Sprite cloudSprite = LoadSprite("Assets/Art/World/cloud1.png");
        if (cloudSprite == null) cloudSprite = LoadSprite("Assets/Art/World/cloud2.png");

        if (cloudSprite == null) return; // No cloud art available

        var cloudData = new[] {
            new { x = 0.12f, y = 0.85f, w = 140f, h = 60f, a = 0.5f },
            new { x = 0.45f, y = 0.92f, w = 180f, h = 70f, a = 0.4f },
            new { x = 0.78f, y = 0.82f, w = 120f, h = 50f, a = 0.45f },
            new { x = 0.30f, y = 0.75f, w = 100f, h = 45f, a = 0.3f },
            new { x = 0.88f, y = 0.72f, w = 110f, h = 48f, a = 0.35f },
        };

        for (int i = 0; i < cloudData.Length; i++)
        {
            var cd = cloudData[i];
            var go = new GameObject($"Cloud_{i}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(cd.x, cd.y);
            rt.anchorMax = new Vector2(cd.x, cd.y);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cd.w, cd.h);
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = cloudSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = new Color(1, 1, 1, cd.a);
        }
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
