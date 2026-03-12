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
/// Builds the ProfileSelection and ProfileCreation scenes.
/// Run via Tools > Kids Learning Game > Setup Profile Scenes.
/// </summary>
public class ProfileSceneSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1080, 1920);

    // Colors
    private static readonly Color BgColor = HexColor("#F5F0EB");
    private static readonly Color AccentColor = HexColor("#90CAF9");
    private static readonly Color DarkText = HexColor("#4A4A4A");
    private static readonly Color LightText = Color.white;

    private static readonly string[] AvatarColors = {
        "#EF9A9A", "#F48FB1", "#CE93D8", "#B39DDB",
        "#90CAF9", "#80DEEA", "#80CBC4", "#A5D6A7",
        "#FFF59D", "#FFCC80", "#FFAB91", "#BCAAA4"
    };

    [MenuItem("Tools/Kids Learning Game/Setup Profile Scenes")]
    public static void RunSetup()
    {
        if (!EditorUtility.DisplayDialog(
            "Profile Scenes Setup",
            "This will create/overwrite:\n• ProfileSelection scene\n• ProfileCreation scene\n• ProfileCard prefab\n\nContinue?",
            "Build", "Cancel"))
            return;

        RunSetupSilent();
        EditorSceneManager.OpenScene("Assets/Scenes/ProfileSelection.unity");
        EditorUtility.DisplayDialog("Done!", "Profile scenes built successfully.\nPress Play to test!", "OK");
    }

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Profile Setup", "Loading sprites…", 0.1f);
            var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
            var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");

            if (roundedRect == null || circleSprite == null)
            {
                Debug.LogError("Required sprites not found. Run 'Setup Project' first.");
                return;
            }

            EditorUtility.DisplayProgressBar("Profile Setup", "Creating ProfileCard prefab…", 0.2f);
            var cardPrefab = CreateProfileCardPrefab(circleSprite, roundedRect);

            EditorUtility.DisplayProgressBar("Profile Setup", "Building ProfileSelection scene…", 0.4f);
            CreateProfileSelectionScene(cardPrefab, circleSprite, roundedRect);

            EditorUtility.DisplayProgressBar("Profile Setup", "Building ProfileCreation scene…", 0.7f);
            CreateProfileCreationScene(circleSprite, roundedRect);

            EditorUtility.DisplayProgressBar("Profile Setup", "Updating build settings…", 0.9f);
            AddScenesToBuild();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void AddScenesToBuild()
    {
        var existing = EditorBuildSettings.scenes;
        var sceneList = new List<EditorBuildSettingsScene>(existing);

        string[] profileScenes = {
            "Assets/Scenes/ProfileSelection.unity",
            "Assets/Scenes/ProfileCreation.unity"
        };

        foreach (var scenePath in profileScenes)
        {
            bool found = false;
            foreach (var s in sceneList)
            {
                if (s.path == scenePath) { found = true; s.enabled = true; break; }
            }
            if (!found)
                sceneList.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        }

        // Ensure ProfileSelection is at index 0
        for (int i = 0; i < sceneList.Count; i++)
        {
            if (sceneList[i].path == "Assets/Scenes/ProfileSelection.unity" && i != 0)
            {
                var scene = sceneList[i];
                sceneList.RemoveAt(i);
                sceneList.Insert(0, scene);
                break;
            }
        }

        EditorBuildSettings.scenes = sceneList.ToArray();
    }

    // ─────────────────────────────────────────
    //  PROFILE CARD PREFAB
    // ─────────────────────────────────────────

    private static GameObject CreateProfileCardPrefab(Sprite circle, Sprite roundedRect)
    {
        EnsureFolder("Assets/Prefabs/UI");

        var root = new GameObject("ProfileCard");
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(480, 600);

        // Invisible button hit area
        var hitImg = root.AddComponent<Image>();
        hitImg.color = new Color(1, 1, 1, 0f);
        hitImg.raycastTarget = true;

        var btn = root.AddComponent<Button>();
        btn.targetGraphic = hitImg;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.highlightedColor = Color.white;
        btn.colors = colors;

        // Avatar circle (large, centered at top)
        var avatarGO = new GameObject("AvatarCircle");
        avatarGO.transform.SetParent(root.transform, false);
        var avatarRT = avatarGO.AddComponent<RectTransform>();
        avatarRT.anchorMin = new Vector2(0.5f, 1f);
        avatarRT.anchorMax = new Vector2(0.5f, 1f);
        avatarRT.pivot = new Vector2(0.5f, 1f);
        avatarRT.anchoredPosition = new Vector2(0, -20);
        avatarRT.sizeDelta = new Vector2(360, 360);
        var avatarImg = avatarGO.AddComponent<Image>();
        avatarImg.sprite = circle;
        avatarImg.color = AccentColor;
        avatarImg.raycastTarget = false;

        // Shadow on avatar
        var shadow = avatarGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.2f);
        shadow.effectDistance = new Vector2(2, -3);

        // Avatar image (for custom photos, hidden by default — fills entire circle via mask)
        var avatarPhotoGO = new GameObject("AvatarImage");
        avatarPhotoGO.transform.SetParent(avatarGO.transform, false);
        var avatarPhotoRT = avatarPhotoGO.AddComponent<RectTransform>();
        StretchFull(avatarPhotoRT);
        var avatarPhotoImg = avatarPhotoGO.AddComponent<Image>();
        avatarPhotoImg.preserveAspect = false;
        avatarPhotoImg.raycastTarget = false;
        avatarPhotoGO.SetActive(false);

        // Mask on avatar for photo
        avatarGO.AddComponent<Mask>().showMaskGraphic = true;

        // Initial letter (centered on avatar)
        var initialGO = new GameObject("Initial");
        initialGO.transform.SetParent(avatarGO.transform, false);
        var initialRT = initialGO.AddComponent<RectTransform>();
        StretchFull(initialRT);
        var initialTMP = initialGO.AddComponent<TextMeshProUGUI>();
        initialTMP.text = "?";
        initialTMP.fontSize = 144;
        initialTMP.fontStyle = FontStyles.Bold;
        initialTMP.color = Color.white;
        initialTMP.alignment = TextAlignmentOptions.Center;
        initialTMP.raycastTarget = false;

        // Name text below avatar
        var nameGO = new GameObject("NameText");
        nameGO.transform.SetParent(root.transform, false);
        var nameRT = nameGO.AddComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0, 0);
        nameRT.anchorMax = new Vector2(1, 0);
        nameRT.pivot = new Vector2(0.5f, 0);
        nameRT.anchoredPosition = new Vector2(0, 20);
        nameRT.sizeDelta = new Vector2(0, 160);
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "Name";
        nameTMP.fontSize = 56;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = DarkText;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.enableWordWrapping = false;
        nameTMP.overflowMode = TextOverflowModes.Ellipsis;
        nameTMP.raycastTarget = false;

        // Wire ProfileCardView
        var cardView = root.AddComponent<ProfileCardView>();
        cardView.avatarCircle = avatarImg;
        cardView.avatarImage = avatarPhotoImg;
        cardView.initialText = initialTMP;
        cardView.nameText = nameTMP;
        cardView.button = btn;

        // Save prefab
        string path = "Assets/Prefabs/UI/ProfileCard.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        return prefab;
    }

    // ─────────────────────────────────────────
    //  PROFILE SELECTION SCENE
    // ─────────────────────────────────────────

    private static void CreateProfileSelectionScene(GameObject cardPrefab, Sprite circle, Sprite roundedRect)
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGO = CreateCamera();

        // EventSystem
        CreateEventSystem();

        // Canvas
        var canvasGO = new GameObject("ProfileCanvas");
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

        // ── Title area (top portion) ──
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(safeArea.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.88f);
        titleRT.anchorMax = new Vector2(1, 0.97f);
        titleRT.offsetMin = new Vector2(40, 0);
        titleRT.offsetMax = new Vector2(-40, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "\u05DE\u05D9 \u05DE\u05E9\u05D7\u05E7?"; // מי משחק? (Who's playing?)
        titleTMP.fontSize = 64;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = DarkText;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.isRightToLeftText = true;
        titleTMP.raycastTarget = false;

        // ── Profile grid area (centered) ──
        var gridArea = new GameObject("ProfileGrid");
        gridArea.transform.SetParent(safeArea.transform, false);
        var gridAreaRT = gridArea.AddComponent<RectTransform>();
        gridAreaRT.anchorMin = new Vector2(0, 0.10f);
        gridAreaRT.anchorMax = new Vector2(1, 0.85f);
        gridAreaRT.offsetMin = new Vector2(40, 0);
        gridAreaRT.offsetMax = new Vector2(-40, 0);

        // Grid layout
        var grid = gridArea.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(480, 600);
        grid.spacing = new Vector2(32, 32);
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.padding = new RectOffset(20, 20, 20, 20);

        // ── Add Profile Card (built into scene, not instantiated) ──
        var addCardGO = new GameObject("AddProfileCard");
        addCardGO.transform.SetParent(gridArea.transform, false);
        var addCardRT = addCardGO.AddComponent<RectTransform>();
        addCardRT.sizeDelta = new Vector2(480, 600);

        // Hit area
        var addHitImg = addCardGO.AddComponent<Image>();
        addHitImg.color = new Color(1, 1, 1, 0f);
        addHitImg.raycastTarget = true;

        var addBtn = addCardGO.AddComponent<Button>();
        addBtn.targetGraphic = addHitImg;

        // Plus circle
        var plusCircleGO = new GameObject("PlusCircle");
        plusCircleGO.transform.SetParent(addCardGO.transform, false);
        var plusCircleRT = plusCircleGO.AddComponent<RectTransform>();
        plusCircleRT.anchorMin = new Vector2(0.5f, 1f);
        plusCircleRT.anchorMax = new Vector2(0.5f, 1f);
        plusCircleRT.pivot = new Vector2(0.5f, 1f);
        plusCircleRT.anchoredPosition = new Vector2(0, -20);
        plusCircleRT.sizeDelta = new Vector2(360, 360);
        var plusCircleImg = plusCircleGO.AddComponent<Image>();
        plusCircleImg.sprite = circle;
        plusCircleImg.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        plusCircleImg.raycastTarget = false;

        // Dashed border effect
        var plusBorder = plusCircleGO.AddComponent<Outline>();
        plusBorder.effectColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        plusBorder.effectDistance = new Vector2(2, 2);

        // Plus text
        var plusTextGO = new GameObject("PlusText");
        plusTextGO.transform.SetParent(plusCircleGO.transform, false);
        var plusTextRT = plusTextGO.AddComponent<RectTransform>();
        StretchFull(plusTextRT);
        var plusTMP = plusTextGO.AddComponent<TextMeshProUGUI>();
        plusTMP.text = "+";
        plusTMP.fontSize = 160;
        plusTMP.color = HexColor("#999999");
        plusTMP.alignment = TextAlignmentOptions.Center;
        plusTMP.raycastTarget = false;

        // "Add" label
        var addLabelGO = new GameObject("AddLabel");
        addLabelGO.transform.SetParent(addCardGO.transform, false);
        var addLabelRT = addLabelGO.AddComponent<RectTransform>();
        addLabelRT.anchorMin = new Vector2(0, 0);
        addLabelRT.anchorMax = new Vector2(1, 0);
        addLabelRT.pivot = new Vector2(0.5f, 0);
        addLabelRT.anchoredPosition = new Vector2(0, 20);
        addLabelRT.sizeDelta = new Vector2(0, 160);
        var addLabelTMP = addLabelGO.AddComponent<TextMeshProUGUI>();
        addLabelTMP.text = "\u05D4\u05D5\u05E1\u05E4\u05D4"; // הוספה (Add)
        addLabelTMP.fontSize = 56;
        addLabelTMP.color = HexColor("#999999");
        addLabelTMP.alignment = TextAlignmentOptions.Center;
        addLabelTMP.isRightToLeftText = true;
        addLabelTMP.raycastTarget = false;

        // ── Controller ──
        var controller = canvasGO.AddComponent<ProfileSelectionController>();
        controller.profileContainer = gridAreaRT;
        controller.profileCardPrefab = cardPrefab;
        controller.addProfileCard = addCardGO;
        controller.titleText = titleTMP;

        // "Who playing?" sound
        var whoPlayingClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/On boarding/Who playing.mp3");
        if (whoPlayingClip != null)
            controller.whoPlayingSound = whoPlayingClip;
        else
            Debug.LogWarning("Onboarding sound not found: Assets/Sounds/On boarding/Who playing.mp3");

        // Wire add button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            addBtn.onClick, controller.OnAddProfilePressed);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ProfileSelection.unity");
    }

    // ─────────────────────────────────────────
    //  PROFILE CREATION SCENE
    // ─────────────────────────────────────────

    private static void CreateProfileCreationScene(Sprite circle, Sprite roundedRect)
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateEventSystem();

        var canvasGO = new GameObject("CreationCanvas");
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

        // Back button (top-left, home icon)
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var backBtnGO = CreateIconButton(safeArea.transform, "BackButton", homeIcon,
            new Vector2(16, -15), new Vector2(0, 1), new Vector2(0, 1), new Vector2(90, 90));

        // ── Content area ──
        var contentArea = new GameObject("ContentArea");
        contentArea.transform.SetParent(safeArea.transform, false);
        var contentRT = contentArea.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.05f, 0.05f);
        contentRT.anchorMax = new Vector2(0.95f, 0.90f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        // ── STEP 0: Greeting ──
        var stepGreeting = CreateStepPanel(contentArea.transform, "StepGreeting");
        var greetTitle = CreateText(stepGreeting.transform, "GreetTitle",
            "\u05E9\u05DC\u05D5\u05DD!", 72, DarkText, // !שלום (Hello!)
            new Vector2(0, 0.55f), new Vector2(1, 0.85f), true);
        var greetSub = CreateText(stepGreeting.transform, "GreetSub",
            "\u05D1\u05D5\u05D0\u05D5 \u05E0\u05DB\u05D9\u05E8 \u05D0\u05D5\u05EA\u05DA!", 40, HexColor("#777777"), // !בואו נכיר אותך
            new Vector2(0, 0.40f), new Vector2(1, 0.55f), true);
        var greetNextBtn = CreateBigButton(stepGreeting.transform, "NextButton",
            "\u05D9\u05D0\u05DC\u05DC\u05D4!", AccentColor, // !יאללה
            new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.30f), roundedRect, true);

        // ── STEP 1: Record Name ──
        var stepRecord = CreateStepPanel(contentArea.transform, "StepRecordName");
        CreateText(stepRecord.transform, "RecordTitle",
            "\u05D0\u05DE\u05D5\u05E8 \u05D0\u05EA \u05D4\u05E9\u05DD \u05E9\u05DC\u05DA", 48, DarkText, // אמור את השם שלך
            new Vector2(0, 0.70f), new Vector2(1, 0.90f), true);

        // Record indicator (red circle)
        var recIndicator = new GameObject("RecordIndicator");
        recIndicator.transform.SetParent(stepRecord.transform, false);
        var recIndRT = recIndicator.AddComponent<RectTransform>();
        recIndRT.anchorMin = new Vector2(0.5f, 0.55f);
        recIndRT.anchorMax = new Vector2(0.5f, 0.55f);
        recIndRT.sizeDelta = new Vector2(30, 30);
        var recIndImg = recIndicator.AddComponent<Image>();
        recIndImg.sprite = circle;
        recIndImg.color = Color.red;
        recIndicator.SetActive(false);

        // Record button (big circle with microphone icon) — 3x size
        var micIcon = LoadSprite("Assets/Art/Microphone.png");
        var recordBtn = CreateSpriteCircleButton(stepRecord.transform, "RecordButton", circle, micIcon,
            HexColor("#EF5350"), new Vector2(0.5f, 0.45f), new Vector2(360, 360));

        // Stop button (uses stop icon) — 3x size
        var stopIcon = LoadSprite("Assets/Art/Icons/stop.png");
        var stopBtn = CreateSpriteCircleButton(stepRecord.transform, "StopRecordButton", circle, stopIcon,
            HexColor("#EF5350"), new Vector2(0.5f, 0.45f), new Vector2(360, 360));
        stopBtn.SetActive(false);

        // Play button (sound icon) — 3x size
        var soundIcon = LoadSprite("Assets/Art/Sound.png");
        var playBtn = CreateSpriteCircleButton(stepRecord.transform, "PlayRecordButton", circle, soundIcon,
            HexColor("#4CAF50"), new Vector2(0.5f, 0.25f), new Vector2(240, 240));
        playBtn.SetActive(false);

        // Skip button
        var skipBtn = CreateBigButton(stepRecord.transform, "SkipButton",
            "\u05D3\u05DC\u05D2", HexColor("#BDBDBD"), // דלג (Skip)
            new Vector2(0.30f, 0.05f), new Vector2(0.70f, 0.15f), roundedRect, true);

        // Next button (shown after recording)
        var recNextBtn = CreateBigButton(stepRecord.transform, "RecordNextButton",
            "\u05D4\u05DE\u05E9\u05DA", AccentColor, // המשך (Continue)
            new Vector2(0.15f, 0.05f), new Vector2(0.85f, 0.18f), roundedRect, true);
        recNextBtn.SetActive(false);

        // ── STEP 2: Type Name ──
        var stepName = CreateStepPanel(contentArea.transform, "StepTypeName");
        CreateText(stepName.transform, "NameTitle",
            "\u05DE\u05D4 \u05D4\u05E9\u05DD \u05E9\u05DC\u05DA?", 48, DarkText, // ?מה השם שלך
            new Vector2(0, 0.70f), new Vector2(1, 0.90f), true);

        // Name input field
        var inputGO = new GameObject("NameInput");
        inputGO.transform.SetParent(stepName.transform, false);
        var inputRT = inputGO.AddComponent<RectTransform>();
        inputRT.anchorMin = new Vector2(0.1f, 0.45f);
        inputRT.anchorMax = new Vector2(0.9f, 0.60f);
        inputRT.offsetMin = Vector2.zero;
        inputRT.offsetMax = Vector2.zero;

        // Input background
        var inputBgImg = inputGO.AddComponent<Image>();
        inputBgImg.sprite = roundedRect;
        inputBgImg.type = Image.Type.Sliced;
        inputBgImg.color = Color.white;

        // Input shadow
        var inputShadow = inputGO.AddComponent<Shadow>();
        inputShadow.effectColor = new Color(0, 0, 0, 0.1f);
        inputShadow.effectDistance = new Vector2(0, -2);

        // Text area
        var textAreaGO = new GameObject("Text Area");
        textAreaGO.transform.SetParent(inputGO.transform, false);
        var textAreaRT = textAreaGO.AddComponent<RectTransform>();
        StretchFull(textAreaRT);
        textAreaRT.offsetMin = new Vector2(20, 5);
        textAreaRT.offsetMax = new Vector2(-20, -5);

        // Input text
        var inputTextGO = new GameObject("Text");
        inputTextGO.transform.SetParent(textAreaGO.transform, false);
        var inputTextRT = inputTextGO.AddComponent<RectTransform>();
        StretchFull(inputTextRT);
        var inputTMP = inputTextGO.AddComponent<TextMeshProUGUI>();
        inputTMP.fontSize = 40;
        inputTMP.color = DarkText;
        inputTMP.alignment = TextAlignmentOptions.Center;
        inputTMP.isRightToLeftText = true;

        // Placeholder
        var placeholderGO = new GameObject("Placeholder");
        placeholderGO.transform.SetParent(textAreaGO.transform, false);
        var placeholderRT = placeholderGO.AddComponent<RectTransform>();
        StretchFull(placeholderRT);
        var placeholderTMP = placeholderGO.AddComponent<TextMeshProUGUI>();
        placeholderTMP.text = "\u05DB\u05EA\u05D1\u05D5 \u05DB\u05D0\u05DF..."; // ...כתבו כאן
        placeholderTMP.fontSize = 40;
        placeholderTMP.fontStyle = FontStyles.Italic;
        placeholderTMP.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        placeholderTMP.alignment = TextAlignmentOptions.Center;
        placeholderTMP.isRightToLeftText = true;

        // TMP_InputField component
        var inputField = inputGO.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRT;
        inputField.textComponent = inputTMP;
        inputField.placeholder = placeholderTMP;
        inputField.fontAsset = inputTMP.font;
        inputField.pointSize = 40;
        inputField.characterLimit = 20;

        var nameNextBtn = CreateBigButton(stepName.transform, "NameNextButton",
            "\u05D4\u05DE\u05E9\u05DA", AccentColor, // המשך
            new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.30f), roundedRect, true);
        nameNextBtn.GetComponent<Button>().interactable = false;

        // ── STEP 3: Choose Age ──
        var stepAge = CreateStepPanel(contentArea.transform, "StepChooseAge");
        CreateText(stepAge.transform, "AgeTitle",
            "\u05D1\u05DF/\u05D1\u05EA \u05DB\u05DE\u05D4 \u05D0\u05EA/\u05D4?", 48, DarkText, // ?בן/בת כמה את/ה
            new Vector2(0, 0.75f), new Vector2(1, 0.92f), true);

        // Age buttons (1-8 in a 4x2 grid)
        var ageGridGO = new GameObject("AgeGrid");
        ageGridGO.transform.SetParent(stepAge.transform, false);
        var ageGridRT = ageGridGO.AddComponent<RectTransform>();
        ageGridRT.anchorMin = new Vector2(0.05f, 0.30f);
        ageGridRT.anchorMax = new Vector2(0.95f, 0.75f);
        ageGridRT.offsetMin = Vector2.zero;
        ageGridRT.offsetMax = Vector2.zero;
        var ageGrid = ageGridGO.AddComponent<GridLayoutGroup>();
        ageGrid.cellSize = new Vector2(140, 140);
        ageGrid.spacing = new Vector2(24, 24);
        ageGrid.childAlignment = TextAnchor.MiddleCenter;
        ageGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        ageGrid.constraintCount = 4;

        var ageButtons = new Button[8];
        for (int i = 0; i < 8; i++)
        {
            var ageBtnGO = new GameObject($"Age{i + 1}");
            ageBtnGO.transform.SetParent(ageGridGO.transform, false);
            var ageBtnImg = ageBtnGO.AddComponent<Image>();
            ageBtnImg.sprite = circle;
            ageBtnImg.color = HexColor("#E0E0E0");

            var ageBtnShadow = ageBtnGO.AddComponent<Shadow>();
            ageBtnShadow.effectColor = new Color(0, 0, 0, 0.15f);
            ageBtnShadow.effectDistance = new Vector2(1, -2);

            var ageBtnComp = ageBtnGO.AddComponent<Button>();
            ageBtnComp.targetGraphic = ageBtnImg;
            ageButtons[i] = ageBtnComp;

            var ageLabelGO = new GameObject("Label");
            ageLabelGO.transform.SetParent(ageBtnGO.transform, false);
            var ageLabelRT = ageLabelGO.AddComponent<RectTransform>();
            StretchFull(ageLabelRT);
            var ageLabelTMP = ageLabelGO.AddComponent<TextMeshProUGUI>();
            ageLabelTMP.text = (i + 1).ToString();
            ageLabelTMP.fontSize = 52;
            ageLabelTMP.fontStyle = FontStyles.Bold;
            ageLabelTMP.color = DarkText;
            ageLabelTMP.alignment = TextAlignmentOptions.Center;
            ageLabelTMP.raycastTarget = false;
        }

        var ageNextBtn = CreateBigButton(stepAge.transform, "AgeNextButton",
            "\u05D4\u05DE\u05E9\u05DA", AccentColor,
            new Vector2(0.15f, 0.08f), new Vector2(0.85f, 0.22f), roundedRect, true);
        ageNextBtn.GetComponent<Button>().interactable = false;

        // ── STEP 4: Choose Color ──
        var stepColor = CreateStepPanel(contentArea.transform, "StepChooseColor");
        CreateText(stepColor.transform, "ColorTitle",
            "\u05D1\u05D7\u05E8\u05D5 \u05E6\u05D1\u05E2", 48, DarkText, // בחרו צבע
            new Vector2(0, 0.88f), new Vector2(1, 0.98f), true);

        // ── Card Preview (mimics profile selection card) ──
        var cardPreviewGO = new GameObject("CardPreview");
        cardPreviewGO.transform.SetParent(stepColor.transform, false);
        var cardPreviewRT = cardPreviewGO.AddComponent<RectTransform>();
        cardPreviewRT.anchorMin = new Vector2(0.5f, 0.58f);
        cardPreviewRT.anchorMax = new Vector2(0.5f, 0.58f);
        cardPreviewRT.sizeDelta = new Vector2(260, 340);

        // Avatar circle (large, centered at top of card)
        var colorPreviewGO = new GameObject("AvatarCircle");
        colorPreviewGO.transform.SetParent(cardPreviewGO.transform, false);
        var colorPreviewRT = colorPreviewGO.AddComponent<RectTransform>();
        colorPreviewRT.anchorMin = new Vector2(0.5f, 1f);
        colorPreviewRT.anchorMax = new Vector2(0.5f, 1f);
        colorPreviewRT.pivot = new Vector2(0.5f, 1f);
        colorPreviewRT.anchoredPosition = new Vector2(0, -5);
        colorPreviewRT.sizeDelta = new Vector2(220, 220);
        var colorPreviewImg = colorPreviewGO.AddComponent<Image>();
        colorPreviewImg.sprite = circle;
        colorPreviewImg.color = AccentColor;

        // Shadow on avatar
        var cpShadow = colorPreviewGO.AddComponent<Shadow>();
        cpShadow.effectColor = new Color(0, 0, 0, 0.2f);
        cpShadow.effectDistance = new Vector2(2, -3);

        // Mask for photo clipping
        colorPreviewGO.AddComponent<Mask>().showMaskGraphic = true;

        // Photo preview (hidden by default, shown when photo is picked)
        var colorPreviewPhotoGO = new GameObject("Photo");
        colorPreviewPhotoGO.transform.SetParent(colorPreviewGO.transform, false);
        var cpPhotoRT = colorPreviewPhotoGO.AddComponent<RectTransform>();
        StretchFull(cpPhotoRT);
        var cpPhotoImg = colorPreviewPhotoGO.AddComponent<Image>();
        cpPhotoImg.preserveAspect = false;
        cpPhotoImg.raycastTarget = false;
        colorPreviewPhotoGO.SetActive(false);

        // Initial letter
        var colorPreviewInitialGO = new GameObject("Initial");
        colorPreviewInitialGO.transform.SetParent(colorPreviewGO.transform, false);
        var cpInitRT = colorPreviewInitialGO.AddComponent<RectTransform>();
        StretchFull(cpInitRT);
        var cpInitTMP = colorPreviewInitialGO.AddComponent<TextMeshProUGUI>();
        cpInitTMP.text = "?";
        cpInitTMP.fontSize = 96;
        cpInitTMP.fontStyle = FontStyles.Bold;
        cpInitTMP.color = Color.white;
        cpInitTMP.alignment = TextAlignmentOptions.Center;
        cpInitTMP.raycastTarget = false;

        // Name text below avatar (mimics profile card name)
        var cpNameGO = new GameObject("PreviewName");
        cpNameGO.transform.SetParent(cardPreviewGO.transform, false);
        var cpNameRT = cpNameGO.AddComponent<RectTransform>();
        cpNameRT.anchorMin = new Vector2(0, 0);
        cpNameRT.anchorMax = new Vector2(1, 0);
        cpNameRT.pivot = new Vector2(0.5f, 0);
        cpNameRT.anchoredPosition = new Vector2(0, 5);
        cpNameRT.sizeDelta = new Vector2(0, 100);
        var cpNameTMP = cpNameGO.AddComponent<TextMeshProUGUI>();
        cpNameTMP.text = "";
        cpNameTMP.fontSize = 40;
        cpNameTMP.fontStyle = FontStyles.Bold;
        cpNameTMP.color = DarkText;
        cpNameTMP.alignment = TextAlignmentOptions.Center;
        cpNameTMP.enableWordWrapping = false;
        cpNameTMP.overflowMode = TextOverflowModes.Ellipsis;
        cpNameTMP.isRightToLeftText = true;
        cpNameTMP.raycastTarget = false;

        // Take Selfie button (below preview, above color grid)
        var cameraIcon = LoadSprite("Assets/Art/Gallery.png");
        var pickPhotoBtn = CreateIconLabelButton(stepColor.transform, "PickPhotoButton",
            cameraIcon, "\u05E6\u05DC\u05DE\u05D5 \u05E1\u05DC\u05E4\u05D9", HexColor("#78909C"), // צלמו סלפי
            new Vector2(0.10f, 0.42f), new Vector2(0.90f, 0.52f), roundedRect);

        // Color grid
        var colorGridGO = new GameObject("ColorGrid");
        colorGridGO.transform.SetParent(stepColor.transform, false);
        var colorGridRT = colorGridGO.AddComponent<RectTransform>();
        colorGridRT.anchorMin = new Vector2(0.05f, 0.18f);
        colorGridRT.anchorMax = new Vector2(0.95f, 0.42f);
        colorGridRT.offsetMin = Vector2.zero;
        colorGridRT.offsetMax = Vector2.zero;
        var colorGrid = colorGridGO.AddComponent<GridLayoutGroup>();
        colorGrid.cellSize = new Vector2(90, 90);
        colorGrid.spacing = new Vector2(16, 16);
        colorGrid.childAlignment = TextAnchor.MiddleCenter;
        colorGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        colorGrid.constraintCount = 4;

        var colorButtons = new Button[AvatarColors.Length];
        for (int i = 0; i < AvatarColors.Length; i++)
        {
            var colorBtnGO = new GameObject($"Color{i}");
            colorBtnGO.transform.SetParent(colorGridGO.transform, false);
            var colorBtnImg = colorBtnGO.AddComponent<Image>();
            colorBtnImg.sprite = circle;
            ColorUtility.TryParseHtmlString(AvatarColors[i], out Color c);
            colorBtnImg.color = c;

            var colorBtnComp = colorBtnGO.AddComponent<Button>();
            colorBtnComp.targetGraphic = colorBtnImg;
            colorButtons[i] = colorBtnComp;

            // Selection outline (disabled by default)
            var outline = colorBtnGO.AddComponent<Outline>();
            outline.effectColor = DarkText;
            outline.effectDistance = new Vector2(3, 3);
            outline.enabled = false;
        }

        var colorNextBtn = CreateBigButton(stepColor.transform, "ColorNextButton",
            "\u05D4\u05DE\u05E9\u05DA", AccentColor,
            new Vector2(0.15f, 0.03f), new Vector2(0.85f, 0.15f), roundedRect, true);
        colorNextBtn.GetComponent<Button>().interactable = false;

        // ── STEP 5: Done ──
        var stepDone = CreateStepPanel(contentArea.transform, "StepDone");
        CreateText(stepDone.transform, "DoneTitle",
            "\u05DE\u05E2\u05D5\u05DC\u05D4!", 64, DarkText, // !מעולה (Great!)
            new Vector2(0, 0.70f), new Vector2(1, 0.88f), true);

        // Done avatar
        var doneAvatarGO = new GameObject("DoneAvatar");
        doneAvatarGO.transform.SetParent(stepDone.transform, false);
        var doneAvatarRT = doneAvatarGO.AddComponent<RectTransform>();
        doneAvatarRT.anchorMin = new Vector2(0.5f, 0.45f);
        doneAvatarRT.anchorMax = new Vector2(0.5f, 0.45f);
        doneAvatarRT.sizeDelta = new Vector2(180, 180);
        var doneAvatarImg = doneAvatarGO.AddComponent<Image>();
        doneAvatarImg.sprite = circle;
        doneAvatarImg.color = AccentColor;

        // Mask for photo clipping
        doneAvatarGO.AddComponent<Mask>().showMaskGraphic = true;

        // Done avatar photo (hidden by default)
        var doneAvatarPhotoGO = new GameObject("Photo");
        doneAvatarPhotoGO.transform.SetParent(doneAvatarGO.transform, false);
        var donePhotoRT = doneAvatarPhotoGO.AddComponent<RectTransform>();
        StretchFull(donePhotoRT);
        var donePhotoImg = doneAvatarPhotoGO.AddComponent<Image>();
        donePhotoImg.preserveAspect = true;
        donePhotoImg.raycastTarget = false;
        doneAvatarPhotoGO.SetActive(false);

        var doneInitialGO = new GameObject("DoneInitial");
        doneInitialGO.transform.SetParent(doneAvatarGO.transform, false);
        var doneInitRT = doneInitialGO.AddComponent<RectTransform>();
        StretchFull(doneInitRT);
        var doneInitTMP = doneInitialGO.AddComponent<TextMeshProUGUI>();
        doneInitTMP.text = "?";
        doneInitTMP.fontSize = 72;
        doneInitTMP.fontStyle = FontStyles.Bold;
        doneInitTMP.color = Color.white;
        doneInitTMP.alignment = TextAlignmentOptions.Center;
        doneInitTMP.raycastTarget = false;

        // Done name
        var doneNameGO = new GameObject("DoneName");
        doneNameGO.transform.SetParent(stepDone.transform, false);
        var doneNameRT = doneNameGO.AddComponent<RectTransform>();
        doneNameRT.anchorMin = new Vector2(0, 0.30f);
        doneNameRT.anchorMax = new Vector2(1, 0.42f);
        doneNameRT.offsetMin = Vector2.zero;
        doneNameRT.offsetMax = Vector2.zero;
        var doneNameTMP = doneNameGO.AddComponent<TextMeshProUGUI>();
        doneNameTMP.text = "";
        doneNameTMP.fontSize = 48;
        doneNameTMP.fontStyle = FontStyles.Bold;
        doneNameTMP.color = DarkText;
        doneNameTMP.alignment = TextAlignmentOptions.Center;
        doneNameTMP.isRightToLeftText = true;
        doneNameTMP.raycastTarget = false;

        var doneBtn = CreateBigButton(stepDone.transform, "DoneButton",
            "\u05D9\u05D0\u05DC\u05DC\u05D4 \u05DC\u05E9\u05D7\u05E7!", AccentColor, // !יאללה לשחק
            new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.25f), roundedRect, true);

        // ── Webcam Panel (Desktop camera preview overlay) ──
        var webcamPanelGO = new GameObject("WebcamPanel");
        webcamPanelGO.transform.SetParent(canvasGO.transform, false);
        var webcamPanelRT = webcamPanelGO.AddComponent<RectTransform>();
        StretchFull(webcamPanelRT);

        // Dark overlay background
        var webcamBgImg = webcamPanelGO.AddComponent<Image>();
        webcamBgImg.color = new Color(0, 0, 0, 0.85f);
        webcamBgImg.raycastTarget = true;

        // Webcam preview (centered square)
        var webcamPreviewGO = new GameObject("WebcamPreview");
        webcamPreviewGO.transform.SetParent(webcamPanelGO.transform, false);
        var webcamPreviewRT = webcamPreviewGO.AddComponent<RectTransform>();
        webcamPreviewRT.anchorMin = new Vector2(0.5f, 0.5f);
        webcamPreviewRT.anchorMax = new Vector2(0.5f, 0.5f);
        webcamPreviewRT.sizeDelta = new Vector2(700, 700);
        webcamPreviewRT.anchoredPosition = new Vector2(0, 80);
        var webcamRawImage = webcamPreviewGO.AddComponent<RawImage>();
        webcamRawImage.color = Color.white;

        // Round mask on preview
        var webcamMaskGO = new GameObject("WebcamMask");
        webcamMaskGO.transform.SetParent(webcamPanelGO.transform, false);
        var webcamMaskRT = webcamMaskGO.AddComponent<RectTransform>();
        webcamMaskRT.anchorMin = webcamPreviewRT.anchorMin;
        webcamMaskRT.anchorMax = webcamPreviewRT.anchorMax;
        webcamMaskRT.sizeDelta = webcamPreviewRT.sizeDelta;
        webcamMaskRT.anchoredPosition = webcamPreviewRT.anchoredPosition;
        var webcamMaskImg = webcamMaskGO.AddComponent<Image>();
        webcamMaskImg.sprite = circle;
        webcamMaskImg.color = Color.white;
        webcamMaskImg.raycastTarget = false;
        webcamMaskGO.AddComponent<Mask>().showMaskGraphic = false;

        // Re-parent preview inside mask
        webcamPreviewGO.transform.SetParent(webcamMaskGO.transform, false);
        webcamPreviewRT.anchorMin = Vector2.zero;
        webcamPreviewRT.anchorMax = Vector2.one;
        webcamPreviewRT.offsetMin = Vector2.zero;
        webcamPreviewRT.offsetMax = Vector2.zero;
        webcamPreviewRT.anchoredPosition = Vector2.zero;
        webcamPreviewRT.sizeDelta = Vector2.zero;

        // Capture button
        var webcamCaptureBtn = CreateBigButton(webcamPanelGO.transform, "CaptureButton",
            "\u05E6\u05DC\u05DE\u05D5!", AccentColor, // !צלמו
            new Vector2(0.20f, 0.08f), new Vector2(0.80f, 0.18f), roundedRect, true);

        // Cancel button
        var webcamCancelBtn = CreateBigButton(webcamPanelGO.transform, "CancelButton",
            "\u05D1\u05D9\u05D8\u05D5\u05DC", HexColor("#EF5350"), // ביטול
            new Vector2(0.30f, 0.02f), new Vector2(0.70f, 0.08f), roundedRect, true);

        webcamPanelGO.SetActive(false);

        // ── Controller ──
        var controller = canvasGO.AddComponent<ProfileCreationController>();
        controller.stepGreeting = stepGreeting;
        controller.stepRecordName = stepRecord;
        controller.stepTypeName = stepName;
        controller.stepChooseAge = stepAge;
        controller.stepChooseColor = stepColor;
        controller.stepDone = stepDone;

        controller.greetingNextButton = greetNextBtn.GetComponent<Button>();
        controller.recordButton = recordBtn.GetComponent<Button>();
        controller.stopRecordButton = stopBtn.GetComponent<Button>();
        controller.playRecordButton = playBtn.GetComponent<Button>();
        controller.skipRecordButton = skipBtn.GetComponent<Button>();
        controller.recordNextButton = recNextBtn.GetComponent<Button>();
        controller.recordIndicator = recIndImg;

        controller.nameInput = inputField;
        controller.nameNextButton = nameNextBtn.GetComponent<Button>();

        controller.ageButtons = ageButtons;
        controller.ageNextButton = ageNextBtn.GetComponent<Button>();

        controller.colorButtons = colorButtons;
        controller.colorPreview = colorPreviewImg;
        controller.colorPreviewPhoto = cpPhotoImg;
        controller.colorPreviewInitial = cpInitTMP;
        controller.colorPreviewName = cpNameTMP;
        controller.colorNextButton = colorNextBtn.GetComponent<Button>();
        controller.pickPhotoButton = pickPhotoBtn.GetComponent<Button>();

        controller.doneNameText = doneNameTMP;
        controller.doneAvatar = doneAvatarImg;
        controller.doneAvatarPhoto = donePhotoImg;
        controller.doneInitial = doneInitTMP;
        controller.doneButton = doneBtn.GetComponent<Button>();

        controller.backButton = backBtnGO.GetComponent<Button>();

        // Webcam (desktop)
        controller.webcamPanel = webcamPanelGO;
        controller.webcamPreview = webcamRawImage;
        controller.webcamCaptureButton = webcamCaptureBtn.GetComponent<Button>();
        controller.webcamCancelButton = webcamCancelBtn.GetComponent<Button>();

        // Onboarding sounds
        string soundFolder = "Assets/Sounds/On boarding/";
        string[] stepSoundFiles = {
            "Hey, lets meet you.mp3",
            "What is your name.mp3",
            "Write your name.mp3",
            "What is your age.mp3",
            "What is you favorite color.mp3",
            "Lets play.mp3"
        };
        var stepClips = new AudioClip[stepSoundFiles.Length];
        for (int i = 0; i < stepSoundFiles.Length; i++)
        {
            stepClips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(soundFolder + stepSoundFiles[i]);
            if (stepClips[i] == null)
                Debug.LogWarning($"Onboarding sound not found: {soundFolder}{stepSoundFiles[i]}");
        }
        controller.stepSounds = stepClips;

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ProfileCreation.unity");
    }

    // ─────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────

    private static GameObject CreateStepPanel(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        StretchFull(rt);
        go.SetActive(false); // controller activates the right step
        return go;
    }

    private static GameObject CreateText(Transform parent, string name, string text, int fontSize, Color color,
        Vector2 anchorMin, Vector2 anchorMax, bool rtl = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.isRightToLeftText = rtl;
        tmp.raycastTarget = false;
        return go;
    }

    private static GameObject CreateBigButton(Transform parent, string name, string label, Color bgColor,
        Vector2 anchorMin, Vector2 anchorMax, Sprite roundedRect, bool rtl = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.sprite = roundedRect;
        img.type = Image.Type.Sliced;
        img.color = bgColor;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.2f);
        shadow.effectDistance = new Vector2(0, -3);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        StretchFull(labelRT);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = label;
        labelTMP.fontSize = 42;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.color = Color.white;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.isRightToLeftText = rtl;
        labelTMP.raycastTarget = false;

        return go;
    }

    /// <summary>Circle button with a sprite icon instead of text.</summary>
    private static GameObject CreateSpriteCircleButton(Transform parent, string name, Sprite circle,
        Sprite icon, Color bgColor, Vector2 anchorPos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorPos;
        rt.anchorMax = anchorPos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.sprite = circle;
        img.color = bgColor;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.25f);
        shadow.effectDistance = new Vector2(2, -3);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        // Sprite icon centered inside
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        var iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.2f, 0.2f);
        iconRT.anchorMax = new Vector2(0.8f, 0.8f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = icon;
        iconImg.preserveAspect = true;
        iconImg.color = Color.white;
        iconImg.raycastTarget = false;

        return go;
    }

    /// <summary>Rounded button with a sprite icon on the left and text label on the right.</summary>
    private static GameObject CreateIconLabelButton(Transform parent, string name,
        Sprite icon, string label, Color bgColor, Vector2 anchorMin, Vector2 anchorMax, Sprite roundedRect)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bgImg = go.AddComponent<Image>();
        bgImg.sprite = roundedRect;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = bgColor;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.2f);
        shadow.effectDistance = new Vector2(0, -3);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bgImg;

        // Icon on the left
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        var iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.05f, 0.15f);
        iconRT.anchorMax = new Vector2(0.25f, 0.85f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = icon;
        iconImg.preserveAspect = true;
        iconImg.color = Color.white;
        iconImg.raycastTarget = false;

        // Text label on the right
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.25f, 0f);
        labelRT.anchorMax = new Vector2(0.95f, 1f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = label;
        labelTMP.fontSize = 34;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.color = Color.white;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.isRightToLeftText = true;
        labelTMP.raycastTarget = false;

        return go;
    }

    private static GameObject CreateCircleButton(Transform parent, string name, Sprite circle,
        string icon, Color color, Vector2 anchorPos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorPos;
        rt.anchorMax = anchorPos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.sprite = circle;
        img.color = color;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.25f);
        shadow.effectDistance = new Vector2(2, -3);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var labelGO = new GameObject("Icon");
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        StretchFull(labelRT);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = icon;
        labelTMP.fontSize = 48;
        labelTMP.color = Color.white;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.raycastTarget = false;

        return go;
    }

    private static GameObject CreateIconButton(Transform parent, string name, Sprite icon,
        Vector2 pos, Vector2 anchorMin, Vector2 pivot, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMin;
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

    private static GameObject CreateCamera()
    {
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgColor;
        cam.orthographic = true;
        camGO.AddComponent<AudioListener>();
        var urpType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpType != null) camGO.AddComponent(urpType);
        return camGO;
    }

    private static void CreateEventSystem()
    {
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inputType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputType != null) esGO.AddComponent(inputType);
        else esGO.AddComponent<StandaloneInputModule>();
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
}
