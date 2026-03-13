using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the WorldScene with layered parallax backgrounds, sun/moon,
/// interactive trees/bushes, cloud system, and the existing animal/balloon logic.
/// </summary>
public class WorldSceneSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1080, 1920);
    private static readonly Color TopBarColor = HexColor("#5BA84C");
    private const int TopBarHeight = 110;

    // Day mode default colors (environment handles transitions)
    private static readonly Color DaySky = HexColor("#8FD4F5");
    private static readonly Color DayMountains = HexColor("#DCEEF8");
    private static readonly Color DayHillsLarge = HexColor("#B7D7D6");
    private static readonly Color DayHills = HexColor("#9FCBC5");
    private static readonly Color DayGroundBack = HexColor("#8ED36B");
    private static readonly Color DayGroundFront = HexColor("#79C956");
    private static readonly Color DayCloudTint = HexColor("#F4FBFF");

    private static readonly string[] AllAnimals = {
        "Bear", "Bird", "Cat", "Chicken", "Cow", "Dog", "Donkey", "Duck",
        "Elephant", "Fish", "Frog", "Giraffe", "Horse", "Lion", "Monkey",
        "Sheep", "Snake", "Turtle", "Zebra"
    };

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("World Scene Setup", "Building animal animation data…", 0.3f);
            BuildAnimalData();

            EditorUtility.DisplayProgressBar("World Scene Setup", "Creating base animator…", 0.4f);
            CreateBaseAnimatorController();

            EditorUtility.DisplayProgressBar("World Scene Setup", "Importing world art to Resources…", 0.5f);
            CopyWorldArtToResources();

            EditorUtility.DisplayProgressBar("World Scene Setup", "Building scene…", 0.6f);
            CreateScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ─────────────────────────────────────────
    //  COPY WORLD ART TO RESOURCES
    // ─────────────────────────────────────────

    private static void CopyWorldArtToResources()
    {
        EnsureFolder("Assets/Resources/WorldArt");
        string srcDir = "Assets/Art/World";
        string dstDir = "Assets/Resources/WorldArt";

        // Copy cloud sprites for runtime cloud system
        for (int i = 1; i <= 8; i++)
        {
            CopyAsset($"{srcDir}/cloud{i}.png", $"{dstDir}/cloud{i}.png");
        }
    }

    private static void CopyAsset(string src, string dst)
    {
        if (!System.IO.File.Exists(src)) return;
        if (System.IO.File.Exists(dst)) System.IO.File.Delete(dst);
        System.IO.File.Copy(src, dst, true);
        AssetDatabase.ImportAsset(dst);

        var importer = AssetImporter.GetAtPath(dst) as TextureImporter;
        if (importer != null)
        {
            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }
            if (dirty) importer.SaveAndReimport();
        }
    }

    // ─────────────────────────────────────────
    //  ANIMAL ANIMATION DATA (unchanged)
    // ─────────────────────────────────────────

    public static void BuildAnimalData()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/AnimalAnim");

        int count = 0;
        foreach (var animalId in AllAnimals)
        {
            string artDir = $"Assets/Art/Animals/{animalId}/Art";
            string assetPath = $"Assets/Resources/AnimalAnim/{animalId}.asset";
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);

            var data = ScriptableObject.CreateInstance<AnimalAnimData>();
            data.animalId = animalId;
            data.idleFrames = LoadSpritesFromFolder($"{artDir}/Idle");
            data.floatingFrames = LoadSpritesFromFolder($"{artDir}/Floating");
            data.successFrames = LoadSpritesFromFolder($"{artDir}/Success");

            string animDir = $"Assets/Art/Animals/{animalId}/Animations";
            string idleClipPath = animalId == "Dog"
                ? $"{animDir}/dogIdle.anim"
                : $"{animDir}/{animalId}Idle.anim";
            var idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(idleClipPath);
            data.idleFps = idleClip != null ? idleClip.frameRate : 30f;
            var floatingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{animDir}/{animalId}Floating.anim");
            data.floatingFps = floatingClip != null ? floatingClip.frameRate : 30f;
            var successClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{animDir}/{animalId}Success.anim");
            data.successFps = successClip != null ? successClip.frameRate : 30f;

            AssetDatabase.CreateAsset(data, assetPath);
            count++;
        }

        EnsureFolder("Assets/Resources/AnimalSprites");
        foreach (var animalId in AllAnimals)
        {
            string srcPath = $"Assets/Art/Animals/{animalId}/Art/Puzzle/{animalId} Main.png";
            string dstPath = $"Assets/Resources/AnimalSprites/{animalId}.png";
            if (!AssetDatabase.CopyAsset(srcPath, dstPath))
            {
                if (System.IO.File.Exists(srcPath))
                {
                    System.IO.File.Copy(srcPath, dstPath, true);
                    AssetDatabase.ImportAsset(dstPath);
                }
            }
            var copiedImporter = AssetImporter.GetAtPath(dstPath) as TextureImporter;
            if (copiedImporter != null && copiedImporter.textureType != TextureImporterType.Sprite)
            {
                copiedImporter.textureType = TextureImporterType.Sprite;
                copiedImporter.spriteImportMode = SpriteImportMode.Single;
                copiedImporter.SaveAndReimport();
            }
        }

        AssetDatabase.SaveAssets();
    }

    private static Sprite[] LoadSpritesFromFolder(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath)) return new Sprite[0];
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        var sprites = new List<Sprite>();
        var paths = new List<string>();
        foreach (var guid in guids) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort();
        foreach (var path in paths)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) { sprites.Add(sprite); continue; }
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (allAssets != null)
                foreach (var asset in allAssets)
                    if (asset is Sprite s) { sprites.Add(s); break; }
        }
        return sprites.ToArray();
    }

    // ─────────────────────────────────────────
    //  BASE ANIMATOR CONTROLLER (unchanged)
    // ─────────────────────────────────────────

    private static void CreateBaseAnimatorController()
    {
        EnsureFolder("Assets/Art/Animals");
        string controllerPath = "Assets/Art/Animals/AnimalBase.controller";
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (existing != null) return;

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("Success", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsFloating", AnimatorControllerParameterType.Bool);
        var rootStateMachine = controller.layers[0].stateMachine;

        var idleClip = new AnimationClip { name = "BaseIdle" };
        var floatingClip = new AnimationClip { name = "BaseFloating" };
        var successClip = new AnimationClip { name = "BaseSuccess" };
        AssetDatabase.AddObjectToAsset(idleClip, controllerPath);
        AssetDatabase.AddObjectToAsset(floatingClip, controllerPath);
        AssetDatabase.AddObjectToAsset(successClip, controllerPath);

        var idleState = rootStateMachine.AddState("Idle", new Vector3(300, 0, 0));
        idleState.motion = idleClip;
        rootStateMachine.defaultState = idleState;
        var floatingState = rootStateMachine.AddState("Floating", new Vector3(300, 100, 0));
        floatingState.motion = floatingClip;
        var successState = rootStateMachine.AddState("Success", new Vector3(550, 0, 0));
        successState.motion = successClip;

        var toFloating = idleState.AddTransition(floatingState);
        toFloating.AddCondition(AnimatorConditionMode.If, 0, "IsFloating");
        toFloating.hasExitTime = false; toFloating.duration = 0f;
        var toIdleFromFloat = floatingState.AddTransition(idleState);
        toIdleFromFloat.AddCondition(AnimatorConditionMode.IfNot, 0, "IsFloating");
        toIdleFromFloat.hasExitTime = false; toIdleFromFloat.duration = 0f;
        var toSuccess = idleState.AddTransition(successState);
        toSuccess.AddCondition(AnimatorConditionMode.If, 0, "Success");
        toSuccess.hasExitTime = false; toSuccess.duration = 0f;
        var toIdleFromSuccess = successState.AddTransition(idleState);
        toIdleFromSuccess.hasExitTime = true; toIdleFromSuccess.exitTime = 1f; toIdleFromSuccess.duration = 0f;

        EditorUtility.SetDirty(controller);

        foreach (var animalId in AllAnimals)
        {
            string overridePath = $"Assets/Art/Animals/{animalId}/Animations/{animalId}.overrideController";
            var overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            if (overrideController != null)
            {
                overrideController.runtimeAnimatorController = controller;
                string animDir = $"Assets/Art/Animals/{animalId}/Animations";
                string idlePath = animalId == "Dog" ? $"{animDir}/dogIdle.anim" : $"{animDir}/{animalId}Idle.anim";
                var animalIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(idlePath);
                var animalFloating = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{animDir}/{animalId}Floating.anim");
                var animalSuccess = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{animDir}/{animalId}Success.anim");
                var newOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                newOverrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(idleClip, animalIdle));
                newOverrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(floatingClip, animalFloating));
                newOverrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(successClip, animalSuccess));
                overrideController.ApplyOverrides(newOverrides);
                EditorUtility.SetDirty(overrideController);
            }
        }
    }

    // ─────────────────────────────────────────
    //  SCENE
    // ─────────────────────────────────────────

    private static void CreateScene()
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");
        string artDir = "Assets/Art/World";

        // Load world art sprites
        var mountainsSprite = LoadSprite($"{artDir}/mountains.png");
        var hillsLargeSprite = LoadSprite($"{artDir}/hillsLarge.png");
        var hillsSprite = LoadSprite($"{artDir}/hills.png");
        var groundLayer1Sprite = LoadSprite($"{artDir}/groundLayer1.png");
        var groundLayer2Sprite = LoadSprite($"{artDir}/groundLayer2.png");
        var cloudLayerB1Sprite = LoadSprite($"{artDir}/cloudLayerB1.png");
        var cloudLayerB2Sprite = LoadSprite($"{artDir}/cloudLayerB2.png");
        var cloudLayer1Sprite = LoadSprite($"{artDir}/cloudLayer1.png");
        var cloudLayer2Sprite = LoadSprite($"{artDir}/cloudLayer2.png");
        var sunSprite = LoadSprite($"{artDir}/sun.png");
        var moonSprite = LoadSprite($"{artDir}/moonFull.png");

        // Tree/bush sprites
        var treeSprite = LoadSprite($"{artDir}/tree.png");
        var treeLongSprite = LoadSprite($"{artDir}/treeLong.png");
        var treeSmall1Sprite = LoadSprite($"{artDir}/treeSmall_green1.png");
        var bush1Sprite = LoadSprite($"{artDir}/bush1.png");
        var bush2Sprite = LoadSprite($"{artDir}/bush2.png");

        // Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = DaySky;
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
        var canvasGO = new GameObject("WorldCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Safe area
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        StretchFull(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // ── Top Bar ──
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
        titleTMP.text = HebrewFixer.Fix("\u05D4\u05E2\u05D5\u05DC\u05DD \u05E9\u05DC\u05D9");
        titleTMP.isRightToLeftText = false;
        titleTMP.fontSize = 42;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button
        var homeIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Icons/home.png");
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(16, -10), new Vector2(90, 90));

        // Profile avatar
        var profileBtn = new GameObject("ProfileButton");
        profileBtn.transform.SetParent(topBar.transform, false);
        var profileBtnRT = profileBtn.AddComponent<RectTransform>();
        profileBtnRT.anchorMin = new Vector2(1, 0.5f);
        profileBtnRT.anchorMax = new Vector2(1, 0.5f);
        profileBtnRT.pivot = new Vector2(1, 0.5f);
        profileBtnRT.anchoredPosition = new Vector2(-16, 0);
        profileBtnRT.sizeDelta = new Vector2(70, 70);
        var profileBtnImg = profileBtn.AddComponent<Image>();
        if (circleSprite != null) profileBtnImg.sprite = circleSprite;
        profileBtnImg.color = HexColor("#90CAF9");

        var profileInitialGO = new GameObject("Initial");
        profileInitialGO.transform.SetParent(profileBtn.transform, false);
        var profileInitialRT = profileInitialGO.AddComponent<RectTransform>();
        StretchFull(profileInitialRT);
        var profileInitialTMP = profileInitialGO.AddComponent<TextMeshProUGUI>();
        profileInitialTMP.text = "?";
        profileInitialTMP.fontSize = 30;
        profileInitialTMP.fontStyle = FontStyles.Bold;
        profileInitialTMP.color = Color.white;
        profileInitialTMP.alignment = TextAlignmentOptions.Center;
        profileInitialTMP.raycastTarget = false;

        // ── Viewport ──
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(safeArea.transform, false);
        var viewportRT = viewport.AddComponent<RectTransform>();
        StretchFull(viewportRT);
        viewportRT.offsetMax = new Vector2(0, -TopBarHeight);
        viewport.AddComponent<RectMask2D>();

        // ── World Content (wide horizontal container) ──
        var worldContent = new GameObject("WorldContent");
        worldContent.transform.SetParent(viewport.transform, false);
        var worldContentRT = worldContent.AddComponent<RectTransform>();
        worldContentRT.anchorMin = new Vector2(0, 0);
        worldContentRT.anchorMax = new Vector2(0, 1);
        worldContentRT.pivot = new Vector2(0, 0.5f);
        worldContentRT.sizeDelta = new Vector2(2000, 0);
        worldContentRT.anchoredPosition = Vector2.zero;

        // ════════════════════════════════════════
        //  LAYERED BACKGROUND (far to near)
        // ════════════════════════════════════════

        // Layer 1: Sky background (full area)
        var skyBgGO = CreateStretchImage(worldContent.transform, "SkyBackground", DaySky);
        skyBgGO.GetComponent<Image>().raycastTarget = false;

        // Layer 2: Mountains (very far, top area)
        var mountainsGO = CreateSpriteLayer(worldContent.transform, "Mountains", mountainsSprite,
            new Vector2(0, 0.45f), new Vector2(1, 0.85f), DayMountains);

        // Layer 3: Large hills (mid-far)
        var hillsLargeGO = CreateSpriteLayer(worldContent.transform, "HillsLarge", hillsLargeSprite,
            new Vector2(0, 0.35f), new Vector2(1, 0.65f), DayHillsLarge);

        // Layer 4: Cloud layer back (behind hills)
        var cloudLayerB1GO = CreateSpriteLayer(worldContent.transform, "CloudLayerB1", cloudLayerB1Sprite,
            new Vector2(0, 0.55f), new Vector2(1, 0.8f), DayCloudTint);
        var cloudLayerB2GO = CreateSpriteLayer(worldContent.transform, "CloudLayerB2", cloudLayerB2Sprite,
            new Vector2(0, 0.5f), new Vector2(1, 0.75f), DayCloudTint);

        // Layer 5: Small hills
        var hillsGO = CreateSpriteLayer(worldContent.transform, "Hills", hillsSprite,
            new Vector2(0, 0.25f), new Vector2(1, 0.5f), DayHills);

        // Layer 6: Cloud layer front
        var cloudLayer1GO = CreateSpriteLayer(worldContent.transform, "CloudLayer1", cloudLayer1Sprite,
            new Vector2(0, 0.45f), new Vector2(1, 0.7f), DayCloudTint);
        var cloudLayer2GO = CreateSpriteLayer(worldContent.transform, "CloudLayer2", cloudLayer2Sprite,
            new Vector2(0, 0.4f), new Vector2(1, 0.65f), DayCloudTint);

        // Layer 7: Ground back layer
        var groundBackGO = CreateSpriteLayer(worldContent.transform, "GroundBack", groundLayer1Sprite,
            new Vector2(0, 0), new Vector2(1, 0.35f), DayGroundBack);

        // Layer 8: Ground front layer
        var groundFrontGO = CreateSpriteLayer(worldContent.transform, "GroundFront", groundLayer2Sprite,
            new Vector2(0, 0), new Vector2(1, 0.25f), DayGroundFront);

        // ── Sun (in sky, right side) ──
        var sunGO = new GameObject("Sun");
        sunGO.transform.SetParent(worldContent.transform, false);
        var sunRT = sunGO.AddComponent<RectTransform>();
        sunRT.anchorMin = new Vector2(0, 0);
        sunRT.anchorMax = new Vector2(0, 0);
        sunRT.pivot = new Vector2(0.5f, 0.5f);
        sunRT.sizeDelta = new Vector2(180, 180);
        sunRT.anchoredPosition = new Vector2(800, 1400);
        var sunImg = sunGO.AddComponent<Image>();
        sunImg.sprite = sunSprite;
        sunImg.preserveAspect = true;
        sunImg.raycastTarget = true;
        sunImg.color = Color.white;

        // Sun glow (behind sun)
        var sunGlowGO = new GameObject("SunGlow");
        sunGlowGO.transform.SetParent(sunGO.transform, false);
        sunGlowGO.transform.SetAsFirstSibling();
        var sunGlowRT = sunGlowGO.AddComponent<RectTransform>();
        sunGlowRT.anchorMin = new Vector2(-0.6f, -0.6f);
        sunGlowRT.anchorMax = new Vector2(1.6f, 1.6f);
        sunGlowRT.offsetMin = Vector2.zero;
        sunGlowRT.offsetMax = Vector2.zero;
        var sunGlowImg = sunGlowGO.AddComponent<Image>();
        if (circleSprite != null) sunGlowImg.sprite = circleSprite;
        sunGlowImg.color = new Color(1f, 0.95f, 0.6f, 0.25f);
        sunGlowImg.raycastTarget = false;

        // ── Moon (starts off-screen below) ──
        var moonGO = new GameObject("Moon");
        moonGO.transform.SetParent(worldContent.transform, false);
        var moonRT = moonGO.AddComponent<RectTransform>();
        moonRT.anchorMin = new Vector2(0, 0);
        moonRT.anchorMax = new Vector2(0, 0);
        moonRT.pivot = new Vector2(0.5f, 0.5f);
        moonRT.sizeDelta = new Vector2(150, 150);
        moonRT.anchoredPosition = new Vector2(750, 1350); // rest position (environment will move to offscreen)
        var moonImg = moonGO.AddComponent<Image>();
        moonImg.sprite = moonSprite;
        moonImg.preserveAspect = true;
        moonImg.raycastTarget = true;
        moonImg.color = Color.white;

        // Moon glow
        var moonGlowGO = new GameObject("MoonGlow");
        moonGlowGO.transform.SetParent(moonGO.transform, false);
        moonGlowGO.transform.SetAsFirstSibling();
        var moonGlowRT = moonGlowGO.AddComponent<RectTransform>();
        moonGlowRT.anchorMin = new Vector2(-0.5f, -0.5f);
        moonGlowRT.anchorMax = new Vector2(1.5f, 1.5f);
        moonGlowRT.offsetMin = Vector2.zero;
        moonGlowRT.offsetMax = Vector2.zero;
        var moonGlowImg = moonGlowGO.AddComponent<Image>();
        if (circleSprite != null) moonGlowImg.sprite = circleSprite;
        moonGlowImg.color = new Color(0.85f, 0.9f, 1f, 0f); // starts invisible
        moonGlowImg.raycastTarget = false;

        // ── Sky Area (invisible, used for balloon/cloud spawning) ──
        var skyAreaGO = new GameObject("SkyArea");
        skyAreaGO.transform.SetParent(worldContent.transform, false);
        var skyAreaRT = skyAreaGO.AddComponent<RectTransform>();
        skyAreaRT.anchorMin = new Vector2(0, 0.35f);
        skyAreaRT.anchorMax = new Vector2(1, 1);
        skyAreaRT.offsetMin = Vector2.zero;
        skyAreaRT.offsetMax = Vector2.zero;
        // No image — just a container

        // ── Grass Area (invisible, used for animal spawning) ──
        var grassAreaGO = new GameObject("GrassArea");
        grassAreaGO.transform.SetParent(worldContent.transform, false);
        var grassAreaRT = grassAreaGO.AddComponent<RectTransform>();
        grassAreaRT.anchorMin = new Vector2(0, 0);
        grassAreaRT.anchorMax = new Vector2(1, 0.3f);
        grassAreaRT.offsetMin = Vector2.zero;
        grassAreaRT.offsetMax = Vector2.zero;

        // ── Trees & Bushes (interactive props on ground) ──
        // Tree 1 — large tree on the left
        CreateProp(grassAreaGO.transform, "Tree1", treeSprite, WorldProp.PropType.Tree,
            new Vector2(350, 20), new Vector2(180, 300));
        // Tree 2 — tall tree in the middle-right
        CreateProp(grassAreaGO.transform, "Tree2", treeLongSprite, WorldProp.PropType.Tree,
            new Vector2(1100, 15), new Vector2(160, 340));
        // Small tree
        CreateProp(grassAreaGO.transform, "Tree3", treeSmall1Sprite, WorldProp.PropType.Tree,
            new Vector2(1600, 25), new Vector2(120, 200));
        // Bush 1
        CreateProp(grassAreaGO.transform, "Bush1", bush1Sprite, WorldProp.PropType.Bush,
            new Vector2(550, 10), new Vector2(100, 80));
        // Bush 2
        CreateProp(grassAreaGO.transform, "Bush2", bush2Sprite, WorldProp.PropType.Bush,
            new Vector2(1400, 8), new Vector2(90, 70));

        // ── Drawing Gallery Easel (on grass, far left) ──
        var easelGO = new GameObject("GalleryEasel");
        easelGO.transform.SetParent(grassAreaGO.transform, false);
        var easelRT = easelGO.AddComponent<RectTransform>();
        easelRT.anchorMin = Vector2.zero;
        easelRT.anchorMax = Vector2.zero;
        easelRT.pivot = new Vector2(0.5f, 0);
        easelRT.sizeDelta = new Vector2(160, 200);
        easelRT.anchoredPosition = new Vector2(80, 30);

        var boardImg = easelGO.AddComponent<Image>();
        boardImg.color = HexColor("#8D6E63");
        boardImg.raycastTarget = true;

        var canvasOnEasel = new GameObject("Canvas");
        canvasOnEasel.transform.SetParent(easelGO.transform, false);
        var canvasOnEaselRT = canvasOnEasel.AddComponent<RectTransform>();
        canvasOnEaselRT.anchorMin = new Vector2(0.1f, 0.25f);
        canvasOnEaselRT.anchorMax = new Vector2(0.9f, 0.85f);
        canvasOnEaselRT.offsetMin = Vector2.zero;
        canvasOnEaselRT.offsetMax = Vector2.zero;
        var canvasOnEaselImg = canvasOnEasel.AddComponent<Image>();
        canvasOnEaselImg.color = Color.white;
        canvasOnEaselImg.raycastTarget = false;

        var dotColors = new Color[] { HexColor("#EF4444"), HexColor("#3B82F6"), HexColor("#FACC15"), HexColor("#22C55E") };
        for (int d = 0; d < dotColors.Length; d++)
        {
            var dotGO = new GameObject($"Dot{d}");
            dotGO.transform.SetParent(canvasOnEasel.transform, false);
            var dotRT = dotGO.AddComponent<RectTransform>();
            float dx = 0.15f + d * 0.22f;
            float dy = 0.3f + (d % 2) * 0.3f;
            dotRT.anchorMin = new Vector2(dx, dy);
            dotRT.anchorMax = new Vector2(dx + 0.18f, dy + 0.25f);
            dotRT.offsetMin = Vector2.zero;
            dotRT.offsetMax = Vector2.zero;
            var dotImg = dotGO.AddComponent<Image>();
            if (circleSprite != null) dotImg.sprite = circleSprite;
            dotImg.color = dotColors[d];
            dotImg.raycastTarget = false;
        }

        var easelBtn = easelGO.AddComponent<Button>();
        easelBtn.targetGraphic = boardImg;

        // ── Environment Controller ──
        var envComponent = canvasGO.AddComponent<WorldEnvironment>();
        envComponent.skyBackground = skyBgGO.GetComponent<Image>();
        envComponent.mountainsLayer = mountainsGO.GetComponent<Image>();
        envComponent.hillsLargeLayer = hillsLargeGO.GetComponent<Image>();
        envComponent.hillsLayer = hillsGO.GetComponent<Image>();
        envComponent.groundBackLayer = groundBackGO.GetComponent<Image>();
        envComponent.groundFrontLayer = groundFrontGO.GetComponent<Image>();
        envComponent.sunRT = sunRT;
        envComponent.moonRT = moonRT;
        envComponent.sunImage = sunImg;
        envComponent.moonImage = moonImg;
        envComponent.sunGlow = sunGlowImg;
        envComponent.moonGlow = moonGlowImg;
        envComponent.cloudLayerBack1 = cloudLayerB1GO.GetComponent<Image>();
        envComponent.cloudLayerBack2 = cloudLayerB2GO.GetComponent<Image>();
        envComponent.cloudLayerFront1 = cloudLayer1GO.GetComponent<Image>();
        envComponent.cloudLayerFront2 = cloudLayer2GO.GetComponent<Image>();

        // ── Cloud System ──
        var cloudSystemComp = canvasGO.AddComponent<WorldCloudSystem>();
        cloudSystemComp.skyArea = skyAreaRT;

        // ── World Controller ──
        var controller = canvasGO.AddComponent<WorldController>();
        controller.gameDatabase = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/Games/GameDatabase.asset");
        controller.circleSprite = circleSprite;
        controller.worldContent = worldContentRT;
        controller.skyArea = skyAreaRT;
        controller.grassArea = grassAreaRT;
        controller.homeButton = homeGO.GetComponent<Button>();
        controller.galleryButton = easelBtn;
        controller.profileAvatar = profileBtnImg;
        controller.profileInitial = profileInitialTMP;
        controller.environment = envComponent;
        controller.cloudSystem = cloudSystemComp;

        // ── Input Handler ──
        var inputHandler = canvasGO.AddComponent<WorldInputHandler>();
        inputHandler.worldContent = worldContentRT;
        inputHandler.viewport = viewportRT;
        inputHandler.environment = envComponent;
        inputHandler.sunRT = sunRT;
        inputHandler.moonRT = moonRT;

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/WorldScene.unity");
    }

    // ─────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────

    private static Sprite LoadSprite(string path)
    {
        // Try direct load first (works for Single sprite mode)
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;

        // For Multiple sprite mode, search sub-assets
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (allAssets != null)
        {
            foreach (var asset in allAssets)
            {
                if (asset is Sprite s) return s;
            }
        }
        return null;
    }

    private static GameObject CreateSpriteLayer(Transform parent, string name, Sprite sprite,
        Vector2 anchorMin, Vector2 anchorMax, Color tint)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.color = tint;
        img.preserveAspect = false;
        img.raycastTarget = false;
        return go;
    }

    private static void CreateProp(Transform parent, string name, Sprite sprite,
        WorldProp.PropType type, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = true;
        img.color = Color.white;

        var prop = go.AddComponent<WorldProp>();
        prop.propType = type;
    }

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

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
