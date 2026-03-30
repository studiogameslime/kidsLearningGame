using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the FlappyBird scene — landscape layout.
/// Full layered background (clouds → mountains → hills → ground)
/// matching the project's visual style. Bird with floating animation,
/// wood pipe obstacles, parallax scrolling background.
/// </summary>
public class FlappyBirdSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;
    private const string WorldArt = "Assets/Art/World/";

    // Colors matching TowerBuilder / World scene palette
    private static readonly Color SkyColor        = HexColor("#8FD4F5");
    private static readonly Color CloudTint       = Color.white;
    private static readonly Color MountainTint    = new Color(0.78f, 0.88f, 0.95f, 1f);
    private static readonly Color HillsLargeTint  = HexColor("#B7D7D6");
    private static readonly Color HillsTint       = HexColor("#9FCBC5");
    private static readonly Color GroundBackTint  = HexColor("#8ED36B");
    private static readonly Color GroundFrontTint = HexColor("#79C956");

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Flappy Bird", "Building scene...", 0.5f);
            BuildScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static void BuildScene()
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Load all background sprites
        var cloudLayerB1  = LoadSprite(WorldArt + "cloudLayerB1.png");
        var cloudLayerB2  = LoadSprite(WorldArt + "cloudLayerB2.png");
        var cloudLayer1   = LoadSprite(WorldArt + "cloudLayer1.png");
        var cloudLayer2   = LoadSprite(WorldArt + "cloudLayer2.png");
        var mountainA     = LoadSprite(WorldArt + "mountainA.png");
        var mountainB     = LoadSprite(WorldArt + "mountainB.png");
        var mountainC     = LoadSprite(WorldArt + "mountainC.png");
        var mountains     = LoadSprite(WorldArt + "mountains.png");
        var hillsLarge    = LoadSprite(WorldArt + "hillsLarge.png");
        var hills         = LoadSprite(WorldArt + "hills.png");
        var groundLayer1  = LoadSprite(WorldArt + "groundLayer1.png");
        var groundLayer2  = LoadSprite(WorldArt + "groundLayer2.png");
        var pipeSpr       = LoadSprite("Assets/Art/TowerGame/TowerUnused/elementWood019.png");

        // ── Camera ──
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SkyColor;
        cam.orthographic = true;
        var urp = System.Type.GetType(
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urp != null) camGO.AddComponent(urp);

        // ── EventSystem ──
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inp = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inp != null) esGO.AddComponent(inp);
        else esGO.AddComponent<StandaloneInputModule>();

        // ── Canvas ──
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        var root = canvasGO.transform;

        // ═══════════════════════════════════════════════════
        //  BACKGROUND LAYERS (back to front, full-width stretch)
        //
        //  Matching TowerBuilder / World scene layering:
        //  Sky → Far clouds → Near clouds → Mountains →
        //  Large hills → Near hills → Ground back → Ground front
        // ═══════════════════════════════════════════════════

        // 1. SKY — solid base color
        var skyGO = BgLayer(root, "Sky", null, 0, 0, 1, 1, SkyColor);

        // 2. FAR CLOUDS (upper sky, very slow parallax)
        var cloudB1GO = BgLayer(root, "CloudLayerB1", cloudLayerB1,
            0, 0.55f, 1, 1f, CloudTint);
        var cloudB2GO = BgLayer(root, "CloudLayerB2", cloudLayerB2,
            0, 0.50f, 1, 0.92f, CloudTint);

        // 3. NEAR CLOUDS (slightly lower, slow parallax)
        var cloud1GO = BgLayer(root, "CloudLayer1", cloudLayer1,
            0, 0.48f, 1, 0.88f, new Color(1, 1, 1, 0.8f));
        var cloud2GO = BgLayer(root, "CloudLayer2", cloudLayer2,
            0, 0.45f, 1, 0.82f, new Color(1, 1, 1, 0.7f));

        // 4. FAR MOUNTAINS (behind hills)
        var mtnsGO = BgLayer(root, "Mountains", mountains,
            0, 0.35f, 1, 0.70f, MountainTint);
        // Additional mountain peaks for variety
        BgLayer(root, "MountainA", mountainA,
            0, 0.33f, 0.5f, 0.68f, new Color(MountainTint.r, MountainTint.g, MountainTint.b, 0.7f));
        BgLayer(root, "MountainC", mountainC,
            0.5f, 0.33f, 1, 0.68f, new Color(MountainTint.r, MountainTint.g, MountainTint.b, 0.7f));

        // 5. LARGE HILLS — overlapping mountains
        var hillsLargeGO = BgLayer(root, "HillsLarge", hillsLarge,
            0, 0.22f, 1, 0.55f, HillsLargeTint);

        // 6. NEAR HILLS
        var hillsGO = BgLayer(root, "Hills", hills,
            0, 0.15f, 1, 0.45f, HillsTint);

        // 7. GROUND BACK LAYER
        var groundBackGO = BgLayer(root, "GroundBack", groundLayer1,
            0, 0, 1, 0.35f, GroundBackTint);

        // 8. GROUND FRONT LAYER
        var groundFrontGO = BgLayer(root, "GroundFront", groundLayer2,
            0, 0, 1, 0.20f, GroundFrontTint);

        // ═══════════════════════════════════════
        //  SAFE AREA + HEADER
        // ═══════════════════════════════════════

        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(root, false);
        var safeRT = safeGO.AddComponent<RectTransform>();
        Full(safeRT);
        safeGO.AddComponent<SafeAreaHandler>();

        var bar = CreateBar(safeGO.transform);

        // Title: מעוף הציפור
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(bar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var tmp = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(tmp, "\u05DE\u05E2\u05D5\u05E3 \u05D4\u05E6\u05D9\u05E4\u05D5\u05E8");
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = Btn(bar.transform, "HomeButton", homeIcon, 24, 0, 90);

        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = BtnRight(bar.transform, "TrophyButton", trophyIcon, -16, -20, 70);

        // ═══════════════════════════════════════
        //  PLAY AREA
        // ═══════════════════════════════════════

        var playGO = new GameObject("PlayArea");
        playGO.transform.SetParent(safeGO.transform, false);
        var playRT = playGO.AddComponent<RectTransform>();
        playRT.anchorMin = new Vector2(0, 0);
        playRT.anchorMax = new Vector2(1, 1);
        playRT.offsetMin = Vector2.zero;
        playRT.offsetMax = new Vector2(0, -TopBarHeight);

        var obstGO = new GameObject("ObstacleContainer");
        obstGO.transform.SetParent(playRT, false);
        var obstRT = obstGO.AddComponent<RectTransform>();
        Full(obstRT);
        // Clip pipes to play area so they don't render into the header
        obstGO.AddComponent<RectMask2D>();

        // ═══════════════════════════════════════
        //  BIRD
        // ═══════════════════════════════════════

        var birdAnimData = AnimalAnimData.Load("Bird");

        var birdGO = new GameObject("Bird");
        birdGO.transform.SetParent(playRT, false);
        var birdRT = birdGO.AddComponent<RectTransform>();
        birdRT.anchorMin = new Vector2(0.5f, 0f);
        birdRT.anchorMax = new Vector2(0.5f, 0f);
        birdRT.pivot = new Vector2(0.5f, 0.5f);
        birdRT.sizeDelta = new Vector2(480, 480);

        var birdImg = birdGO.AddComponent<Image>();
        birdImg.preserveAspect = true;
        birdImg.raycastTarget = false;

        if (birdAnimData != null && birdAnimData.floatingFrames != null && birdAnimData.floatingFrames.Length > 0)
            birdImg.sprite = birdAnimData.floatingFrames[0];

        var animator = birdGO.AddComponent<UISpriteAnimator>();
        animator.targetImage = birdImg;
        if (birdAnimData != null)
        {
            animator.idleFrames = birdAnimData.floatingFrames;
            animator.floatingFrames = birdAnimData.floatingFrames;
            animator.successFrames = birdAnimData.successFrames;
            animator.framesPerSecond = birdAnimData.floatingFps > 0 ? birdAnimData.floatingFps : 30f;
        }

        // ═══════════════════════════════════════
        //  CONTROLLER
        // ═══════════════════════════════════════

        var ctrl = canvasGO.AddComponent<FlappyBirdController>();
        ctrl.birdRT = birdRT;
        ctrl.birdImage = birdImg;
        ctrl.birdAnimator = animator;
        ctrl.playArea = playRT;
        ctrl.groundFraction = 0.20f; // ground front layer goes up to 0.20
        ctrl.gapSize = 520f;       // generous gap for large bird
        ctrl.pipeWidth = 160f;     // wider pipes to match bird scale
        ctrl.pipeSprite = pipeSpr;
        ctrl.obstacleContainer = obstRT;

        // Parallax layers (slowest to fastest)
        ctrl.parallaxLayers = new RectTransform[]
        {
            cloudB1GO.GetComponent<RectTransform>(),
            cloudB2GO.GetComponent<RectTransform>(),
            cloud1GO.GetComponent<RectTransform>(),
            cloud2GO.GetComponent<RectTransform>(),
            mtnsGO.GetComponent<RectTransform>(),
            hillsLargeGO.GetComponent<RectTransform>(),
            hillsGO.GetComponent<RectTransform>(),
            groundBackGO.GetComponent<RectTransform>(),
            groundFrontGO.GetComponent<RectTransform>(),
        };
        ctrl.parallaxSpeeds = new float[]
        {
            0.02f,  // far clouds B1 — very slow
            0.03f,  // far clouds B2
            0.05f,  // near clouds 1
            0.06f,  // near clouds 2
            0.08f,  // mountains — medium slow
            0.12f,  // large hills
            0.15f,  // near hills — medium
            0.20f,  // ground back
            0.25f,  // ground front — fastest
        };

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = LoadSprite("Assets/UI/Sprites/RoundedRect.png");
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "flappybird";

        // Tutorial hand
        TutorialHandHelper.Create(safeGO.transform, TutorialHandHelper.Anim.Tap,
            new Vector2(0, 0), new Vector2(450, 450), "flappybird");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/FlappyBird.unity");
    }

    // ── HELPERS ──────────────────────────────────────────────────

    private static GameObject BgLayer(Transform p, string name, Sprite spr,
        float x0, float y0, float x1, float y1, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        if (spr != null) img.sprite = spr;
        img.type = Image.Type.Simple;
        img.color = c;
        img.preserveAspect = false;
        img.raycastTarget = false;
        return go;
    }

    private static GameObject CreateBar(Transform parent)
    {
        var bar = new GameObject("TopBar");
        bar.transform.SetParent(parent, false);
        var barRT = bar.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1);
        barRT.anchorMax = new Vector2(1, 1);
        barRT.pivot = new Vector2(0.5f, 1);
        barRT.sizeDelta = new Vector2(0, TopBarHeight);
        var barImg = bar.AddComponent<Image>();
        barImg.color = HexColor("#5BA84C");
        barImg.raycastTarget = false;
        bar.AddComponent<ThemeHeader>();
        return bar;
    }

    private static GameObject Btn(Transform p, string name, Sprite icon, float x, float y, float sz)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(sz, sz);
        var img = go.AddComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        return go;
    }

    private static GameObject BtnRight(Transform p, string name, Sprite icon, float x, float y, float sz)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(sz, sz);
        var img = go.AddComponent<Image>();
        img.sprite = icon; img.preserveAspect = true; img.color = Color.white; img.raycastTarget = true;
        go.AddComponent<Button>().targetGraphic = img;
        return go;
    }

    private static void Full(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    private static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        if (all != null)
            foreach (var o in all)
                if (o is Sprite sp) return sp;
        return null;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
