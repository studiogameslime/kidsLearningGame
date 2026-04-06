using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the SandDrawingScene — a standalone sandbox where kids draw in sand.
/// No external art required; all visuals are procedural.
/// </summary>
public class SandDrawingSetup
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly Color HeaderColor = new Color(0.55f, 0.40f, 0.20f, 0.55f); // warm sandy header
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;
    private static readonly Color BgColor = new Color(0.82f, 0.72f, 0.54f); // warm sand background
    private static readonly Color FrameWood = new Color(0.55f, 0.38f, 0.20f); // wood frame
    private static readonly Color FrameWoodDark = new Color(0.42f, 0.28f, 0.14f); // wood frame edge

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Sand Drawing Setup", "Building scene…", 0.5f);
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
        cam.backgroundColor = BgColor;
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
        var canvasGO = new GameObject("SandDrawingCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background fill
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        Full(bgRT);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = BgColor;
        bgImg.raycastTarget = false;

        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");

        // Wooden frame (outer)
        var frameOuterGO = new GameObject("FrameOuter");
        frameOuterGO.transform.SetParent(canvasGO.transform, false);
        var frameOuterRT = frameOuterGO.AddComponent<RectTransform>();
        frameOuterRT.anchorMin = new Vector2(0.015f, 0.015f);
        frameOuterRT.anchorMax = new Vector2(0.985f, 0.885f);
        frameOuterRT.offsetMin = Vector2.zero;
        frameOuterRT.offsetMax = Vector2.zero;
        var frameOuterImg = frameOuterGO.AddComponent<Image>();
        if (roundedRect != null) { frameOuterImg.sprite = roundedRect; frameOuterImg.type = Image.Type.Sliced; }
        frameOuterImg.color = FrameWoodDark;
        frameOuterImg.raycastTarget = false;
        frameOuterGO.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.3f);

        // Wooden frame (inner)
        var frameInnerGO = new GameObject("FrameInner");
        frameInnerGO.transform.SetParent(canvasGO.transform, false);
        var frameInnerRT = frameInnerGO.AddComponent<RectTransform>();
        frameInnerRT.anchorMin = new Vector2(0.02f, 0.02f);
        frameInnerRT.anchorMax = new Vector2(0.98f, 0.88f);
        frameInnerRT.offsetMin = Vector2.zero;
        frameInnerRT.offsetMax = Vector2.zero;
        var frameInnerImg = frameInnerGO.AddComponent<Image>();
        if (roundedRect != null) { frameInnerImg.sprite = roundedRect; frameInnerImg.type = Image.Type.Sliced; }
        frameInnerImg.color = FrameWood;
        frameInnerImg.raycastTarget = false;

        // Sand Display — RawImage inside the frame
        var sandGO = new GameObject("SandDisplay");
        sandGO.transform.SetParent(canvasGO.transform, false);
        var sandRT = sandGO.AddComponent<RectTransform>();
        sandRT.anchorMin = new Vector2(0.025f, 0.025f);
        sandRT.anchorMax = new Vector2(0.975f, 0.875f);
        sandRT.offsetMin = Vector2.zero;
        sandRT.offsetMax = Vector2.zero;
        var sandRawImg = sandGO.AddComponent<RawImage>();
        sandRawImg.color = Color.white;
        sandRawImg.raycastTarget = true;

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

        // Title — Hebrew "ארגז חול" (Sand Box)
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D0\u05E8\u05D2\u05D6 \u05D7\u05D5\u05DC"); // ארגז חול
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

        // Reset button (bottom-center area)
        var resetGO = new GameObject("ResetButton");
        resetGO.transform.SetParent(safeArea.transform, false);
        var resetRT = resetGO.AddComponent<RectTransform>();
        resetRT.anchorMin = new Vector2(0.5f, 0);
        resetRT.anchorMax = new Vector2(0.5f, 0);
        resetRT.pivot = new Vector2(0.5f, 0);
        resetRT.sizeDelta = new Vector2(160, 60);
        resetRT.anchoredPosition = new Vector2(0, 16);
        var resetImg = resetGO.AddComponent<Image>();
        if (roundedRect != null) { resetImg.sprite = roundedRect; resetImg.type = Image.Type.Sliced; }
        resetImg.color = new Color(0.65f, 0.50f, 0.30f, 0.85f);
        var resetBtn = resetGO.AddComponent<Button>();
        resetBtn.targetGraphic = resetImg;

        // Reset button label
        var resetLabelGO = new GameObject("ResetLabel");
        resetLabelGO.transform.SetParent(resetGO.transform, false);
        var resetLabelRT = resetLabelGO.AddComponent<RectTransform>();
        Full(resetLabelRT);
        var resetLabelTMP = resetLabelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(resetLabelTMP, "\u05E0\u05E7\u05D4"); // נקה (Clean)
        resetLabelTMP.fontSize = 28;
        resetLabelTMP.fontStyle = FontStyles.Bold;
        resetLabelTMP.color = Color.white;
        resetLabelTMP.alignment = TextAlignmentOptions.Center;
        resetLabelTMP.raycastTarget = false;

        // Controller
        var controller = canvasGO.AddComponent<SandDrawingController>();
        controller.sandDisplay = sandRawImg;
        controller.backButton = homeGO.GetComponent<Button>();
        controller.resetButton = resetBtn;
        controller.sandShader = Shader.Find("UI/SandSurface");

        // Save Scene
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SandDrawingScene.unity");
        Debug.Log("[SandDrawingSetup] Scene created: Assets/Scenes/SandDrawingScene.unity");
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
