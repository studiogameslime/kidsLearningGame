using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the LetterBubbles scene — Hebrew letter bubbles floating game.
/// Background uses World art assets (sky, clouds, hills, ground layers).
/// </summary>
public class LetterBubblesSetup
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    // Nature colors (matching World scene)
    private static readonly Color DaySky            = HexColor("#8FD4F5");
    private static readonly Color DayHillsLarge     = HexColor("#B7D7D6");
    private static readonly Color DayHills          = HexColor("#9FCBC5");
    private static readonly Color DayGroundBack     = HexColor("#8ED36B");
    private static readonly Color DayGroundFront    = HexColor("#79C956");
    private static readonly string WorldArtDir      = "Assets/Art/World";
    private static readonly int TopBarHeight  = SetupConstants.HeaderHeight;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Letter Bubbles Setup", "Building scene\u2026", 0.6f);
            CreateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void CreateScene()
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = DaySky;
        cam.orthographic = true;
        var urpType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpType != null) camGO.AddComponent(urpType);

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inputType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputType != null) esGO.AddComponent(inputType);
        else esGO.AddComponent<StandaloneInputModule>();

        var canvasGO = new GameObject("LetterBubblesCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ═══ NATURE BACKGROUND (same art assets as World scene) ═══

        // Load World art sprites
        var hillsLargeSprite = LoadSprite($"{WorldArtDir}/hillsLarge.png");
        var hillsSprite = LoadSprite($"{WorldArtDir}/hills.png");
        var groundLayer1Sprite = LoadSprite($"{WorldArtDir}/groundLayer1.png");
        var groundLayer2Sprite = LoadSprite($"{WorldArtDir}/groundLayer2.png");
        var cloudSprites = new Sprite[8];
        for (int i = 0; i < 8; i++)
            cloudSprites[i] = LoadSprite($"{WorldArtDir}/cloud{i}.png");

        // Sky background
        var skyGO = StretchImg(canvasGO.transform, "SkyBackground", DaySky);
        skyGO.GetComponent<Image>().raycastTarget = false;

        // Decorative clouds (static, scattered across sky)
        for (int i = 0; i < 5; i++)
        {
            var cSprite = cloudSprites[i % cloudSprites.Length];
            if (cSprite == null) continue;
            var cloudGO = new GameObject($"Cloud_{i}");
            cloudGO.transform.SetParent(canvasGO.transform, false);
            var cRT = cloudGO.AddComponent<RectTransform>();
            float cx = 0.1f + i * 0.2f;
            float cy = 0.7f + (i % 3) * 0.08f;
            float cSize = 0.12f + (i % 2) * 0.05f;
            cRT.anchorMin = new Vector2(cx - cSize, cy - cSize * 0.5f);
            cRT.anchorMax = new Vector2(cx + cSize, cy + cSize * 0.5f);
            cRT.offsetMin = Vector2.zero;
            cRT.offsetMax = Vector2.zero;
            var cImg = cloudGO.AddComponent<Image>();
            cImg.sprite = cSprite;
            cImg.preserveAspect = true;
            cImg.color = new Color(1f, 1f, 1f, 0.85f);
            cImg.raycastTarget = false;
        }

        // Large hills (far background)
        CreateSpriteLayer(canvasGO.transform, "HillsLarge", hillsLargeSprite,
            new Vector2(0, 0.3f), new Vector2(1, 0.6f), DayHillsLarge);

        // Small hills (closer)
        CreateSpriteLayer(canvasGO.transform, "Hills", hillsSprite,
            new Vector2(0, 0.2f), new Vector2(1, 0.45f), DayHills);

        // Ground back layer
        CreateSpriteLayer(canvasGO.transform, "GroundBack", groundLayer1Sprite,
            new Vector2(0, 0), new Vector2(1, 0.35f), DayGroundBack);

        // Ground front layer
        CreateSpriteLayer(canvasGO.transform, "GroundFront", groundLayer2Sprite,
            new Vector2(0, 0), new Vector2(1, 0.2f), DayGroundFront);

        // ═══ PLAY AREA (where bubbles float) ═══

        var playAreaGO = new GameObject("PlayArea");
        playAreaGO.transform.SetParent(canvasGO.transform, false);
        var playAreaRT = playAreaGO.AddComponent<RectTransform>();
        playAreaRT.anchorMin = new Vector2(0.02f, 0.02f);
        playAreaRT.anchorMax = new Vector2(0.98f, 0.85f);
        playAreaRT.offsetMin = Vector2.zero;
        playAreaRT.offsetMax = Vector2.zero;

        // ═══ TARGET LETTER DISPLAY (top-left corner) ═══

        var targetBGGO = new GameObject("TargetLetterBG");
        targetBGGO.transform.SetParent(canvasGO.transform, false);
        var targetBGRT = targetBGGO.AddComponent<RectTransform>();
        targetBGRT.anchorMin = new Vector2(0, 1);
        targetBGRT.anchorMax = new Vector2(0, 1);
        targetBGRT.pivot = new Vector2(0, 1);
        targetBGRT.anchoredPosition = new Vector2(20, -130);
        targetBGRT.sizeDelta = new Vector2(120, 120);
        var targetBGImg = targetBGGO.AddComponent<Image>();
        var circleSprite = LoadSprite("Assets/UI/Sprites/Circle.png");
        if (circleSprite != null) targetBGImg.sprite = circleSprite;
        targetBGImg.color = new Color(0.3f, 0.2f, 0.5f, 0.6f);
        targetBGImg.raycastTarget = false;

        var targetTextGO = new GameObject("TargetLetterText");
        targetTextGO.transform.SetParent(targetBGGO.transform, false);
        var targetTextRT = targetTextGO.AddComponent<RectTransform>();
        Full(targetTextRT);
        var targetTextTMP = targetTextGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(targetTextTMP, "\u05D0"); // א placeholder
        targetTextTMP.fontSize = 60;
        targetTextTMP.fontStyle = FontStyles.Bold;
        targetTextTMP.color = Color.white;
        targetTextTMP.alignment = TextAlignmentOptions.Center;
        targetTextTMP.raycastTarget = false;
        targetTextTMP.enableWordWrapping = false;
        targetTextTMP.outlineWidth = 0.2f;
        targetTextTMP.outlineColor = new Color(0, 0, 0, 0.3f);

        // ═══ SAFE AREA + UI ═══

        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        Full(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        var topBar = StretchImg(safeArea.transform, "TopBar", new Color(0.3f, 0.2f, 0.5f, 0.75f));
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.sizeDelta = new Vector2(0, TopBarHeight);
        topBar.GetComponent<Image>().raycastTarget = false;
        topBar.AddComponent<ThemeHeader>();

        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D1\u05D5\u05E2\u05D5\u05EA \u05D0\u05D5\u05EA\u05D9\u05D5\u05EA"); // בועות אותיות
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(1f, 0.96f, 0.88f);
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = IconBtn(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = IconBtn(topBar.transform, "TrophyButton", trophyIcon,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-16, 0), new Vector2(70, 70));

        // ═══ CONTROLLER ═══

        var controller = canvasGO.AddComponent<LetterBubblesController>();
        controller.playArea = playAreaRT;
        controller.titleText = titleTMP;
        controller.targetLetterBG = targetBGImg;
        controller.targetLetterText = targetTextTMP;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = LoadSprite("Assets/UI/Sprites/RoundedRect.png");
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "letterbubbles";

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/LetterBubbles.unity");
        Debug.Log("[LetterBubblesSetup] Scene created: Assets/Scenes/LetterBubbles.unity");
    }

    // ═══ HELPERS ═══

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static GameObject StretchImg(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Full(go.AddComponent<RectTransform>());
        go.AddComponent<Image>().color = color;
        return go;
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

    private static void Full(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject IconBtn(Transform parent, string name, Sprite icon,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        if (icon != null) img.sprite = icon;
        img.preserveAspect = true;
        img.color = Color.white;
        go.AddComponent<Button>();
        return go;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
