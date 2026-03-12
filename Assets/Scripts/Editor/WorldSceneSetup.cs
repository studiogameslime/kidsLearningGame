using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the WorldScene and WorldAnimalData asset.
/// Extracts sprite frames from each animal's .anim clips for UI-based animation.
/// Also creates the base AnimalBase AnimatorController (fixes broken override references).
/// </summary>
public class WorldSceneSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1080, 1920);

    private static readonly Color SkyBottom = HexColor("#E0F0FF");
    private static readonly Color SkyTop = HexColor("#87CEEB");
    private static readonly Color GrassColor = HexColor("#7EC850");
    private static readonly Color TopBarColor = HexColor("#5BA84C");

    private const int TopBarHeight = 110;

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
    //  ANIMAL ANIMATION DATA
    // ─────────────────────────────────────────

    private static void BuildAnimalData()
    {
        // Create per-animal SOs in Resources/AnimalAnim for on-demand loading
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/AnimalAnim");

        int count = 0;
        foreach (var animalId in AllAnimals)
        {
            string animDir = $"Assets/Art/Animals/{animalId}/Animations";
            string idleClipPath = $"{animDir}/{animalId}Idle.anim";
            string floatingClipPath = $"{animDir}/{animalId}Floating.anim";
            string successClipPath = $"{animDir}/{animalId}Success.anim";

            // Handle Dog's lowercase idle
            if (animalId == "Dog")
                idleClipPath = $"{animDir}/dogIdle.anim";

            string assetPath = $"Assets/Resources/AnimalAnim/{animalId}.asset";
            // Delete and recreate to ensure script reference is valid
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);
            var data = ScriptableObject.CreateInstance<AnimalAnimData>();
            AssetDatabase.CreateAsset(data, assetPath);

            data.animalId = animalId;

            var idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(idleClipPath);
            var floatingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(floatingClipPath);
            var successClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(successClipPath);

            data.idleFrames = idleClip != null ? ExtractSprites(idleClip) : null;
            data.idleFps = idleClip != null ? idleClip.frameRate : 30f;
            data.floatingFrames = floatingClip != null ? ExtractSprites(floatingClip) : null;
            data.floatingFps = floatingClip != null ? floatingClip.frameRate : 30f;
            data.successFrames = successClip != null ? ExtractSprites(successClip) : null;
            data.successFps = successClip != null ? successClip.frameRate : 30f;

            EditorUtility.SetDirty(data);
            count++;
        }

        Debug.Log($"AnimalAnimData: built {count} per-animal assets in Resources/AnimalAnim/.");
    }

    private static Sprite[] ExtractSprites(AnimationClip clip)
    {
        var sprites = new List<Sprite>();
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);

        foreach (var binding in bindings)
        {
            var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            foreach (var kf in keyframes)
            {
                if (kf.value is Sprite sprite)
                    sprites.Add(sprite);
            }
        }

        return sprites.ToArray();
    }

    // ─────────────────────────────────────────
    //  BASE ANIMATOR CONTROLLER
    // ─────────────────────────────────────────

    private static void CreateBaseAnimatorController()
    {
        EnsureFolder("Assets/Art/Animals");
        string controllerPath = "Assets/Art/Animals/AnimalBase.controller";

        // Check if it already exists
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (existing != null) return;

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        // Add parameters
        controller.AddParameter("Success", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsFloating", AnimatorControllerParameterType.Bool);

        // Get the base layer state machine
        var rootStateMachine = controller.layers[0].stateMachine;

        // Create placeholder clips for the base (overrides will replace these)
        var idleClip = new AnimationClip { name = "BaseIdle" };
        var floatingClip = new AnimationClip { name = "BaseFloating" };
        var successClip = new AnimationClip { name = "BaseSuccess" };

        AssetDatabase.AddObjectToAsset(idleClip, controllerPath);
        AssetDatabase.AddObjectToAsset(floatingClip, controllerPath);
        AssetDatabase.AddObjectToAsset(successClip, controllerPath);

        // Create states
        var idleState = rootStateMachine.AddState("Idle", new Vector3(300, 0, 0));
        idleState.motion = idleClip;
        rootStateMachine.defaultState = idleState;

        var floatingState = rootStateMachine.AddState("Floating", new Vector3(300, 100, 0));
        floatingState.motion = floatingClip;

        var successState = rootStateMachine.AddState("Success", new Vector3(550, 0, 0));
        successState.motion = successClip;

        // Idle → Floating (when IsFloating = true)
        var toFloating = idleState.AddTransition(floatingState);
        toFloating.AddCondition(AnimatorConditionMode.If, 0, "IsFloating");
        toFloating.hasExitTime = false;
        toFloating.duration = 0f;

        // Floating → Idle (when IsFloating = false)
        var toIdleFromFloat = floatingState.AddTransition(idleState);
        toIdleFromFloat.AddCondition(AnimatorConditionMode.IfNot, 0, "IsFloating");
        toIdleFromFloat.hasExitTime = false;
        toIdleFromFloat.duration = 0f;

        // Idle → Success (on trigger)
        var toSuccess = idleState.AddTransition(successState);
        toSuccess.AddCondition(AnimatorConditionMode.If, 0, "Success");
        toSuccess.hasExitTime = false;
        toSuccess.duration = 0f;

        // Success → Idle (after clip finishes)
        var toIdleFromSuccess = successState.AddTransition(idleState);
        toIdleFromSuccess.hasExitTime = true;
        toIdleFromSuccess.exitTime = 1f;
        toIdleFromSuccess.duration = 0f;

        EditorUtility.SetDirty(controller);

        // Now update all override controllers to point to this new base
        // (They currently reference a missing GUID)
        string newGuid = AssetDatabase.AssetPathToGUID(controllerPath);
        foreach (var animalId in AllAnimals)
        {
            string overridePath = $"Assets/Art/Animals/{animalId}/Animations/{animalId}.overrideController";
            var overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            if (overrideController != null)
            {
                overrideController.runtimeAnimatorController = controller;

                // Re-map the clips
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                overrideController.GetOverrides(overrides);

                // Load the animal-specific clips
                string animDir = $"Assets/Art/Animals/{animalId}/Animations";
                string idlePath = animalId == "Dog"
                    ? $"{animDir}/dogIdle.anim"
                    : $"{animDir}/{animalId}Idle.anim";
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

        Debug.Log("AnimalBase.controller created and all override controllers updated.");
    }

    // ─────────────────────────────────────────
    //  SCENE
    // ─────────────────────────────────────────

    private static void CreateScene()
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");

        // Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SkyTop;
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
        titleTMP.text = "My World";
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

        // Profile avatar (top-right)
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

        // ── World Content ──
        var worldContent = new GameObject("WorldContent");
        worldContent.transform.SetParent(viewport.transform, false);
        var worldContentRT = worldContent.AddComponent<RectTransform>();
        worldContentRT.anchorMin = new Vector2(0, 0);
        worldContentRT.anchorMax = new Vector2(0, 1);
        worldContentRT.pivot = new Vector2(0, 0.5f);
        worldContentRT.sizeDelta = new Vector2(2000, 0);
        worldContentRT.anchoredPosition = Vector2.zero;

        // ── Sky Area (top 60%) ──
        var skyAreaGO = CreateStretchImage(worldContent.transform, "SkyArea", SkyBottom);
        var skyAreaRT = skyAreaGO.GetComponent<RectTransform>();
        skyAreaRT.anchorMin = new Vector2(0, 0.4f);
        skyAreaRT.anchorMax = new Vector2(1, 1);
        skyAreaRT.offsetMin = new Vector2(0, 0);
        skyAreaRT.offsetMax = Vector2.zero;
        skyAreaGO.GetComponent<Image>().raycastTarget = false;

        // ── Grass Area (bottom 40%) ──
        var grassAreaGO = CreateStretchImage(worldContent.transform, "GrassArea", GrassColor);
        var grassAreaRT = grassAreaGO.GetComponent<RectTransform>();
        grassAreaRT.anchorMin = new Vector2(0, 0);
        grassAreaRT.anchorMax = new Vector2(1, 0.4f);
        grassAreaRT.offsetMin = Vector2.zero;
        grassAreaRT.offsetMax = Vector2.zero;
        grassAreaGO.GetComponent<Image>().raycastTarget = false;

        // ── Controller ──
        var controller = canvasGO.AddComponent<WorldController>();
        controller.gameDatabase = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/Games/GameDatabase.asset");
        controller.circleSprite = circleSprite;
        controller.worldContent = worldContentRT;
        controller.skyArea = skyAreaRT;
        controller.grassArea = grassAreaRT;
        controller.homeButton = homeGO.GetComponent<Button>();
        controller.profileAvatar = profileBtnImg;
        controller.profileInitial = profileInitialTMP;

        // ── Input Handler ──
        var inputHandler = canvasGO.AddComponent<WorldInputHandler>();
        inputHandler.worldContent = worldContentRT;
        inputHandler.viewport = viewportRT;

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/WorldScene.unity");
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

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
