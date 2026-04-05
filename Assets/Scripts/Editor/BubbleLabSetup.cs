using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the BubbleLabScene — a standalone sandbox where kids create and pop colorful bubbles.
/// No external art required; all visuals are procedural.
/// </summary>
public class BubbleLabSetup
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    // Background gradient colors
    private static readonly Color BgTop = new Color(0.20f, 0.15f, 0.40f);      // soft purple
    private static readonly Color BgBottom = new Color(0.25f, 0.40f, 0.65f);    // brighter blue
    private static readonly Color HeaderColor = new Color(0.20f, 0.12f, 0.35f, 0.55f); // purple header

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Bubble Lab Setup", "Building scene...", 0.5f);
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

        // Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgTop;
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

        // Canvas
        var canvasGO = new GameObject("BubbleLabCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background gradient — use two overlapping images (top and bottom halves)
        var bgTopGO = new GameObject("BackgroundTop");
        bgTopGO.transform.SetParent(canvasGO.transform, false);
        var bgTopRT = bgTopGO.AddComponent<RectTransform>();
        bgTopRT.anchorMin = new Vector2(0, 0.5f);
        bgTopRT.anchorMax = Vector2.one;
        bgTopRT.offsetMin = Vector2.zero;
        bgTopRT.offsetMax = Vector2.zero;
        var bgTopImg = bgTopGO.AddComponent<Image>();
        bgTopImg.color = BgTop;
        bgTopImg.raycastTarget = false;

        var bgBotGO = new GameObject("BackgroundBottom");
        bgBotGO.transform.SetParent(canvasGO.transform, false);
        var bgBotRT = bgBotGO.AddComponent<RectTransform>();
        bgBotRT.anchorMin = Vector2.zero;
        bgBotRT.anchorMax = new Vector2(1, 0.5f);
        bgBotRT.offsetMin = Vector2.zero;
        bgBotRT.offsetMax = Vector2.zero;
        var bgBotImg = bgBotGO.AddComponent<Image>();
        bgBotImg.color = BgBottom;
        bgBotImg.raycastTarget = false;

        // Middle blend strip for smoother gradient
        var bgMidGO = new GameObject("BackgroundMid");
        bgMidGO.transform.SetParent(canvasGO.transform, false);
        var bgMidRT = bgMidGO.AddComponent<RectTransform>();
        bgMidRT.anchorMin = new Vector2(0, 0.35f);
        bgMidRT.anchorMax = new Vector2(1, 0.65f);
        bgMidRT.offsetMin = Vector2.zero;
        bgMidRT.offsetMax = Vector2.zero;
        var bgMidImg = bgMidGO.AddComponent<Image>();
        bgMidImg.color = Color.Lerp(BgBottom, BgTop, 0.5f);
        bgMidImg.raycastTarget = false;

        // Play Area — full screen, the controller attaches to this
        var playAreaGO = new GameObject("PlayArea");
        playAreaGO.transform.SetParent(canvasGO.transform, false);
        var playAreaRT = playAreaGO.AddComponent<RectTransform>();
        Full(playAreaRT);
        // Needs Image for raycast (the controller implements IPointerDown etc.)
        var playAreaImg = playAreaGO.AddComponent<Image>();
        playAreaImg.color = new Color(0, 0, 0, 0); // fully transparent, just for raycasting
        playAreaImg.raycastTarget = true;

        // Safe Area
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        Full(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // Top Bar
        var topBar = StretchImg(safeArea.transform, "TopBar", HeaderColor);
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.sizeDelta = new Vector2(0, TopBarHeight);
        topBar.GetComponent<Image>().raycastTarget = false;
        topBar.AddComponent<ThemeHeader>();

        // Title — Hebrew
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05DE\u05E2\u05D1\u05D3\u05EA \u05D1\u05D5\u05E2\u05D5\u05EA"); // מעבדת בועות
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Back/Home button (top-left)
        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = IconBtn(topBar.transform, "BackButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        // Controller
        var controller = playAreaGO.AddComponent<BubbleLabController>();
        controller.playArea = playAreaRT;
        controller.backButton = homeGO.GetComponent<Button>();

        // Save Scene
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/BubbleLabScene.unity");
        Debug.Log("[BubbleLabSetup] Scene created: Assets/Scenes/BubbleLabScene.unity");
    }

    // ── Helpers ──

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
        var rt = go.AddComponent<RectTransform>();
        Full(rt);
        var img = go.AddComponent<Image>();
        img.color = color;
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
}
