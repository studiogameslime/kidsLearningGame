using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Builds the MemoryGame scene in LANDSCAPE with a warm wooden play-table theme.
/// Procedural wood plank background, warm color palette matching cream/beige cards.
/// </summary>
public class MemoryGameSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    // Warm palette — matches cream/beige card style
    private static readonly Color TableBaseColor = HexColor("#5C3D2E");     // dark warm brown (surrounding)
    private static readonly Color BoardWoodA = HexColor("#8B6B4A");         // warm medium wood plank
    private static readonly Color BoardWoodB = HexColor("#7E6042");         // slightly darker plank
    private static readonly Color PlankSepColor = HexColor("#5A4030");      // dark plank separator line
    private static readonly Color BoardEdgeColor = HexColor("#6B4D38");     // board outer rim
    private static readonly Color BoardInnerRimColor = HexColor("#A08060"); // inner edge highlight
    private static readonly Color HeaderColor = new Color(0.30f, 0.20f, 0.12f, 0.75f); // warm dark brown

    // Card prefab
    private static readonly Color CardFrameColor = HexColor("#FFF8F0");     // warm cream white
    private static readonly Color CardShadowColor = new Color(0.25f, 0.15f, 0.08f, 0.4f);

    private const int TopBarHeight = 130;
    private const int CardFramePadding = 10;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Memory Game Setup", "Loading sprites\u2026", 0.1f);

            var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
            if (roundedRect == null)
            {
                Debug.LogError("RoundedRect.png not found. Run 'Setup Project' first.");
                return;
            }

            var cardBack = LoadSprite("Assets/Art/BackMemoryCard.png");
            if (cardBack == null)
                Debug.LogWarning("BackMemoryCard.png not found at Assets/Art/.");

            EditorUtility.DisplayProgressBar("Memory Game Setup", "Collecting animal sprites\u2026", 0.2f);
            var animalSprites = CollectAnimalMemorySprites();

            EditorUtility.DisplayProgressBar("Memory Game Setup", "Creating card prefab\u2026", 0.3f);
            var cardPrefab = CreateMemoryCardPrefab(roundedRect);

            EditorUtility.DisplayProgressBar("Memory Game Setup", "Creating category data\u2026", 0.5f);
            var animalsCategory = CreateAnimalsCategory(animalSprites, cardBack);

            EditorUtility.DisplayProgressBar("Memory Game Setup", "Building scene\u2026", 0.7f);
            CreateScene(cardPrefab, animalsCategory, roundedRect);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ── COLLECT SPRITES ──

    private static List<Sprite> CollectAnimalMemorySprites()
    {
        var sprites = new List<Sprite>();
        string basePath = "Assets/Art/Animals";
        string[] animalFolders = AssetDatabase.GetSubFolders(basePath);
        foreach (var folder in animalFolders)
        {
            string animalName = Path.GetFileName(folder);
            string spritePath = $"{folder}/Art/{animalName}MemorySprite.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite != null)
                sprites.Add(sprite);
        }
        Debug.Log($"Collected {sprites.Count} animal memory sprites.");
        return sprites;
    }

    // ── CARD PREFAB ──

    private static MemoryCard CreateMemoryCardPrefab(Sprite roundedRect)
    {
        EnsureFolder("Assets/Prefabs/UI");

        var root = new GameObject("MemoryCard");
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(150, 200);

        // Warm cream frame
        var frame = new GameObject("Frame");
        frame.transform.SetParent(root.transform, false);
        var frameRT = frame.AddComponent<RectTransform>();
        Full(frameRT);
        var frameImg = frame.AddComponent<Image>();
        frameImg.sprite = roundedRect;
        frameImg.type = Image.Type.Sliced;
        frameImg.color = CardFrameColor;
        frameImg.raycastTarget = true;

        // Warm brown shadow
        var shadow = frame.AddComponent<Shadow>();
        shadow.effectColor = CardShadowColor;
        shadow.effectDistance = new Vector2(2, -3);

        // Subtle warm border glow
        var borderGO = new GameObject("Border");
        borderGO.transform.SetParent(root.transform, false);
        var borderRT = borderGO.AddComponent<RectTransform>();
        Full(borderRT);
        borderRT.offsetMin = new Vector2(-2, -2);
        borderRT.offsetMax = new Vector2(2, 2);
        var borderImg = borderGO.AddComponent<Image>();
        borderImg.sprite = roundedRect;
        borderImg.type = Image.Type.Sliced;
        borderImg.color = new Color(0.95f, 0.88f, 0.75f, 0.4f); // warm cream glow
        borderImg.raycastTarget = false;
        borderGO.transform.SetAsFirstSibling();

        // Inner card image
        var inner = new GameObject("CardImage");
        inner.transform.SetParent(root.transform, false);
        var innerRT = inner.AddComponent<RectTransform>();
        Full(innerRT);
        innerRT.offsetMin = new Vector2(CardFramePadding, CardFramePadding);
        innerRT.offsetMax = new Vector2(-CardFramePadding, -CardFramePadding);
        var innerImg = inner.AddComponent<Image>();
        innerImg.sprite = roundedRect;
        innerImg.type = Image.Type.Sliced;
        innerImg.color = Color.white;
        innerImg.preserveAspect = false;
        innerImg.raycastTarget = false;

        // Button
        var btn = root.AddComponent<Button>();
        btn.targetGraphic = frameImg;
        var btnColors = btn.colors;
        btnColors.normalColor = Color.white;
        btnColors.pressedColor = new Color(0.92f, 0.88f, 0.82f, 1f);
        btnColors.highlightedColor = Color.white;
        btnColors.selectedColor = Color.white;
        btn.colors = btnColors;

        var card = root.AddComponent<MemoryCard>();
        card.cardImage = innerImg;
        card.frameBorder = frameImg;
        card.flipRoot = rootRT;

        string path = "Assets/Prefabs/UI/MemoryCard.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<MemoryCard>();
    }

    // ── CATEGORY DATA ──

    private static MemoryCategoryData CreateAnimalsCategory(List<Sprite> sprites, Sprite cardBack)
    {
        EnsureFolder("Assets/Data/MemoryCategories");
        string path = "Assets/Data/MemoryCategories/AnimalsMemory.asset";
        var existing = AssetDatabase.LoadAssetAtPath<MemoryCategoryData>(path);
        MemoryCategoryData data = existing != null ? existing : ScriptableObject.CreateInstance<MemoryCategoryData>();
        if (existing == null) AssetDatabase.CreateAsset(data, path);

        data.categoryKey = "animals";
        data.cardFaces = sprites;
        data.cardBack = cardBack;
        data.pairCount = 10;
        EditorUtility.SetDirty(data);
        return data;
    }

    // ── SCENE ──

    private static void CreateScene(MemoryCard cardPrefab, MemoryCategoryData animalsCategory, Sprite roundedRect)
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = TableBaseColor;
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
        var canvasGO = new GameObject("GameCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── WARM BACKGROUND ──

        // Base: dark warm brown (table surround)
        var bgGO = StretchImg(canvasGO.transform, "Background", TableBaseColor);
        bgGO.GetComponent<Image>().raycastTarget = false;

        // Warm vignette at edges (darker corners)
        CreateWarmVignette(canvasGO.transform, "VignetteTop", true);
        CreateWarmVignette(canvasGO.transform, "VignetteBottom", false);

        // ── SAFE AREA ──
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        Full(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // ── TOP BAR (warm brown, semi-transparent) ──
        var topBar = StretchImg(safeArea.transform, "TopBar", HeaderColor);
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
        Full(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = HebrewFixer.Fix("\u05DE\u05E9\u05D7\u05E7 \u05D6\u05D9\u05DB\u05E8\u05D5\u05DF");
        titleTMP.isRightToLeftText = false;
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(1f, 0.96f, 0.88f, 1f); // warm white
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = IconBtn(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(16, -20), new Vector2(90, 90));

        // ── GAME BOARD (wood plank surface) ──
        var boardGO = new GameObject("BoardPanel");
        boardGO.transform.SetParent(safeArea.transform, false);
        var boardRT = boardGO.AddComponent<RectTransform>();
        boardRT.anchorMin = new Vector2(0.02f, 0.02f);
        boardRT.anchorMax = new Vector2(0.98f, 0.88f);
        boardRT.offsetMin = Vector2.zero;
        boardRT.offsetMax = Vector2.zero;

        // Board outer rim (dark edge)
        var boardImg = boardGO.AddComponent<Image>();
        boardImg.sprite = roundedRect;
        boardImg.type = Image.Type.Sliced;
        boardImg.color = BoardEdgeColor;
        boardImg.raycastTarget = false;

        // Shadow
        var boardShadow = boardGO.AddComponent<Shadow>();
        boardShadow.effectColor = new Color(0.12f, 0.06f, 0.02f, 0.5f);
        boardShadow.effectDistance = new Vector2(5, -5);

        // Inner rim highlight
        var rimGO = new GameObject("InnerRim");
        rimGO.transform.SetParent(boardGO.transform, false);
        var rimRT = rimGO.AddComponent<RectTransform>();
        Full(rimRT);
        rimRT.offsetMin = new Vector2(3, 3);
        rimRT.offsetMax = new Vector2(-3, -3);
        var rimImg = rimGO.AddComponent<Image>();
        rimImg.sprite = roundedRect;
        rimImg.type = Image.Type.Sliced;
        rimImg.color = BoardInnerRimColor;
        rimImg.raycastTarget = false;

        // Wood plank surface (inside the rim)
        var woodSurface = new GameObject("WoodSurface");
        woodSurface.transform.SetParent(boardGO.transform, false);
        var woodSurfaceRT = woodSurface.AddComponent<RectTransform>();
        Full(woodSurfaceRT);
        woodSurfaceRT.offsetMin = new Vector2(6, 6);
        woodSurfaceRT.offsetMax = new Vector2(-6, -6);

        // Create horizontal wood planks
        CreateWoodPlanks(woodSurface.transform, roundedRect);

        // Card grid content (on top of wood)
        var gridContent = new GameObject("GridContent");
        gridContent.transform.SetParent(boardGO.transform, false);
        var gridContentRT = gridContent.AddComponent<RectTransform>();
        Full(gridContentRT);
        gridContentRT.offsetMin = new Vector2(10, 10);
        gridContentRT.offsetMax = new Vector2(-10, -10);

        // GridLayoutGroup (cell size set dynamically by controller)
        var grid = gridContent.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(150, 200);
        grid.spacing = new Vector2(16, 16);
        grid.padding = new RectOffset(20, 20, 16, 16);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        // ── CONTROLLER ──
        var controller = canvasGO.AddComponent<MemoryGameController>();
        controller.categories = new List<MemoryCategoryData> { animalsCategory };
        controller.boardArea = boardRT;
        controller.cardContainer = gridContentRT;
        controller.cardPrefab = cardPrefab;
        controller.cardRotationRange = 3f;
        controller.mismatchDelay = 0.8f;

        // Wire home button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnExitPressed);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MemoryGame.unity");
    }

    // ── WOOD PLANKS ──

    /// <summary>
    /// Creates horizontal wood plank strips across the board surface.
    /// Alternating warm brown shades with thin dark separator lines.
    /// </summary>
    private static void CreateWoodPlanks(Transform parent, Sprite roundedRect)
    {
        int plankCount = 6;
        float sepHeight = 2f;
        Color[] plankColors = {
            BoardWoodA,
            BoardWoodB,
            LerpColor(BoardWoodA, BoardWoodB, 0.3f),
            BoardWoodA,
            LerpColor(BoardWoodB, BoardWoodA, 0.4f),
            BoardWoodB
        };

        for (int i = 0; i < plankCount; i++)
        {
            float yMin = (float)i / plankCount;
            float yMax = (float)(i + 1) / plankCount;

            // Plank panel
            var plankGO = new GameObject($"Plank_{i}");
            plankGO.transform.SetParent(parent, false);
            var plankRT = plankGO.AddComponent<RectTransform>();
            plankRT.anchorMin = new Vector2(0, yMin);
            plankRT.anchorMax = new Vector2(1, yMax);
            plankRT.offsetMin = Vector2.zero;
            plankRT.offsetMax = Vector2.zero;
            var plankImg = plankGO.AddComponent<Image>();
            plankImg.color = plankColors[i % plankColors.Length];
            plankImg.raycastTarget = false;

            // Subtle horizontal grain line (very faint, mid-plank)
            var grainGO = new GameObject($"Grain_{i}");
            grainGO.transform.SetParent(plankGO.transform, false);
            var grainRT = grainGO.AddComponent<RectTransform>();
            grainRT.anchorMin = new Vector2(0.02f, 0.4f);
            grainRT.anchorMax = new Vector2(0.98f, 0.45f);
            grainRT.offsetMin = Vector2.zero;
            grainRT.offsetMax = Vector2.zero;
            var grainImg = grainGO.AddComponent<Image>();
            grainImg.color = new Color(0f, 0f, 0f, 0.06f); // very subtle
            grainImg.raycastTarget = false;

            // Second grain line (offset)
            var grain2GO = new GameObject($"Grain2_{i}");
            grain2GO.transform.SetParent(plankGO.transform, false);
            var grain2RT = grain2GO.AddComponent<RectTransform>();
            grain2RT.anchorMin = new Vector2(0.05f, 0.65f);
            grain2RT.anchorMax = new Vector2(0.95f, 0.69f);
            grain2RT.offsetMin = Vector2.zero;
            grain2RT.offsetMax = Vector2.zero;
            var grain2Img = grain2GO.AddComponent<Image>();
            grain2Img.color = new Color(1f, 1f, 1f, 0.04f); // very subtle highlight
            grain2Img.raycastTarget = false;

            // Dark separator line at top of each plank (except the first)
            if (i > 0)
            {
                var sepGO = new GameObject($"Sep_{i}");
                sepGO.transform.SetParent(parent, false);
                var sepRT = sepGO.AddComponent<RectTransform>();
                sepRT.anchorMin = new Vector2(0, yMin);
                sepRT.anchorMax = new Vector2(1, yMin);
                sepRT.pivot = new Vector2(0.5f, 0.5f);
                sepRT.sizeDelta = new Vector2(0, sepHeight);
                sepRT.anchoredPosition = Vector2.zero;
                var sepImg = sepGO.AddComponent<Image>();
                sepImg.color = PlankSepColor;
                sepImg.raycastTarget = false;
            }
        }
    }

    // ── VIGNETTE ──

    private static void CreateWarmVignette(Transform parent, string name, bool isTop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        if (isTop)
        {
            rt.anchorMin = new Vector2(0, 0.88f);
            rt.anchorMax = new Vector2(1, 1);
        }
        else
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0.12f);
        }
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.08f, 0.03f, 0.2f); // warm dark brown
        img.raycastTarget = false;
    }

    // ── HELPERS ──

    private static Color LerpColor(Color a, Color b, float t)
    {
        return Color.Lerp(a, b, t);
    }

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
