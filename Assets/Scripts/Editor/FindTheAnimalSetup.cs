using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the FindTheAnimal scene in LANDSCAPE with structured zone-based world.
/// Layer order (back→front): Sky → Horizon (mountains) → Clouds → Ground → BackRow → Animals → FrontRow → UI.
/// All gameplay layers are full-stretch with same coordinate system.
/// </summary>
public class FindTheAnimalSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    private const int TopBarHeight = 130;
    private const float GroundTop = 0.45f;
    private const float GroundFrontTop = 0.20f;

    private static readonly Color TopBarColor = HexColor("#FFB74D");
    private const string WorldArt = "Assets/Art/World/";

    private static readonly string[] CloudNames = { "cloud1", "cloud2", "cloud3", "cloud4", "cloud5", "cloud6", "cloud7", "cloud8" };
    private static readonly string[] TreeNames = {
        "tree", "treeLong", "treeLongOrange", "treeOrange", "treePine", "treePineFrozen",
        "treePineOrange", "treePalm", "treeFrozen", "treeSnow", "treeLongSnow"
    };
    private static readonly string[] SmallTreeNames = {
        "treeSmall_green1", "treeSmall_green2", "treeSmall_green3",
        "treeSmall_greenAlt1", "treeSmall_greenAlt2", "treeSmall_greenAlt3",
        "treeSmall_orange1", "treeSmall_orange2", "treeSmall_orange3"
    };
    private static readonly string[] BushNames = {
        "bush1", "bush2", "bush3", "bush4",
        "bushAlt1", "bushAlt2", "bushAlt3", "bushAlt4",
        "bushOrange1", "bushOrange2", "bushOrange3", "bushOrange4"
    };
    private static readonly string[] HouseNames = {
        "houseSmall1", "houseSmall2", "houseSmallAlt1", "houseSmallAlt2"
    };
    private static readonly string[] FenceNames = { "fence", "fenceIron" };

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
            EditorUtility.DisplayProgressBar("Find The Animal Setup", "Ensuring sprite imports…", 0.1f);
            EnsureWorldSpriteImports();

            EditorUtility.DisplayProgressBar("Find The Animal Setup", "Updating data…", 0.3f);
            UpdateGameData();

            EditorUtility.DisplayProgressBar("Find The Animal Setup", "Building scene…", 0.5f);
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

    private static void EnsureWorldSpriteImports()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/World" });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.Sprite)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
        }
    }

    private static void UpdateGameData()
    {
        var game = AssetDatabase.LoadAssetAtPath<GameItemData>("Assets/Data/Games/FindTheObject.asset");
        if (game == null) { Debug.LogError("FindTheObject.asset not found."); return; }

        game.targetSceneName = "FindTheAnimal";
        game.hasSubItems = false;

        string[] animals = {
            "Bear", "Bird", "Cat", "Chicken", "Cow", "Dog", "Donkey", "Duck",
            "Elephant", "Fish", "Frog", "Giraffe", "Horse", "Lion", "Monkey",
            "Sheep", "Snake", "Turtle", "Zebra"
        };

        if (game.subItems == null) game.subItems = new List<SubItemData>();
        game.subItems.Clear();

        for (int i = 0; i < animals.Length; i++)
        {
            string name = animals[i];
            var mainSprite = LoadSprite($"Assets/Art/Animals/{name}/Art/Puzzle/{name} Main.png");
            Sprite thumbSprite = null;
            foreach (var tp in new[] {
                $"Assets/Art/Animals/{name}/Art/{name}Sprite.png",
                $"Assets/Art/Animals/{name}/Art/{name}.png"
            })
            {
                thumbSprite = LoadSprite(tp);
                if (thumbSprite != null) break;
            }
            if (mainSprite == null && thumbSprite == null) continue;

            game.subItems.Add(new SubItemData {
                id = $"find_{name.ToLower()}", title = name,
                cardColor = AnimalColors[i % AnimalColors.Length],
                categoryKey = name.ToLower(), targetSceneName = "FindTheAnimal",
                contentAsset = mainSprite ?? thumbSprite,
                thumbnail = thumbSprite ?? mainSprite
            });
        }
        EditorUtility.SetDirty(game);
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
        camGO.tag = "MainCamera"; camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = HexColor("#B8DBF7");
        cam.orthographic = true;
        var urp = System.Type.GetType(
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urp != null) camGO.AddComponent(urp);

        // EventSystem
        var esGO = new GameObject("EventSystem"); esGO.AddComponent<EventSystem>();
        var inp = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inp != null) esGO.AddComponent(inp); else esGO.AddComponent<StandaloneInputModule>();

        // Canvas (landscape)
        var canvasGO = new GameObject("FindAnimalCanvas");
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

        // Top bar
        var topBar = StretchImg(safeGO.transform, "TopBar", TopBarColor);
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1); topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1); topBarRT.sizeDelta = new Vector2(0, TopBarHeight);
        topBar.GetComponent<Image>().raycastTarget = false;
        topBar.AddComponent<ThemeHeader>();

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT); titleRT.offsetMin = new Vector2(100, 0); titleRT.offsetMax = new Vector2(-200, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = HebrewFixer.Fix("\u05DE\u05E6\u05D0 \u05D0\u05EA \u05D4\u05D7\u05D9\u05D4");
        titleTMP.isRightToLeftText = false; titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold; titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center; titleTMP.raycastTarget = false;

        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = IconBtn(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -20), new Vector2(90, 90));

        // ═══════════════════════════════════════
        //  WORLD AREA — below top bar
        //  Layer order (back→front):
        //  Sky → HorizonLayer → CloudLayer → Ground → GroundFront → BackRow → Animals → FrontRow
        // ═══════════════════════════════════════

        var worldGO = new GameObject("WorldArea");
        worldGO.transform.SetParent(safeGO.transform, false);
        var worldRT = worldGO.AddComponent<RectTransform>();
        Full(worldRT);
        worldRT.offsetMax = new Vector2(0, -TopBarHeight);

        // 1. Sky (full background)
        var skyGO = StretchImg(worldGO.transform, "Sky", HexColor("#B8DBF7"));
        skyGO.GetComponent<Image>().raycastTarget = false;
        var skyImg = skyGO.GetComponent<Image>();

        // 2. Cloud layer
        var cloudGO = new GameObject("CloudLayer");
        cloudGO.transform.SetParent(worldGO.transform, false);
        var cloudRT = cloudGO.AddComponent<RectTransform>();
        Full(cloudRT);

        // 4. Ground (warm sand, bottom 45%)
        var groundGO = StretchImg(worldGO.transform, "Ground", HexColor("#E8D5A3"));
        groundGO.GetComponent<Image>().raycastTarget = false;
        var groundRT = groundGO.GetComponent<RectTransform>();
        groundRT.anchorMin = Vector2.zero;
        groundRT.anchorMax = new Vector2(1, GroundTop);
        groundRT.offsetMin = Vector2.zero; groundRT.offsetMax = Vector2.zero;
        var groundImg = groundGO.GetComponent<Image>();

        // 5. Ground front strip (lighter sand, bottom 20%)
        var groundFrontGO = StretchImg(worldGO.transform, "GroundFront", HexColor("#F0E0B5"));
        groundFrontGO.GetComponent<Image>().raycastTarget = false;
        var groundFrontRT = groundFrontGO.GetComponent<RectTransform>();
        groundFrontRT.anchorMin = Vector2.zero;
        groundFrontRT.anchorMax = new Vector2(1, GroundFrontTop);
        groundFrontRT.offsetMin = Vector2.zero; groundFrontRT.offsetMax = Vector2.zero;
        var groundFrontImg = groundFrontGO.GetComponent<Image>();

        // 6. Back row layer (full stretch — decorations near horizon, behind animals)
        var backRowGO = new GameObject("BackRowLayer");
        backRowGO.transform.SetParent(worldGO.transform, false);
        var backRowRT = backRowGO.AddComponent<RectTransform>();
        Full(backRowRT);

        // 7. Animal layer (full stretch — same coord system as other layers)
        var animalGO = new GameObject("AnimalLayer");
        animalGO.transform.SetParent(worldGO.transform, false);
        var animalRT = animalGO.AddComponent<RectTransform>();
        Full(animalRT);

        // 8. Front row layer (full stretch — foreground + hiding objects, on top of animals)
        var frontRowGO = new GameObject("FrontRowLayer");
        frontRowGO.transform.SetParent(worldGO.transform, false);
        var frontRowRT = frontRowGO.AddComponent<RectTransform>();
        Full(frontRowRT);

        // ═══════════════════════════════════════
        //  TARGET DISPLAY — large, center-top
        //  Shows animal image + "× N" count, clear for toddlers
        // ═══════════════════════════════════════

        var targetCardGO = new GameObject("TargetDisplay");
        targetCardGO.transform.SetParent(worldGO.transform, false);
        var targetCardRT = targetCardGO.AddComponent<RectTransform>();
        targetCardRT.anchorMin = new Vector2(0.5f, 1);
        targetCardRT.anchorMax = new Vector2(0.5f, 1);
        targetCardRT.pivot = new Vector2(0.5f, 1);
        targetCardRT.anchoredPosition = new Vector2(0, -10);
        targetCardRT.sizeDelta = new Vector2(300, 130);
        var targetCardImg = targetCardGO.AddComponent<Image>();
        if (roundedRect != null) { targetCardImg.sprite = roundedRect; targetCardImg.type = Image.Type.Sliced; }
        targetCardImg.color = new Color(1, 1, 1, 0.93f);
        targetCardImg.raycastTarget = false;

        // Drop shadow behind card
        var shadowGO = new GameObject("Shadow");
        shadowGO.transform.SetParent(targetCardGO.transform, false);
        shadowGO.transform.SetAsFirstSibling();
        var shadowRT = shadowGO.AddComponent<RectTransform>();
        shadowRT.anchorMin = Vector2.zero; shadowRT.anchorMax = Vector2.one;
        shadowRT.offsetMin = new Vector2(-4, -6); shadowRT.offsetMax = new Vector2(4, 2);
        var shadowImg = shadowGO.AddComponent<Image>();
        if (roundedRect != null) { shadowImg.sprite = roundedRect; shadowImg.type = Image.Type.Sliced; }
        shadowImg.color = new Color(0, 0, 0, 0.12f);
        shadowImg.raycastTarget = false;

        // Animal image — left side, large
        var tImgGO = new GameObject("TargetImage");
        tImgGO.transform.SetParent(targetCardGO.transform, false);
        var tImgRT = tImgGO.AddComponent<RectTransform>();
        tImgRT.anchorMin = new Vector2(0, 0.5f); tImgRT.anchorMax = new Vector2(0, 0.5f);
        tImgRT.pivot = new Vector2(0, 0.5f);
        tImgRT.anchoredPosition = new Vector2(12, 0); tImgRT.sizeDelta = new Vector2(105, 105);
        var tImg = tImgGO.AddComponent<Image>();
        tImg.preserveAspect = true; tImg.color = Color.white; tImg.raycastTarget = false;

        // "×" label
        var multiplyGO = new GameObject("MultiplyLabel");
        multiplyGO.transform.SetParent(targetCardGO.transform, false);
        var multiplyRT = multiplyGO.AddComponent<RectTransform>();
        multiplyRT.anchorMin = new Vector2(0.5f, 0.5f); multiplyRT.anchorMax = new Vector2(0.5f, 0.5f);
        multiplyRT.pivot = new Vector2(0.5f, 0.5f);
        multiplyRT.anchoredPosition = new Vector2(10, 0); multiplyRT.sizeDelta = new Vector2(40, 60);
        var multiplyTMP = multiplyGO.AddComponent<TextMeshProUGUI>();
        multiplyTMP.text = "\u00D7"; multiplyTMP.fontSize = 46;
        multiplyTMP.fontStyle = FontStyles.Bold; multiplyTMP.color = HexColor("#9E9E9E");
        multiplyTMP.alignment = TextAlignmentOptions.Center; multiplyTMP.raycastTarget = false;

        // Count number — right side with colored circle bg
        var countBgGO = new GameObject("CountBg");
        countBgGO.transform.SetParent(targetCardGO.transform, false);
        var countBgRT = countBgGO.AddComponent<RectTransform>();
        countBgRT.anchorMin = new Vector2(1, 0.5f); countBgRT.anchorMax = new Vector2(1, 0.5f);
        countBgRT.pivot = new Vector2(1, 0.5f);
        countBgRT.anchoredPosition = new Vector2(-18, 0); countBgRT.sizeDelta = new Vector2(72, 72);
        var countBgImg = countBgGO.AddComponent<Image>();
        if (circleSprite != null) countBgImg.sprite = circleSprite;
        countBgImg.color = new Color(1, 0.44f, 0.26f, 0.18f); countBgImg.raycastTarget = false;

        var countGO = new GameObject("RemainingCount");
        countGO.transform.SetParent(targetCardGO.transform, false);
        var countRT = countGO.AddComponent<RectTransform>();
        countRT.anchorMin = new Vector2(1, 0.5f); countRT.anchorMax = new Vector2(1, 0.5f);
        countRT.pivot = new Vector2(1, 0.5f);
        countRT.anchoredPosition = new Vector2(-18, 0); countRT.sizeDelta = new Vector2(72, 72);
        var countTMP = countGO.AddComponent<TextMeshProUGUI>();
        countTMP.text = "0"; countTMP.fontSize = 56;
        countTMP.fontStyle = FontStyles.Bold; countTMP.color = HexColor("#FF7043");
        countTMP.alignment = TextAlignmentOptions.Center; countTMP.raycastTarget = false;

        // ═══════════════════════════════════════
        //  LOAD WORLD ART + WIRE CONTROLLER
        // ═══════════════════════════════════════

        var ctrl = canvasGO.AddComponent<FindTheAnimalController>();

        ctrl.targetImage = tImg;
        ctrl.remainingText = countTMP;

        ctrl.skyImage = skyImg;
        ctrl.groundImage = groundImg;
        ctrl.groundFrontImage = groundFrontImg;
        ctrl.cloudLayer = cloudRT;
        ctrl.backRowLayer = backRowRT;
        ctrl.animalLayer = animalRT;
        ctrl.frontRowLayer = frontRowRT;

        ctrl.cloudSprites = LoadSprites(CloudNames);
        ctrl.treeSprites = LoadSprites(TreeNames);
        ctrl.smallTreeSprites = LoadSprites(SmallTreeNames);
        ctrl.bushSprites = LoadSprites(BushNames);
        ctrl.houseSprites = LoadSprites(HouseNames);
        ctrl.fenceSprites = LoadSprites(FenceNames);

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/FindTheAnimal.unity");
    }

    // ─── Helpers ───

    private static Sprite[] LoadSprites(string[] names)
    {
        var list = new List<Sprite>();
        foreach (var n in names) { var s = LoadSprite(WorldArt + n + ".png"); if (s != null) list.Add(s); }
        return list.ToArray();
    }

    private static GameObject StretchImg(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Full(go.AddComponent<RectTransform>());
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static GameObject IconBtn(Transform p, string name, Sprite icon,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name); go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = icon; img.preserveAspect = true; img.color = Color.white;
        go.AddComponent<Button>().targetGraphic = img;
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
