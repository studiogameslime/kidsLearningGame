using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the ColorMixing scene for the kids learning game.
/// Run via Tools > Kids Learning Game > Setup Color Mixing.
/// </summary>
public class ColorMixingSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1080, 1920);

    private static readonly Color BgColor     = HexColor("#F3E5F5"); // soft lavender
    private static readonly Color TopBarColor = HexColor("#CE93D8"); // purple-ish

    private const int TopBarHeight   = 130;
    private const int BottomBarHeight = 120;

    [MenuItem("Tools/Kids Learning Game/Setup Color Mixing")]
    public static void RunSetup()
    {
        if (!EditorUtility.DisplayDialog(
            "Color Mixing Setup",
            "This will create/overwrite:\n• ColorMixing scene\n\nContinue?",
            "Build", "Cancel"))
            return;

        RunSetupSilent();
        EditorSceneManager.OpenScene("Assets/Scenes/ColorMixing.unity");
        EditorUtility.DisplayDialog("Done!", "Color Mixing built.\nPress Play to test!", "OK");
    }

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Color Mixing Setup", "Building scene…", 0.5f);
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
        var canvasGO = new GameObject("ColorMixingCanvas");
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

        // ── TOP BAR ──
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
        titleTMP.text = "Color Mixing";
        titleTMP.fontSize = 48;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button (top-left)
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(16, -15), new Vector2(90, 90));

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

        // ── TARGET COLOR (top of play area) ──
        // Label
        var targetLabel = new GameObject("TargetLabel");
        targetLabel.transform.SetParent(playArea.transform, false);
        var targetLabelRT = targetLabel.AddComponent<RectTransform>();
        targetLabelRT.anchorMin = new Vector2(0.5f, 1);
        targetLabelRT.anchorMax = new Vector2(0.5f, 1);
        targetLabelRT.pivot = new Vector2(0.5f, 1);
        targetLabelRT.anchoredPosition = new Vector2(0, -20);
        targetLabelRT.sizeDelta = new Vector2(400, 60);
        var labelTMP = targetLabel.AddComponent<TextMeshProUGUI>();
        labelTMP.text = "Make this color!";
        labelTMP.fontSize = 38;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.color = HexColor("#7B1FA2");
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.raycastTarget = false;

        // Target circle with glow ring
        var targetGlow = new GameObject("TargetGlow");
        targetGlow.transform.SetParent(playArea.transform, false);
        var targetGlowRT = targetGlow.AddComponent<RectTransform>();
        targetGlowRT.anchorMin = new Vector2(0.5f, 1);
        targetGlowRT.anchorMax = new Vector2(0.5f, 1);
        targetGlowRT.pivot = new Vector2(0.5f, 1);
        targetGlowRT.anchoredPosition = new Vector2(0, -90);
        targetGlowRT.sizeDelta = new Vector2(230, 230);
        var glowImg = targetGlow.AddComponent<Image>();
        if (circleSprite != null) glowImg.sprite = circleSprite;
        glowImg.color = new Color(1f, 1f, 1f, 0.3f);
        glowImg.raycastTarget = false;

        var targetGO = new GameObject("TargetCircle");
        targetGO.transform.SetParent(targetGlow.transform, false);
        var targetRT = targetGO.AddComponent<RectTransform>();
        targetRT.anchorMin = new Vector2(0.5f, 0.5f);
        targetRT.anchorMax = new Vector2(0.5f, 0.5f);
        targetRT.sizeDelta = new Vector2(190, 190);
        var targetImg = targetGO.AddComponent<Image>();
        if (circleSprite != null) targetImg.sprite = circleSprite;
        targetImg.color = Color.white;
        targetImg.raycastTarget = false;

        // Target shine
        var targetShine = new GameObject("Shine");
        targetShine.transform.SetParent(targetGO.transform, false);
        var tsRT = targetShine.AddComponent<RectTransform>();
        tsRT.anchorMin = new Vector2(0.15f, 0.55f);
        tsRT.anchorMax = new Vector2(0.45f, 0.85f);
        tsRT.offsetMin = Vector2.zero;
        tsRT.offsetMax = Vector2.zero;
        var tsImg = targetShine.AddComponent<Image>();
        if (circleSprite != null) tsImg.sprite = circleSprite;
        tsImg.color = new Color(1f, 1f, 1f, 0.3f);
        tsImg.raycastTarget = false;

        // ── MIX SLOTS AREA (center) ──
        var slotsArea = new GameObject("SlotsArea");
        slotsArea.transform.SetParent(playArea.transform, false);
        var slotsAreaRT = slotsArea.AddComponent<RectTransform>();
        slotsAreaRT.anchorMin = new Vector2(0.5f, 0.5f);
        slotsAreaRT.anchorMax = new Vector2(0.5f, 0.5f);
        slotsAreaRT.anchoredPosition = new Vector2(0, 50);
        slotsAreaRT.sizeDelta = new Vector2(500, 250);

        float slotSize = 180f;
        float slotSpacing = 160f; // distance from center

        // Left slot (empty circle)
        var slotLeft = new GameObject("SlotLeft");
        slotLeft.transform.SetParent(slotsArea.transform, false);
        var slotLeftRT = slotLeft.AddComponent<RectTransform>();
        slotLeftRT.anchorMin = new Vector2(0.5f, 0.5f);
        slotLeftRT.anchorMax = new Vector2(0.5f, 0.5f);
        slotLeftRT.anchoredPosition = new Vector2(-slotSpacing, 0);
        slotLeftRT.sizeDelta = new Vector2(slotSize, slotSize);
        var slotLeftImg = slotLeft.AddComponent<Image>();
        if (circleSprite != null) slotLeftImg.sprite = circleSprite;
        slotLeftImg.color = new Color(0.88f, 0.88f, 0.88f, 0.5f);
        slotLeftImg.raycastTarget = false;

        // Dashed border hint for left slot
        var slotLeftBorder = new GameObject("Border");
        slotLeftBorder.transform.SetParent(slotLeft.transform, false);
        var slbRT = slotLeftBorder.AddComponent<RectTransform>();
        slbRT.anchorMin = new Vector2(-0.04f, -0.04f);
        slbRT.anchorMax = new Vector2(1.04f, 1.04f);
        slbRT.offsetMin = Vector2.zero;
        slbRT.offsetMax = Vector2.zero;
        var slbImg = slotLeftBorder.AddComponent<Image>();
        if (circleSprite != null) slbImg.sprite = circleSprite;
        slbImg.color = new Color(0.75f, 0.65f, 0.82f, 0.4f);
        slbImg.raycastTarget = false;
        slotLeftBorder.transform.SetAsFirstSibling();

        // Right slot (empty circle)
        var slotRight = new GameObject("SlotRight");
        slotRight.transform.SetParent(slotsArea.transform, false);
        var slotRightRT = slotRight.AddComponent<RectTransform>();
        slotRightRT.anchorMin = new Vector2(0.5f, 0.5f);
        slotRightRT.anchorMax = new Vector2(0.5f, 0.5f);
        slotRightRT.anchoredPosition = new Vector2(slotSpacing, 0);
        slotRightRT.sizeDelta = new Vector2(slotSize, slotSize);
        var slotRightImg = slotRight.AddComponent<Image>();
        if (circleSprite != null) slotRightImg.sprite = circleSprite;
        slotRightImg.color = new Color(0.88f, 0.88f, 0.88f, 0.5f);
        slotRightImg.raycastTarget = false;

        // Dashed border hint for right slot
        var slotRightBorder = new GameObject("Border");
        slotRightBorder.transform.SetParent(slotRight.transform, false);
        var srbRT = slotRightBorder.AddComponent<RectTransform>();
        srbRT.anchorMin = new Vector2(-0.04f, -0.04f);
        srbRT.anchorMax = new Vector2(1.04f, 1.04f);
        srbRT.offsetMin = Vector2.zero;
        srbRT.offsetMax = Vector2.zero;
        var srbImg = slotRightBorder.AddComponent<Image>();
        if (circleSprite != null) srbImg.sprite = circleSprite;
        srbImg.color = new Color(0.75f, 0.65f, 0.82f, 0.4f);
        srbImg.raycastTarget = false;
        slotRightBorder.transform.SetAsFirstSibling();

        // Plus sign between slots
        var plusGO = new GameObject("PlusSign");
        plusGO.transform.SetParent(slotsArea.transform, false);
        var plusRT = plusGO.AddComponent<RectTransform>();
        plusRT.anchorMin = new Vector2(0.5f, 0.5f);
        plusRT.anchorMax = new Vector2(0.5f, 0.5f);
        plusRT.sizeDelta = new Vector2(60, 60);
        var plusTMP = plusGO.AddComponent<TextMeshProUGUI>();
        plusTMP.text = "+";
        plusTMP.fontSize = 60;
        plusTMP.fontStyle = FontStyles.Bold;
        plusTMP.color = new Color(0.6f, 0.5f, 0.7f, 0.6f);
        plusTMP.alignment = TextAlignmentOptions.Center;
        plusTMP.raycastTarget = false;

        // Result circle (below slots, initially hidden)
        var resultGO = new GameObject("ResultCircle");
        resultGO.transform.SetParent(slotsArea.transform, false);
        var resultRT = resultGO.AddComponent<RectTransform>();
        resultRT.anchorMin = new Vector2(0.5f, 0.5f);
        resultRT.anchorMax = new Vector2(0.5f, 0.5f);
        resultRT.anchoredPosition = Vector2.zero;
        resultRT.sizeDelta = new Vector2(220, 220);
        var resultImg = resultGO.AddComponent<Image>();
        if (circleSprite != null) resultImg.sprite = circleSprite;
        resultImg.color = new Color(1, 1, 1, 0);
        resultImg.raycastTarget = false;
        resultGO.transform.localScale = Vector3.zero;

        // Result shine
        var resultShine = new GameObject("Shine");
        resultShine.transform.SetParent(resultGO.transform, false);
        var rsRT = resultShine.AddComponent<RectTransform>();
        rsRT.anchorMin = new Vector2(0.15f, 0.55f);
        rsRT.anchorMax = new Vector2(0.45f, 0.85f);
        rsRT.offsetMin = Vector2.zero;
        rsRT.offsetMax = Vector2.zero;
        var rsImg = resultShine.AddComponent<Image>();
        if (circleSprite != null) rsImg.sprite = circleSprite;
        rsImg.color = new Color(1f, 1f, 1f, 0.3f);
        rsImg.raycastTarget = false;

        // ── COLOR PALETTE (bottom of play area) ──
        var palette = new GameObject("ColorPalette");
        palette.transform.SetParent(playArea.transform, false);
        var paletteRT = palette.AddComponent<RectTransform>();
        paletteRT.anchorMin = new Vector2(0.5f, 0);
        paletteRT.anchorMax = new Vector2(0.5f, 0);
        paletteRT.pivot = new Vector2(0.5f, 0);
        paletteRT.anchoredPosition = new Vector2(0, 40);
        paletteRT.sizeDelta = new Vector2(900, 200);

        // ── CONTROLLER ──
        var controller = canvasGO.AddComponent<ColorMixingController>();
        controller.playArea = playAreaRT;
        controller.targetColorCircle = targetImg;
        controller.slotLeftImage = slotLeftImg;
        controller.slotRightImage = slotRightImg;
        controller.resultCircle = resultImg;
        controller.colorPalette = paletteRT;
        controller.circleSprite = circleSprite;

        // Wire buttons
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ColorMixing.unity");
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
