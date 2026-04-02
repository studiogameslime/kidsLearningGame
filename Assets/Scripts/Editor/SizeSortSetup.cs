using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the SizeSort scene — farm world with tractor + 3 carts.
/// Child drags fruits by size into matching carts.
/// </summary>
public class SizeSortSetup
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    private static readonly Color SkyTop      = HexColor("#87CEEB");
    private static readonly Color SkyBottom   = HexColor("#B8E4F0");
    private static readonly Color GrassBack   = HexColor("#7EC850");
    private static readonly Color GrassFront  = HexColor("#5DAA35");
    private static readonly Color HillsFar    = HexColor("#B7D7D6");
    private static readonly Color HillsNear   = HexColor("#9FCBC5");
    private static readonly Color HeaderColor = new Color(0.1f, 0.4f, 0.2f, 0.65f);
    private static readonly int TopBarHeight  = SetupConstants.HeaderHeight;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Size Sort Setup", "Copying assets…", 0.3f);
            CopyAssets();

            EditorUtility.DisplayProgressBar("Size Sort Setup", "Building scene…", 0.6f);
            CreateScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void CopyAssets()
    {
        EnsureFolder("Assets/Resources/Tractor");

        Copy("Assets/Art/Tractor/Tractor.png", "Assets/Resources/Tractor/Tractor.png");
        Copy("Assets/Art/Tractor/Big cart.png", "Assets/Resources/Tractor/BigCart.png");
        Copy("Assets/Art/Tractor/Medium cart.png", "Assets/Resources/Tractor/MediumCart.png");
        Copy("Assets/Art/Tractor/Small cart.png", "Assets/Resources/Tractor/SmallCart.png");
        Copy("Assets/Art/Fruits/Fruits.png", "Assets/Resources/Tractor/Fruits.png");

        CopyMeta("Assets/Art/Tractor/Tractor.png.meta", "Assets/Resources/Tractor/Tractor.png.meta");
        CopyMeta("Assets/Art/Tractor/Big cart.png.meta", "Assets/Resources/Tractor/BigCart.png.meta");
        CopyMeta("Assets/Art/Tractor/Medium cart.png.meta", "Assets/Resources/Tractor/MediumCart.png.meta");
        CopyMeta("Assets/Art/Tractor/Small cart.png.meta", "Assets/Resources/Tractor/SmallCart.png.meta");
        CopyMeta("Assets/Art/Fruits/Fruits.png.meta", "Assets/Resources/Tractor/Fruits.png.meta");
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
        cam.backgroundColor = SkyTop;
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
        var canvasGO = new GameObject("SizeSortCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        string artDir = "Assets/Art/World";
        var hillsLargeSprite = LoadSprite($"{artDir}/hillsLarge.png");
        var hillsSprite      = LoadSprite($"{artDir}/hills.png");
        var groundLayer1     = LoadSprite($"{artDir}/groundLayer1.png");
        var groundLayer2     = LoadSprite($"{artDir}/groundLayer2.png");
        var sunSprite        = LoadSprite($"{artDir}/sun.png");

        // ═══ BACKGROUND LAYERS ═══

        // Sky
        var skyGO = StretchImg(canvasGO.transform, "Sky", SkyTop);
        skyGO.GetComponent<Image>().raycastTarget = false;

        var skyBtm = StretchImg(canvasGO.transform, "SkyBottom", SkyBottom);
        skyBtm.GetComponent<Image>().raycastTarget = false;
        var skyBtmRT = skyBtm.GetComponent<RectTransform>();
        skyBtmRT.anchorMin = new Vector2(0, 0.25f);
        skyBtmRT.anchorMax = new Vector2(1, 0.55f);

        // Sun
        if (sunSprite != null)
        {
            var sunGO = new GameObject("Sun");
            sunGO.transform.SetParent(canvasGO.transform, false);
            var sunRT = sunGO.AddComponent<RectTransform>();
            sunRT.anchorMin = new Vector2(1, 1);
            sunRT.anchorMax = new Vector2(1, 1);
            sunRT.pivot = new Vector2(1, 1);
            sunRT.sizeDelta = new Vector2(120, 120);
            sunRT.anchoredPosition = new Vector2(-40, -20);
            var sunImg = sunGO.AddComponent<Image>();
            sunImg.sprite = sunSprite;
            sunImg.preserveAspect = true;
            sunImg.raycastTarget = false;
        }

        // Background clouds
        CreateBgClouds(canvasGO.transform);

        // Hills far (higher, covers sky-grass transition)
        var hillsFarGO = CreateSpriteLayer(canvasGO.transform, "HillsFar", hillsLargeSprite,
            new Vector2(0, 0.35f), new Vector2(1, 0.65f), HillsFar);
        var hillsFarRT = hillsFarGO.GetComponent<RectTransform>();

        // Hills near
        var hillsNearGO = CreateSpriteLayer(canvasGO.transform, "HillsNear", hillsSprite,
            new Vector2(0, 0.25f), new Vector2(1, 0.5f), HillsNear);
        var hillsNearRT = hillsNearGO.GetComponent<RectTransform>();

        // Grass back (taller to cover more area)
        var grassBackGO = CreateSpriteLayer(canvasGO.transform, "GrassBack", groundLayer1,
            new Vector2(0, 0), new Vector2(1, 0.45f), GrassBack);
        var grassBackRT = grassBackGO.GetComponent<RectTransform>();

        // Grass front
        var grassFrontGO = CreateSpriteLayer(canvasGO.transform, "GrassFront", groundLayer2,
            new Vector2(0, 0), new Vector2(1, 0.25f), GrassFront);
        var grassFrontRT = grassFrontGO.GetComponent<RectTransform>();

        // ═══ GAMEPLAY ═══

        var gameplayGO = new GameObject("GameplayArea");
        gameplayGO.transform.SetParent(canvasGO.transform, false);
        var gameplayRT = gameplayGO.AddComponent<RectTransform>();
        Full(gameplayRT);

        // Load tractor/cart sprites
        var tractorSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Tractor/Tractor.png");
        var bigCartSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Tractor/Big cart.png");
        var medCartSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Tractor/Medium cart.png");
        var smlCartSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Tractor/Small cart.png");

        Sprite tractorSpr = FindSprite(tractorSprites);
        Sprite bigCartSpr = FindSprite(bigCartSprites);
        Sprite medCartSpr = FindSprite(medCartSprites);
        Sprite smlCartSpr = FindSprite(smlCartSprites);

        float groundY = 60f; // Y position on grass

        // Tractor — faces left, positioned on the left side
        var tractorGO = new GameObject("Tractor");
        tractorGO.transform.SetParent(gameplayGO.transform, false);
        var tractorRT = tractorGO.AddComponent<RectTransform>();
        tractorRT.anchorMin = tractorRT.anchorMax = new Vector2(0.5f, 0);
        tractorRT.pivot = new Vector2(0.5f, 0);
        tractorRT.sizeDelta = new Vector2(600, 480);
        tractorRT.anchoredPosition = new Vector2(-200, groundY); // will be repositioned by controller
        // no flip — sprite already faces the correct direction
        var tractorImg = tractorGO.AddComponent<Image>();
        if (tractorSpr != null) tractorImg.sprite = tractorSpr;
        tractorImg.preserveAspect = true;
        tractorImg.raycastTarget = false;

        // Create carts in back-to-front order (small first = behind, tractor last = front)
        // Small cart (farthest, rendered behind)
        var smCartGO = CreateCartGO(gameplayGO.transform, "SmallCart", smlCartSpr, 330, 260, groundY, 0);
        var smCart = smCartGO.GetComponent<SizeSortCart>();

        // Medium cart
        var mdCartGO = CreateCartGO(gameplayGO.transform, "MediumCart", medCartSpr, 400, 320, groundY, 1);
        var mdCart = mdCartGO.GetComponent<SizeSortCart>();

        // Large cart (closest to tractor, rendered in front of other carts)
        var lgCartGO = CreateCartGO(gameplayGO.transform, "LargeCart", bigCartSpr, 480, 380, groundY, 2);
        var lgCart = lgCartGO.GetComponent<SizeSortCart>();

        // Move tractor to last sibling so it renders on top of all carts
        tractorGO.transform.SetAsLastSibling();

        // Fruit spawn area (above carts, higher up)
        var fruitAreaGO = new GameObject("FruitSpawnArea");
        fruitAreaGO.transform.SetParent(canvasGO.transform, false);
        var fruitAreaRT = fruitAreaGO.AddComponent<RectTransform>();
        fruitAreaRT.anchorMin = new Vector2(0.05f, 0.55f);
        fruitAreaRT.anchorMax = new Vector2(0.95f, 0.80f);
        fruitAreaRT.offsetMin = Vector2.zero;
        fruitAreaRT.offsetMax = Vector2.zero;

        // ═══ SAFE AREA + UI ═══

        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        Full(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        var topBar = StretchImg(safeArea.transform, "TopBar", HeaderColor);
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.sizeDelta = new Vector2(0, TopBarHeight);
        topBar.GetComponent<Image>().raycastTarget = false;
        topBar.AddComponent<ThemeHeader>();

        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05DE\u05D9\u05D5\u05DF \u05DC\u05E4\u05D9 \u05D2\u05D5\u05D3\u05DC"); // מיון לפי גודל
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = IconBtn(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = IconBtn(topBar.transform, "TrophyButton", trophyIcon,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-16, 0), new Vector2(70, 70));

        // ═══ CONTROLLER ═══

        var controller = canvasGO.AddComponent<SizeSortController>();
        controller.gameplayArea = gameplayRT;
        controller.fruitSpawnArea = fruitAreaRT;
        controller.tractorRT = tractorRT;
        controller.largeCart = lgCart;
        controller.mediumCart = mdCart;
        controller.smallCart = smCart;
        controller.hillsFarRT = hillsFarRT;
        controller.hillsNearRT = hillsNearRT;
        controller.grassBackRT = grassBackRT;
        controller.grassFrontRT = grassFrontRT;
        controller.titleText = titleTMP;

        // Wire home button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = LoadSprite("Assets/UI/Sprites/RoundedRect.png");
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "sizesort";

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SizeSort.unity");
        Debug.Log("[SizeSortSetup] Scene created: Assets/Scenes/SizeSort.unity");
    }

    private static GameObject CreateCartGO(Transform parent, string name, Sprite sprite,
        float width, float height, float groundY, int sizeCat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(0, groundY); // positioned by controller

        var img = go.AddComponent<Image>();
        if (sprite != null) img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        var cart = go.AddComponent<SizeSortCart>();
        cart.sizeCategory = sizeCat;

        return go;
    }

    private static Sprite FindSprite(Object[] assets)
    {
        if (assets == null) return null;
        foreach (var a in assets)
            if (a is Sprite s) return s;
        return null;
    }

    // ═══ HELPERS ═══

    private static void CreateBgClouds(Transform parent)
    {
        Sprite cloudSprite = LoadSprite("Assets/Art/World/cloud1.png");
        if (cloudSprite == null) return;
        var clouds = new[] {
            new { x = 0.12f, y = 0.88f, w = 150f, h = 60f, a = 0.5f },
            new { x = 0.50f, y = 0.93f, w = 190f, h = 75f, a = 0.4f },
            new { x = 0.80f, y = 0.84f, w = 130f, h = 52f, a = 0.45f },
        };
        for (int i = 0; i < clouds.Length; i++)
        {
            var cd = clouds[i];
            var go = new GameObject($"BgCloud_{i}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(cd.x, cd.y);
            rt.anchorMax = new Vector2(cd.x, cd.y);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cd.w, cd.h);
            var img = go.AddComponent<Image>();
            img.sprite = cloudSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = new Color(1, 1, 1, cd.a);
        }
    }

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

    private static void Copy(string src, string dst)
    {
        if (!System.IO.File.Exists(src)) return;
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

    private static GameObject StretchImg(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Full(go.AddComponent<RectTransform>());
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static void Full(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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
