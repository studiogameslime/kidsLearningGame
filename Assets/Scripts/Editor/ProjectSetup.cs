using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// One-click project bootstrapper.
/// Run via the menu: Tools > Kids Learning Game > Setup Project.
/// Creates all folders, sprites, prefabs, ScriptableObjects, scenes, and configures build settings.
/// Safe to re-run — overwrites existing assets.
/// </summary>
public class ProjectSetup : EditorWindow
{
    // ─────────────────────────────────────────────
    //  CONSTANTS
    // ─────────────────────────────────────────────

    // Reference resolution for Canvas Scaler (portrait)
    private static readonly Vector2 ReferenceResolution = new Vector2(1080, 1920);

    // Grid layout
    private const int GridPaddingH = 48;
    private const int GridPaddingTop = 24;
    private const int GridPaddingBottom = 24;
    private const int GridSpacing = 32;
    private const int CardWidth = 476;
    private const int CardHeight = 548;

    // Header
    private const int HeaderHeight = 150;

    // Colors (warm, kid-friendly pastels)
    private static readonly Color BgColor = HexColor("#F5F0EB");
    private static readonly Color HeaderBgColor = new Color(1f, 1f, 1f, 0.0f); // transparent
    private static readonly Color CardTextColor = Color.white;

    // Per-game card colors
    private static readonly Color MemoryColor   = HexColor("#B39DDB");
    private static readonly Color PuzzleColor   = HexColor("#FFB74D");
    private static readonly Color ColoringColor = HexColor("#F48FB1");
    private static readonly Color ColorsColor   = HexColor("#81D4FA");
    private static readonly Color LettersColor  = HexColor("#A5D6A7");
    private static readonly Color NumbersColor  = HexColor("#FFF176");

    // Sub-item colors
    private static readonly Color SubAnimals  = HexColor("#CE93D8");
    private static readonly Color SubNumbers  = HexColor("#90CAF9");
    private static readonly Color SubColors   = HexColor("#FFAB91");
    private static readonly Color SubLetters  = HexColor("#80CBC4");

    // Coloring sub-item animal colors
    private static readonly Color[] AnimalColors = new Color[]
    {
        HexColor("#EF9A9A"), HexColor("#F48FB1"), HexColor("#CE93D8"),
        HexColor("#B39DDB"), HexColor("#9FA8DA"), HexColor("#90CAF9"),
        HexColor("#81D4FA"), HexColor("#80DEEA"), HexColor("#80CBC4"),
        HexColor("#A5D6A7"), HexColor("#C5E1A5"), HexColor("#E6EE9C"),
        HexColor("#FFF59D"), HexColor("#FFE082"), HexColor("#FFCC80"),
        HexColor("#FFAB91"), HexColor("#BCAAA4"), HexColor("#B0BEC5"),
        HexColor("#EF9A9A")
    };

    // Asset paths
    private const string SpritesPath    = "Assets/UI/Sprites";
    private const string PrefabsPath    = "Assets/Prefabs/UI";
    private const string DataPath       = "Assets/Data/Games";
    private const string ScenesPath     = "Assets/Scenes";

    // ─────────────────────────────────────────────
    //  MENU ENTRY
    // ─────────────────────────────────────────────

