using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the DrawingGallery scene — grid display of saved drawings.
/// </summary>
public class DrawingGallerySetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1080, 1920);
    private static readonly Color BgColor = HexColor("#FFF8E8");
    private static readonly Color TopBarColor = HexColor("#F48FB1"); // pink, matches coloring game
    private const int TopBarHeight = 130;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Drawing Gallery Setup", "Building scene…", 0.5f);
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

        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");

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
        var canvasGO = new GameObject("GalleryCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        var bgGO = CreateStretchImage(canvasGO.transform, "Background", BgColor);
        bgGO.GetComponent<Image>().raycastTarget = false;

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

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        StretchFull(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = HebrewFixer.Fix("הציורים שלי");
        titleTMP.fontSize = 42;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.isRightToLeftText = false;
        titleTMP.raycastTarget = false;

        // Home button
        var homeIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Icons/home.png");
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(16, -20), new Vector2(90, 90));

        // ── Scroll area ──
        var scrollArea = new GameObject("ScrollArea");
        scrollArea.transform.SetParent(safeArea.transform, false);
        var scrollRT = scrollArea.AddComponent<RectTransform>();
        StretchFull(scrollRT);
        scrollRT.offsetMax = new Vector2(0, -TopBarHeight);

        var scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollArea.AddComponent<RectMask2D>();

        // Grid content
        var gridGO = new GameObject("GridContent");
        gridGO.transform.SetParent(scrollArea.transform, false);
        var gridRT = gridGO.AddComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0, 1);
        gridRT.anchorMax = new Vector2(1, 1);
        gridRT.pivot = new Vector2(0.5f, 1);
        gridRT.anchoredPosition = Vector2.zero;

        var grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(470, 470);
        grid.spacing = new Vector2(30, 30);
        grid.padding = new RectOffset(30, 30, 24, 24);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;

        var csf = gridGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = gridRT;

        // ── Fullscreen panel (hidden) ──
        var fsPanel = CreateStretchImage(safeArea.transform, "FullscreenPanel", new Color(0, 0, 0, 0.85f));
        fsPanel.GetComponent<Image>().raycastTarget = true;
        fsPanel.SetActive(false);

        var fsImageGO = new GameObject("FullscreenImage");
        fsImageGO.transform.SetParent(fsPanel.transform, false);
        var fsImageRT = fsImageGO.AddComponent<RectTransform>();
        fsImageRT.anchorMin = new Vector2(0.05f, 0.15f);
        fsImageRT.anchorMax = new Vector2(0.95f, 0.85f);
        fsImageRT.offsetMin = Vector2.zero;
        fsImageRT.offsetMax = Vector2.zero;
        var fsRawImg = fsImageGO.AddComponent<RawImage>();
        fsRawImg.color = Color.white;
        fsRawImg.raycastTarget = false;

        // Close button on fullscreen
        var closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(fsPanel.transform, false);
        var closeRT = closeGO.AddComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1, 1);
        closeRT.anchorMax = new Vector2(1, 1);
        closeRT.pivot = new Vector2(1, 1);
        closeRT.anchoredPosition = new Vector2(-20, -20);
        closeRT.sizeDelta = new Vector2(80, 80);
        var closeImg = closeGO.AddComponent<Image>();
        closeImg.color = new Color(1, 1, 1, 0.8f);
        var closeBtn = closeGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;

        var closeTxtGO = new GameObject("X");
        closeTxtGO.transform.SetParent(closeGO.transform, false);
        var closeTxtRT = closeTxtGO.AddComponent<RectTransform>();
        StretchFull(closeTxtRT);
        var closeTxt = closeTxtGO.AddComponent<TextMeshProUGUI>();
        closeTxt.text = "✕";
        closeTxt.fontSize = 40;
        closeTxt.color = Color.black;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.raycastTarget = false;

        // ── Controller ──
        var controller = canvasGO.AddComponent<DrawingGalleryController>();
        controller.gridContainer = gridRT;
        controller.homeButton = homeGO.GetComponent<Button>();
        controller.fullscreenPanel = fsPanel;
        controller.fullscreenImage = fsRawImg;
        controller.fullscreenCloseButton = closeBtn;
        controller.roundedRectSprite = roundedRect;

        // ── Empty state text (hidden by controller when drawings exist) ──
        var emptyGO = new GameObject("EmptyText");
        emptyGO.transform.SetParent(scrollArea.transform, false);
        var emptyRT = emptyGO.AddComponent<RectTransform>();
        emptyRT.anchorMin = new Vector2(0.1f, 0.4f);
        emptyRT.anchorMax = new Vector2(0.9f, 0.6f);
        emptyRT.offsetMin = Vector2.zero;
        emptyRT.offsetMax = Vector2.zero;
        var emptyTMP = emptyGO.AddComponent<TextMeshProUGUI>();
        emptyTMP.text = HebrewFixer.Fix("עדיין אין ציורים!\nצבעו ושמרו ציור כדי לראות אותו כאן");
        emptyTMP.fontSize = 36;
        emptyTMP.color = new Color(0.5f, 0.4f, 0.4f);
        emptyTMP.alignment = TextAlignmentOptions.Center;
        emptyTMP.isRightToLeftText = false;
        emptyTMP.raycastTarget = false;

        controller.emptyText = emptyGO;

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/DrawingGallery.unity");
    }

    // ── Helpers ──

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
