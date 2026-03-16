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

    // Reference resolution for Canvas Scaler (landscape)
    private static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

    // Grid layout
    private const int GridPaddingH = 48;
    private const int GridPaddingTop = 24;
    private const int GridPaddingBottom = 24;
    private const int GridSpacing = 28;
    private const int CardWidth = 580;
    private const int CardHeight = 520;

    // Header
    private const int HeaderHeight = 130;

    // Colors (warm, kid-friendly pastels)
    private static readonly Color BgColor = HexColor("#F5F0EB");
    private static readonly Color HeaderBgColor = new Color(1f, 1f, 1f, 0.0f); // transparent
    private static readonly Color CardTextColor = Color.white;

    // Per-game card colors
    private static readonly Color MemoryColor      = HexColor("#B39DDB");
    private static readonly Color PuzzleColor      = HexColor("#FFB74D");
    private static readonly Color ColoringColor    = HexColor("#F48FB1");
    private static readonly Color FillDotsColor    = HexColor("#81D4FA");
    private static readonly Color FindCountColor   = HexColor("#A5D6A7");
    private static readonly Color FindObjectColor  = HexColor("#FFF176");

    private static readonly Color ShadowsColor     = HexColor("#FFCC80");

    // Sub-item colors
    private static readonly Color SubAnimals  = HexColor("#CE93D8");
    // (SubNumbers, SubColors, SubLetters removed — those games were removed)

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

    [MenuItem("Tools/Kids Learning Game/Reset Save Data")]
    public static void ResetSaveData()
    {
        if (!EditorUtility.DisplayDialog(
            "Reset Save Data",
            "This will delete ALL saved profiles, drawings, and recordings.\n\nThe app will start fresh as if newly installed.\n\nThis cannot be undone!",
            "Reset Everything", "Cancel"))
            return;

        string persistentPath = Application.persistentDataPath;

        // Delete profiles.json
        string profilesJson = System.IO.Path.Combine(persistentPath, "profiles.json");
        if (System.IO.File.Exists(profilesJson))
            System.IO.File.Delete(profilesJson);

        // Delete profiles/ directory (audio, drawings, etc.)
        string profilesDir = System.IO.Path.Combine(persistentPath, "profiles");
        if (System.IO.Directory.Exists(profilesDir))
            System.IO.Directory.Delete(profilesDir, true);

        Debug.Log($"Save data reset. Deleted: {profilesJson}, {profilesDir}");
        EditorUtility.DisplayDialog("Done!", "All save data has been deleted.\n\nThe app will start fresh on next Play.", "OK");
    }

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
            EditorUtility.DisplayProgressBar("Setting up project…", "Setting up Hebrew font…", 0.02f);
            HebrewFontSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Creating sprites…", 0.05f);
            var roundedRect = CreateRoundedRectSprite();
            var circleSprite = CreateCircleSprite();

            EditorUtility.DisplayProgressBar("Setting up project…", "Creating prefabs…", 0.15f);
            var cardPrefab = CreateCardPrefab(roundedRect);

            EditorUtility.DisplayProgressBar("Setting up project…", "Creating data assets…", 0.25f);
            var database = CreateDataAssets();

            EditorUtility.DisplayProgressBar("Setting up project…", "Creating scenes…", 0.40f);
            CreateAllScenes(cardPrefab, database, roundedRect, circleSprite);

            EditorUtility.DisplayProgressBar("Setting up project…", "Configuring settings…", 0.85f);
            ConfigurePlayerSettings();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Build all game scenes
            EditorUtility.DisplayProgressBar("Setting up project…", "Building Memory Game…", 0.88f);
            MemoryGameSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Coloring Game…", 0.92f);
            ColoringGameSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Puzzle Game…", 0.96f);
            PuzzleGameSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Connect The Dots…", 0.90f);
            ConnectTheDotsSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Color Mixing…", 0.91f);
            ColorMixingSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Shadow Match…", 0.92f);
            ShadowMatchSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Find The Animal…", 0.95f);
            FindTheAnimalSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Counting Game…", 0.96f);
            CountingGameSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Color Voice Game…", 0.965f);
            ColorVoiceGameSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Tower Builder…", 0.968f);
            TowerBuilderSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Ball Maze…", 0.969f);
            BallMazeSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Tower Stack…", 0.971f);
            TowerStackSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Shared Sticker…", 0.973f);
            SharedStickerSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Flappy Bird…", 0.975f);
            FlappyBirdSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Simon Says…", 0.977f);
            SimonSaysSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Animal Data…", 0.965f);
            WorldSceneSetup.BuildAnimalData();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Profile Scenes…", 0.97f);
            ProfileSceneSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Home Scene…", 0.98f);
            HomeSceneSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Discovery Reveal…", 0.985f);
            DiscoveryRevealSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Drawing Gallery…", 0.992f);
            DrawingGallerySetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building World Scene…", 0.99f);
            WorldSceneSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Creating Analytics configs…", 0.995f);
            AnalyticsSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Parent Dashboard…", 0.997f);
            ParentDashboardSetup.RunSetupSilent();

            // Open ProfileSelection scene (entry point)
            EditorSceneManager.OpenScene($"{ScenesPath}/ProfileSelection.unity");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorUtility.DisplayDialog(
            "Setup Complete!",
            "Project foundation created successfully.\n\n" +
            "• 6 scenes created and added to Build Settings\n" +
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

        // Invisible background for button hit area
        var bgImage = root.AddComponent<Image>();
        bgImage.color = new Color(1f, 1f, 1f, 0f); // fully transparent
        bgImage.raycastTarget = true;

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

        // Title text (hidden — kids can't read, but kept for sub-selection menus)
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(root.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.05f, 0.82f);
        titleRT.anchorMax = new Vector2(0.95f, 0.98f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "";
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = CardTextColor;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.enableWordWrapping = true;
        titleTMP.overflowMode = TextOverflowModes.Ellipsis;
        titleTMP.raycastTarget = false;
        titleGO.SetActive(false);

        // Thumbnail mask container (rounded corners via Mask)
        var thumbMaskGO = new GameObject("ThumbnailMask");
        thumbMaskGO.transform.SetParent(root.transform, false);
        var thumbMaskRT = thumbMaskGO.AddComponent<RectTransform>();
        thumbMaskRT.anchorMin = Vector2.zero;
        thumbMaskRT.anchorMax = Vector2.one;
        thumbMaskRT.offsetMin = new Vector2(14, 14);
        thumbMaskRT.offsetMax = new Vector2(-14, -14);
        var maskImg = thumbMaskGO.AddComponent<Image>();
        maskImg.sprite = roundedRect;
        maskImg.type = Image.Type.Sliced;
        maskImg.raycastTarget = false;
        thumbMaskGO.AddComponent<Mask>().showMaskGraphic = false;

        // Actual thumbnail image (child of mask, stretches to fill)
        var thumbGO = new GameObject("Thumbnail");
        thumbGO.transform.SetParent(thumbMaskGO.transform, false);
        var thumbRT = thumbGO.AddComponent<RectTransform>();
        thumbRT.anchorMin = Vector2.zero;
        thumbRT.anchorMax = Vector2.one;
        thumbRT.offsetMin = Vector2.zero;
        thumbRT.offsetMax = Vector2.zero;
        var thumbImg = thumbGO.AddComponent<Image>();
        thumbImg.preserveAspect = false;
        thumbImg.raycastTarget = false;
        thumbImg.color = Color.white;

        // Frame border (rounded rect outline, sits on top, overlapping the thumbnail edges)
        var frameGO = new GameObject("Frame");
        frameGO.transform.SetParent(root.transform, false);
        var frameRT = frameGO.AddComponent<RectTransform>();
        frameRT.anchorMin = Vector2.zero;
        frameRT.anchorMax = Vector2.one;
        frameRT.anchorMin = Vector2.zero;
        frameRT.anchorMax = Vector2.one;
        frameRT.offsetMin = new Vector2(2, 2);
        frameRT.offsetMax = new Vector2(-2, -2);
        var frameImg = frameGO.AddComponent<Image>();
        frameImg.sprite = roundedRect;
        frameImg.type = Image.Type.Sliced;
        frameImg.color = Color.white;
        frameImg.raycastTarget = false;
        frameImg.fillCenter = false; // only the border, no fill
        frameImg.pixelsPerUnitMultiplier = 0.75f; // slightly thicker border

        // Subtle shadow on the frame
        var shadow = frameGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.15f);
        shadow.effectDistance = new Vector2(0, -3);

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
        cardView.backgroundImage = frameImg; // frame gets tinted with cardColor
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

        string previewPath = "Assets/Art/Games Preview";

        // ── Memory Game ──
        var memory = CreateSO<GameItemData>($"{DataPath}/MemoryGame.asset");
        memory.id = "memory";
        memory.title = "Memory";
        memory.cardColor = MemoryColor;
        memory.targetSceneName = "MemoryGame";
        memory.hasSubItems = false;
        memory.thumbnail = LoadSprite($"{previewPath}/MemoryCards.png");
        memory.nameClip = LoadAudioClip("Assets/Sounds/Games Names/Memory Game.mp3");
        memory.subItems = new List<SubItemData>
        {
            new SubItemData { id = "memory_animals", title = "Animals",  cardColor = SubAnimals, categoryKey = "animals",  targetSceneName = "MemoryGame" },
        };
        EditorUtility.SetDirty(memory);

        // ── Puzzle ──
        var puzzle = CreateSO<GameItemData>($"{DataPath}/Puzzle.asset");
        puzzle.id = "puzzle";
        puzzle.title = "Puzzle";
        puzzle.cardColor = PuzzleColor;
        puzzle.targetSceneName = "PuzzleGame";
        puzzle.hasSubItems = true;
        puzzle.selectionScreenTitle = "\u05D1\u05D7\u05E8\u05D5 \u05E4\u05D0\u05D6\u05DC"; // בחרו פאזל
        puzzle.thumbnail = LoadSprite($"{previewPath}/Puzzle.png");
        puzzle.nameClip = LoadAudioClip("Assets/Sounds/Games Names/Puzzle.mp3");
        puzzle.subItems = new List<SubItemData>();
        // Gallery import option — first item
        puzzle.subItems.Add(new SubItemData
        {
            id = "puzzle_gallery",
            title = "\u05DE\u05D4\u05D2\u05DC\u05E8\u05D9\u05D4", // מהגלריה
            cardColor = new Color(0.85f, 0.75f, 0.95f),
            categoryKey = "gallery",
            targetSceneName = "PuzzleGame"
        });
        for (int i = 0; i < animals.Length; i++)
        {
            string animalName = animals[i];
            // Load the actual puzzle image so the selection card shows the real picture
            string puzzlePath = $"Assets/Art/Animals/{animalName}/Art/Puzzle/{animalName} Main.png";
            var puzzleSprite = LoadSprite(puzzlePath);
            // Fallback to the regular sprite if no puzzle image
            if (puzzleSprite == null)
                puzzleSprite = LoadSprite($"Assets/Art/Animals/{animalName}/Art/{animalName}Sprite.png");

            puzzle.subItems.Add(new SubItemData
            {
                id = $"puzzle_{animalName.ToLower()}",
                title = animalName,
                cardColor = AnimalColors[i % AnimalColors.Length],
                categoryKey = animalName.ToLower(),
                targetSceneName = "PuzzleGame",
                thumbnail = puzzleSprite,
                contentAsset = puzzleSprite
            });
        }
        EditorUtility.SetDirty(puzzle);

        // ── Coloring / Painting ──
        var coloring = CreateSO<GameItemData>($"{DataPath}/Coloring.asset");
        coloring.id = "coloring";
        coloring.title = "Painting";
        coloring.cardColor = ColoringColor;
        coloring.targetSceneName = "ColoringGame";
        coloring.hasSubItems = true;
        coloring.selectionScreenTitle = "\u05D1\u05D7\u05E8\u05D5 \u05E6\u05D9\u05D5\u05E8"; // בחרו ציור
        coloring.thumbnail = LoadSprite($"{previewPath}/Painting.png");
        coloring.nameClip = LoadAudioClip("Assets/Sounds/Games Names/Painting.mp3");
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

        // ── Connect The Dots ──
        var fillDots = CreateSO<GameItemData>($"{DataPath}/FillTheDots.asset");
        fillDots.id = "fillthedots";
        fillDots.title = "Connect the Dots";
        fillDots.cardColor = FillDotsColor;
        fillDots.targetSceneName = "ConnectTheDots";
        fillDots.hasSubItems = false;
        fillDots.thumbnail = LoadSprite($"{previewPath}/FillTheDots.png");
        fillDots.nameClip = LoadAudioClip("Assets/Sounds/Games Names/Connect the dots.mp3");
        EditorUtility.SetDirty(fillDots);

        // ── Counting Game ──
        var findCount = CreateSO<GameItemData>($"{DataPath}/FindTheCount.asset");
        findCount.id = "findthecount";
        findCount.title = "How Many?";
        findCount.cardColor = FindCountColor;
        findCount.targetSceneName = "CountingGame";
        findCount.hasSubItems = false;
        findCount.thumbnail = LoadSprite($"{previewPath}/FindTheCount.png");
        findCount.nameClip = LoadAudioClip("Assets/Sounds/Games Names/How Many.mp3");
        EditorUtility.SetDirty(findCount);

        // ── Find The Animal ──
        var findObject = CreateSO<GameItemData>($"{DataPath}/FindTheObject.asset");
        findObject.id = "findtheobject";
        findObject.title = "Find the Animal";
        findObject.cardColor = FindObjectColor;
        findObject.targetSceneName = "FindTheAnimal";
        findObject.hasSubItems = false;
        findObject.thumbnail = LoadSprite($"{previewPath}/FindTheObject.png");
        findObject.nameClip = LoadAudioClip("Assets/Sounds/Games Names/Find the animal.mp3");
        EditorUtility.SetDirty(findObject);

        // ── Shadow Match ──
        var shadows = CreateSO<GameItemData>($"{DataPath}/Shadows.asset");
        shadows.id = "shadows";
        shadows.title = "Shadow Match";
        shadows.cardColor = ShadowsColor;
        shadows.targetSceneName = "ShadowMatch";
        shadows.hasSubItems = false;
        shadows.thumbnail = LoadSprite($"{previewPath}/Shadows.png");
        shadows.nameClip = LoadAudioClip("Assets/Sounds/Games Names/Shadows.mp3");
        EditorUtility.SetDirty(shadows);

        // ── Color Mixing ──
        var colorMix = CreateSO<GameItemData>($"{DataPath}/ColorMixing.asset");
        colorMix.id = "colormixing";
        colorMix.title = "Color Mixing";
        colorMix.cardColor = HexColor("#CE93D8");
        colorMix.targetSceneName = "ColorMixing";
        colorMix.hasSubItems = false;
        colorMix.thumbnail = LoadSprite($"{previewPath}/ColorMixing.png");
        colorMix.nameClip = LoadAudioClip("Assets/Sounds/Games Names/Color mixing.mp3");
        colorMix.subItems = new List<SubItemData>();
        for (int i = 0; i < animals.Length; i++)
        {
            string name = animals[i];
            string mainPath = $"Assets/Art/Animals/{name}/Art/Puzzle/{name} Main.png";
            var mainSprite = LoadSprite(mainPath);
            Sprite thumbSprite = LoadSprite($"Assets/Art/Animals/{name}/Art/{name}Sprite.png");
            if (mainSprite == null && thumbSprite == null) continue;
            colorMix.subItems.Add(new SubItemData
            {
                id = $"mix_{name.ToLower()}", title = name,
                cardColor = AnimalColors[i % AnimalColors.Length],
                categoryKey = name.ToLower(), targetSceneName = "ColorMixing",
                contentAsset = mainSprite != null ? mainSprite : thumbSprite,
                thumbnail = thumbSprite != null ? thumbSprite : mainSprite
            });
        }
        EditorUtility.SetDirty(colorMix);

        // ── Ball Maze ──
        var ballMaze = CreateSO<GameItemData>($"{DataPath}/BallMaze.asset");
        ballMaze.id = "ballmaze";
        ballMaze.title = "Ball Maze";
        ballMaze.cardColor = HexColor("#4FC3F7");
        ballMaze.targetSceneName = "BallMaze";
        ballMaze.hasSubItems = false;
        ballMaze.thumbnail = LoadSprite($"{previewPath}/Maze.png");
        ballMaze.nameClip = LoadAudioClip("Assets/Sounds/Games Names/Maze.mp3");
        EditorUtility.SetDirty(ballMaze);

        // ── Color Voice (Say the Color) ──
        var colorVoice = CreateSO<GameItemData>($"{DataPath}/ColorVoice.asset");
        colorVoice.id = "colorvoice";
        colorVoice.title = "Say the Color";
        colorVoice.cardColor = HexColor("#FF8A65");
        colorVoice.targetSceneName = "ColorVoice";
        colorVoice.hasSubItems = false;
        colorVoice.thumbnail = LoadSprite($"{previewPath}/ColorsRecognize.png");
        EditorUtility.SetDirty(colorVoice);

        // ── Tower Builder ──
        var tower = CreateSO<GameItemData>($"{DataPath}/TowerBuilder.asset");
        tower.id = "towerbuilder";
        tower.title = "Tower Builder";
        tower.cardColor = HexColor("#42A5F5");
        tower.targetSceneName = "TowerBuilder";
        tower.thumbnail = LoadSprite($"{previewPath}/Lego.png");
        tower.hasSubItems = true;
        tower.selectionScreenTitle = "\u05D1\u05E0\u05D4 \u05D0\u05EA \u05D4\u05DE\u05D2\u05D3\u05DC"; // בנה את המגדל
        tower.subItems = new List<SubItemData>
        {
            new SubItemData { id = "tower_easy",      title = "\u05E7\u05DC",          cardColor = HexColor("#66BB6A"), categoryKey = "0", targetSceneName = "TowerBuilder" },
            new SubItemData { id = "tower_medium",    title = "\u05D1\u05D9\u05E0\u05D5\u05E0\u05D9", cardColor = HexColor("#FFA726"), categoryKey = "1", targetSceneName = "TowerBuilder" },
            new SubItemData { id = "tower_hard",      title = "\u05E7\u05E9\u05D4",    cardColor = HexColor("#EF5350"), categoryKey = "2", targetSceneName = "TowerBuilder" },
            new SubItemData { id = "tower_veryhard",  title = "\u05DE\u05D0\u05EA\u05D2\u05E8", cardColor = HexColor("#AB47BC"), categoryKey = "3", targetSceneName = "TowerBuilder" },
        };
        EditorUtility.SetDirty(tower);

        // ── Tower Stack ──
        var towerStack = CreateSO<GameItemData>($"{DataPath}/TowerStack.asset");
        towerStack.id = "towerstack";
        towerStack.title = "Tower Stack";
        towerStack.cardColor = HexColor("#FF7043");
        towerStack.targetSceneName = "TowerStack";
        towerStack.hasSubItems = false;
        towerStack.thumbnail = LoadSprite($"{previewPath}/BlocksTower.png");
        EditorUtility.SetDirty(towerStack);

        // ── Shared Sticker (Spot It) ──
        var sharedSticker = CreateSO<GameItemData>($"{DataPath}/SharedSticker.asset");
        sharedSticker.id = "sharedsticker";
        sharedSticker.title = "Shared Sticker";
        sharedSticker.cardColor = HexColor("#8D6E63");
        sharedSticker.targetSceneName = "SharedSticker";
        sharedSticker.hasSubItems = false;
        sharedSticker.thumbnail = LoadSprite($"{previewPath}/FindTheSimilar.png");
        EditorUtility.SetDirty(sharedSticker);

        // ── Flappy Bird ──
        var flappyBird = CreateSO<GameItemData>($"{DataPath}/FlappyBird.asset");
        flappyBird.id = "flappybird";
        flappyBird.title = "Flappy Bird";
        flappyBird.cardColor = HexColor("#81D4FA");
        flappyBird.targetSceneName = "FlappyBird";
        flappyBird.thumbnail = LoadSprite($"{previewPath}/FlyingBird.png");
        flappyBird.hasSubItems = false;
        EditorUtility.SetDirty(flappyBird);

        // ── Simon Says ──
        var simonSays = CreateSO<GameItemData>($"{DataPath}/SimonSays.asset");
        simonSays.id = "simonsays";
        simonSays.title = "Simon Says";
        simonSays.cardColor = HexColor("#AB47BC");
        simonSays.targetSceneName = "SimonSays";
        simonSays.thumbnail = LoadSprite($"{previewPath}/SimonSays.png");
        simonSays.hasSubItems = false;
        EditorUtility.SetDirty(simonSays);

        // ── Game Database ──
        var db = CreateSO<GameDatabase>($"{DataPath}/GameDatabase.asset");
        db.games = new List<GameItemData> { memory, puzzle, coloring, fillDots, shadows, findObject, findCount, colorMix, ballMaze, colorVoice, tower, towerStack, sharedSticker, flappyBird, simonSays };
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
        string[] gameScenes = { "MemoryGame", "PuzzleGame", "ColoringGame", "ConnectTheDots", "ColorMixing", "ShadowMatch", "FindTheAnimal", "CountingGame" };
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
        var header = CreateHeader(safeArea.transform, "\u05D1\u05D7\u05E8\u05D5 \u05DE\u05E9\u05D7\u05E7", showBack: false, roundedRect, circleSprite); // בחרו משחק

        // Scroll view with grid
        var scrollContent = CreateScrollGrid(safeArea.transform, HeaderHeight);

        // Profile switch button (top-right corner, small avatar circle)
        var profileIcon = LoadSprite("Assets/UI/Sprites/Circle.png");
        var profileBtnGO = new GameObject("ProfileButton");
        profileBtnGO.transform.SetParent(header.transform, false);
        var profileBtnRT = profileBtnGO.AddComponent<RectTransform>();
        profileBtnRT.anchorMin = new Vector2(1, 0.5f);
        profileBtnRT.anchorMax = new Vector2(1, 0.5f);
        profileBtnRT.pivot = new Vector2(1, 0.5f);
        profileBtnRT.anchoredPosition = new Vector2(-24, 0);
        profileBtnRT.sizeDelta = new Vector2(70, 70);
        var profileBtnImg = profileBtnGO.AddComponent<Image>();
        profileBtnImg.sprite = profileIcon;
        profileBtnImg.color = HexColor("#90CAF9");
        profileBtnImg.raycastTarget = true;
        var profileBtnComp = profileBtnGO.AddComponent<Button>();
        profileBtnComp.targetGraphic = profileBtnImg;

        // Mask for circular photo clipping
        profileBtnGO.AddComponent<Mask>().showMaskGraphic = true;

        // Profile photo image (hidden by default, shown at runtime if profile has photo)
        var profilePhotoGO = new GameObject("Photo");
        profilePhotoGO.transform.SetParent(profileBtnGO.transform, false);
        var profilePhotoRT = profilePhotoGO.AddComponent<RectTransform>();
        StretchFull(profilePhotoRT);
        var profilePhotoImg = profilePhotoGO.AddComponent<Image>();
        profilePhotoImg.preserveAspect = false;
        profilePhotoImg.raycastTarget = false;
        profilePhotoGO.SetActive(false);

        // Profile initial text on the button
        var profileInitGO = new GameObject("Initial");
        profileInitGO.transform.SetParent(profileBtnGO.transform, false);
        var profileInitRT = profileInitGO.AddComponent<RectTransform>();
        StretchFull(profileInitRT);
        var profileInitTMP = profileInitGO.AddComponent<TextMeshProUGUI>();
        profileInitTMP.text = "\u263A"; // smiley fallback, updated at runtime
        profileInitTMP.fontSize = 32;
        profileInitTMP.fontStyle = FontStyles.Bold;
        profileInitTMP.color = Color.white;
        profileInitTMP.alignment = TextAlignmentOptions.Center;
        profileInitTMP.raycastTarget = false;

        // Main Menu Controller
        var controller = canvasGO.AddComponent<MainMenuController>();
        controller.database = database;
        controller.cardContainer = scrollContent;
        controller.cardPrefab = cardPrefab;
        controller.profileButtonImage = profileBtnImg;
        controller.profileButtonPhoto = profilePhotoImg;
        controller.profileButtonInitial = profileInitTMP;

        // Wire profile button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            profileBtnComp.onClick, controller.OnSwitchProfilePressed);

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
        var header = CreateHeader(safeArea.transform, "\u05D1\u05D7\u05E8\u05D5", showBack: true, roundedRect, circleSprite); // בחרו
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
        camGO.AddComponent<AudioListener>();

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

        // Background (themed at runtime)
        var headerImg = headerGO.AddComponent<Image>();
        headerImg.color = HeaderBgColor;
        headerImg.raycastTarget = false;
        headerGO.AddComponent<ThemeHeader>();

        // Title text
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(headerGO.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        StretchFull(titleRT);
        titleRT.offsetMin = new Vector2(showBack ? 120 : 40, 10);
        titleRT.offsetMax = new Vector2(-40, -10);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = HebrewFixer.Fix(title);
        titleTMP.fontSize = 52;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = HexColor("#4A4A4A");
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.isRightToLeftText = false;
        titleTMP.enableWordWrapping = false;
        titleTMP.overflowMode = TextOverflowModes.Ellipsis;
        titleTMP.raycastTarget = false;

        // Back button (if needed) — uses home icon matching all game scenes
        if (showBack)
        {
            var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
            var backGO = new GameObject("BackButton");
            backGO.transform.SetParent(headerGO.transform, false);
            var backRT = backGO.AddComponent<RectTransform>();
            backRT.anchorMin = new Vector2(0, 0.5f);
            backRT.anchorMax = new Vector2(0, 0.5f);
            backRT.pivot = new Vector2(0, 0.5f);
            backRT.anchoredPosition = new Vector2(24, 0);
            backRT.sizeDelta = new Vector2(90, 90);

            var backImg = backGO.AddComponent<Image>();
            backImg.sprite = homeIcon;
            backImg.preserveAspect = true;
            backImg.color = Color.white;
            backImg.raycastTarget = true;

            var backBtn = backGO.AddComponent<Button>();
            backBtn.targetGraphic = backImg;
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

        // Background (themed at runtime)
        var headerImg = headerGO.AddComponent<Image>();
        headerImg.color = HeaderBgColor;
        headerImg.raycastTarget = false;
        headerGO.AddComponent<ThemeHeader>();

        // Title text (centered)
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(headerGO.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        StretchFull(titleRT);
        titleRT.offsetMin = new Vector2(120, 10);
        titleRT.offsetMax = new Vector2(-120, -10);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = HebrewFixer.Fix(title);
        titleTMP.isRightToLeftText = false;
        titleTMP.fontSize = 48;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = HexColor("#4A4A4A");
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.enableWordWrapping = false;
        titleTMP.overflowMode = TextOverflowModes.Ellipsis;
        titleTMP.raycastTarget = false;

        // Home button (left) — sprite icon matching all other games
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        CreateIconHeaderButton(headerGO.transform, "HomeButton", homeIcon,
            new Vector2(24, 0), new Vector2(0, 0.5f));

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

    private static GameObject CreateIconHeaderButton(Transform parent, string name, Sprite icon,
        Vector2 pos, Vector2 anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(90, 90);

        var img = go.AddComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

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
        grid.constraintCount = 3;

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
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        // Set default resolution for editor testing (landscape)
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
    }

    private static void ConfigureBuildSettings()
    {
        string[] scenePaths = {
            $"{ScenesPath}/ProfileSelection.unity",
            $"{ScenesPath}/ProfileCreation.unity",
            $"{ScenesPath}/HomeScene.unity",
            $"{ScenesPath}/MainMenu.unity",
            $"{ScenesPath}/SelectionMenu.unity",
            $"{ScenesPath}/MemoryGame.unity",
            $"{ScenesPath}/PuzzleGame.unity",
            $"{ScenesPath}/ColoringGame.unity",
            $"{ScenesPath}/ConnectTheDots.unity",
            $"{ScenesPath}/ColorMixing.unity",
            $"{ScenesPath}/ShadowMatch.unity",
            $"{ScenesPath}/FindTheAnimal.unity",
            $"{ScenesPath}/CountingGame.unity",
            $"{ScenesPath}/ColorVoice.unity",
            $"{ScenesPath}/TowerBuilder.unity",
            $"{ScenesPath}/BallMaze.unity",
            $"{ScenesPath}/TowerStack.unity",
            $"{ScenesPath}/SharedSticker.unity",
            $"{ScenesPath}/FlappyBird.unity",
            $"{ScenesPath}/SimonSays.unity",
            $"{ScenesPath}/DiscoveryReveal.unity",
            $"{ScenesPath}/DrawingGallery.unity",
            $"{ScenesPath}/WorldScene.unity",
            $"{ScenesPath}/ParentDashboard.unity",
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

    private static AudioClip LoadAudioClip(string path)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