    [MenuItem("Tools/Kids Learning Game/Setup Project")]
    public static void RunSetup()
    {
        if (!EditorUtility.DisplayDialog(
            "Kids Learning Game — Project Setup",
            "This will create/overwrite:\n• Sprites\n• Prefabs\n• ScriptableObject data\n• All scenes\n• Build settings\n• Player settings (portrait)\n\nContinue?",
            "Build Everything", "Cancel"))
            return;

        try
        {
            EditorUtility.DisplayProgressBar("Setting up project…", "Creating sprites…", 0.05f);
            var roundedRect = CreateRoundedRectSprite();
            var circleSprite = CreateCircleSprite();

            EditorUtility.DisplayProgressBar("Setting up project…", "Creating prefabs…", 0.15f);
            var cardPrefab = CreateCardPrefab(roundedRect);

            EditorUtility.DisplayProgressBar("Setting up project…", "Creating data assets…", 0.25f);
            var database = CreateDataAssets();

            EditorUtility.DisplayProgressBar("Setting up project…", "Creating scenes…", 0.40f);
            CreateAllScenes(cardPrefab, database, roundedRect, circleSprite);

            EditorUtility.DisplayProgressBar("Setting up project…", "Configuring settings…", 0.90f);
            ConfigurePlayerSettings();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Open MainMenu scene
            EditorSceneManager.OpenScene($"{ScenesPath}/MainMenu.unity");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorUtility.DisplayDialog(
            "Setup Complete!",
            "Project foundation created successfully.\n\n" +
            "• 8 scenes created and added to Build Settings\n" +
            "• Player settings configured for portrait\n" +
            "• MainMenu scene is now open\n\n" +
            "Press Play to test the full flow!",
            "OK");
    }

    // ─────────────────────────────────────────────
    //  SPRITES
    // ─────────────────────────────────────────────

    private static Sprite CreateRoundedRectSprite()
    {
        EnsureFolder(SpritesPath);

        int size = 128;
        int radius = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = DistToRoundedRect(x, y, size, size, radius);
                // Soft anti-aliased edge
                float alpha = Mathf.Clamp01(1f - dist);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        string path = $"{SpritesPath}/RoundedRect.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        // Configure import settings for 9-slice
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(radius, radius, radius, radius);
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite CreateCircleSprite()
    {
        EnsureFolder(SpritesPath);

        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        float center = (size - 1) / 2f;
        float radius = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) - radius;
                float alpha = Mathf.Clamp01(1f - dist);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        string path = $"{SpritesPath}/Circle.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>Signed distance from pixel to rounded-rect edge (negative = inside).</summary>
    private static float DistToRoundedRect(int px, int py, int w, int h, int r)
    {
        // Quadrant-relative position from nearest corner center
        float cx, cy;
        if (px < r) cx = r - px; else if (px >= w - r) cx = px - (w - r - 1); else cx = 0;
        if (py < r) cy = r - py; else if (py >= h - r) cy = py - (h - r - 1); else cy = 0;

        if (cx > 0 && cy > 0)
            return Mathf.Sqrt(cx * cx + cy * cy) - r; // corner
        return Mathf.Max(cx, cy) - (cx > 0 || cy > 0 ? r : 0); // edge (always inside for non-corner)
    }

    // ─────────────────────────────────────────────
    //  CARD PREFAB
    // ─────────────────────────────────────────────

    private static GameCardView CreateCardPrefab(Sprite roundedRect)
    {
        EnsureFolder(PrefabsPath);

        // Root — the card itself
        var root = new GameObject("GameCard");
        var rootRT = root.AddComponent<RectTransform>();

        // Background image (rounded rect, tinted per card)
        var bgImage = root.AddComponent<Image>();
        bgImage.sprite = roundedRect;
        bgImage.type = Image.Type.Sliced;
        bgImage.pixelsPerUnitMultiplier = 1;
        bgImage.color = Color.white;
        bgImage.raycastTarget = true;

        // Subtle shadow
        var shadow = root.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
        shadow.effectDistance = new Vector2(0, -4);

        // Button
        var btn = root.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.1f;
        btn.colors = colors;
        btn.targetGraphic = bgImage;

        // Title text (top area)
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(root.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.05f, 0.82f);
        titleRT.anchorMax = new Vector2(0.95f, 0.98f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "Game Title";
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = CardTextColor;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.enableWordWrapping = true;
        titleTMP.overflowMode = TextOverflowModes.Ellipsis;
        titleTMP.raycastTarget = false;

        // Thumbnail image (below title, fills most of the card)
        var thumbGO = new GameObject("Thumbnail");
        thumbGO.transform.SetParent(root.transform, false);
        var thumbRT = thumbGO.AddComponent<RectTransform>();
        thumbRT.anchorMin = new Vector2(0.05f, 0.02f);
        thumbRT.anchorMax = new Vector2(0.95f, 0.82f);
        thumbRT.offsetMin = Vector2.zero;
        thumbRT.offsetMax = Vector2.zero;
        var thumbImg = thumbGO.AddComponent<Image>();
        thumbImg.preserveAspect = true;
        thumbImg.raycastTarget = false;
        thumbImg.color = Color.white;

        // Placeholder icon (shown when no thumbnail)
        var phGO = new GameObject("PlaceholderIcon");
        phGO.transform.SetParent(root.transform, false);
        var phRT = phGO.AddComponent<RectTransform>();
        phRT.anchorMin = new Vector2(0.2f, 0.2f);
        phRT.anchorMax = new Vector2(0.8f, 0.75f);
        phRT.offsetMin = Vector2.zero;
        phRT.offsetMax = Vector2.zero;
        var phImg = phGO.AddComponent<Image>();
        phImg.color = new Color(1f, 1f, 1f, 0.35f);
        phImg.preserveAspect = true;
        phImg.raycastTarget = false;

        // Wire up the GameCardView component
        var cardView = root.AddComponent<GameCardView>();
        cardView.backgroundImage = bgImage;
        cardView.thumbnailImage = thumbImg;
        cardView.titleText = titleTMP;
        cardView.button = btn;
        cardView.placeholderIcon = phImg;

        // Save prefab
        string prefabPath = $"{PrefabsPath}/GameCard.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        return prefab.GetComponent<GameCardView>();
    }

    // ─────────────────────────────────────────────
    //  SCRIPTABLEOBJECT DATA
    // ─────────────────────────────────────────────

    private static GameDatabase CreateDataAssets()
    {
        EnsureFolder(DataPath);

        // Animal names for coloring sub-items
        string[] animals = {
            "Bear", "Bird", "Cat", "Chicken", "Cow", "Dog", "Donkey", "Duck",
            "Elephant", "Fish", "Frog", "Giraffe", "Horse", "Lion", "Monkey",
            "Sheep", "Snake", "Turtle", "Zebra"
        };

        // ── Memory Game ──
        var memory = CreateSO<GameItemData>($"{DataPath}/MemoryGame.asset");
        memory.id = "memory";
        memory.title = "Memory";
        memory.cardColor = MemoryColor;
        memory.targetSceneName = "MemoryGame";
        memory.hasSubItems = false;
        memory.selectionScreenTitle = "Choose a Category";
        memory.subItems = new List<SubItemData>
        {
            new SubItemData { id = "memory_animals", title = "Animals",  cardColor = SubAnimals, categoryKey = "animals",  targetSceneName = "MemoryGame" },
            new SubItemData { id = "memory_numbers", title = "Numbers",  cardColor = SubNumbers, categoryKey = "numbers",  targetSceneName = "MemoryGame" },
            new SubItemData { id = "memory_colors",  title = "Colors",   cardColor = SubColors,  categoryKey = "colors",   targetSceneName = "MemoryGame" },
            new SubItemData { id = "memory_letters", title = "Letters",  cardColor = SubLetters, categoryKey = "letters",  targetSceneName = "MemoryGame" },
        };
        EditorUtility.SetDirty(memory);

        // ── Coloring ──
        var coloring = CreateSO<GameItemData>($"{DataPath}/Coloring.asset");
        coloring.id = "coloring";
        coloring.title = "Coloring";
        coloring.cardColor = ColoringColor;
        coloring.targetSceneName = "ColoringGame";
        coloring.hasSubItems = true;
        coloring.selectionScreenTitle = "Choose a Picture";
        coloring.subItems = new List<SubItemData>();
        for (int i = 0; i < animals.Length; i++)
        {
            coloring.subItems.Add(new SubItemData
            {
                id = $"coloring_{animals[i].ToLower()}",
                title = animals[i],
                cardColor = AnimalColors[i % AnimalColors.Length],
                categoryKey = animals[i].ToLower(),
                targetSceneName = "ColoringGame"
            });
        }
        EditorUtility.SetDirty(coloring);

        // ── Puzzle (sub-item selection) ──
        var puzzle = CreateSO<GameItemData>($"{DataPath}/Puzzle.asset");
        puzzle.id = "puzzle";
        puzzle.title = "Puzzle";
        puzzle.cardColor = PuzzleColor;
        puzzle.targetSceneName = "PuzzleGame";
        puzzle.hasSubItems = true;
        puzzle.selectionScreenTitle = "Choose a Puzzle";
        puzzle.subItems = new List<SubItemData>();
        for (int i = 0; i < animals.Length; i++)
        {
            puzzle.subItems.Add(new SubItemData
            {
                id = $"puzzle_{animals[i].ToLower()}",
                title = animals[i],
                cardColor = AnimalColors[i % AnimalColors.Length],
                categoryKey = animals[i].ToLower(),
                targetSceneName = "PuzzleGame"
            });
        }
        EditorUtility.SetDirty(puzzle);

        // ── Colors (direct launch) ──
        var colors = CreateSO<GameItemData>($"{DataPath}/Colors.asset");
        colors.id = "colors";
        colors.title = "Colors";
        colors.cardColor = ColorsColor;
        colors.targetSceneName = "ColorsGame";
        colors.hasSubItems = false;
        EditorUtility.SetDirty(colors);

        // ── Letters (direct launch) ──
        var letters = CreateSO<GameItemData>($"{DataPath}/Letters.asset");
        letters.id = "letters";
        letters.title = "Letters";
        letters.cardColor = LettersColor;
        letters.targetSceneName = "LettersGame";
        letters.hasSubItems = false;
        EditorUtility.SetDirty(letters);

        // ── Numbers (direct launch) ──
        var numbers = CreateSO<GameItemData>($"{DataPath}/Numbers.asset");
        numbers.id = "numbers";
        numbers.title = "Numbers";
        numbers.cardColor = NumbersColor;
        numbers.targetSceneName = "NumbersGame";
        numbers.hasSubItems = false;
        EditorUtility.SetDirty(numbers);

        // ── Game Database ──
        var db = CreateSO<GameDatabase>($"{DataPath}/GameDatabase.asset");
        db.games = new List<GameItemData> { memory, puzzle, coloring, colors, letters, numbers };
        EditorUtility.SetDirty(db);

        return db;
    }

    // ─────────────────────────────────────────────
    //  SCENES
    // ─────────────────────────────────────────────

    private static void CreateAllScenes(GameCardView cardPrefab, GameDatabase database, Sprite roundedRect, Sprite circleSprite)
    {
        EnsureFolder(ScenesPath);

        // Delete the default SampleScene if it exists
        string sampleScene = $"{ScenesPath}/SampleScene.unity";
        if (File.Exists(sampleScene))
            AssetDatabase.DeleteAsset(sampleScene);

        CreateMainMenuScene(cardPrefab, database, roundedRect, circleSprite);
        CreateSelectionMenuScene(cardPrefab, roundedRect, circleSprite);

        // Placeholder game scenes
        string[] gameScenes = { "MemoryGame", "PuzzleGame", "ColoringGame", "ColorsGame", "LettersGame", "NumbersGame" };
        foreach (var sceneName in gameScenes)
        {
            float progress = 0.4f + 0.5f * (System.Array.IndexOf(gameScenes, sceneName) / (float)gameScenes.Length);
            EditorUtility.DisplayProgressBar("Setting up project…", $"Creating {sceneName} scene…", progress);
            CreatePlaceholderGameScene(sceneName, roundedRect, circleSprite);
        }
    }

    private static void CreateMainMenuScene(GameCardView cardPrefab, GameDatabase database, Sprite roundedRect, Sprite circleSprite)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGO = CreateCamera();

        // EventSystem
        CreateEventSystem();

        // Canvas
        var canvas = CreateCanvas("MainCanvas");
        var canvasGO = canvas.gameObject;

        // Background (full-screen color, must be first child so it renders behind everything)
        var bgGO = CreateFullStretchImage(canvasGO.transform, "Background", BgColor, null, -1);

        // Safe Area
        var safeArea = CreateSafeArea(canvasGO.transform);

        // Header
        var header = CreateHeader(safeArea.transform, "Choose a Game", showBack: false, roundedRect, circleSprite);

        // Scroll view with grid
        var scrollContent = CreateScrollGrid(safeArea.transform, HeaderHeight);

        // Main Menu Controller
        var controller = canvasGO.AddComponent<MainMenuController>();
        controller.database = database;
        controller.cardContainer = scrollContent;
        controller.cardPrefab = cardPrefab;

        EditorSceneManager.SaveScene(scene, $"{ScenesPath}/MainMenu.unity");
    }

    private static void CreateSelectionMenuScene(GameCardView cardPrefab, Sprite roundedRect, Sprite circleSprite)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateEventSystem();

        var canvas = CreateCanvas("SelectionCanvas");
        var canvasGO = canvas.gameObject;

        var bgGO = CreateFullStretchImage(canvasGO.transform, "Background", BgColor, null, -1);
        var safeArea = CreateSafeArea(canvasGO.transform);

        // Header with back button
        var header = CreateHeader(safeArea.transform, "Choose", showBack: true, roundedRect, circleSprite);
        var titleTMP = header.transform.Find("TitleText").GetComponent<TextMeshProUGUI>();

        // Scroll grid
        var scrollContent = CreateScrollGrid(safeArea.transform, HeaderHeight);

        // Controller
        var controller = canvasGO.AddComponent<SelectionMenuController>();
        controller.titleText = titleTMP;
        controller.cardContainer = scrollContent;
        controller.cardPrefab = cardPrefab;

        // Wire back button
        var backBtn = header.transform.Find("BackButton");
        if (backBtn != null)
        {
            var btn = backBtn.GetComponent<Button>();
            // Use UnityEvent persistent call via SerializedObject
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                btn.onClick,
                controller.OnBackPressed
            );
        }

        EditorSceneManager.SaveScene(scene, $"{ScenesPath}/SelectionMenu.unity");
    }

