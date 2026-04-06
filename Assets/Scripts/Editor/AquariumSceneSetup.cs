using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the AquariumScene — a collectible sandbox where players
/// can watch fish, feed them, and arrange decorations.
/// Layered underwater background with ambient effects.
/// </summary>
public class AquariumSceneSetup
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly Color HeaderColor = new Color(0.15f, 0.55f, 0.75f, 0.55f); // soft blue, semi-transparent
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    // Ocean palette — soft, luminous, aquarium feel
    private static readonly Color WaterTop       = HexColor("#A8E6F0");  // bright aqua surface
    private static readonly Color WaterMid       = HexColor("#62C7E8");  // clean turquoise
    private static readonly Color WaterBottom    = HexColor("#4AAED4");  // gentle deeper blue
    private static readonly Color WaveFarColor   = HexColor("#8AD4EA");  // far wave — soft, faded
    private static readonly Color WaveNearColor  = HexColor("#6EC5E2");  // near wave — slightly richer
    private static readonly Color SandBack       = HexColor("#F0D8A8");  // warm creamy sand
    private static readonly Color SandFront      = HexColor("#F8E8C4");  // soft pale sand

    // Glass tank frame
    private static readonly Color FrameColor     = HexColor("#B8D8E8");  // soft blue-grey glass
    private static readonly float FrameWidth     = 18f;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Aquarium Scene Setup", "Copying assets to Resources…", 0.3f);
            CopyAquariumToResources();

            EditorUtility.DisplayProgressBar("Aquarium Scene Setup", "Building scene…", 0.6f);
            CreateScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ── RESOURCES ──

    private static void CopyAquariumToResources()
    {
        EnsureFolder("Assets/Resources/Aquarium");

        // Copy sprite sheets to Resources for runtime loading
        CopyAsset("Assets/Art/Aquarium/Fish.png", "Assets/Resources/Aquarium/Fish.png");
        CopyAsset("Assets/Art/Aquarium/AquariumItem.png", "Assets/Resources/Aquarium/AquariumItem.png");
        CopyAsset("Assets/Art/Aquarium/AquariumIcon.png", "Assets/Resources/Aquarium/AquariumIcon.png");
        CopyAsset("Assets/Art/Aquarium/Food.png", "Assets/Resources/Aquarium/Food.png");
        CopyAsset("Assets/Art/Gift.png", "Assets/Resources/Gift.png");
        CopyMeta("Assets/Art/Gift.png.meta", "Assets/Resources/Gift.png.meta");

        // Copy meta files to preserve sprite slicing
        CopyMeta("Assets/Art/Aquarium/Fish.png.meta", "Assets/Resources/Aquarium/Fish.png.meta");
        CopyMeta("Assets/Art/Aquarium/AquariumItem.png.meta", "Assets/Resources/Aquarium/AquariumItem.png.meta");
        CopyMeta("Assets/Art/Aquarium/AquariumIcon.png.meta", "Assets/Resources/Aquarium/AquariumIcon.png.meta");
        CopyMeta("Assets/Art/Aquarium/Food.png.meta", "Assets/Resources/Aquarium/Food.png.meta");
    }

    private static void CopyAsset(string src, string dst)
    {
        if (!System.IO.File.Exists(src)) { Debug.LogWarning($"[AquariumSetup] Source not found: {src}"); return; }
        if (System.IO.File.Exists(dst)) System.IO.File.Delete(dst);
        System.IO.File.Copy(src, dst, true);
        AssetDatabase.ImportAsset(dst);
    }

    private static void CopyMeta(string src, string dst)
    {
        if (!System.IO.File.Exists(src)) return;
        if (System.IO.File.Exists(dst)) System.IO.File.Delete(dst);
        System.IO.File.Copy(src, dst, true);
    }

    // ── SCENE ──

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
        cam.backgroundColor = WaterMid;
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
        var canvasGO = new GameObject("AquariumCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");
        string artDir = "Assets/Art/World";

        // Reuse World art sprites with ocean/sand colors
        var hillsLargeSprite  = LoadSprite($"{artDir}/hillsLarge.png");
        var hillsSprite       = LoadSprite($"{artDir}/hills.png");
        var groundLayer1      = LoadSprite($"{artDir}/groundLayer1.png");
        var groundLayer2      = LoadSprite($"{artDir}/groundLayer2.png");

        // ════════════════════════════════════════
        //  LAYERED BACKGROUND (far to near)
        //  Vertical water gradient + wave shapes + sand.
        // ════════════════════════════════════════

        // Water gradient: 3 horizontal bands (top=light aqua, mid=blue, bottom=deep blue)
        var waterTopGO = StretchImg(canvasGO.transform, "WaterTop", WaterTop);
        waterTopGO.GetComponent<Image>().raycastTarget = false;
        var waterTopRT = waterTopGO.GetComponent<RectTransform>();
        waterTopRT.anchorMin = new Vector2(0, 0.6f);
        waterTopRT.anchorMax = new Vector2(1, 1);

        var waterBtmGO = StretchImg(canvasGO.transform, "WaterBottom", WaterBottom);
        waterBtmGO.GetComponent<Image>().raycastTarget = false;
        var waterBtmRT = waterBtmGO.GetComponent<RectTransform>();
        waterBtmRT.anchorMin = new Vector2(0, 0);
        waterBtmRT.anchorMax = new Vector2(1, 0.55f);

        // Far wave (faded, lighter — creates depth)
        var waveFarGO = CreateSpriteLayer(canvasGO.transform, "WaveFar", hillsLargeSprite,
            new Vector2(0, 0.35f), new Vector2(1, 0.65f), WaveFarColor);

        // Near wave (richer color — closer to viewer)
        var waveNearGO = CreateSpriteLayer(canvasGO.transform, "WaveNear", hillsSprite,
            new Vector2(0, 0.25f), new Vector2(1, 0.5f), WaveNearColor);

        // Sand back layer (warm golden)
        var sandBackGO = CreateSpriteLayer(canvasGO.transform, "SandBack", groundLayer1,
            new Vector2(0, 0), new Vector2(1, 0.35f), SandBack);

        // Sand front layer (lighter, warmer)
        var sandFrontGO = CreateSpriteLayer(canvasGO.transform, "SandFront", groundLayer2,
            new Vector2(0, 0), new Vector2(1, 0.2f), SandFront);
        var sandRT = sandFrontGO.GetComponent<RectTransform>();

        // ── Background life layer (behind gameplay, non-interactive) ──
        var bgLifeGO = new GameObject("BackgroundLife");
        bgLifeGO.transform.SetParent(canvasGO.transform, false);
        var bgLifeRT = bgLifeGO.AddComponent<RectTransform>();
        Full(bgLifeRT);
        var bgLife = bgLifeGO.AddComponent<AquariumBackgroundLife>();
        bgLife.areaRT = bgLifeRT;
        bgLife.circleSprite = circleSprite;

        // ── Gameplay area (fish + decorations — ON TOP of all background layers) ──
        var gameplayGO = new GameObject("GameplayArea");
        gameplayGO.transform.SetParent(canvasGO.transform, false);
        var gameplayRT = gameplayGO.AddComponent<RectTransform>();
        Full(gameplayRT);
        gameplayRT.anchorMin = new Vector2(0.02f, 0.15f);
        gameplayRT.anchorMax = new Vector2(0.88f, 0.88f);
        gameplayRT.offsetMin = Vector2.zero;
        gameplayRT.offsetMax = Vector2.zero;

        // ── FX area (bubbles + light rays + poppable bubbles, on top of everything except UI) ──
        var fxGO = new GameObject("FXArea");
        fxGO.transform.SetParent(canvasGO.transform, false);
        var fxRT = fxGO.AddComponent<RectTransform>();
        Full(fxRT);

        var ambience = fxGO.AddComponent<AquariumAmbience>();
        ambience.areaRT = fxRT;
        ambience.circleSprite = circleSprite;

        // ── GLASS FRAME (aquarium tank look) ──
        CreateGlassFrame(canvasGO.transform, circleSprite);

        // ── SAFE AREA (UI) ──
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        Full(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // ── TOP BAR ──
        var topBar = StretchImg(safeArea.transform, "TopBar", HeaderColor);
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.sizeDelta = new Vector2(0, TopBarHeight);
        topBar.GetComponent<Image>().raycastTarget = false;
        topBar.AddComponent<ThemeHeader>();

        // Title
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D0\u05E7\u05D5\u05D5\u05E8\u05D9\u05D5\u05DD"); // אקווריום
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

        // ── FOOD BUTTON (top-right, below header) ──
        var foodBtnGO = new GameObject("FoodButton");
        foodBtnGO.transform.SetParent(safeArea.transform, false);
        var foodBtnRT = foodBtnGO.AddComponent<RectTransform>();
        foodBtnRT.anchorMin = new Vector2(1, 1);
        foodBtnRT.anchorMax = new Vector2(1, 1);
        foodBtnRT.pivot = new Vector2(1, 1);
        foodBtnRT.sizeDelta = new Vector2(200, 200);
        foodBtnRT.anchoredPosition = new Vector2(-16, -TopBarHeight - 10);
        var foodBtnImg = foodBtnGO.AddComponent<Image>();
        // Food_0 sprite loaded at runtime by controller
        foodBtnImg.preserveAspect = true;
        foodBtnImg.color = Color.white;
        var foodBtn = foodBtnGO.AddComponent<Button>();
        foodBtn.targetGraphic = foodBtnImg;

        // ── PROGRESS BAR (bottom center, above sand) ──
        var progressBgGO = new GameObject("ProgressBarBg");
        progressBgGO.transform.SetParent(safeArea.transform, false);
        var progressBgRT = progressBgGO.AddComponent<RectTransform>();
        progressBgRT.anchorMin = new Vector2(0.3f, 0);
        progressBgRT.anchorMax = new Vector2(0.7f, 0);
        progressBgRT.pivot = new Vector2(0.5f, 0);
        progressBgRT.sizeDelta = new Vector2(0, 18);
        progressBgRT.anchoredPosition = new Vector2(0, 30);
        var progressBgImg = progressBgGO.AddComponent<Image>();
        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
        if (roundedRect != null) { progressBgImg.sprite = roundedRect; progressBgImg.type = Image.Type.Sliced; }
        progressBgImg.color = new Color(0f, 0f, 0f, 0.3f);
        progressBgImg.raycastTarget = false;

        var progressFillGO = new GameObject("ProgressBarFill");
        progressFillGO.transform.SetParent(progressBgGO.transform, false);
        var progressFillRT = progressFillGO.AddComponent<RectTransform>();
        progressFillRT.anchorMin = Vector2.zero;
        progressFillRT.anchorMax = new Vector2(0, 1);
        progressFillRT.offsetMin = new Vector2(2, 2);
        progressFillRT.offsetMax = new Vector2(-2, -2);
        var progressFillImg = progressFillGO.AddComponent<Image>();
        if (roundedRect != null) { progressFillImg.sprite = roundedRect; progressFillImg.type = Image.Type.Sliced; }
        progressFillImg.color = new Color(0.3f, 0.7f, 1f);
        progressFillImg.raycastTarget = false;

        // ── EMPTY HINT TEXT ──
        var hintGO = new GameObject("EmptyHintText");
        hintGO.transform.SetParent(canvasGO.transform, false);
        var hintRT = hintGO.AddComponent<RectTransform>();
        hintRT.anchorMin = new Vector2(0.2f, 0.4f);
        hintRT.anchorMax = new Vector2(0.8f, 0.6f);
        hintRT.offsetMin = Vector2.zero;
        hintRT.offsetMax = Vector2.zero;
        var hintTMP = hintGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(hintTMP, "\u05E9\u05D7\u05E7\u05D5 \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05DB\u05D3\u05D9 \u05DC\u05D2\u05DC\u05D5\u05EA \u05D3\u05D2\u05D9\u05DD!");
        hintTMP.fontSize = 32;
        hintTMP.color = new Color(1, 1, 1, 0.5f);
        hintTMP.alignment = TextAlignmentOptions.Center;
        hintTMP.fontStyle = FontStyles.Bold;
        hintTMP.raycastTarget = false;
        hintGO.SetActive(false);

        // ── CONTROLLER ──
        var controller = canvasGO.AddComponent<AquariumController>();
        controller.gameplayArea = gameplayRT;
        controller.sandLayer = sandRT;
        controller.backButton = homeGO.GetComponent<Button>();
        controller.foodButton = foodBtn;
        controller.foodButtonImage = foodBtnImg;
        controller.backgroundImage = waterTopGO.GetComponent<Image>();
        // Gift sprite loaded at runtime from Resources/GiftBox
        controller.emptyHintText = hintTMP;
        controller.ambience = ambience;
        controller.progressBarBg = progressBgRT;
        controller.progressBarFill = progressFillRT;
        controller.progressBarFillImage = progressFillImg;

        // ── SAVE SCENE ──
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/AquariumScene.unity");
        Debug.Log("[AquariumSetup] Scene created: Assets/Scenes/AquariumScene.unity");
    }

    // ── GLASS TANK FRAME ──

    private static void CreateGlassFrame(Transform parent, Sprite circleSprite)
    {
        var frame = new GameObject("GlassFrame");
        frame.transform.SetParent(parent, false);
        var frameRT = frame.AddComponent<RectTransform>();
        Full(frameRT);

        // ── Thick glass borders (outer frame of the tank) ──
        Color outerFrame = new Color(FrameColor.r * 0.85f, FrameColor.g * 0.85f, FrameColor.b * 0.85f, 0.75f);
        Color innerEdge = new Color(1f, 1f, 1f, 0.15f); // inner glass edge highlight

        // Outer borders
        CreateBorder(frame.transform, "FrameTop", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, FrameWidth), outerFrame);
        CreateBorder(frame.transform, "FrameBottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, FrameWidth), outerFrame);
        CreateBorder(frame.transform, "FrameLeft", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(FrameWidth, 0), outerFrame);
        CreateBorder(frame.transform, "FrameRight", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(FrameWidth, 0), outerFrame);

        // Inner edge highlights (thin white line inside the frame)
        float edgeW = 2f;
        CreateBorder(frame.transform, "EdgeTop", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, edgeW), innerEdge);
        CreateBorder(frame.transform, "EdgeLeft", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(edgeW, 0), innerEdge);

        // Glossy top edge (light strip at very top — simulates glass thickness reflection)
        var glossGO = new GameObject("GlossyTopEdge");
        glossGO.transform.SetParent(frame.transform, false);
        var glossRT = glossGO.AddComponent<RectTransform>();
        glossRT.anchorMin = new Vector2(0.02f, 1);
        glossRT.anchorMax = new Vector2(0.98f, 1);
        glossRT.pivot = new Vector2(0.5f, 1);
        glossRT.sizeDelta = new Vector2(0, 6);
        glossRT.anchoredPosition = new Vector2(0, -FrameWidth);
        var glossImg = glossGO.AddComponent<Image>();
        glossImg.color = new Color(1f, 1f, 1f, 0.1f);
        glossImg.raycastTarget = false;

        // ── Glass reflections (vertical streaks on left side) ──

        // Main reflection — tall, left side
        CreateReflection(frame.transform, "ReflectLeft1",
            new Vector2(0.015f, 0.15f), new Vector2(0.03f, 0.85f),
            new Color(1f, 1f, 1f, 0.1f));

        // Secondary reflection — shorter, slightly right
        CreateReflection(frame.transform, "ReflectLeft2",
            new Vector2(0.04f, 0.3f), new Vector2(0.05f, 0.7f),
            new Color(1f, 1f, 1f, 0.06f));

        // Right side subtle reflection
        CreateReflection(frame.transform, "ReflectRight",
            new Vector2(0.97f, 0.2f), new Vector2(0.985f, 0.6f),
            new Color(1f, 1f, 1f, 0.05f));

        // ── Corner glows (soft circles at corners for glass roundness) ──
        if (circleSprite != null)
        {
            CreateCornerGlow(frame.transform, circleSprite, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), 100f);   // top-left
            CreateCornerGlow(frame.transform, circleSprite, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -10), 80f);   // top-right
            CreateCornerGlow(frame.transform, circleSprite, new Vector2(0, 0), new Vector2(0, 0), new Vector2(10, 10), 60f);     // bottom-left
        }
    }

    private static void CreateBorder(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 sizeDelta, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    private static void CreateReflection(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    private static void CreateCornerGlow(Transform parent, Sprite circleSprite,
        Vector2 anchor, Vector2 pivot, Vector2 offset, float size)
    {
        var go = new GameObject("CornerGlow");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = offset;
        var img = go.AddComponent<Image>();
        img.sprite = circleSprite;
        img.color = new Color(1f, 1f, 1f, 0.07f);
        img.raycastTarget = false;
    }

    // ── HELPERS ──

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
