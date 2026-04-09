using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Builds the WorldScene with layered parallax backgrounds, sun/moon,
/// interactive trees/bushes, cloud system, and the existing animal/balloon logic.
/// </summary>
public class WorldSceneSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly Color TopBarColor = HexColor("#5BA84C");
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    // Day mode default colors (environment handles transitions)
    private static readonly Color DaySky = HexColor("#8FD4F5");
    private static readonly Color DayHillsLarge = HexColor("#B7D7D6");
    private static readonly Color DayHills = HexColor("#9FCBC5");
    private static readonly Color DayGroundBack = HexColor("#8ED36B");
    private static readonly Color DayGroundFront = HexColor("#79C956");
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
        var hillsLargeSprite = LoadSprite($"{artDir}/hillsLarge.png");
        var hillsSprite = LoadSprite($"{artDir}/hills.png");
        var groundLayer1Sprite = LoadSprite($"{artDir}/groundLayer1.png");
        var groundLayer2Sprite = LoadSprite($"{artDir}/groundLayer2.png");
        var sunSprite = LoadSprite($"{artDir}/sun.png");
        var moonSprite = LoadSprite($"{artDir}/moonFull.png");

        // Tree/bush sprites
        var treeSprite = LoadSprite($"{artDir}/tree.png");
        var treeLongSprite = LoadSprite($"{artDir}/treeLong.png");
        var treeSmall1Sprite = LoadSprite($"{artDir}/treeSmall_green1.png");

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
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Safe area — only for the top bar (buttons/text must avoid cutout)
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
        HebrewText.SetText(titleTMP, "\u05D4\u05E2\u05D5\u05DC\u05DD \u05E9\u05DC\u05D9");
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button (UI_1_2, white silhouette)
        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        // Games collection button (top-right, before profile avatar)
        var gamesIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Icons/menuGrid.png");
        var gamesGO = CreateIconButton(topBar.transform, "GamesButton", gamesIcon,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-100, 0), new Vector2(90, 90));

        // Parent dashboard button (UI_1_4 gear, top-right, icon + label "איזור הורים")
        var parentIcon = UISheetHelper.GearIcon;
        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");

        var parentDashBtn = new GameObject("ParentDashboardButton");
        parentDashBtn.transform.SetParent(topBar.transform, false);
        var pdRT = parentDashBtn.AddComponent<RectTransform>();
        pdRT.anchorMin = new Vector2(1, 0.5f);
        pdRT.anchorMax = new Vector2(1, 0.5f);
        pdRT.pivot = new Vector2(1, 0.5f);
        pdRT.anchoredPosition = new Vector2(-12, 0);
        pdRT.sizeDelta = new Vector2(210, 60);

        var pdBgImg = parentDashBtn.AddComponent<Image>();
        if (roundedRect != null) { pdBgImg.sprite = roundedRect; pdBgImg.type = Image.Type.Sliced; }
        pdBgImg.color = new Color(1f, 1f, 1f, 0.25f);
        pdBgImg.raycastTarget = true;
        parentDashBtn.AddComponent<Button>().targetGraphic = pdBgImg;

        // Icon
        var pdIconGO = new GameObject("Icon");
        pdIconGO.transform.SetParent(parentDashBtn.transform, false);
        var pdIconRT = pdIconGO.AddComponent<RectTransform>();
        pdIconRT.anchorMin = new Vector2(1, 0); pdIconRT.anchorMax = new Vector2(1, 1);
        pdIconRT.pivot = new Vector2(1, 0.5f);
        pdIconRT.anchoredPosition = new Vector2(-8, 0);
        pdIconRT.sizeDelta = new Vector2(36, 36);
        var pdIconImg = pdIconGO.AddComponent<Image>();
        pdIconImg.sprite = parentIcon;
        pdIconImg.preserveAspect = true;
        pdIconImg.color = Color.white;
        pdIconImg.raycastTarget = false;

        // Label
        var pdLabelGO = new GameObject("Label");
        pdLabelGO.transform.SetParent(parentDashBtn.transform, false);
        var pdLabelRT = pdLabelGO.AddComponent<RectTransform>();
        pdLabelRT.anchorMin = Vector2.zero; pdLabelRT.anchorMax = new Vector2(1, 1);
        pdLabelRT.offsetMin = new Vector2(12, 0); pdLabelRT.offsetMax = new Vector2(-52, 0);
        var pdLabelTMP = pdLabelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(pdLabelTMP, "\u05D0\u05D9\u05D6\u05D5\u05E8 \u05D4\u05D5\u05E8\u05D9\u05DD"); // איזור הורים
        pdLabelTMP.fontSize = 20; pdLabelTMP.fontStyle = FontStyles.Bold;
        pdLabelTMP.color = Color.white;
        pdLabelTMP.alignment = TextAlignmentOptions.Center;
        pdLabelTMP.raycastTarget = false;

        // ── Viewport (full screen — sky/grass fill everything including cutout area) ──
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(canvasGO.transform, false);
        viewport.transform.SetSiblingIndex(0); // behind safe area
        var viewportRT = viewport.AddComponent<RectTransform>();
        StretchFull(viewportRT);
        viewportRT.offsetMax = new Vector2(0, -TopBarHeight);
        viewport.AddComponent<RectMask2D>();

        // ── World Content (fills viewport; runtime expands when more animals unlock) ──
        var worldContent = new GameObject("WorldContent");
        worldContent.transform.SetParent(viewport.transform, false);
        var worldContentRT = worldContent.AddComponent<RectTransform>();
        // Content must be WIDER than viewport for horizontal scrolling
        // 3x viewport width = scrollable world with wrap
        worldContentRT.anchorMin = new Vector2(0, 0);
        worldContentRT.anchorMax = new Vector2(0, 1);  // anchor left, stretch vertically
        worldContentRT.pivot = new Vector2(0, 0.5f);
        worldContentRT.sizeDelta = new Vector2(1920 * 3, 0);  // 3x screen width
        worldContentRT.anchoredPosition = Vector2.zero;

        // ════════════════════════════════════════
        //  LAYERED BACKGROUND (far to near)
        // ════════════════════════════════════════

        // Layer 1: Sky background (full area)
        var skyBgGO = CreateStretchImage(worldContent.transform, "SkyBackground", DaySky);
        skyBgGO.GetComponent<Image>().raycastTarget = false;

        // Stars container (behind clouds/hills, populated at runtime)
        var starsGO = new GameObject("Stars");
        starsGO.transform.SetParent(worldContent.transform, false);
        var starsRT = starsGO.AddComponent<RectTransform>();
        starsRT.anchorMin = new Vector2(0, 0.5f);
        starsRT.anchorMax = new Vector2(1, 1);
        starsRT.offsetMin = Vector2.zero;
        starsRT.offsetMax = Vector2.zero;

        // Layer 2: Large hills (mid-far, no mountains)
        var hillsLargeGO = CreateSpriteLayer(worldContent.transform, "HillsLarge", hillsLargeSprite,
            new Vector2(0, 0.35f), new Vector2(1, 0.65f), DayHillsLarge);

        // Layer 5: Small hills
        var hillsGO = CreateSpriteLayer(worldContent.transform, "Hills", hillsSprite,
            new Vector2(0, 0.25f), new Vector2(1, 0.5f), DayHills);

        // ── Sun (top-right corner, equal padding from top and right) ──
        // Sun in worldContent between sky and ground layers (correct z-order)
        // WorldController counter-offsets X at runtime to keep it visually fixed
        var sunGO = new GameObject("Sun");
        sunGO.transform.SetParent(worldContent.transform, false);
        sunGO.transform.SetSiblingIndex(2); // after SkyBackground(0) + Stars(1)
        var sunRT = sunGO.AddComponent<RectTransform>();
        // Anchor top-left (0,1) so counter-offset math works with scrolling
        sunRT.anchorMin = new Vector2(0, 1);
        sunRT.anchorMax = new Vector2(0, 1);
        sunRT.pivot = new Vector2(0.5f, 1);
        sunRT.sizeDelta = new Vector2(160, 160);
        sunRT.anchoredPosition = new Vector2(1750, -30); // right side of first visible screen
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

        // ── Moon (same corner as sun, starts hidden below) ──
        var moonGO = new GameObject("Moon");
        // Moon in worldContent same z-layer as sun
        moonGO.transform.SetParent(worldContent.transform, false);
        moonGO.transform.SetSiblingIndex(3);
        var moonRT = moonGO.AddComponent<RectTransform>();
        moonRT.anchorMin = new Vector2(0, 1);
        moonRT.anchorMax = new Vector2(0, 1);
        moonRT.pivot = new Vector2(0.5f, 1);
        moonRT.sizeDelta = new Vector2(130, 130);
        moonRT.anchoredPosition = new Vector2(1750, -45);
        var moonImg = moonGO.AddComponent<Image>();
        moonImg.sprite = moonSprite;
        moonImg.preserveAspect = true;
        moonImg.raycastTarget = true;
        moonImg.color = Color.white;

        // Layer 7: Ground back layer (extended up to cover gap between clouds and grass)
        var groundBackGO = CreateSpriteLayer(worldContent.transform, "GroundBack", groundLayer1Sprite,
            new Vector2(0, 0), new Vector2(1, 0.45f), DayGroundBack);

        // Layer 8: Ground front layer
        var groundFrontGO = CreateSpriteLayer(worldContent.transform, "GroundFront", groundLayer2Sprite,
            new Vector2(0, 0), new Vector2(1, 0.25f), DayGroundFront);

        // ── Sky Area (invisible, used for balloon/cloud spawning) ──
        var skyAreaGO = new GameObject("SkyArea");
        skyAreaGO.transform.SetParent(worldContent.transform, false);
        var skyAreaRT = skyAreaGO.AddComponent<RectTransform>();
        skyAreaRT.anchorMin = new Vector2(0, 0.45f);
        skyAreaRT.anchorMax = new Vector2(1, 1);
        skyAreaRT.offsetMin = Vector2.zero;
        skyAreaRT.offsetMax = Vector2.zero;
        // No image — just a container

        // ── Grass Area (invisible, used for animal spawning) ──
        var grassAreaGO = new GameObject("GrassArea");
        grassAreaGO.transform.SetParent(worldContent.transform, false);
        var grassAreaRT = grassAreaGO.AddComponent<RectTransform>();
        grassAreaRT.anchorMin = new Vector2(0, 0);
        grassAreaRT.anchorMax = new Vector2(1, 0.45f);
        grassAreaRT.offsetMin = Vector2.zero;
        grassAreaRT.offsetMax = Vector2.zero;

        // ═══════════════════════════════════════
        //  PROPS — anchor-proportional X positions
        //  for responsive layout across resolutions
        //
        //  Composition (left → right):
        //    Easel (6%) — Tree L (15%) — ToyBox (50%) — Tree R (85%)
        //
        //  Trees frame the scene, ToyBox is the focal point.
        // ═══════════════════════════════════════

        // Tree Left — background framing element, upper grass ridge
        CreatePropAnchored(grassAreaGO.transform, "TreeLeft", treeSprite, WorldProp.PropType.Tree,
            0.15f, 380, new Vector2(160, 260));
        // Tree Right — center screen, right side
        CreatePropAnchored(grassAreaGO.transform, "TreeRight", treeLongSprite, WorldProp.PropType.Tree,
            0.55f, 360, new Vector2(140, 300));

        // ── Toy Box — centered focal point, upper grass ridge ──
        var toyBoxSprite = LoadSprite("Assets/Art/Toy Box.png");

        var toyBoxGO = new GameObject("ToyBox");
        toyBoxGO.transform.SetParent(grassAreaGO.transform, false);
        var toyBoxRT = toyBoxGO.AddComponent<RectTransform>();
        toyBoxRT.anchorMin = new Vector2(0.50f, 0);
        toyBoxRT.anchorMax = new Vector2(0.50f, 0);
        toyBoxRT.pivot = new Vector2(0.5f, 0);
        toyBoxRT.sizeDelta = new Vector2(300, 330);
        toyBoxRT.anchoredPosition = new Vector2(-80, 330);

        var toyBoxImg = toyBoxGO.AddComponent<Image>();
        toyBoxImg.sprite = toyBoxSprite;
        toyBoxImg.preserveAspect = true;
        toyBoxImg.raycastTarget = true;
        toyBoxImg.color = Color.white;
        // Shrink tap area: 40px inset from each side, 80px from top (lid area)
        toyBoxImg.raycastPadding = new Vector4(40, 20, 40, 80);

        var worldToyBox = toyBoxGO.AddComponent<WorldToyBox>();

        // ── Sticker Tree — between Tree Left and ToyBox ──
        var stickerTreeAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Sticker Tree/StikerTree.png");
        var treeStageList = new List<Sprite>();
        if (stickerTreeAssets != null)
        {
            foreach (var asset in stickerTreeAssets)
                if (asset is Sprite spr) treeStageList.Add(spr);
        }
        treeStageList.Sort((a, b) =>
        {
            int na = 0, nb = 0;
            var pa = a.name.Split('_'); if (pa.Length > 1) int.TryParse(pa[pa.Length - 1], out na);
            var pb = b.name.Split('_'); if (pb.Length > 1) int.TryParse(pb[pb.Length - 1], out nb);
            return na.CompareTo(nb);
        });

        var stickerTreeGO = new GameObject("StickerTree");
        stickerTreeGO.transform.SetParent(grassAreaGO.transform, false);
        var stickerTreeRT = stickerTreeGO.AddComponent<RectTransform>();
        // Left screen (0-0.333 of total width). Place at ~0.18 = center-right of left screen
        stickerTreeRT.anchorMin = new Vector2(0.18f, 0);
        stickerTreeRT.anchorMax = new Vector2(0.18f, 0);
        stickerTreeRT.pivot = new Vector2(0.5f, 0);
        stickerTreeRT.sizeDelta = new Vector2(60, 80); // starts as seedling
        stickerTreeRT.anchoredPosition = new Vector2(0, 180);

        var stickerTreeImg = stickerTreeGO.AddComponent<Image>();
        if (treeStageList.Count > 0) stickerTreeImg.sprite = treeStageList[0];
        stickerTreeImg.preserveAspect = true;
        stickerTreeImg.raycastTarget = true;

        var stickerTreeCtrl = stickerTreeGO.AddComponent<StickerTreeController>();
        stickerTreeCtrl.treeStages = treeStageList.ToArray();

        // ── Painting Easel — far left, lower grass ──
        var easelSprite = LoadSprite("Assets/Art/Easel.png");
        var easelGO = new GameObject("PaintingEasel");
        easelGO.transform.SetParent(grassAreaGO.transform, false);
        var easelRT = easelGO.AddComponent<RectTransform>();
        easelRT.anchorMin = new Vector2(0.06f, 0);
        easelRT.anchorMax = new Vector2(0.06f, 0);
        easelRT.pivot = new Vector2(0.5f, 0);
        easelRT.sizeDelta = new Vector2(160, 200);
        easelRT.anchoredPosition = new Vector2(0, 150);

        var easelImg = easelGO.AddComponent<Image>();
        easelImg.sprite = easelSprite;
        easelImg.preserveAspect = true;
        easelImg.raycastTarget = true;
        easelImg.color = Color.white;

        // ── Alin Guide — far right, matching Easel gap from ToyBox (50% + 44% = 94%) ──
        var alinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/AlinGuide.prefab");
        if (alinPrefab != null)
        {
            // Ground shadow (dark ellipse under Alin's feet)
            var alinShadowGO = new GameObject("AlinShadow");
            alinShadowGO.transform.SetParent(grassAreaGO.transform, false);
            var alinShadowRT = alinShadowGO.AddComponent<RectTransform>();
            // Center screen: X anchor ~0.31+0.31 = center-screen right side
            // With 3 screens, center screen spans anchors 0.333-0.666
            // Place Alin at right side of center screen: ~0.62
            alinShadowRT.anchorMin = new Vector2(0.62f, 0);
            alinShadowRT.anchorMax = new Vector2(0.62f, 0);
            alinShadowRT.pivot = new Vector2(0.5f, 0.5f);
            alinShadowRT.sizeDelta = new Vector2(120, 30);
            alinShadowRT.anchoredPosition = new Vector2(0, 148);
            var alinShadowImg = alinShadowGO.AddComponent<Image>();
            var circleSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");
            if (circleSpr != null) alinShadowImg.sprite = circleSpr;
            alinShadowImg.color = new Color(0, 0, 0, 0.18f);
            alinShadowImg.raycastTarget = false;

            // Alin character
            var alinGO = (GameObject)PrefabUtility.InstantiatePrefab(alinPrefab, grassAreaGO.transform);
            var alinRT = alinGO.GetComponent<RectTransform>();
            alinRT.anchorMin = new Vector2(0.62f, 0);
            alinRT.anchorMax = new Vector2(0.62f, 0);
            alinRT.pivot = new Vector2(0.5f, 0);
            alinRT.sizeDelta = new Vector2(160, 360);
            alinRT.anchoredPosition = new Vector2(0, 150);
            // Start visible in world (idle pose)
            var alinGuide = alinGO.GetComponent<AlinGuide>();
            if (alinGuide != null)
                alinGuide.startVisible = true;
        }
        else
        {
            Debug.LogWarning("AlinGuide prefab not found. Run 'Tools > Kids Learning Game > Setup Alin Guide' first.");
        }

        // ── Gallery Overlay (full-screen, above everything, initially hidden) ──
        var roundedRectSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");

        var galleryOverlay = new GameObject("GalleryOverlay");
        galleryOverlay.transform.SetParent(safeArea.transform, false);
        var galleryOverlayRT = galleryOverlay.AddComponent<RectTransform>();
        StretchFull(galleryOverlayRT);

        // Dim background
        var dimBgGO = new GameObject("DimBackground");
        dimBgGO.transform.SetParent(galleryOverlay.transform, false);
        var dimBgRT = dimBgGO.AddComponent<RectTransform>();
        StretchFull(dimBgRT);
        var dimBgImg = dimBgGO.AddComponent<Image>();
        dimBgImg.color = new Color(0, 0, 0, 0.45f);
        dimBgImg.raycastTarget = true;

        // Gallery panel (centered, rounded)
        var galleryPanel = new GameObject("GalleryPanel");
        galleryPanel.transform.SetParent(galleryOverlay.transform, false);
        var galleryPanelRT = galleryPanel.AddComponent<RectTransform>();
        galleryPanelRT.anchorMin = new Vector2(0.15f, 0.10f);
        galleryPanelRT.anchorMax = new Vector2(0.85f, 0.90f);
        galleryPanelRT.offsetMin = Vector2.zero;
        galleryPanelRT.offsetMax = Vector2.zero;
        var galleryPanelImg = galleryPanel.AddComponent<Image>();
        galleryPanelImg.color = HexColor("#FFF8F0");
        if (roundedRectSprite != null) galleryPanelImg.sprite = roundedRectSprite;
        galleryPanelImg.type = Image.Type.Sliced;
        galleryPanelImg.raycastTarget = true;

        // Panel border/frame
        var panelBorder = new GameObject("Border");
        panelBorder.transform.SetParent(galleryPanel.transform, false);
        panelBorder.transform.SetAsFirstSibling();
        var panelBorderRT = panelBorder.AddComponent<RectTransform>();
        panelBorderRT.anchorMin = new Vector2(-0.005f, -0.003f);
        panelBorderRT.anchorMax = new Vector2(1.005f, 1.003f);
        panelBorderRT.offsetMin = Vector2.zero;
        panelBorderRT.offsetMax = Vector2.zero;
        var panelBorderImg = panelBorder.AddComponent<Image>();
        panelBorderImg.color = HexColor("#E8D5C0");
        if (roundedRectSprite != null) panelBorderImg.sprite = roundedRectSprite;
        panelBorderImg.type = Image.Type.Sliced;
        panelBorderImg.raycastTarget = false;

        // Header bar
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(galleryPanel.transform, false);
        var headerRT = headerGO.AddComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 1);
        headerRT.anchorMax = new Vector2(1, 1);
        headerRT.pivot = new Vector2(0.5f, 1);
        headerRT.sizeDelta = new Vector2(0, 100);

        // Title
        var galleryTitleGO = new GameObject("Title");
        galleryTitleGO.transform.SetParent(headerGO.transform, false);
        var galleryTitleRT = galleryTitleGO.AddComponent<RectTransform>();
        StretchFull(galleryTitleRT);
        galleryTitleRT.offsetMin = new Vector2(30, 0);
        galleryTitleRT.offsetMax = new Vector2(-80, 0);
        var galleryTitleTMP = galleryTitleGO.AddComponent<TMPro.TextMeshProUGUI>();
        HebrewText.SetText(galleryTitleTMP, "\u05D4\u05E6\u05D9\u05D5\u05E8\u05D9\u05DD \u05E9\u05DC\u05D9");
        galleryTitleTMP.fontSize = 36;
        galleryTitleTMP.fontStyle = TMPro.FontStyles.Bold;
        galleryTitleTMP.color = HexColor("#5B4636");
        galleryTitleTMP.alignment = TMPro.TextAlignmentOptions.Center;
        galleryTitleTMP.raycastTarget = false;

        // Close button
        var closeBtnGO = new GameObject("CloseButton");
        closeBtnGO.transform.SetParent(headerGO.transform, false);
        var closeBtnRT = closeBtnGO.AddComponent<RectTransform>();
        closeBtnRT.anchorMin = new Vector2(1, 0.5f);
        closeBtnRT.anchorMax = new Vector2(1, 0.5f);
        closeBtnRT.pivot = new Vector2(1, 0.5f);
        closeBtnRT.anchoredPosition = new Vector2(-16, 0);
        closeBtnRT.sizeDelta = new Vector2(64, 64);
        var closeBtnImg = closeBtnGO.AddComponent<Image>();
        if (circleSprite != null) closeBtnImg.sprite = circleSprite;
        closeBtnImg.color = HexColor("#E57373");
        closeBtnImg.raycastTarget = true;
        var closeBtn = closeBtnGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeBtnImg;

        // X text on close button
        var xTextGO = new GameObject("X");
        xTextGO.transform.SetParent(closeBtnGO.transform, false);
        var xTextRT = xTextGO.AddComponent<RectTransform>();
        StretchFull(xTextRT);
        var xTextTMP = xTextGO.AddComponent<TMPro.TextMeshProUGUI>();
        xTextTMP.text = "\u00D7";
        xTextTMP.fontSize = 38;
        xTextTMP.fontStyle = TMPro.FontStyles.Bold;
        xTextTMP.color = Color.white;
        xTextTMP.alignment = TMPro.TextAlignmentOptions.Center;
        xTextTMP.raycastTarget = false;

        // "New Drawing" button
        var newDrawBtnGO = new GameObject("NewDrawingButton");
        newDrawBtnGO.transform.SetParent(galleryPanel.transform, false);
        var newDrawBtnRT = newDrawBtnGO.AddComponent<RectTransform>();
        newDrawBtnRT.anchorMin = new Vector2(0, 1);
        newDrawBtnRT.anchorMax = new Vector2(1, 1);
        newDrawBtnRT.pivot = new Vector2(0.5f, 1);
        newDrawBtnRT.anchoredPosition = new Vector2(0, -100);
        newDrawBtnRT.sizeDelta = new Vector2(-40, 70);
        var newDrawBtnImg = newDrawBtnGO.AddComponent<Image>();
        newDrawBtnImg.color = HexColor("#66BB6A");
        if (roundedRectSprite != null) newDrawBtnImg.sprite = roundedRectSprite;
        newDrawBtnImg.type = Image.Type.Sliced;
        newDrawBtnImg.raycastTarget = true;
        var newDrawBtn = newDrawBtnGO.AddComponent<Button>();
        newDrawBtn.targetGraphic = newDrawBtnImg;

        // + icon and text
        var newDrawTextGO = new GameObject("Text");
        newDrawTextGO.transform.SetParent(newDrawBtnGO.transform, false);
        var newDrawTextRT = newDrawTextGO.AddComponent<RectTransform>();
        StretchFull(newDrawTextRT);
        var newDrawTMP = newDrawTextGO.AddComponent<TMPro.TextMeshProUGUI>();
        HebrewText.SetText(newDrawTMP, "\u05E6\u05D9\u05D5\u05E8 \u05D7\u05D3\u05E9" + "  +");
        newDrawTMP.fontSize = 30;
        newDrawTMP.fontStyle = TMPro.FontStyles.Bold;
        newDrawTMP.color = Color.white;
        newDrawTMP.alignment = TMPro.TextAlignmentOptions.Center;
        newDrawTMP.raycastTarget = false;

        // Scroll View for drawings grid
        var scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(galleryPanel.transform, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(20, 20);
        scrollRT.offsetMax = new Vector2(-20, -180);
        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollGO.AddComponent<RectMask2D>();

        // Grid content
        var gridContentGO = new GameObject("Content");
        gridContentGO.transform.SetParent(scrollGO.transform, false);
        var gridContentRT = gridContentGO.AddComponent<RectTransform>();
        gridContentRT.anchorMin = new Vector2(0, 1);
        gridContentRT.anchorMax = new Vector2(1, 1);
        gridContentRT.pivot = new Vector2(0.5f, 1);
        gridContentRT.sizeDelta = new Vector2(0, 0);
        var gridLayout = gridContentGO.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(240, 240);
        gridLayout.spacing = new Vector2(20, 20);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperCenter;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 3;
        gridLayout.padding = new RectOffset(10, 10, 10, 10);
        var contentSizeFitter = gridContentGO.AddComponent<ContentSizeFitter>();
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = gridContentRT;

        // Empty state text
        var emptyGO = new GameObject("EmptyText");
        emptyGO.transform.SetParent(galleryPanel.transform, false);
        var emptyRT = emptyGO.AddComponent<RectTransform>();
        StretchFull(emptyRT);
        var emptyTMP = emptyGO.AddComponent<TMPro.TextMeshProUGUI>();
        HebrewText.SetText(emptyTMP, "\u05E2\u05D3\u05D9\u05D9\u05DF \u05D0\u05D9\u05DF \u05E6\u05D9\u05D5\u05E8\u05D9\u05DD");
        emptyTMP.fontSize = 32;
        emptyTMP.color = HexColor("#A0A0A0");
        emptyTMP.alignment = TMPro.TextAlignmentOptions.Center;
        emptyTMP.raycastTarget = false;

        // Wire up WorldEasel component
        var worldEasel = easelGO.AddComponent<WorldEasel>();
        worldEasel.overlayRoot = galleryOverlay;
        worldEasel.dimBackground = dimBgImg;
        worldEasel.panelRT = galleryPanelRT;
        worldEasel.gridContainer = gridContentRT;
        worldEasel.closeButton = closeBtn;
        worldEasel.emptyText = emptyGO;
        worldEasel.roundedRectSprite = roundedRectSprite;
        worldEasel.newDrawingButton = newDrawBtn;
        worldEasel.gameDatabase = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/Games/GameDatabase.asset");

        // ── Environment Controller ──
        var envComponent = canvasGO.AddComponent<WorldEnvironment>();
        envComponent.skyBackground = skyBgGO.GetComponent<Image>();
        envComponent.hillsLargeLayer = hillsLargeGO.GetComponent<Image>();
        envComponent.hillsLayer = hillsGO.GetComponent<Image>();
        envComponent.groundBackLayer = groundBackGO.GetComponent<Image>();
        envComponent.groundFrontLayer = groundFrontGO.GetComponent<Image>();
        envComponent.sunRT = sunRT;
        envComponent.moonRT = moonRT;
        envComponent.sunImage = sunImg;
        envComponent.moonImage = moonImg;
        envComponent.sunGlow = sunGlowImg;
        envComponent.starsContainer = starsRT;
        // ── Cloud System ──
        var cloudSystemComp = canvasGO.AddComponent<WorldCloudSystem>();
        cloudSystemComp.skyArea = skyAreaRT;

        // Parent area button now in header (parentDashBtn above)

        // ── World Controller ──
        var controller = canvasGO.AddComponent<WorldController>();
        controller.gameDatabase = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/Games/GameDatabase.asset");
        controller.circleSprite = circleSprite;
        controller.worldContent = worldContentRT;
        controller.skyArea = skyAreaRT;
        controller.grassArea = grassAreaRT;
        controller.homeButton = homeGO.GetComponent<Button>();
        controller.gamesButton = gamesGO.GetComponent<Button>();
        // albumButton removed from header — now a world object
        controller.parentAreaButton = parentDashBtn.GetComponent<Button>();
        controller.environment = envComponent;
        controller.cloudSystem = cloudSystemComp;
        controller.headerTitleTMP = titleTMP;
        controller.sunRT = sunRT;
        controller.moonRT = moonRT;

        // ── Arrow Navigation Buttons (left/right screen switch) ──
        // Arrow buttons with icon sprites
        var arrowLeftSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Icons/arrowLeft.png");
        var arrowRightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Icons/arrowRight.png");

        var arrowLeftGO = new GameObject("ArrowLeft");
        arrowLeftGO.transform.SetParent(safeArea.transform, false);
        var alRT = arrowLeftGO.AddComponent<RectTransform>();
        alRT.anchorMin = new Vector2(0, 0.5f); alRT.anchorMax = new Vector2(0, 0.5f);
        alRT.pivot = new Vector2(0, 0.5f);
        alRT.anchoredPosition = new Vector2(16, 0); alRT.sizeDelta = new Vector2(160, 160);
        var alImg = arrowLeftGO.AddComponent<Image>();
        alImg.sprite = arrowLeftSprite; alImg.preserveAspect = true;
        alImg.color = new Color(1f, 1f, 1f, 0.5f); alImg.raycastTarget = true;
        var alBtn = arrowLeftGO.AddComponent<Button>(); alBtn.targetGraphic = alImg;

        var arrowRightGO = new GameObject("ArrowRight");
        arrowRightGO.transform.SetParent(safeArea.transform, false);
        var arRT = arrowRightGO.AddComponent<RectTransform>();
        arRT.anchorMin = new Vector2(1, 0.5f); arRT.anchorMax = new Vector2(1, 0.5f);
        arRT.pivot = new Vector2(1, 0.5f);
        arRT.anchoredPosition = new Vector2(-16, 0); arRT.sizeDelta = new Vector2(160, 160);
        var arImg = arrowRightGO.AddComponent<Image>();
        arImg.sprite = arrowRightSprite; arImg.preserveAspect = true;
        arImg.color = new Color(1f, 1f, 1f, 0.5f); arImg.raycastTarget = true;
        var arBtn = arrowRightGO.AddComponent<Button>(); arBtn.targetGraphic = arImg;

        controller.arrowLeftButton = alBtn;
        controller.arrowRightButton = arBtn;

        // ══════════════════════════════════════════════════════════
        // ── Reward Reveal ──
        var rewardReveal = canvasGO.AddComponent<RewardRevealController>();
        rewardReveal.grassArea = grassAreaRT;
        rewardReveal.skyArea = skyAreaRT;
        rewardReveal.giftSprite = LoadSprite("Assets/Art/Gift.png");
        rewardReveal.circleSprite = circleSprite;
        rewardReveal.gameDatabase = controller.gameDatabase;
        controller.rewardReveal = rewardReveal;

        // ── Collectible Album ──
        var album = canvasGO.AddComponent<CollectibleAlbumController>();
        album.circleSprite = circleSprite;
        album.roundedRect = roundedRectSprite;
        album.gameDatabase = controller.gameDatabase;
        album.achievementTabSprite = LoadSprite("Assets/Art/Toy Box.png");

        // Wire sticker category arrays from individual sprite sheets
        album.animalsStickers  = LoadStickerSheet("animalsStickers");
        album.lettersStickers  = LoadStickerSheet("lettersStickers");
        album.numbersStickers  = LoadStickerSheet("numbersStickers");
        album.balloonsStickers = LoadStickerSheet("ballonsStickers");
        album.aquariumStickers = LoadStickerSheet("aquatiumStickers");
        album.carsStickers     = LoadStickerSheet("carsStickers");
        album.foodStickers     = LoadStickerSheet("foodStickers");
        album.artStickers      = LoadStickerSheet("artStickers");
        album.natureStickers   = LoadStickerSheet("natureStickers");

        // Wire nature stickers to sticker tree (general/nature category)
        var stCtrl = stickerTreeGO.GetComponent<StickerTreeController>();
        if (stCtrl != null) stCtrl.stickerSprites = album.natureStickers;

        // ── Input Handler ──
        var inputHandler = canvasGO.AddComponent<WorldInputHandler>();
        inputHandler.worldContent = worldContentRT;
        inputHandler.viewport = viewportRT;
        inputHandler.environment = envComponent;
        inputHandler.sunRT = sunRT;
        inputHandler.moonRT = moonRT;

        // Load tutorial hand Tap sprites for inactivity hint
        var tapGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Tutorial Hand/Tap" });
        var tapSprites = tapGuids
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Distinct()
            .OrderBy(p => p)
            .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p))
            .Where(s => s != null)
            .ToArray();
        inputHandler.hintHandFrames = tapSprites;

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

    /// <summary>
    /// Create a prop with proportional X anchor and absolute Y position.
    /// X adapts to any world width; Y is relative to parent bottom.
    /// </summary>
    private static void CreatePropAnchored(Transform parent, string name, Sprite sprite,
        WorldProp.PropType type, float anchorX, float posY, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchorX, 0);
        rt.anchorMax = new Vector2(anchorX, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, posY);
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
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.selectedColor = Color.white;
        btn.colors = colors;
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

    private static Sprite LoadSpriteFromSheet(string sheetPath, string spriteName)
    {
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
        if (allAssets != null)
            foreach (var asset in allAssets)
                if (asset is Sprite spr && spr.name == spriteName) return spr;
        return null;
    }

    private static Sprite[] LoadStickerSheet(string textureName)
    {
        string path = $"Assets/Art/Stickers/{textureName}.png";
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (allAssets == null) return new Sprite[0];

        var sprites = new List<Sprite>();
        foreach (var asset in allAssets)
            if (asset is Sprite spr) sprites.Add(spr);

        // Sort by position in sprite sheet: top-to-bottom rows, left-to-right within row
        sprites.Sort((a, b) =>
        {
            // Higher y = higher row (top of texture). Compare descending.
            int rowCmp = b.rect.y.CompareTo(a.rect.y);
            if (rowCmp != 0) return rowCmp;
            // Same row: lower x = further left. Compare ascending.
            return a.rect.x.CompareTo(b.rect.x);
        });
        return sprites.ToArray();
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