    private static void CreatePlaceholderGameScene(string sceneName, Sprite roundedRect, Sprite circleSprite)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateEventSystem();

        var canvas = CreateCanvas("GameCanvas");
        var canvasGO = canvas.gameObject;

        var bgGO = CreateFullStretchImage(canvasGO.transform, "Background", BgColor, null, -1);
        var safeArea = CreateSafeArea(canvasGO.transform);

        // Header with home button
        var header = CreateGameHeader(safeArea.transform, sceneName, roundedRect, circleSprite);
        var titleTMP = header.transform.Find("TitleText").GetComponent<TextMeshProUGUI>();

        // Info panel (centered content)
        var infoPanelGO = new GameObject("InfoPanel");
        infoPanelGO.transform.SetParent(safeArea.transform, false);
        var infoPanelRT = infoPanelGO.AddComponent<RectTransform>();
        infoPanelRT.anchorMin = new Vector2(0.08f, 0.15f);
        infoPanelRT.anchorMax = new Vector2(0.92f, 0.75f);
        infoPanelRT.offsetMin = Vector2.zero;
        infoPanelRT.offsetMax = Vector2.zero;

        // Panel background
        var panelBg = infoPanelGO.AddComponent<Image>();
        panelBg.sprite = roundedRect;
        panelBg.type = Image.Type.Sliced;
        panelBg.color = new Color(1f, 1f, 1f, 0.85f);
        panelBg.raycastTarget = false;

