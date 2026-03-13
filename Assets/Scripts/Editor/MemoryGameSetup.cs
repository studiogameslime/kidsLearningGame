using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Builds the MemoryGame scene, card prefab, and data assets.
/// Run via Tools > Kids Learning Game > Setup Memory Game.
/// </summary>
public class MemoryGameSetup : EditorWindow
{
    // ── Design constants ──
    private static readonly Vector2 ReferenceResolution = new Vector2(1080, 1920);

    // Colors matching the reference image
    private static readonly Color BgColor       = HexColor("#4A3728");   // dark brown
    private static readonly Color TopBarColor   = HexColor("#F0A882");   // salmon/peach
    private static readonly Color CardFrameColor = Color.white;

    // Grid
    private const int Columns = 4;
    private const int CardSize = 200;
    private const int CardSpacing = 24;
    private const int GridPadH = 48;
    private const int GridPadV = 24;

    // Bars
    private const int TopBarHeight = 120;
    private const int BottomBarHeight = 80;

    // Card prefab
    private const int CardFramePadding = 12; // white border thickness
    private const int CardCornerRadius = 24;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Memory Game Setup", "Loading sprites…", 0.1f);

            var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
            if (roundedRect == null)
            {
                Debug.LogError("RoundedRect.png not found. Run 'Setup Project' first.");
                return;
            }

            var cardBack = LoadSpriteFromPath("Assets/Art/BackMemoryCard.png");
            if (cardBack == null)
                Debug.LogWarning("BackMemoryCard.png not found at Assets/Art/. Using placeholder.");

            EditorUtility.DisplayProgressBar("Memory Game Setup", "Collecting animal sprites…", 0.2f);
            var animalSprites = CollectAnimalMemorySprites();

            EditorUtility.DisplayProgressBar("Memory Game Setup", "Creating card prefab…", 0.3f);
            var cardPrefab = CreateMemoryCardPrefab(roundedRect);

            EditorUtility.DisplayProgressBar("Memory Game Setup", "Creating category data…", 0.5f);
            var animalsCategory = CreateAnimalsCategory(animalSprites, cardBack);

