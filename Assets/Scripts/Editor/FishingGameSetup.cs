using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the FishingGame scene using the same layered environmental construction
/// as the World scene, but adapted for a sea setting.
///
/// Layer structure (back to front):
///   1. skyLayer          — full sky background
///   2. cloudLayerB1      — distant cloud layer (existing cloud assets)
///   3. cloudLayerB2      — closer cloud layer
///   4. sun               — sun in sky
///   5. seaBack           — distant sea (hills sprite tinted blue, creates wavy horizon)
///   6. seaSurface        — waterline layer (groundLayer1 tinted aqua, organic edge)
///   7. boatLayer         — Elroey on the surface
///   8. speechBubble      — target fish indicator
///   9. seaMid            — main water body (groundLayer2 tinted blue)
///  10. fishSwimArea      — interactive fish zone
///  11. seaFront          — foreground water overlay (hills tinted, low opacity)
///  12. seaDeep           — deep water flat
///  13. sandLayer         — seabed (groundLayer1 tinted sand)
///  14. header            — UI bar
///  15. fishingLine       — line visual
/// </summary>
public class FishingGameSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    // Art paths
    private const string WorldArt = "Assets/Art/World";
    private const string FishingArt = "Assets/Art/Fishing";

    // Color palette
    private static readonly Color SkyColor     = HexColor("#A8DEFF");
    private static readonly Color SeaBackTint  = HexColor("#6BC8E8");  // distant sea — light
    private static readonly Color SeaSurfTint  = HexColor("#5EBFD8");  // surface — organic edge
    private static readonly Color SeaMidTint   = HexColor("#3BA8CC");  // mid water body
    private static readonly Color SeaFrontTint = new Color(0.18f, 0.55f, 0.72f, 0.35f); // foreground overlay
    private static readonly Color SeaDeepColor = HexColor("#2A8CC4");  // deep flat
    private static readonly Color SandColor     = HexColor("#F2D9A0");  // warm sandy beige
    private static readonly Color SandDarkColor = HexColor("#D4B87A"); // darker sand accent
    private static readonly Color FoamColor    = new Color(1f, 1f, 1f, 0.2f);
    private static readonly Color TopBarColor  = new Color(0.37f, 0.78f, 0.91f, 0.85f);
    private static readonly Color BubbleColor  = new Color(1f, 1f, 1f, 0.92f);

    // Vertical layout anchors
    private const float WaterlineY = 0.50f;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Fishing Game Setup", "Updating game data…", 0.2f);
            UpdateGameData();
            EditorUtility.DisplayProgressBar("Fishing Game Setup", "Building scene…", 0.5f);
            BuildScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void UpdateGameData()
    {
        var game = AssetDatabase.LoadAssetAtPath<GameItemData>("Assets/Data/Games/FishingGame.asset");
        if (game == null) return;
        game.targetSceneName = "FishingGame";
        game.hasSubItems = false;
        EditorUtility.SetDirty(game);
    }

    private static void BuildScene()
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Load shared UI sprites
        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");

        // Load world environment sprites (reused as sea layers)
        var hillsLargeSprite = LoadSprite($"{WorldArt}/hillsLarge.png");
        var hillsSprite      = LoadSprite($"{WorldArt}/hills.png");
        var groundLayer1     = LoadSprite($"{WorldArt}/groundLayer1.png");
        var groundLayer2     = LoadSprite($"{WorldArt}/groundLayer2.png");
        var sunSprite        = LoadSprite($"{WorldArt}/sun.png");

        // Load cloud assets
        var cloud1 = LoadSprite($"{WorldArt}/cloud1.png");
        var cloud3 = LoadSprite($"{WorldArt}/cloud3.png");
        var cloud5 = LoadSprite($"{WorldArt}/cloud5.png");
        var cloud6 = LoadSprite($"{WorldArt}/cloud6.png");
        var cloud7 = LoadSprite($"{WorldArt}/cloud7.png");

        // ── Camera ──
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera"; camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SkyColor;
        cam.orthographic = true;
        var urp = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urp != null) camGO.AddComponent(urp);

        // ── EventSystem ──
        var esGO = new GameObject("EventSystem"); esGO.AddComponent<EventSystem>();
        var inp = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inp != null) esGO.AddComponent(inp); else esGO.AddComponent<StandaloneInputModule>();

        // ── Canvas ──
        var canvasGO = new GameObject("FishingUICanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(canvasGO.transform, false);
        Full(safeGO.AddComponent<RectTransform>());
        safeGO.AddComponent<SafeAreaHandler>();

        // ════════════════════════════════════════════════════
        //  LAYER 1: SKY BACKGROUND (full canvas)
        // ════════════════════════════════════════════════════

        var skyGO = StretchImage(safeGO.transform, "skyLayer", SkyColor);
        skyGO.GetComponent<Image>().raycastTarget = false;

        // ════════════════════════════════════════════════════
        //  LAYER 2: CLOUD LAYERS (using existing world cloud assets)
        // ════════════════════════════════════════════════════

        // cloudLayerB1 — distant, smaller, higher
        PlaceCloud(safeGO.transform, "cloudLayerB1_a", cloud1, 0.08f, 0.88f, 160);
        PlaceCloud(safeGO.transform, "cloudLayerB1_b", cloud5, 0.35f, 0.92f, 130);
        PlaceCloud(safeGO.transform, "cloudLayerB1_c", cloud7, 0.75f, 0.86f, 140);

        // cloudLayerB2 — closer, larger, lower
        PlaceCloud(safeGO.transform, "cloudLayerB2_a", cloud3, 0.22f, 0.82f, 200);
        PlaceCloud(safeGO.transform, "cloudLayerB2_b", cloud6, 0.58f, 0.78f, 190);

        // ════════════════════════════════════════════════════
        //  LAYER 3: SUN
        // ════════════════════════════════════════════════════

        if (sunSprite != null)
        {
            var sunGO = new GameObject("Sun");
            sunGO.transform.SetParent(safeGO.transform, false);
            var sunRT = sunGO.AddComponent<RectTransform>();
            sunRT.anchorMin = sunRT.anchorMax = new Vector2(0.88f, 0.90f);
            sunRT.sizeDelta = new Vector2(130, 130);
            var sunImg = sunGO.AddComponent<Image>();
            sunImg.sprite = sunSprite; sunImg.preserveAspect = true;
            sunImg.color = Color.white; sunImg.raycastTarget = false;

            // Sun glow
            var glowGO = new GameObject("SunGlow");
            glowGO.transform.SetParent(sunGO.transform, false);
            glowGO.transform.SetAsFirstSibling();
            var glowRT = glowGO.AddComponent<RectTransform>();
            glowRT.anchorMin = new Vector2(-0.5f, -0.5f);
            glowRT.anchorMax = new Vector2(1.5f, 1.5f);
            glowRT.offsetMin = Vector2.zero; glowRT.offsetMax = Vector2.zero;
            var glowImg = glowGO.AddComponent<Image>();
            if (circleSprite != null) glowImg.sprite = circleSprite;
            glowImg.color = new Color(1f, 0.95f, 0.7f, 0.2f);
            glowImg.raycastTarget = false;
        }

        // ════════════════════════════════════════════════════
        //  LAYER 4: seaBack — distant sea horizon
        //  Uses hillsLarge sprite tinted blue → gives organic wavy horizon
        // ════════════════════════════════════════════════════

        if (hillsLargeSprite != null)
            CreateSpriteLayer(safeGO.transform, "seaBack", hillsLargeSprite,
                new Vector2(0, WaterlineY - 0.18f), new Vector2(1, WaterlineY + 0.08f), SeaBackTint);

        // ════════════════════════════════════════════════════
        //  LAYER 5: seaSurface — waterline with organic edge
        //  Uses groundLayer1 sprite tinted aqua → wavy surface line
        // ════════════════════════════════════════════════════

        if (groundLayer1 != null)
            CreateSpriteLayer(safeGO.transform, "seaSurface", groundLayer1,
                new Vector2(0, WaterlineY - 0.12f), new Vector2(1, WaterlineY + 0.04f), SeaSurfTint);

        // Foam highlight at waterline
        var foamGO = StretchImage(safeGO.transform, "foamLayer", FoamColor);
        var foamRT = foamGO.GetComponent<RectTransform>();
        foamRT.anchorMin = new Vector2(0, WaterlineY - 0.01f);
        foamRT.anchorMax = new Vector2(1, WaterlineY + 0.015f);
        foamRT.offsetMin = Vector2.zero; foamRT.offsetMax = Vector2.zero;
        foamGO.GetComponent<Image>().raycastTarget = false;

        // ════════════════════════════════════════════════════
        //  LAYER 6: ELROEY IN BOAT — sitting ON the surface
        // ════════════════════════════════════════════════════

        var elroeySprite = LoadSprite($"{FishingArt}/Elroey in the boat.png");

        // Boat shadow/ripple on water (placed before Elroey so it's behind)
        var boatShadowGO = new GameObject("BoatShadow");
        boatShadowGO.transform.SetParent(safeGO.transform, false);
        var bsRT = boatShadowGO.AddComponent<RectTransform>();
        bsRT.anchorMin = new Vector2(0.25f, WaterlineY);
        bsRT.anchorMax = new Vector2(0.25f, WaterlineY);
        bsRT.pivot = new Vector2(0.5f, 0.6f);
        bsRT.anchoredPosition = new Vector2(0, -30);
        bsRT.sizeDelta = new Vector2(280, 35);
        var bsImg = boatShadowGO.AddComponent<Image>();
        if (circleSprite != null) bsImg.sprite = circleSprite;
        bsImg.color = new Color(0.12f, 0.30f, 0.45f, 0.25f);
        bsImg.raycastTarget = false;

        var elroeyGO = new GameObject("Elroey");
        elroeyGO.transform.SetParent(safeGO.transform, false);
        var elroeyRT = elroeyGO.AddComponent<RectTransform>();
        // Left side, anchored at waterline, hull sinks into water
        elroeyRT.anchorMin = new Vector2(0.25f, WaterlineY);
        elroeyRT.anchorMax = new Vector2(0.25f, WaterlineY);
        elroeyRT.pivot = new Vector2(0.5f, 0.18f); // pivot at hull bottom
        elroeyRT.anchoredPosition = new Vector2(0, -45); // sink well into water
        elroeyRT.sizeDelta = new Vector2(300, 360);
        var elroeyImg = elroeyGO.AddComponent<Image>();
        elroeyImg.sprite = elroeySprite;
        elroeyImg.preserveAspect = true;
        elroeyImg.raycastTarget = false;

        // RodTipAnchor — child of Elroey, positioned at the exact tip of the fishing rod.
        // Looking at the sprite: the rod extends from Elroey's hands up-right,
        // the tip is at roughly the top-right corner of the image.
        var rodTipGO = new GameObject("RodTipAnchor");
        rodTipGO.transform.SetParent(elroeyGO.transform, false);
        var rodTipRT = rodTipGO.AddComponent<RectTransform>();
        // Rod tip is at ~80% X (right side) and ~95% Y (top) of the Elroey sprite
        rodTipRT.anchorMin = new Vector2(0.78f, 0.95f);
        rodTipRT.anchorMax = new Vector2(0.78f, 0.95f);
        rodTipRT.sizeDelta = Vector2.zero;
        rodTipRT.anchoredPosition = Vector2.zero;

        // ════════════════════════════════════════════════════
        //  LAYER 7: SPEECH BUBBLE
        // ════════════════════════════════════════════════════

        var bubbleGO = new GameObject("SpeechBubble");
        bubbleGO.transform.SetParent(safeGO.transform, false);
        var bubbleRT = bubbleGO.AddComponent<RectTransform>();
        bubbleRT.anchorMin = new Vector2(0.25f, WaterlineY);
        bubbleRT.anchorMax = new Vector2(0.25f, WaterlineY);
        bubbleRT.pivot = new Vector2(0f, 0f);
        bubbleRT.anchoredPosition = new Vector2(160, 250);
        bubbleRT.sizeDelta = new Vector2(150, 150);
        var bubbleBgImg = bubbleGO.AddComponent<Image>();
        if (roundedRect != null) { bubbleBgImg.sprite = roundedRect; bubbleBgImg.type = Image.Type.Sliced; }
        bubbleBgImg.color = BubbleColor;
        bubbleBgImg.raycastTarget = false;

        var bubbleFishGO = new GameObject("TargetFish");
        bubbleFishGO.transform.SetParent(bubbleGO.transform, false);
        var bfRT = bubbleFishGO.AddComponent<RectTransform>();
        bfRT.anchorMin = new Vector2(0.1f, 0.1f); bfRT.anchorMax = new Vector2(0.9f, 0.9f);
        bfRT.offsetMin = Vector2.zero; bfRT.offsetMax = Vector2.zero;
        var bubbleFishImg = bubbleFishGO.AddComponent<Image>();
        bubbleFishImg.preserveAspect = true; bubbleFishImg.raycastTarget = false;

        // ════════════════════════════════════════════════════
        //  LAYER 8: seaMid — main water body (gameplay zone)
        //  Uses groundLayer2 tinted blue → organic upper edge
        // ════════════════════════════════════════════════════

        if (groundLayer2 != null)
            CreateSpriteLayer(safeGO.transform, "seaMid", groundLayer2,
                new Vector2(0, 0.08f), new Vector2(1, WaterlineY - 0.05f), SeaMidTint);

        // ════════════════════════════════════════════════════
        //  LAYER 9: FISH SWIM AREA (interactive zone)
        // ════════════════════════════════════════════════════

        var swimGO = new GameObject("fishSwimArea");
        swimGO.transform.SetParent(safeGO.transform, false);
        var swimRT = swimGO.AddComponent<RectTransform>();
        swimRT.anchorMin = new Vector2(0, 0.10f);
        swimRT.anchorMax = new Vector2(1, WaterlineY - 0.08f);
        swimRT.offsetMin = new Vector2(40, 0);
        swimRT.offsetMax = new Vector2(-40, 0);

        // ════════════════════════════════════════════════════
        //  LAYER 10: seaFront — foreground water overlay (depth)
        //  Uses hills sprite tinted blue with low opacity
        // ════════════════════════════════════════════════════

        if (hillsSprite != null)
            CreateSpriteLayer(safeGO.transform, "seaFront", hillsSprite,
                new Vector2(0, 0.02f), new Vector2(1, 0.25f), SeaFrontTint);

        // ════════════════════════════════════════════════════
        //  LAYER 11: seaDeep — deep water flat fill behind sand
        // ════════════════════════════════════════════════════

        var deepGO = StretchImage(safeGO.transform, "seaDeep", SeaDeepColor);
        var deepRT = deepGO.GetComponent<RectTransform>();
        deepRT.anchorMin = new Vector2(0, 0);
        deepRT.anchorMax = new Vector2(1, 0.12f);
        deepRT.offsetMin = Vector2.zero; deepRT.offsetMax = Vector2.zero;
        deepGO.GetComponent<Image>().raycastTarget = false;

        // ════════════════════════════════════════════════════
        //  LAYER 12: sandLayer — seabed with organic shaped silhouette
        //  Uses hills sprite (wavy top edge) tinted to warm sand.
        //  Same approach as World scene uses groundLayer for terrain.
        // ════════════════════════════════════════════════════

        // Sand back layer (darker, taller — provides depth behind main sand)
        if (hillsLargeSprite != null)
            CreateSpriteLayer(safeGO.transform, "sandBack", hillsLargeSprite,
                new Vector2(0, -0.05f), new Vector2(1, 0.16f), SandDarkColor);

        // Sand front layer (lighter, organic wavy top edge — the visible seabed)
        if (hillsSprite != null)
            CreateSpriteLayer(safeGO.transform, "sandLayer", hillsSprite,
                new Vector2(0, -0.05f), new Vector2(1, 0.12f), SandColor);

        // ════════════════════════════════════════════════════
        //  HEADER (topmost UI)
        // ════════════════════════════════════════════════════

        var bar = StretchImage(safeGO.transform, "TopBar", TopBarColor);
        var barRT = bar.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1); barRT.anchorMax = new Vector2(1, 1);
        barRT.pivot = new Vector2(0.5f, 1); barRT.sizeDelta = new Vector2(0, TopBarHeight);
        bar.GetComponent<Image>().raycastTarget = false;
        bar.AddComponent<ThemeHeader>();

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(bar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT); titleRT.offsetMin = new Vector2(100, 0); titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D3\u05D9\u05D2"); // דיג
        titleTMP.fontSize = 36; titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white; titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = IconBtn(bar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(90, 90));

        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = IconBtn(bar.transform, "TrophyButton", trophyIcon,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(112, -30), new Vector2(70, 70));

        var progGO = new GameObject("ProgressText");
        progGO.transform.SetParent(bar.transform, false);
        var progRT = progGO.AddComponent<RectTransform>();
        progRT.anchorMin = new Vector2(1, 0.5f); progRT.anchorMax = new Vector2(1, 0.5f);
        progRT.pivot = new Vector2(1, 0.5f);
        progRT.anchoredPosition = new Vector2(-24, 0); progRT.sizeDelta = new Vector2(120, 50);
        var progTMP = progGO.AddComponent<TextMeshProUGUI>();
        progTMP.text = "0/5"; progTMP.fontSize = 30; progTMP.fontStyle = FontStyles.Bold;
        progTMP.color = Color.white; progTMP.alignment = TextAlignmentOptions.Right;
        progTMP.raycastTarget = false;

        // ════════════════════════════════════════════════════
        //  LOAD FISH SPRITES
        // ════════════════════════════════════════════════════

        var fishSpriteList = new List<Sprite>();
        var fishIdList = new List<string>();
        string[] fishNames = {
            "Circle-shaped fish", "Diamond-shaped fish", "Heart-shaped fish",
            "Pentagon-shaped fish", "Rectangle-shaped fish", "Square-shaped fish",
            "Star-shaped fish", "Triangle-shaped fish"
        };

        var allFishAssets = AssetDatabase.LoadAllAssetsAtPath($"{FishingArt}/Fishes.png");
        var fishDict = new Dictionary<string, Sprite>();
        foreach (var asset in allFishAssets)
            if (asset is Sprite spr) fishDict[spr.name] = spr;
        foreach (string name in fishNames)
            if (fishDict.TryGetValue(name, out Sprite spr))
            { fishSpriteList.Add(spr); fishIdList.Add(name); }

        // ════════════════════════════════════════════════════
        //  FISHING LINE
        // ════════════════════════════════════════════════════

        var lineGO = new GameObject("FishingLine");
        lineGO.transform.SetParent(safeGO.transform, false);
        lineGO.AddComponent<RectTransform>();
        var fishingLine = lineGO.AddComponent<FishingLine>();
        fishingLine.rodTip = rodTipRT;

        // ════════════════════════════════════════════════════
        //  CONTROLLER
        // ════════════════════════════════════════════════════

        var ctrl = canvasGO.AddComponent<FishingGameController>();
        ctrl.elroeyRT = elroeyRT;
        ctrl.rodTipRT = rodTipRT;
        ctrl.speechBubbleFish = bubbleFishImg;
        ctrl.swimArea = swimRT;
        ctrl.progressText = progTMP;
        ctrl.fishingLine = fishingLine;
        ctrl.fishSprites = fishSpriteList.ToArray();
        ctrl.fishIds = fishIdList.ToArray();

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = roundedRect;
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "fishing";

        TutorialHandHelper.Create(safeGO.transform, TutorialHandHelper.Anim.Tap,
            new Vector2(0, -200), new Vector2(400, 400), "fishing");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/FishingGame.unity");
    }

    // ─────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────

    /// <summary>
    /// Creates a sprite-based environment layer (same pattern as World scene).
    /// Stretches the sprite across the anchor range with a color tint.
    /// This gives organic edges (wavy hills/ground shapes) instead of flat color blocks.
    /// </summary>
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

    private static void PlaceCloud(Transform parent, string name, Sprite sprite,
        float anchorX, float anchorY, float width)
    {
        if (sprite == null) return;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(anchorX, anchorY);
        rt.sizeDelta = new Vector2(width, width * 0.45f);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.color = new Color(1f, 1f, 1f, 0.75f);
        img.raycastTarget = false;
    }

    private static GameObject IconBtn(Transform p, string name, Sprite icon,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = icon; img.preserveAspect = true; img.color = Color.white;
        go.AddComponent<Button>().targetGraphic = img;
        return go;
    }

    private static GameObject StretchImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Full(go.AddComponent<RectTransform>());
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static void Full(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    private static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        if (all != null) foreach (var o in all) if (o is Sprite sp) return sp;
        return null;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c); return c;
    }
}
