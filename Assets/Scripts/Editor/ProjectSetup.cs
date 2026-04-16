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
    private const int HeaderHeight = SetupConstants.HeaderHeight;

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
            CreateSilhouetteMaterial();

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

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Laundry Sorting…", 0.966f);
            LaundrySortingSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Ball Maze…", 0.969f);
            BallMazeSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Bakery Game…", 0.972f);
            BakeryGameSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Sock Match…", 0.9725f);
            SockMatchSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Shared Sticker…", 0.973f);
            SharedStickerSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Flappy Bird…", 0.975f);
            FlappyBirdSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Simon Says…", 0.977f);
            SimonSaysSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Pattern Copy…", 0.979f);
            PatternCopySetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Letter Game…", 0.981f);
            LetterGameSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Number Maze…", 0.983f);
            NumberMazeSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Odd One Out…", 0.985f);
            OddOneOutSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Quantity Match…", 0.987f);
            QuantityMatchSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Number Train…", 0.989f);
            NumberTrainSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Letter Train…", 0.990f);
            LetterTrainSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Fishing Game…", 0.9902f);
            FishingGameSetup.RunSetupSilent();



            EditorUtility.DisplayProgressBar("Setting up project…", "Building Image Gallery…", 0.9905f);
            ImageGallerySetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Connect Match…", 0.991f);
            ConnectMatchSetup.RunSetupSilent();

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

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Aquarium Scene…", 0.998f);
            AquariumSceneSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Sand Drawing…", 0.9981f);
            SandDrawingSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Bubble Lab…", 0.9982f);
            BubbleLabSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Size Sort…", 0.998f);
            SizeSortSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Color Sort…", 0.998f);
            ColorSortSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Color Catch…", 0.9985f);
            ColorCatchSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Tower Builder (Lego)…", 0.9975f);
            TowerBuilderSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Fruit Puzzle…", 0.998f);
            FruitPuzzleSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Letter Bubbles…", 0.998f);
            LetterBubblesSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Spin Puzzle…", 0.9988f);
            SpinPuzzleSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Xylophone…", 0.999f);
            XylophoneSetup.RunSetupSilent();

            EditorUtility.DisplayProgressBar("Setting up project…", "Building Half Puzzle…", 0.9992f);
            HalfPuzzleSetup.RunSetupSilent();

            // Color Studio — hidden for now
            // EditorUtility.DisplayProgressBar("Setting up project…", "Building Color Studio…", 0.9995f);
            // ColorStudioSetup.RunSetupSilent();

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

    private static void CreateSilhouetteMaterial()
    {
        EnsureFolder("Assets/Resources");
        string path = "Assets/Resources/SilhouetteMaterial.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return; // already exists
        var shader = Shader.Find("UI/Silhouette");
        if (shader == null) { Debug.LogWarning("UI/Silhouette shader not found"); return; }
        var mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] Created SilhouetteMaterial in Resources");
    }

    // Auto-create silhouette material on editor load if missing
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void EnsureSilhouetteMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/SilhouetteMaterial.mat") == null)
            CreateSilhouetteMaterial();
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
        // Leave space: top 42px for game name, bottom 36px for difficulty
        var thumbMaskGO = new GameObject("ThumbnailMask");
        thumbMaskGO.transform.SetParent(root.transform, false);
        var thumbMaskRT = thumbMaskGO.AddComponent<RectTransform>();
        thumbMaskRT.anchorMin = Vector2.zero;
        thumbMaskRT.anchorMax = Vector2.one;
        thumbMaskRT.offsetMin = new Vector2(14, 40);
        thumbMaskRT.offsetMax = new Vector2(-14, -44);
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
        // Image gallery option — first item (opens unified in-game gallery)
        puzzle.subItems.Add(new SubItemData
        {
            id = "puzzle_gallery",
            title = "\u05DE\u05D4\u05EA\u05DE\u05D5\u05E0\u05D5\u05EA \u05E9\u05DC\u05D9", // מהתמונות שלי
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
            if (puzzleSprite != null)
                Debug.Log($"[ProjectSetup] Loaded puzzle sprite for {animalName}: {puzzleSprite.name}");
            else
            {
                Debug.LogWarning($"[ProjectSetup] No puzzle sprite at {puzzlePath}, using idle sprite");
                puzzleSprite = LoadSprite($"Assets/Art/Animals/{animalName}/Art/{animalName}Sprite.png");
            }

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
            string cName = animals[i];
            var coloringThumb = LoadSprite($"Assets/Art/Animals/{cName}/Art/{cName}Sprite.png");
            coloring.subItems.Add(new SubItemData
            {
                id = $"coloring_{cName.ToLower()}",
                title = cName,
                cardColor = AnimalColors[i % AnimalColors.Length],
                categoryKey = cName.ToLower(),
                targetSceneName = "ColoringGame",
                thumbnail = coloringThumb
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
        findObject.hasSubItems = true;
        findObject.thumbnail = LoadSprite($"{previewPath}/FindTheObject.png");
        findObject.nameClip = LoadAudioClip("Assets/Sounds/Games Names/Find the animal.mp3");
        EditorUtility.SetDirty(findObject);

        // ── Shadow Match ──
        var shadows = CreateSO<GameItemData>($"{DataPath}/Shadows.asset");
        shadows.id = "shadows";
        shadows.title = "Shadow Match";
        shadows.cardColor = ShadowsColor;
        shadows.targetSceneName = "ShadowMatch";
        shadows.hasSubItems = true;
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

        // ── Shared Sticker (Spot It) ──
        var sharedSticker = CreateSO<GameItemData>($"{DataPath}/SharedSticker.asset");
        sharedSticker.id = "sharedsticker";
        sharedSticker.title = "Shared Sticker";
        sharedSticker.cardColor = HexColor("#8D6E63");
        sharedSticker.targetSceneName = "SharedSticker";
        sharedSticker.hasSubItems = false;
        sharedSticker.thumbnail = LoadSprite($"{previewPath}/FindTheSimilar.png");
        sharedSticker.nameClip = LoadAudioClip("Assets/Sounds/Games Names/מה משותף.mp3");
        EditorUtility.SetDirty(sharedSticker);

        // ── Flappy Bird ──
        var flappyBird = CreateSO<GameItemData>($"{DataPath}/FlappyBird.asset");
        flappyBird.id = "flappybird";
        flappyBird.title = "Flappy Bird";
        flappyBird.cardColor = HexColor("#81D4FA");
        flappyBird.targetSceneName = "FlappyBird";
        flappyBird.thumbnail = LoadSprite($"{previewPath}/FlyingBird.png");
        flappyBird.hasSubItems = false;
        flappyBird.nameClip = LoadAudioClip("Assets/Sounds/Games Names/הציפור המעופפת.mp3");
        EditorUtility.SetDirty(flappyBird);

        // ── Simon Says ──
        var simonSays = CreateSO<GameItemData>($"{DataPath}/SimonSays.asset");
        simonSays.id = "simonsays";
        simonSays.title = "Simon Says";
        simonSays.cardColor = HexColor("#AB47BC");
        simonSays.targetSceneName = "SimonSays";
        simonSays.thumbnail = LoadSprite($"{previewPath}/SimonSays.png");
        simonSays.hasSubItems = false;
        simonSays.nameClip = LoadAudioClip("Assets/Sounds/Games Names/סיימון.mp3");
        EditorUtility.SetDirty(simonSays);

        // ── Pattern Copy ──
        var patternCopy = CreateSO<GameItemData>($"{DataPath}/PatternCopy.asset");
        patternCopy.id = "patterncopy";
        patternCopy.title = "Pattern Copy";
        patternCopy.cardColor = HexColor("#7E57C2");
        patternCopy.targetSceneName = "PatternCopy";
        patternCopy.thumbnail = LoadSprite($"{previewPath}/PatternCopy.png");
        patternCopy.hasSubItems = false;
        patternCopy.nameClip = LoadAudioClip("Assets/Sounds/Games Names/להעתיק את הצורה.mp3");
        EditorUtility.SetDirty(patternCopy);

        // ── Letters (First Letter) ──
        var letters = CreateSO<GameItemData>($"{DataPath}/Letters.asset");
        letters.id = "letters";
        letters.title = "First Letter";
        letters.cardColor = HexColor("#66BB6A");
        letters.targetSceneName = "LettersGame";
        letters.thumbnail = LoadSprite($"{previewPath}/MissingLetter.png");
        letters.hasSubItems = false;
        letters.nameClip = LoadAudioClip("Assets/Sounds/Games Names/האות החסרה.mp3");
        EditorUtility.SetDirty(letters);

        // ── Number Maze ──
        var numberMaze = CreateSO<GameItemData>($"{DataPath}/NumberMaze.asset");
        numberMaze.id = "numbermaze";
        numberMaze.title = "Number Maze";
        numberMaze.cardColor = HexColor("#42A5F5");
        numberMaze.targetSceneName = "NumberMaze";
        numberMaze.thumbnail = LoadSprite($"{previewPath}/NumbersMaze.png");
        numberMaze.hasSubItems = false;
        numberMaze.nameClip = LoadAudioClip("Assets/Sounds/Games Names/מבוך המספרים.mp3");
        EditorUtility.SetDirty(numberMaze);

        // ── Odd One Out ──
        var oddOneOut = CreateSO<GameItemData>($"{DataPath}/OddOneOut.asset");
        oddOneOut.id = "oddoneout";
        oddOneOut.title = "Odd One Out";
        oddOneOut.cardColor = HexColor("#FF7043");
        oddOneOut.targetSceneName = "OddOneOut";
        oddOneOut.thumbnail = LoadSprite($"{previewPath}/FindTheDifferent.png");
        oddOneOut.hasSubItems = false;
        oddOneOut.nameClip = LoadAudioClip("Assets/Sounds/Games Names/מה יוצא דופן.mp3");
        EditorUtility.SetDirty(oddOneOut);

        // ── Quantity Match ──
        var quantityMatch = CreateSO<GameItemData>($"{DataPath}/QuantityMatch.asset");
        quantityMatch.id = "quantitymatch";
        quantityMatch.title = "Quantity Match";
        quantityMatch.cardColor = HexColor("#AB47BC");
        quantityMatch.targetSceneName = "QuantityMatch";
        quantityMatch.thumbnail = LoadSprite($"{previewPath}/MatchCount.png");
        quantityMatch.hasSubItems = false;
        quantityMatch.nameClip = LoadAudioClip("Assets/Sounds/Games Names/כמות ומספר.mp3");
        EditorUtility.SetDirty(quantityMatch);

        // ── Number Train ──
        var numberTrain = CreateSO<GameItemData>($"{DataPath}/NumberTrain.asset");
        numberTrain.id = "numbertrain";
        numberTrain.title = "Number Train";
        numberTrain.cardColor = HexColor("#43A047");
        numberTrain.targetSceneName = "NumberTrain";
        numberTrain.thumbnail = LoadSprite($"{previewPath}/NumbersTrain.png");
        numberTrain.hasSubItems = false;
        numberTrain.nameClip = LoadAudioClip("Assets/Sounds/Games Names/רכבת המספרים.mp3");
        EditorUtility.SetDirty(numberTrain);

        // ── Letter Train ──
        var letterTrain = CreateSO<GameItemData>($"{DataPath}/LetterTrain.asset");
        letterTrain.id = "lettertrain";
        letterTrain.title = "Letter Train";
        letterTrain.cardColor = HexColor("#7E57C2");
        letterTrain.targetSceneName = "LetterTrain";
        letterTrain.thumbnail = LoadSprite($"{previewPath}/LettersTrain.png");
        letterTrain.hasSubItems = false;
        letterTrain.nameClip = LoadAudioClip("Assets/Sounds/Games Names/רכבת האותיות.mp3");
        EditorUtility.SetDirty(letterTrain);

        // ── Fishing Game ──
        var fishingGame = CreateSO<GameItemData>($"{DataPath}/FishingGame.asset");
        fishingGame.id = "fishing";
        fishingGame.title = "\u05D3\u05D9\u05D2"; // דיג
        fishingGame.cardColor = HexColor("#29B6F6");
        fishingGame.targetSceneName = "FishingGame";
        fishingGame.hasSubItems = false;
        fishingGame.thumbnail = LoadSprite($"{previewPath}/Fishing.png");
        fishingGame.nameClip = LoadAudioClip("Assets/Sounds/Games Names/דייג.mp3");
        EditorUtility.SetDirty(fishingGame);

        // ── Connect Match ──
        var connectMatch = CreateSO<GameItemData>($"{DataPath}/ConnectMatch.asset");
        connectMatch.id = "connectmatch";
        connectMatch.title = "Connect Match";
        connectMatch.cardColor = HexColor("#26A69A");
        connectMatch.targetSceneName = "ConnectMatch";
        connectMatch.thumbnail = LoadSprite($"{previewPath}/ConnectMatch.png");
        connectMatch.hasSubItems = false;
        connectMatch.nameClip = LoadAudioClip("Assets/Sounds/Games Names/חבר וצייר.mp3");
        EditorUtility.SetDirty(connectMatch);

        // ── Laundry Sorting ──
        var laundrySorting = CreateSO<GameItemData>($"{DataPath}/LaundrySorting.asset");
        laundrySorting.id = "laundrysorting";
        laundrySorting.title = "Laundry Sorting";
        laundrySorting.cardColor = HexColor("#42A5F5");
        laundrySorting.targetSceneName = "LaundrySorting";
        laundrySorting.thumbnail = LoadSprite($"{previewPath}/LaundrySorting.png");
        laundrySorting.hasSubItems = false;
        laundrySorting.nameClip = LoadAudioClip("Assets/Sounds/Games Names/מיון כביסה ופירות.mp3");
        EditorUtility.SetDirty(laundrySorting);

        // ── Bakery Game ──
        var bakery = CreateSO<GameItemData>($"{DataPath}/Bakery.asset");
        bakery.id = "bakery";
        bakery.title = "Bakery";
        bakery.cardColor = HexColor("#FFAB91");
        bakery.targetSceneName = "BakeryGame";
        bakery.hasSubItems = false;
        bakery.thumbnail = LoadSprite($"{previewPath}/Bakery.png");
        bakery.nameClip = LoadAudioClip("Assets/Sounds/Games Names/מאפייה.mp3");
        EditorUtility.SetDirty(bakery);

        // ── Sock Match ──
        var sockMatch = CreateSO<GameItemData>($"{DataPath}/SockMatch.asset");
        sockMatch.id = "sockmatch";
        sockMatch.title = "Sock Match";
        sockMatch.cardColor = HexColor("#80DEEA");
        sockMatch.targetSceneName = "SockMatch";
        sockMatch.hasSubItems = false;
        sockMatch.nameClip = LoadAudioClip("Assets/Sounds/Games Names/מיון גרביים.mp3");
        EditorUtility.SetDirty(sockMatch);


        // ── Game Database ──
        // ── Size Sort ──
        var sizeSort = CreateSO<GameItemData>($"{DataPath}/SizeSort.asset");
        sizeSort.id = "sizesort";
        sizeSort.title = "Size Sort";
        sizeSort.cardColor = HexColor("#8BC34A");
        sizeSort.targetSceneName = "SizeSort";
        sizeSort.hasSubItems = false;
        sizeSort.thumbnail = LoadSprite($"{previewPath}/SizeSort.png");
        sizeSort.nameClip = LoadAudioClip("Assets/Sounds/Games Names/מיון לפי גודל.mp3");
        EditorUtility.SetDirty(sizeSort);

        // ── Color Sort ──
        var colorSort = CreateSO<GameItemData>($"{DataPath}/ColorSort.asset");
        colorSort.id = "colorsort";
        colorSort.title = "Color Sort";
        colorSort.cardColor = HexColor("#FF7043");
        colorSort.targetSceneName = "ColorSort";
        colorSort.hasSubItems = false;
        colorSort.thumbnail = LoadSprite($"{previewPath}/Color Sorting.png");
        colorSort.nameClip = LoadAudioClip("Assets/Sounds/Games Names/מיון צבעים.mp3");
        EditorUtility.SetDirty(colorSort);

        // ── Color Catch ──
        var colorCatch = CreateSO<GameItemData>($"{DataPath}/ColorCatch.asset");
        colorCatch.id = "colorcatch";
        colorCatch.title = "Color Catch";
        colorCatch.cardColor = HexColor("#26C6DA");
        colorCatch.targetSceneName = "ColorCatch";
        colorCatch.hasSubItems = false;
        colorCatch.thumbnail = LoadSprite($"{previewPath}/Color Catch.png");
        colorCatch.nameClip = LoadAudioClip("Assets/Sounds/Games Names/תפוס צבעים.mp3");
        EditorUtility.SetDirty(colorCatch);

        // ── Fruit Puzzle ──
        var fruitPuzzle = CreateSO<GameItemData>($"{DataPath}/FruitPuzzle.asset");
        fruitPuzzle.id = "vehiclepuzzle";
        fruitPuzzle.title = "Vehicle Puzzle";
        fruitPuzzle.cardColor = HexColor("#FF8A65");
        fruitPuzzle.targetSceneName = "FruitPuzzle";
        fruitPuzzle.hasSubItems = false;
        fruitPuzzle.thumbnail = LoadSprite($"{previewPath}/Cars Puzzle.png");
        fruitPuzzle.nameClip = LoadAudioClip("Assets/Sounds/Games Names/פאזל רכבים.mp3");
        EditorUtility.SetDirty(fruitPuzzle);

        // ── Letter Bubbles ──
        var letterBubbles = CreateSO<GameItemData>($"{DataPath}/LetterBubbles.asset");
        letterBubbles.id = "letterbubbles";
        letterBubbles.title = "Letter Bubbles";
        letterBubbles.cardColor = HexColor("#7E57C2");
        letterBubbles.targetSceneName = "LetterBubbles";
        letterBubbles.hasSubItems = false;
        letterBubbles.thumbnail = LoadSprite($"{previewPath}/LettersPops.png");
        letterBubbles.nameClip = LoadAudioClip("Assets/Sounds/Games Names/בועות של אותיות.mp3");
        EditorUtility.SetDirty(letterBubbles);

        // ── Tower Builder (Lego) ──
        var tower = CreateSO<GameItemData>($"{DataPath}/TowerBuilder.asset");
        tower.id = "towerbuilder";
        tower.title = "Lego";
        tower.cardColor = HexColor("#FF8A65");
        tower.targetSceneName = "TowerBuilder";
        tower.thumbnail = LoadSprite($"{previewPath}/Lego.png");
        tower.hasSubItems = false;
        tower.nameClip = LoadAudioClip("Assets/Sounds/Games Names/לגו.mp3");
        EditorUtility.SetDirty(tower);

        // ── Spin Puzzle ──
        var spinPuzzle = CreateSO<GameItemData>($"{DataPath}/SpinPuzzle.asset");
        spinPuzzle.id = "spinpuzzle";
        spinPuzzle.title = "\u05E1\u05D5\u05D1\u05D1 \u05D5\u05D4\u05EA\u05D0\u05DD";
        spinPuzzle.cardColor = HexColor("#AB47BC");
        spinPuzzle.targetSceneName = "SpinPuzzle";
        spinPuzzle.thumbnail = LoadSprite($"{previewPath}/FlipPuzzle.png");
        spinPuzzle.hasSubItems = false;
        EditorUtility.SetDirty(spinPuzzle);

        // ── Half Puzzle ──
        var halfPuzzle = CreateSO<GameItemData>($"{DataPath}/HalfPuzzle.asset");
        halfPuzzle.id = "halfpuzzle";
        halfPuzzle.title = "\u05D7\u05D1\u05E8\u05D5 \u05D7\u05E6\u05D0\u05D9\u05DD"; // חברו חצאים
        halfPuzzle.cardColor = HexColor("#66BB6A");
        halfPuzzle.targetSceneName = "HalfPuzzle";
        halfPuzzle.thumbnail = LoadSprite($"{previewPath}/Puzzle.png"); // reuse puzzle preview for now
        halfPuzzle.hasSubItems = false;
        EditorUtility.SetDirty(halfPuzzle);

        var db = CreateSO<GameDatabase>($"{DataPath}/GameDatabase.asset");
        db.games = new List<GameItemData> { memory, puzzle, coloring, fillDots, shadows, findObject, findCount, colorMix, ballMaze, sharedSticker, flappyBird, simonSays, patternCopy, letters, numberMaze, oddOneOut, quantityMatch, numberTrain, letterTrain, fishingGame, connectMatch, laundrySorting, bakery, sockMatch, sizeSort, colorSort, colorCatch, fruitPuzzle, letterBubbles, tower, spinPuzzle, halfPuzzle };
        EditorUtility.SetDirty(db);

        // Copy GameDatabase to Resources so Resources.Load works at runtime
        EnsureFolder("Assets/Resources");
        AssetDatabase.CopyAsset($"{DataPath}/GameDatabase.asset", "Assets/Resources/GameDatabase.asset");

        // Copy 2-player badge to Resources for runtime loading
        if (!AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/2 Player.png"))
            AssetDatabase.CopyAsset("Assets/Art/2 Player.png", "Assets/Resources/2 Player.png");

        // Validate age baseline configuration
        ValidateAgeBaseline(db);

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
        var header = CreateHeader(safeArea.transform, "\u05D1\u05D7\u05E8\u05D5 \u05DE\u05E9\u05D7\u05E7", showBack: true, roundedRect, circleSprite); // בחרו משחק

        // Games count hint (between header and grid)
        var hintGO = new GameObject("GamesCountHint");
        hintGO.transform.SetParent(safeArea.transform, false);
        var hintRT = hintGO.AddComponent<RectTransform>();
        hintRT.anchorMin = new Vector2(0, 1);
        hintRT.anchorMax = new Vector2(1, 1);
        hintRT.pivot = new Vector2(0.5f, 1);
        hintRT.anchoredPosition = new Vector2(0, -HeaderHeight);
        hintRT.sizeDelta = new Vector2(0, 44);
        var hintTMP = hintGO.AddComponent<TextMeshProUGUI>();
        hintTMP.fontSize = 28;
        hintTMP.color = new Color(0.35f, 0.35f, 0.35f, 0.85f);
        hintTMP.alignment = TextAlignmentOptions.Center;
        hintTMP.raycastTarget = false;
        hintGO.SetActive(false); // shown at runtime if needed

        // Scroll view with grid (shifted down to make room for hint)
        var scrollContent = CreateScrollGrid(safeArea.transform, HeaderHeight + 44);

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
        controller.gamesCountHint = hintTMP;
        controller.profileButtonImage = profileBtnImg;
        controller.profileButtonPhoto = profilePhotoImg;
        controller.profileButtonInitial = profileInitTMP;

        // Wire back button
        var backButton = header.transform.Find("BackButton")?.GetComponent<Button>();
        if (backButton != null)
        {
            controller.backToWorldButton = backButton;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                backButton.onClick, controller.OnBackToWorldPressed);
        }

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
        scaler.matchWidthOrHeight = 0.5f;
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
        HebrewText.SetText(titleTMP, title);
        titleTMP.fontSize = 52;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = HexColor("#4A4A4A");
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.enableWordWrapping = false;
        titleTMP.overflowMode = TextOverflowModes.Ellipsis;
        titleTMP.raycastTarget = false;

        // Back button (if needed) — uses home icon matching all game scenes
        if (showBack)
        {
            var homeIcon = UISheetHelper.HomeIcon;
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
        HebrewText.SetText(titleTMP, title);
        titleTMP.fontSize = 48;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = HexColor("#4A4A4A");
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.enableWordWrapping = false;
        titleTMP.overflowMode = TextOverflowModes.Ellipsis;
        titleTMP.raycastTarget = false;

        // Home button (left) — sprite icon matching all other games
        var homeIcon = UISheetHelper.HomeIcon;
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
            $"{ScenesPath}/BallMaze.unity",
            $"{ScenesPath}/SharedSticker.unity",
            $"{ScenesPath}/FlappyBird.unity",
            $"{ScenesPath}/SimonSays.unity",
            $"{ScenesPath}/PatternCopy.unity",
            $"{ScenesPath}/LettersGame.unity",
            $"{ScenesPath}/NumberMaze.unity",
            $"{ScenesPath}/OddOneOut.unity",
            $"{ScenesPath}/QuantityMatch.unity",
            $"{ScenesPath}/NumberTrain.unity",
            $"{ScenesPath}/LetterTrain.unity",
            $"{ScenesPath}/FishingGame.unity",
            $"{ScenesPath}/ImageGallery.unity",
            $"{ScenesPath}/ConnectMatch.unity",
            $"{ScenesPath}/LaundrySorting.unity",
            $"{ScenesPath}/BakeryGame.unity",
            $"{ScenesPath}/SockMatch.unity",
            $"{ScenesPath}/DiscoveryReveal.unity",
            $"{ScenesPath}/DrawingGallery.unity",
            $"{ScenesPath}/WorldScene.unity",
            $"{ScenesPath}/ParentDashboard.unity",
            $"{ScenesPath}/AquariumScene.unity",
            $"{ScenesPath}/SizeSort.unity",
            $"{ScenesPath}/ColorSort.unity",
            $"{ScenesPath}/ColorCatch.unity",
            $"{ScenesPath}/TowerBuilder.unity",
            $"{ScenesPath}/SpinPuzzle.unity",
            $"{ScenesPath}/FruitPuzzle.unity",
            $"{ScenesPath}/LetterBubbles.unity",
            $"{ScenesPath}/SandDrawingScene.unity",
            $"{ScenesPath}/BubbleLabScene.unity",
            $"{ScenesPath}/XylophoneScene.unity",
            $"{ScenesPath}/HalfPuzzle.unity",
            // $"{ScenesPath}/ColorStudioScene.unity", // hidden for now
        };

        var buildScenes = scenePaths.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
        EditorBuildSettings.scenes = buildScenes;
    }

    // ─────────────────────────────────────────────
    //  UTILITY
    // ─────────────────────────────────────────────

    private static void ValidateAgeBaseline(GameDatabase db)
    {
        // Validate game IDs in database are unique
        var ids = new HashSet<string>();
        foreach (var game in db.games)
        {
            if (string.IsNullOrEmpty(game.id))
                Debug.LogError($"[ProjectSetup] Game has empty ID: {game.name}");
            else if (!ids.Add(game.id))
                Debug.LogError($"[ProjectSetup] Duplicate game ID: {game.id}");
        }

        // Cross-validate: check that all game IDs in AgeBaselineConfig exist in the database
        var allBuckets = AgeBaselineConfig.GetAllResolvedBuckets();
        foreach (var kvp in allBuckets)
        {
            foreach (var entry in kvp.Value)
            {
                if (!ids.Contains(entry.gameId))
                    Debug.LogError($"[ProjectSetup] AgeBaseline age {kvp.Key}: game ID '{entry.gameId}' not found in GameDatabase");
            }
        }

        // Run the full baseline validation
        AgeBaselineConfig.Validate();
        AgeBaselineConfig.LogBaseline();

        Debug.Log($"[ProjectSetup] Validated {db.games.Count} games, {ids.Count} unique IDs");
    }

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
