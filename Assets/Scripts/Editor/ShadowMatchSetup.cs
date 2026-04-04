using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the ShadowMatch scene completely from scratch (portrait layout).
///
/// Visual hierarchy (back to front):
///   Sky → Distant hills → Ground → Top bar → Shadows row → Animals row
///
/// Design goals:
///   - Two clear rows: shadows in the sky, animals on the ground
///   - Large ground area with soft pastel green (distinct from World scene)
///   - Clean, calm background — no distracting decorations
///   - Large, easy-to-grab animals for toddlers
/// </summary>
public class ShadowMatchSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    // Palette — calm sky, soft pastel ground
    private static readonly Color SkyColor      = HexColor("#D0E4FF");
    private static readonly Color HillFarColor  = HexColor("#C4D8EC");
    private static readonly Color HillNearColor = HexColor("#B5CFBE");
    private static readonly Color GroundColor   = HexColor("#C8E6B0"); // soft pastel green, different from world
    private static readonly Color TopBarColor   = HexColor("#8BAAC8");

    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    private static readonly Color[] AnimalColors = {
        HexColor("#EF9A9A"), HexColor("#F48FB1"), HexColor("#CE93D8"),
        HexColor("#B39DDB"), HexColor("#9FA8DA"), HexColor("#90CAF9"),
        HexColor("#80DEEA"), HexColor("#80CBC4"), HexColor("#A5D6A7"),
        HexColor("#C5E1A5"), HexColor("#E6EE9C"), HexColor("#FFF59D"),
        HexColor("#FFE082"), HexColor("#FFCC80"), HexColor("#FFAB91"),
        HexColor("#BCAAA4"), HexColor("#B0BEC5"), HexColor("#CFD8DC"),
        HexColor("#F8BBD0")
    };

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Shadow Match", "Data...", 0.2f);
            UpdateGameData();
            EditorUtility.DisplayProgressBar("Shadow Match", "Scene...", 0.6f);
            BuildScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // ─── DATA ───────────────────────────────

    private static void UpdateGameData()
    {
        var game = AssetDatabase.LoadAssetAtPath<GameItemData>("Assets/Data/Games/Shadows.asset");
        if (game == null) { Debug.LogError("Shadows.asset not found."); return; }

        game.targetSceneName = "ShadowMatch";
        game.hasSubItems = false;

        string[] names = {
            "Bear","Bird","Cat","Chicken","Cow","Dog","Donkey","Duck",
            "Elephant","Fish","Frog","Giraffe","Horse","Lion","Monkey",
            "Sheep","Snake","Turtle","Zebra"
        };

        if (game.subItems == null) game.subItems = new List<SubItemData>();
        game.subItems.Clear();

        for (int i = 0; i < names.Length; i++)
        {
            string n = names[i];
            Sprite spr = LoadSprite($"Assets/Art/Animals/{n}/Art/{n}Sprite.png")
                      ?? LoadSprite($"Assets/Art/Animals/{n}/Art/{n}.png");
            if (spr == null) continue;

            game.subItems.Add(new SubItemData {
                id = $"shadow_{n.ToLower()}",
                title = n,
                cardColor = AnimalColors[i % AnimalColors.Length],
                categoryKey = n.ToLower(),
                targetSceneName = "ShadowMatch",
                contentAsset = spr,
                thumbnail = spr
            });
        }

        EditorUtility.SetDirty(game);
    }

    // ─── SCENE ──────────────────────────────

    private static void BuildScene()
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string a = "Assets/Art/World";

        // Sprites
        var hillsSpr = LoadSprite($"{a}/hillsLarge.png");
        var grassSpr = LoadSprite($"{a}/groundLayer1.png");
        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");

        // ── Camera ──
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SkyColor;
        cam.orthographic = true;
        var urp = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
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

        // ═══════════════════════════════════
        //  BACKGROUND — 3 simple layers
        //
        //  Top ~55%  = sky (open, calm)
        //  Middle    = gentle hills transition
        //  Bottom ~45% = ground (pastel green play field)
        // ═══════════════════════════════════

        // 1. SKY — full screen fill (camera bg color handles it, but explicit layer for safety)
        Layer(root, "Sky", null, 0, 0, 1, 1, SkyColor);

        // 2. DISTANT HILLS — soft transition between sky and ground
        Layer(root, "HillsFar", hillsSpr, 0, 0.42f, 1, 0.58f, HillFarColor);

        // 3. HILLS NEAR — slightly lower, blending into ground
        Layer(root, "HillsNear", hillsSpr, 0, 0.38f, 1, 0.52f, HillNearColor);

        // 4. GROUND — large pastel green play field (bottom 45%)
        Layer(root, "Ground", grassSpr, 0, 0, 1, 0.45f, GroundColor);

        // ═══════════════════════════════════
        //  SAFE AREA + TOP BAR
        // ═══════════════════════════════════

        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(root, false);
        var safeRT = safeGO.AddComponent<RectTransform>();
        Full(safeRT);
        safeGO.AddComponent<SafeAreaHandler>();

        // Top bar
        var bar = Fill(safeGO.transform, "TopBar", TopBarColor);
        var barRT = bar.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1);
        barRT.anchorMax = new Vector2(1, 1);
        barRT.pivot = new Vector2(0.5f, 1);
        barRT.sizeDelta = new Vector2(0, TopBarHeight);
        bar.GetComponent<Image>().raycastTarget = false;
        bar.AddComponent<ThemeHeader>();

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(bar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var tmp = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(tmp, "\u05D7\u05D9\u05D4 \u05D5\u05E6\u05DC"); // חיה וצל
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        // Home button
        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = Btn(bar.transform, "HomeButton", homeIcon, 24, 0, 90);

        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = BtnRight(bar.transform, "TrophyButton", trophyIcon, -16, -20, 70);

        // ═══════════════════════════════════
        //  GAMEPLAY ZONES (portrait — two horizontal rows)
        //
        //  Shadow row: upper portion, in the sky
        //    anchors: x 0.03–0.97, y 0.58–0.78
        //    → single horizontal row of 4 silhouettes
        //
        //  Animal row: lower portion, on the ground
        //    anchors: x 0.03–0.97, y 0.10–0.38
        //    → single horizontal row of 4 draggable animals
        //
        //  Clear vertical gap between rows (~20% of screen)
        // ═══════════════════════════════════

        var shadowsGO = new GameObject("ShadowsArea");
        shadowsGO.transform.SetParent(safeGO.transform, false);
        var shadowsRT = shadowsGO.AddComponent<RectTransform>();
        shadowsRT.anchorMin = new Vector2(0.03f, 0.58f);
        shadowsRT.anchorMax = new Vector2(0.97f, 0.78f);
        shadowsRT.offsetMin = Vector2.zero;
        shadowsRT.offsetMax = Vector2.zero;

        var animalsGO = new GameObject("AnimalsArea");
        animalsGO.transform.SetParent(safeGO.transform, false);
        var animalsRT = animalsGO.AddComponent<RectTransform>();
        animalsRT.anchorMin = new Vector2(0.03f, 0.18f);
        animalsRT.anchorMax = new Vector2(0.97f, 0.42f);
        animalsRT.offsetMin = Vector2.zero;
        animalsRT.offsetMax = Vector2.zero;

        // ═══════════════════════════════════
        //  CONTROLLER
        // ═══════════════════════════════════

        var ctrl = canvasGO.AddComponent<ShadowMatchController>();
        ctrl.shadowsArea = shadowsRT;
        ctrl.animalsRow = animalsRT;
        ctrl.circleSprite = circleSprite;
        ctrl.silhouetteShader = Shader.Find("UI/Silhouette");
        ctrl.animalCount = 4;
        ctrl.shadowSize = 400f;
        ctrl.animalSize = 400f;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = LoadSprite("Assets/UI/Sprites/RoundedRect.png");
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "shadows";

        // Tutorial hand
        TutorialHandHelper.Create(safeGO.transform, TutorialHandHelper.Anim.Tap,
            new Vector2(-100, 0), new Vector2(450, 450), "shadows");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ShadowMatch.unity");
    }

    // ─── HELPERS ────────────────────────────

    private static GameObject Layer(Transform p, string name, Sprite spr,
        float x0, float y0, float x1, float y1, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        if (spr != null) img.sprite = spr;
        img.color = c;
        img.preserveAspect = false;
        img.raycastTarget = false;
        return go;
    }

    private static GameObject Fill(Transform p, string name, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        Full(rt);
        go.AddComponent<Image>().color = c;
        return go;
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
        img.sprite = icon;
        img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
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
