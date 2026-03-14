using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the ShadowMatch scene completely from scratch.
///
/// Visual hierarchy (back to front):
///   Sky → Cloud wisp → Mountains → Hills → Grass → [props] → Shadows → Animals
///
/// Design goals:
///   - Clean, spacious, toddler-readable
///   - Distinct from the World scene (different composition, fewer layers)
///   - Gameplay zones are visually dominant
///   - Background supports but never competes
/// </summary>
public class ShadowMatchSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    // Palette — soft lavender sky, muted landscape
    private static readonly Color SkyColor      = HexColor("#C9D8FF");
    private static readonly Color CloudColor    = HexColor("#E8F0FF");
    private static readonly Color MountainColor = HexColor("#C8D7EE");
    private static readonly Color HillColor     = HexColor("#AFCFC8");
    private static readonly Color GrassColor    = HexColor("#9FD78D");
    private static readonly Color TopBarColor   = HexColor("#8BAAC8");
    private static readonly Color PropColor     = HexColor("#BFCFC6");

    private const int TopBarHeight = 80;

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
        var mountainsSpr = LoadSprite($"{a}/mountains.png");
        var hillsSpr     = LoadSprite($"{a}/hillsLarge.png");
        var grassSpr     = LoadSprite($"{a}/groundLayer1.png");
        var cloudSpr     = LoadSprite($"{a}/cloudLayerB1.png");
        var house1       = LoadSprite($"{a}/houseSmall1.png");
        var house2       = LoadSprite($"{a}/houseSmall2.png");
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
        //  BACKGROUND — 5 layers (landscape)
        //
        //  Left half  = shadows (against open sky)
        //  Right half = draggable animals (on grass)
        //
        //  Clouds stay HIGH in the sky.
        //  Nothing decorative in the gameplay zones.
        // ═══════════════════════════════════

        // 1. SKY — full screen fill
        Layer(root, "Sky", null, 0, 0, 1, 1, SkyColor);

        // 2. CLOUD — thin wisp, high in sky
        Layer(root, "Cloud", cloudSpr, 0, 0.78f, 1, 0.92f, CloudColor);

        // 3. MOUNTAINS — faint, distant
        Layer(root, "Mountains", mountainsSpr, 0, 0.28f, 1, 0.50f, MountainColor);

        // 4. HILLS — soft layer above grass
        Layer(root, "Hills", hillsSpr, 0, 0.15f, 1, 0.35f, HillColor);

        // 5. GRASS — ground field
        Layer(root, "Grass", grassSpr, 0, 0, 1, 0.20f, GrassColor);

        // ═══════════════════════════════════
        //  PROPS — minimal, 2 tiny distant houses only
        //  Placed on the hill ridge, far from gameplay
        // ═══════════════════════════════════

        Prop(root, "HouseL", house1, 180, 380, 50, 50);
        Prop(root, "HouseR", house2, 1740, 370, 45, 45);

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
        tmp.text = HebrewFixer.Fix("\u05D4\u05EA\u05D0\u05DE\u05EA \u05E6\u05DC\u05DC\u05D9\u05DD");
        tmp.isRightToLeftText = false;
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        // Home button
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = Btn(bar.transform, "HomeButton", homeIcon, 16, -8, 64);

        // ═══════════════════════════════════
        //  GAMEPLAY ZONES (landscape)
        //
        //  Shadow zone: left half, clean sky behind
        //    anchors 0.02–0.48 horiz, 0.08–0.88 vert
        //    → 2x2 grid of shadows against open sky
        //
        //  Animal zone: right half, on grass/hills
        //    anchors 0.52–0.98 horiz, 0.08–0.88 vert
        //    → 2x2 grid of draggable animals
        //
        //  Vertical gap (0.48–0.52) = natural separator
        // ═══════════════════════════════════

        var shadowsGO = new GameObject("ShadowsArea");
        shadowsGO.transform.SetParent(safeGO.transform, false);
        var shadowsRT = shadowsGO.AddComponent<RectTransform>();
        shadowsRT.anchorMin = new Vector2(0.02f, 0.08f);
        shadowsRT.anchorMax = new Vector2(0.48f, 0.88f);
        shadowsRT.offsetMin = Vector2.zero;
        shadowsRT.offsetMax = Vector2.zero;

        var animalsGO = new GameObject("AnimalsArea");
        animalsGO.transform.SetParent(safeGO.transform, false);
        var animalsRT = animalsGO.AddComponent<RectTransform>();
        animalsRT.anchorMin = new Vector2(0.52f, 0.08f);
        animalsRT.anchorMax = new Vector2(0.98f, 0.88f);
        animalsRT.offsetMin = Vector2.zero;
        animalsRT.offsetMax = Vector2.zero;

        // ═══════════════════════════════════
        //  CONTROLLER
        // ═══════════════════════════════════

        var ctrl = canvasGO.AddComponent<ShadowMatchController>();
        ctrl.shadowsArea = shadowsRT;
        ctrl.animalsRow = animalsRT;
        ctrl.circleSprite = circleSprite;
        ctrl.animalCount = 4;
        ctrl.shadowSize = 200f;
        ctrl.animalSize = 220f;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

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

    private static void Prop(Transform p, string name, Sprite spr, float x, float y, float w, float h)
    {
        if (spr == null) return;
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        var img = go.AddComponent<Image>();
        img.sprite = spr;
        img.preserveAspect = true;
        img.color = PropColor;
        img.raycastTarget = true;
        go.AddComponent<ShadowMatchProp>();
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
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
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