            EditorUtility.DisplayProgressBar("Memory Game Setup", "Building MemoryGame scene…", 0.7f);
            CreateMemoryGameScene(cardPrefab, animalsCategory, roundedRect);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ─────────────────────────────────────────
    //  COLLECT SPRITES
    // ─────────────────────────────────────────

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
            else
                Debug.LogWarning($"Memory sprite not found: {spritePath}");
        }

        Debug.Log($"Collected {sprites.Count} animal memory sprites.");
        return sprites;
    }

    // ─────────────────────────────────────────
    //  CARD PREFAB
    // ─────────────────────────────────────────

    private static MemoryCard CreateMemoryCardPrefab(Sprite roundedRect)
    {
        EnsureFolder("Assets/Prefabs/UI");

        // Root — acts as the flip transform and has the Button
        var root = new GameObject("MemoryCard");
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(CardSize, CardSize);

        // White frame/border (slightly larger than inner card)
        var frame = new GameObject("Frame");
        frame.transform.SetParent(root.transform, false);
        var frameRT = frame.AddComponent<RectTransform>();
        StretchFull(frameRT);
        var frameImg = frame.AddComponent<Image>();
        frameImg.sprite = roundedRect;
        frameImg.type = Image.Type.Sliced;
        frameImg.color = CardFrameColor;
        frameImg.raycastTarget = true;

        // Shadow on the frame
        var shadow = frame.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
        shadow.effectDistance = new Vector2(3, -3);

        // Inner card image (inset by frame padding)
        var inner = new GameObject("CardImage");
        inner.transform.SetParent(root.transform, false);
        var innerRT = inner.AddComponent<RectTransform>();
        StretchFull(innerRT);
        innerRT.offsetMin = new Vector2(CardFramePadding, CardFramePadding);
        innerRT.offsetMax = new Vector2(-CardFramePadding, -CardFramePadding);
        var innerImg = inner.AddComponent<Image>();
        innerImg.sprite = roundedRect;
        innerImg.type = Image.Type.Sliced;
        innerImg.color = Color.white;
        innerImg.preserveAspect = false;
        innerImg.raycastTarget = false;

        // Button on root
        var btn = root.AddComponent<Button>();
        btn.targetGraphic = frameImg;
        var btnColors = btn.colors;
        btnColors.normalColor = Color.white;
        btnColors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        btnColors.highlightedColor = Color.white;
        btnColors.selectedColor = Color.white;
        btn.colors = btnColors;
        btn.transition = Selectable.Transition.ColorTint;

        // MemoryCard component
        var card = root.AddComponent<MemoryCard>();
        card.cardImage = innerImg;
        card.frameBorder = frameImg;
        card.flipRoot = rootRT;

        // Save prefab
        string path = "Assets/Prefabs/UI/MemoryCard.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        return prefab.GetComponent<MemoryCard>();
    }

    // ─────────────────────────────────────────
    //  CATEGORY DATA
    // ─────────────────────────────────────────

    private static MemoryCategoryData CreateAnimalsCategory(List<Sprite> sprites, Sprite cardBack)
    {
        EnsureFolder("Assets/Data/MemoryCategories");

        string path = "Assets/Data/MemoryCategories/AnimalsMemory.asset";
        var existing = AssetDatabase.LoadAssetAtPath<MemoryCategoryData>(path);
        MemoryCategoryData data;
        if (existing != null)
        {
            data = existing;
        }
        else
        {
            data = ScriptableObject.CreateInstance<MemoryCategoryData>();
            AssetDatabase.CreateAsset(data, path);
        }

        data.categoryKey = "animals";
        data.cardFaces = sprites;
        data.cardBack = cardBack;
        data.pairCount = 10; // 20 cards total
        EditorUtility.SetDirty(data);

        return data;
    }

    // ─────────────────────────────────────────
    //  SCENE
    // ─────────────────────────────────────────

    private static void CreateMemoryGameScene(MemoryCard cardPrefab, MemoryCategoryData animalsCategory, Sprite roundedRect)
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
        var inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModuleType != null) esGO.AddComponent(inputModuleType);
        else esGO.AddComponent<StandaloneInputModule>();

        // ── Canvas ──
        var canvasGO = new GameObject("GameCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Background (dark brown, full screen) ──
        var bgGO = CreateImage(canvasGO.transform, "Background", BgColor, null);
        StretchFull(bgGO.GetComponent<RectTransform>());
        bgGO.GetComponent<Image>().raycastTarget = false;

        // ── Top bar (salmon/peach) ──
        var topBar = CreateImage(canvasGO.transform, "TopBar", TopBarColor, null);
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
        titleTMP.text = "\u05DE\u05E9\u05D7\u05E7 \u05D6\u05D9\u05DB\u05E8\u05D5\u05DF"; // משחק זיכרון
        titleTMP.isRightToLeftText = true;
        titleTMP.fontSize = 48;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // ── Bottom bar (salmon/peach) ──
        var bottomBar = CreateImage(canvasGO.transform, "BottomBar", TopBarColor, null);
        var bottomBarRT = bottomBar.GetComponent<RectTransform>();
        bottomBarRT.anchorMin = new Vector2(0, 0);
        bottomBarRT.anchorMax = new Vector2(1, 0);
        bottomBarRT.pivot = new Vector2(0.5f, 0);
        bottomBarRT.sizeDelta = new Vector2(0, BottomBarHeight);
        bottomBar.GetComponent<Image>().raycastTarget = false;

        // ── Safe Area ──
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        StretchFull(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // ── Card Grid (ScrollRect for safety, but should fit without scroll) ──
        var gridArea = new GameObject("CardGrid");
        gridArea.transform.SetParent(safeArea.transform, false);
        var gridAreaRT = gridArea.AddComponent<RectTransform>();
        StretchFull(gridAreaRT);
        gridAreaRT.offsetMax = new Vector2(0, -TopBarHeight);
        gridAreaRT.offsetMin = new Vector2(0, BottomBarHeight);

        // Grid content with GridLayoutGroup
        var gridContent = new GameObject("GridContent");
        gridContent.transform.SetParent(gridArea.transform, false);
        var gridContentRT = gridContent.AddComponent<RectTransform>();
        StretchFull(gridContentRT);

        var grid = gridContent.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(CardSize, CardSize);
        grid.spacing = new Vector2(CardSpacing, CardSpacing);
        grid.padding = new RectOffset(GridPadH, GridPadH, GridPadV, GridPadV);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;

        // ── Home button (top-left corner, using home icon) ──
        var homeIcon = LoadSpriteFromPath("Assets/Art/Icons/home.png");

        var homeGO = new GameObject("HomeButton");
        homeGO.transform.SetParent(safeArea.transform, false);
        var homeRT = homeGO.AddComponent<RectTransform>();
        homeRT.anchorMin = new Vector2(0, 1);
        homeRT.anchorMax = new Vector2(0, 1);
        homeRT.pivot = new Vector2(0, 1);
        homeRT.anchoredPosition = new Vector2(16, -15);
        homeRT.sizeDelta = new Vector2(90, 90);

        var homeImg = homeGO.AddComponent<Image>();
        homeImg.sprite = homeIcon;
        homeImg.preserveAspect = true;
        homeImg.raycastTarget = true;
        homeImg.color = Color.white;

        var homeBtn = homeGO.AddComponent<Button>();
        homeBtn.targetGraphic = homeImg;
        var homeBtnColors = homeBtn.colors;
        homeBtnColors.normalColor = Color.white;
        homeBtnColors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        homeBtnColors.highlightedColor = Color.white;
        homeBtn.colors = homeBtnColors;

        // ── Controller ──
        var controller = canvasGO.AddComponent<MemoryGameController>();
        controller.categories = new List<MemoryCategoryData> { animalsCategory };
        controller.cardContainer = gridContentRT;
        controller.cardPrefab = cardPrefab;
        controller.matchCountText = null;
        controller.moveCountText = null;
        controller.columns = Columns;
        controller.cardRotationRange = 4f;
        controller.mismatchDelay = 0.8f;

        // Wire home button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeBtn.onClick, controller.OnExitPressed);

        // Save scene
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MemoryGame.unity");
    }

    // ─────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────

    private static GameObject CreateImage(Transform parent, string name, Color color, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        if (sprite != null) img.type = Image.Type.Sliced;
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

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    /// <summary>Load a Sprite from a path, handling both Single and Multiple sprite modes.</summary>
    private static Sprite LoadSpriteFromPath(string path)
    {
        // Try direct load first (works for Single sprite mode)
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;

        // For Multiple mode, load all sub-assets and grab the first Sprite
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var asset in allAssets)
        {
            if (asset is Sprite s)
                return s;
        }

        return null;
    }
}
