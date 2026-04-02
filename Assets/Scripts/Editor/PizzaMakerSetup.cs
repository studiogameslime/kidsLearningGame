using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Editor setup for the Pizza Maker scene.
/// Creates the pizza base, crust, inner area, toolbar, preview, and wires the controller.
/// </summary>
public class PizzaMakerSetup : EditorWindow
{
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    // Pizza colors
    private static readonly Color DoughColor = new Color(0.92f, 0.82f, 0.55f);
    private static readonly Color CrustColor = new Color(0.78f, 0.62f, 0.38f);
    private static readonly Color CrustEdge = new Color(0.65f, 0.50f, 0.30f);
    private static readonly Color TableColor = new Color(0.42f, 0.28f, 0.18f);
    private static readonly Color ToolBarBg = new Color(0.35f, 0.23f, 0.14f, 0.85f);

    public static void ShowWindow() => RunSetupSilent();

    public static void RunSetupSilent() => BuildScene();

    private static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = TableColor;
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var urp = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urp != null) camGO.AddComponent(urp);

        // EventSystem
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        var inputModule = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModule != null) esGO.AddComponent(inputModule);
        else esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        WoodTableBackground.CreateBackground(canvasGO.transform);

        // Safe Area
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(canvasGO.transform, false);
        Full(safeGO.AddComponent<RectTransform>());
        safeGO.AddComponent<SafeAreaHandler>();

        // ── Top Bar ──
        var roundedRect = LoadSprite("Assets/UI/Sprites/RoundedRect.png");
        var circleSprite = LoadSprite("Assets/UI/Sprites/Circle.png");

        var topBar = new GameObject("TopBar");
        topBar.transform.SetParent(safeGO.transform, false);
        var topBarRT = topBar.AddComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.sizeDelta = new Vector2(0, TopBarHeight);
        topBar.AddComponent<Image>().color = WoodTableBackground.HeaderColor;
        topBar.AddComponent<ThemeHeader>();

        // Title
        var titleGO = MakeTMP(topBar.transform, "Title",
            "\u05D4\u05E4\u05D9\u05E6\u05E8\u05D9\u05D9\u05D4", 42, WoodTableBackground.TitleTextColor); // הפיצרייה
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.1f, 0);
        titleRT.anchorMax = new Vector2(0.9f, 1);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;
        titleGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Home button
        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        // ── Play Area ──
        var playAreaGO = new GameObject("PlayArea");
        playAreaGO.transform.SetParent(safeGO.transform, false);
        var playAreaRT = playAreaGO.AddComponent<RectTransform>();
        playAreaRT.anchorMin = Vector2.zero;
        playAreaRT.anchorMax = Vector2.one;
        playAreaRT.offsetMin = Vector2.zero;
        playAreaRT.offsetMax = new Vector2(0, -TopBarHeight);

        // ── Pizza Base (centered, large) ──
        float pizzaSize = 620;
        var pizzaGO = new GameObject("Pizza");
        pizzaGO.transform.SetParent(playAreaGO.transform, false);
        var pizzaRT = pizzaGO.AddComponent<RectTransform>();
        pizzaRT.anchorMin = new Vector2(0.5f, 0.55f);
        pizzaRT.anchorMax = new Vector2(0.5f, 0.55f);
        pizzaRT.sizeDelta = new Vector2(pizzaSize, pizzaSize);

        // Plate/shadow under pizza
        var plateGO = new GameObject("Plate");
        plateGO.transform.SetParent(pizzaGO.transform, false);
        var plateRT = plateGO.AddComponent<RectTransform>();
        plateRT.anchorMin = new Vector2(-0.04f, -0.04f);
        plateRT.anchorMax = new Vector2(1.04f, 1.04f);
        plateRT.offsetMin = Vector2.zero;
        plateRT.offsetMax = Vector2.zero;
        var plateImg = plateGO.AddComponent<Image>();
        if (circleSprite != null) plateImg.sprite = circleSprite;
        plateImg.color = new Color(0.25f, 0.15f, 0.08f, 0.25f);
        plateImg.raycastTarget = false;

        // Crust (outer ring)
        var crustGO = new GameObject("Crust");
        crustGO.transform.SetParent(pizzaGO.transform, false);
        var crustRT = crustGO.AddComponent<RectTransform>();
        Full(crustRT);
        var crustImg = crustGO.AddComponent<Image>();
        if (circleSprite != null) crustImg.sprite = circleSprite;
        crustImg.color = CrustColor;
        crustImg.raycastTarget = false;

        // Crust edge (slightly darker ring)
        var edgeGO = new GameObject("CrustEdge");
        edgeGO.transform.SetParent(crustGO.transform, false);
        var edgeRT = edgeGO.AddComponent<RectTransform>();
        edgeRT.anchorMin = new Vector2(-0.01f, -0.01f);
        edgeRT.anchorMax = new Vector2(1.01f, 1.01f);
        edgeRT.offsetMin = Vector2.zero;
        edgeRT.offsetMax = Vector2.zero;
        var edgeImg = edgeGO.AddComponent<Image>();
        if (circleSprite != null) edgeImg.sprite = circleSprite;
        edgeImg.color = CrustEdge;
        edgeImg.raycastTarget = false;
        edgeGO.transform.SetAsFirstSibling(); // behind crust

        // Pizza base (dough — inner circle)
        var baseGO = new GameObject("PizzaBase");
        baseGO.transform.SetParent(pizzaGO.transform, false);
        var baseRT = baseGO.AddComponent<RectTransform>();
        baseRT.anchorMin = new Vector2(0.1f, 0.1f);
        baseRT.anchorMax = new Vector2(0.9f, 0.9f);
        baseRT.offsetMin = Vector2.zero;
        baseRT.offsetMax = Vector2.zero;
        var baseImg = baseGO.AddComponent<Image>();
        if (circleSprite != null) baseImg.sprite = circleSprite;
        baseImg.color = DoughColor;
        baseImg.raycastTarget = false;

        // Inner interactive area (where sauce/cheese/toppings go — mask to circle)
        var innerGO = new GameObject("PizzaInner");
        innerGO.transform.SetParent(pizzaGO.transform, false);
        var innerRT = innerGO.AddComponent<RectTransform>();
        innerRT.anchorMin = new Vector2(0.1f, 0.1f);
        innerRT.anchorMax = new Vector2(0.9f, 0.9f);
        innerRT.offsetMin = Vector2.zero;
        innerRT.offsetMax = Vector2.zero;
        // Mask to keep sauce/cheese inside circle
        var maskImg = innerGO.AddComponent<Image>();
        if (circleSprite != null) maskImg.sprite = circleSprite;
        maskImg.color = Color.white; // full alpha needed for stencil mask to work
        maskImg.raycastTarget = false;
        innerGO.AddComponent<Mask>().showMaskGraphic = false; // hides visually but mask still clips

        // ── Tool Bar (bottom) ──
        var toolBarGO = new GameObject("ToolBar");
        toolBarGO.transform.SetParent(playAreaGO.transform, false);
        var toolBarRT = toolBarGO.AddComponent<RectTransform>();
        toolBarRT.anchorMin = new Vector2(0.1f, 0);
        toolBarRT.anchorMax = new Vector2(0.9f, 0.15f);
        toolBarRT.offsetMin = new Vector2(0, 10);
        toolBarRT.offsetMax = new Vector2(0, 0);
        var toolBarImg = toolBarGO.AddComponent<Image>();
        if (roundedRect != null) { toolBarImg.sprite = roundedRect; toolBarImg.type = Image.Type.Sliced; }
        toolBarImg.color = ToolBarBg;
        toolBarImg.raycastTarget = false;

        // Step label
        var labelGO = MakeTMP(toolBarGO.transform, "StepLabel", "", 28, new Color(1, 0.96f, 0.88f));
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0, 0);
        labelRT.anchorMax = new Vector2(1, 0.3f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        var labelTMP = labelGO.GetComponent<TextMeshProUGUI>();
        labelTMP.alignment = TextAlignmentOptions.Center;

        // ── Reference Preview (top-left corner) ──
        var previewGO = new GameObject("Preview");
        previewGO.transform.SetParent(playAreaGO.transform, false);
        var previewRT = previewGO.AddComponent<RectTransform>();
        previewRT.anchorMin = new Vector2(0, 1);
        previewRT.anchorMax = new Vector2(0, 1);
        previewRT.pivot = new Vector2(0, 1);
        previewRT.anchoredPosition = new Vector2(20, -10);
        previewRT.sizeDelta = new Vector2(200, 200);
        var previewBg = previewGO.AddComponent<Image>();
        if (roundedRect != null) { previewBg.sprite = roundedRect; previewBg.type = Image.Type.Sliced; }
        previewBg.color = new Color(1, 1, 1, 0.15f);
        previewBg.raycastTarget = false;

        // ── Load Topping Sprites ──
        var singleSprites = LoadSlicedSprites("Assets/Art/Pizza Maker/Pizza extras.png");
        var groupSprites = LoadSlicedSprites("Assets/Art/Pizza Maker/Pizza extras packages.png");

        // ── Controller ──
        var ctrl = canvasGO.AddComponent<PizzaMakerController>();
        ctrl.pizzaArea = pizzaRT;
        ctrl.pizzaBase = baseImg;
        ctrl.pizzaCrust = crustImg;
        ctrl.pizzaInner = innerRT;
        ctrl.circleSprite = circleSprite;
        ctrl.toolBar = toolBarRT;
        ctrl.stepLabel = labelTMP;
        ctrl.previewArea = previewRT;
        ctrl.toppingSingles = singleSprites;
        ctrl.toppingGroups = groupSprites;

        // Wire home button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        // Tutorial hand
        TutorialHandHelper.Create(safeGO.transform, TutorialHandHelper.Anim.Tap,
            new Vector2(0, -30), new Vector2(450, 450), "pizzamaker");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/PizzaMaker.unity");
        Debug.Log("Pizza Maker scene created.");
    }

    // ── Helpers ──

    private static Sprite[] LoadSlicedSprites(string path)
    {
        var list = new List<Sprite>();
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (assets != null)
        {
            foreach (var a in assets)
                if (a is Sprite s) list.Add(s);
        }
        list.Sort((a, b) =>
        {
            int na = 0, nb = 0;
            var pa = a.name.Split('_'); if (pa.Length > 1) int.TryParse(pa[pa.Length - 1], out na);
            var pb = b.name.Split('_'); if (pb.Length > 1) int.TryParse(pb[pb.Length - 1], out nb);
            return na.CompareTo(nb);
        });
        return list.ToArray();
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
        img.raycastTarget = true;
        go.AddComponent<Button>().targetGraphic = img;
        return go;
    }

    private static GameObject MakeTMP(Transform parent, string name, string text, int fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(tmp, text);
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return go;
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void Full(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
