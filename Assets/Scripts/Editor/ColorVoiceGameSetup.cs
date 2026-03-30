using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the ColorVoice scene.
/// Run via Tools > Kids Learning Game > Setup Color Voice Game.
/// </summary>
public class ColorVoiceGameSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    private static readonly Color BgColor     = HexColor("#FFF8E1"); // warm cream
    private static readonly Color TopBarColor = HexColor("#FF8A65"); // warm coral
    private static readonly Color DarkText    = HexColor("#4A4A4A");

    private static readonly int TopBarHeight   = SetupConstants.HeaderHeight;
    private const int BottomBarHeight = 120;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Color Voice Setup", "Building scene…", 0.5f);
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

    // ─────────────────────────────────────────
    //  SCENE
    // ─────────────────────────────────────────

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
        cam.backgroundColor = BgColor;
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
        var canvasGO = new GameObject("ColorVoiceCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
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

        // ── TOP BAR ──
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
        HebrewText.SetText(titleTMP, "\u05D0\u05DE\u05E8\u05D5 \u05D0\u05EA \u05D4\u05E6\u05D1\u05E2"); // אמרו את הצבע
        titleTMP.fontSize = 48;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button (top-left)
        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = CreateIconButton(topBar.transform, "TrophyButton", trophyIcon,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-16, -20), new Vector2(70, 70));

        // ── BOTTOM BAR ──
        var bottomBar = CreateStretchImage(safeArea.transform, "BottomBar", new Color(1, 1, 1, 0));
        var bottomBarRT = bottomBar.GetComponent<RectTransform>();
        bottomBarRT.anchorMin = new Vector2(0, 0);
        bottomBarRT.anchorMax = new Vector2(1, 0);
        bottomBarRT.pivot = new Vector2(0.5f, 0);
        bottomBarRT.sizeDelta = new Vector2(0, BottomBarHeight);
        bottomBar.GetComponent<Image>().raycastTarget = false;

        // ── PLAY AREA ──
        var playArea = new GameObject("PlayArea");
        playArea.transform.SetParent(safeArea.transform, false);
        var playAreaRT = playArea.AddComponent<RectTransform>();
        StretchFull(playAreaRT);
        playAreaRT.offsetMax = new Vector2(0, -TopBarHeight);
        playAreaRT.offsetMin = new Vector2(0, BottomBarHeight);

        // ── Progress text (top of play area) ──
        var progressGO = new GameObject("ProgressText");
        progressGO.transform.SetParent(playArea.transform, false);
        var progressRT = progressGO.AddComponent<RectTransform>();
        progressRT.anchorMin = new Vector2(0.5f, 1f);
        progressRT.anchorMax = new Vector2(0.5f, 1f);
        progressRT.pivot = new Vector2(0.5f, 1f);
        progressRT.anchoredPosition = new Vector2(0, -10);
        progressRT.sizeDelta = new Vector2(200, 60);
        var progressTMP = progressGO.AddComponent<TextMeshProUGUI>();
        progressTMP.text = "1/7";
        progressTMP.fontSize = 36;
        progressTMP.color = HexColor("#999999");
        progressTMP.alignment = TextAlignmentOptions.Center;
        progressTMP.raycastTarget = false;

        // ── Instruction text (above color circle) ──
        var instrGO = new GameObject("InstructionText");
        instrGO.transform.SetParent(playArea.transform, false);
        var instrRT = instrGO.AddComponent<RectTransform>();
        instrRT.anchorMin = new Vector2(0.05f, 0.70f);
        instrRT.anchorMax = new Vector2(0.95f, 0.82f);
        instrRT.offsetMin = Vector2.zero;
        instrRT.offsetMax = Vector2.zero;
        var instrTMP = instrGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(instrTMP, "?\u05D0\u05D9\u05D6\u05D4 \u05E6\u05D1\u05E2 \u05D6\u05D4"); // ?איזה צבע זה
        instrTMP.fontSize = 52;
        instrTMP.fontStyle = FontStyles.Bold;
        instrTMP.color = DarkText;
        instrTMP.alignment = TextAlignmentOptions.Center;
        instrTMP.raycastTarget = false;

        // ── Large color circle (center) ──
        var colorCircleGO = new GameObject("ColorCircle");
        colorCircleGO.transform.SetParent(playArea.transform, false);
        var colorCircleRT = colorCircleGO.AddComponent<RectTransform>();
        colorCircleRT.anchorMin = new Vector2(0.5f, 0.5f);
        colorCircleRT.anchorMax = new Vector2(0.5f, 0.5f);
        colorCircleRT.sizeDelta = new Vector2(1000, 1000);
        colorCircleRT.anchoredPosition = new Vector2(0, 60);
        var colorCircleImg = colorCircleGO.AddComponent<Image>();
        colorCircleImg.sprite = circleSprite;
        colorCircleImg.color = HexColor("#FF4444");
        colorCircleImg.raycastTarget = false;

        // Shadow on color circle
        var circShadow = colorCircleGO.AddComponent<Shadow>();
        circShadow.effectColor = new Color(0, 0, 0, 0.15f);
        circShadow.effectDistance = new Vector2(3, -5);

        // ── Color label (inside circle) ──
        var colorLabelGO = new GameObject("ColorLabel");
        colorLabelGO.transform.SetParent(colorCircleGO.transform, false);
        var colorLabelRT = colorLabelGO.AddComponent<RectTransform>();
        StretchFull(colorLabelRT);
        var colorLabelTMP = colorLabelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(colorLabelTMP, "\u05D0\u05D3\u05D5\u05DD"); // אדום
        colorLabelTMP.fontSize = 110;
        colorLabelTMP.fontStyle = FontStyles.Bold;
        colorLabelTMP.color = Color.white;
        colorLabelTMP.alignment = TextAlignmentOptions.Center;
        colorLabelTMP.raycastTarget = false;

        // ── Microphone icon (below circle, pulsing indicator) ──
        var micSprite = LoadSprite("Assets/Art/Microphone.png");
        var micGO = new GameObject("MicIcon");
        micGO.transform.SetParent(playArea.transform, false);
        var micRT = micGO.AddComponent<RectTransform>();
        micRT.anchorMin = new Vector2(0.5f, 0.5f);
        micRT.anchorMax = new Vector2(0.5f, 0.5f);
        micRT.sizeDelta = new Vector2(100, 100);
        micRT.anchoredPosition = new Vector2(0, -480);
        var micImg = micGO.AddComponent<Image>();
        micImg.sprite = micSprite;
        micImg.preserveAspect = true;
        micImg.color = TopBarColor;
        micImg.raycastTarget = false;

        // ── Feedback text (below mic) ──
        var feedbackGO = new GameObject("FeedbackText");
        feedbackGO.transform.SetParent(playArea.transform, false);
        var feedbackRT = feedbackGO.AddComponent<RectTransform>();
        feedbackRT.anchorMin = new Vector2(0.05f, 0.05f);
        feedbackRT.anchorMax = new Vector2(0.95f, 0.18f);
        feedbackRT.offsetMin = Vector2.zero;
        feedbackRT.offsetMax = Vector2.zero;
        var feedbackTMP = feedbackGO.AddComponent<TextMeshProUGUI>();
        feedbackTMP.text = "";
        feedbackTMP.fontSize = 56;
        feedbackTMP.fontStyle = FontStyles.Bold;
        feedbackTMP.color = HexColor("#4CAF50");
        feedbackTMP.alignment = TextAlignmentOptions.Center;
        feedbackTMP.raycastTarget = false;
        feedbackGO.SetActive(false);

        // ── Debug text (very bottom, small, for dev testing) ──
        var debugGO = new GameObject("DebugText");
        debugGO.transform.SetParent(playArea.transform, false);
        var debugRT = debugGO.AddComponent<RectTransform>();
        debugRT.anchorMin = new Vector2(0.02f, 0.0f);
        debugRT.anchorMax = new Vector2(0.98f, 0.05f);
        debugRT.offsetMin = Vector2.zero;
        debugRT.offsetMax = Vector2.zero;
        var debugTMP = debugGO.AddComponent<TextMeshProUGUI>();
        debugTMP.text = "";
        debugTMP.fontSize = 22;
        debugTMP.color = HexColor("#AAAAAA");
        debugTMP.alignment = TextAlignmentOptions.Center;
        debugTMP.raycastTarget = false;
        debugGO.SetActive(false);

        // ── Restart button (bottom-right of top bar) ──
        var restartIcon = UISheetHelper.HomeIcon; // reuse icon
        var restartGO = CreateIconButton(topBar.transform, "RestartButton", restartIcon,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-16, -15), new Vector2(80, 80));

        // ── CONTROLLER ──
        var controller = canvasGO.AddComponent<ColorVoiceController>();
        controller.colorCircle = colorCircleImg;
        controller.colorLabel = colorLabelTMP;
        controller.micIcon = micImg;
        controller.instructionText = instrTMP;
        controller.feedbackText = feedbackTMP;
        controller.progressText = progressTMP;
        controller.debugText = debugTMP;

        // Wire buttons
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            restartGO.GetComponent<Button>().onClick, controller.OnRestartPressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = roundedRect;
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "colorvoice";

        // Tutorial hand
        TutorialHandHelper.Create(safeArea.transform, TutorialHandHelper.Anim.Tap,
            new Vector2(0, 0), new Vector2(450, 450), "colorvoice");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ColorVoice.unity");
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

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