        var panelShadow = infoPanelGO.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.1f);
        panelShadow.effectDistance = new Vector2(0, -3);

        // Info text inside panel
        var infoGO = new GameObject("InfoText");
        infoGO.transform.SetParent(infoPanelGO.transform, false);
        var infoRT = infoGO.AddComponent<RectTransform>();
        infoRT.anchorMin = new Vector2(0.08f, 0.08f);
        infoRT.anchorMax = new Vector2(0.92f, 0.92f);
        infoRT.offsetMin = Vector2.zero;
        infoRT.offsetMax = Vector2.zero;
        var infoTMP = infoGO.AddComponent<TextMeshProUGUI>();
        infoTMP.text = $"Placeholder for {sceneName}\n\nSelection info will appear here at runtime.";
        infoTMP.fontSize = 34;
        infoTMP.color = HexColor("#555555");
        infoTMP.alignment = TextAlignmentOptions.Center;
        infoTMP.enableWordWrapping = true;
        infoTMP.raycastTarget = false;

        // Controller
        var controller = canvasGO.AddComponent<PlaceholderGameController>();
        controller.titleText = titleTMP;
        controller.infoText = infoTMP;

        // Wire home button
        var homeBtn = header.transform.Find("HomeButton");
        if (homeBtn != null)
        {
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                homeBtn.GetComponent<Button>().onClick,
                controller.OnHomePressed
            );
        }

        // Wire restart button
        var restartBtn = header.transform.Find("RestartButton");
        if (restartBtn != null)
        {
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                restartBtn.GetComponent<Button>().onClick,
                controller.OnRestartPressed
            );
        }

        EditorSceneManager.SaveScene(scene, $"{ScenesPath}/{sceneName}.unity");
    }

    // ─────────────────────────────────────────────
    //  UI BUILDING HELPERS
    // ─────────────────────────────────────────────

    private static GameObject CreateCamera()
    {
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgColor;
        cam.orthographic = true;
        cam.orthographicSize = 5;
        // Add URP camera data if available
        var urpCamType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpCamType != null)
            camGO.AddComponent(urpCamType);
        return camGO;
    }

    private static void CreateEventSystem()
    {
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();

        // Try to use the new Input System module first, fall back to standalone
        var inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModuleType != null)
            esGO.AddComponent(inputModuleType);
        else
            esGO.AddComponent<StandaloneInputModule>();
    }

    private static Canvas CreateCanvas(string name)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0f; // Match width for portrait
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        go.AddComponent<GraphicRaycaster>();

        return canvas;
    }

    private static RectTransform CreateSafeArea(Transform parent)
    {
        var go = new GameObject("SafeArea");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        StretchFull(rt);
        go.AddComponent<SafeAreaHandler>();
        return rt;
    }

    private static GameObject CreateFullStretchImage(Transform parent, string name, Color color, Sprite sprite, int siblingIndex = -1)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        if (siblingIndex >= 0)
            go.transform.SetSiblingIndex(siblingIndex);
        var rt = go.AddComponent<RectTransform>();
        StretchFull(rt);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.raycastTarget = false;
        return go;
    }

    /// <summary>Creates the header bar for MainMenu and SelectionMenu scenes.</summary>
    private static GameObject CreateHeader(Transform parent, string title, bool showBack, Sprite roundedRect, Sprite circleSprite)
    {
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(parent, false);
        var headerRT = headerGO.AddComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 1);
        headerRT.anchorMax = new Vector2(1, 1);
        headerRT.pivot = new Vector2(0.5f, 1);
        headerRT.sizeDelta = new Vector2(0, HeaderHeight);

        // Title text
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(headerGO.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        StretchFull(titleRT);
        titleRT.offsetMin = new Vector2(showBack ? 120 : 40, 10);
        titleRT.offsetMax = new Vector2(-40, -10);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = title;
        titleTMP.fontSize = 52;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = HexColor("#4A4A4A");
        titleTMP.alignment = showBack ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
        titleTMP.enableWordWrapping = false;
        titleTMP.overflowMode = TextOverflowModes.Ellipsis;
        titleTMP.raycastTarget = false;

        // Back button (if needed)
        if (showBack)
        {
            var backGO = new GameObject("BackButton");
            backGO.transform.SetParent(headerGO.transform, false);
            var backRT = backGO.AddComponent<RectTransform>();
            backRT.anchorMin = new Vector2(0, 0.5f);
            backRT.anchorMax = new Vector2(0, 0.5f);
            backRT.pivot = new Vector2(0, 0.5f);
            backRT.anchoredPosition = new Vector2(24, 0);
            backRT.sizeDelta = new Vector2(90, 90);

            var backImg = backGO.AddComponent<Image>();
            backImg.sprite = roundedRect;
            backImg.type = Image.Type.Sliced;
            backImg.color = new Color(0f, 0f, 0f, 0.06f);

            var backBtn = backGO.AddComponent<Button>();
            var backColors = backBtn.colors;
            backColors.normalColor = new Color(0f, 0f, 0f, 0.06f);
            backColors.pressedColor = new Color(0f, 0f, 0f, 0.15f);
            backColors.highlightedColor = new Color(0f, 0f, 0f, 0.1f);
            backBtn.colors = backColors;
            backBtn.targetGraphic = backImg;

            // Back arrow text
            var arrowGO = new GameObject("Arrow");
            arrowGO.transform.SetParent(backGO.transform, false);
            var arrowRT = arrowGO.AddComponent<RectTransform>();
            StretchFull(arrowRT);
            var arrowTMP = arrowGO.AddComponent<TextMeshProUGUI>();
            arrowTMP.text = "<";
            arrowTMP.fontSize = 36;
            arrowTMP.color = HexColor("#4A4A4A");
            arrowTMP.alignment = TextAlignmentOptions.Center;
            arrowTMP.raycastTarget = false;
        }

        return headerGO;
    }

    /// <summary>Creates the header bar for game scenes with Home and Restart buttons.</summary>
    private static GameObject CreateGameHeader(Transform parent, string title, Sprite roundedRect, Sprite circleSprite)
    {
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(parent, false);
        var headerRT = headerGO.AddComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 1);
        headerRT.anchorMax = new Vector2(1, 1);
        headerRT.pivot = new Vector2(0.5f, 1);
        headerRT.sizeDelta = new Vector2(0, HeaderHeight);

        // Title text (centered)
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(headerGO.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        StretchFull(titleRT);
        titleRT.offsetMin = new Vector2(120, 10);
        titleRT.offsetMax = new Vector2(-120, -10);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = title;
        titleTMP.fontSize = 48;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = HexColor("#4A4A4A");
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.enableWordWrapping = false;
        titleTMP.overflowMode = TextOverflowModes.Ellipsis;
        titleTMP.raycastTarget = false;

        // Home button (left)
        CreateHeaderButton(headerGO.transform, "HomeButton", "\u2302", // ⌂
            new Vector2(24, 0), new Vector2(0, 0.5f), roundedRect);

        // Restart button (right)
        CreateHeaderButton(headerGO.transform, "RestartButton", "\u21BB", // ↻
            new Vector2(-24, 0), new Vector2(1, 0.5f), roundedRect, anchorRight: true);

        return headerGO;
    }

    private static GameObject CreateHeaderButton(Transform parent, string name, string icon,
        Vector2 pos, Vector2 anchor, Sprite roundedRect, bool anchorRight = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchorRight ? new Vector2(1, 0.5f) : new Vector2(0, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(90, 90);

        var img = go.AddComponent<Image>();
        img.sprite = roundedRect;
        img.type = Image.Type.Sliced;
        img.color = new Color(0f, 0f, 0f, 0.06f);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0f, 0f, 0f, 0.06f);
        colors.pressedColor = new Color(0f, 0f, 0f, 0.15f);
        colors.highlightedColor = new Color(0f, 0f, 0f, 0.1f);
        btn.colors = colors;
        btn.targetGraphic = img;

        var labelGO = new GameObject("Icon");
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        StretchFull(labelRT);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = icon;
        labelTMP.fontSize = 40;
        labelTMP.color = HexColor("#4A4A4A");
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.raycastTarget = false;

        return go;
    }

    /// <summary>Creates a ScrollRect with a GridLayoutGroup content area.</summary>
    private static Transform CreateScrollGrid(Transform parent, int topOffset)
    {
        // Scroll View
        var scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(parent, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        StretchFull(scrollRT);
        scrollRT.offsetMax = new Vector2(0, -topOffset);

        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.decelerationRate = 0.12f;

        // Scroll background (transparent, needed for raycasting)
        var scrollImg = scrollGO.AddComponent<Image>();
        scrollImg.color = Color.clear;

        // Viewport
        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var viewportRT = viewportGO.AddComponent<RectTransform>();
        StretchFull(viewportRT);
        var viewportImg = viewportGO.AddComponent<Image>();
        viewportImg.color = Color.white;
        viewportImg.raycastTarget = true;
        var mask = viewportGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // Content
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.sizeDelta = new Vector2(0, 0);

        // Grid layout
        var grid = contentGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(CardWidth, CardHeight);
        grid.spacing = new Vector2(GridSpacing, GridSpacing);
        grid.padding = new RectOffset(GridPaddingH, GridPaddingH, GridPaddingTop, GridPaddingBottom);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;

        // Content Size Fitter (to grow the content as cards are added)
        var fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Wire ScrollRect references
        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;

        return contentGO.transform;
    }

    // ─────────────────────────────────────────────
    //  SETTINGS
    // ─────────────────────────────────────────────

    private static void ConfigurePlayerSettings()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        // Set default resolution for editor testing (portrait)
        PlayerSettings.defaultScreenWidth = 1080;
        PlayerSettings.defaultScreenHeight = 1920;
    }

    private static void ConfigureBuildSettings()
    {
        string[] scenePaths = {
            $"{ScenesPath}/MainMenu.unity",
            $"{ScenesPath}/SelectionMenu.unity",
            $"{ScenesPath}/MemoryGame.unity",
            $"{ScenesPath}/PuzzleGame.unity",
            $"{ScenesPath}/ColoringGame.unity",
            $"{ScenesPath}/ColorsGame.unity",
            $"{ScenesPath}/LettersGame.unity",
            $"{ScenesPath}/NumbersGame.unity",
        };

        var buildScenes = scenePaths.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
        EditorBuildSettings.scenes = buildScenes;
    }

    // ─────────────────────────────────────────────
    //  UTILITY
    // ─────────────────────────────────────────────

    private static T CreateSO<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            // Clear and reuse
            return existing;
        }

        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string[] parts = path.Split('/');
        string current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
