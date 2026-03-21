using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the LaundrySorting scene — landscape layout.
/// Left: washing machine on grass. Right: scattered items to sort.
/// </summary>
public class LaundrySortingSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    // Colors matching WorldScene
    private static readonly Color DaySky = HexColor("#8FD4F5");
    private static readonly Color DayHillsLarge = HexColor("#B7D7D6");
    private static readonly Color DayHills = HexColor("#9FCBC5");
    private static readonly Color DayGroundBack = HexColor("#8ED36B");
    private static readonly Color DayGroundFront = HexColor("#79C956");
    private static readonly Color HeaderColor = new Color(0.30f, 0.20f, 0.12f, 0.65f);
    private static readonly string WorldArt = "Assets/Art/World";

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Laundry Sorting", "Building scene...", 0.5f);
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

        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");

        // ── Camera ──
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = DaySky;
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

        // ── Layered background (same style as WorldScene) ──
        var hillsLargeSprite = LoadSprite($"{WorldArt}/hillsLarge.png");
        var hillsSprite = LoadSprite($"{WorldArt}/hills.png");
        var groundLayer1Sprite = LoadSprite($"{WorldArt}/groundLayer1.png");
        var groundLayer2Sprite = LoadSprite($"{WorldArt}/groundLayer2.png");

        // Sky
        var skyGO = Layer(root, "Sky", null, 0, 0, 1, 1, DaySky);
        skyGO.GetComponent<Image>().raycastTarget = false;

        // Cloud layers (decorative, behind hills)
        var cloudLayer1 = LoadSprite($"{WorldArt}/cloudLayer1.png");
        var cloudLayer2 = LoadSprite($"{WorldArt}/cloudLayer2.png");
        if (cloudLayer1 != null)
            CreateSpriteLayer(root, "CloudLayer1", cloudLayer1, new Vector2(0, 0.55f), new Vector2(1, 0.95f), new Color(1, 1, 1, 0.6f));
        if (cloudLayer2 != null)
            CreateSpriteLayer(root, "CloudLayer2", cloudLayer2, new Vector2(0, 0.50f), new Vector2(1, 0.85f), new Color(1, 1, 1, 0.4f));

        // Individual clouds (will drift at runtime)
        var cloudSprites = new List<Sprite>();
        for (int i = 1; i <= 8; i++)
        {
            var cs = LoadSprite($"{WorldArt}/cloud{i}.png");
            if (cs != null) cloudSprites.Add(cs);
        }
        var cloudContainer = new GameObject("Clouds");
        cloudContainer.transform.SetParent(root, false);
        var cloudContainerRT = cloudContainer.AddComponent<RectTransform>();
        Full(cloudContainerRT);
        for (int i = 0; i < Mathf.Min(5, cloudSprites.Count); i++)
        {
            var cgo = new GameObject($"Cloud_{i}");
            cgo.transform.SetParent(cloudContainer.transform, false);
            var crt = cgo.AddComponent<RectTransform>();
            float cx = (float)(i + 1) / 6f + Random.Range(-0.05f, 0.05f);
            float cy = Random.Range(0.60f, 0.92f);
            float sz = Random.Range(100f, 200f);
            crt.anchorMin = new Vector2(cx, cy);
            crt.anchorMax = new Vector2(cx, cy);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(sz * 1.8f, sz);
            crt.anchoredPosition = Vector2.zero;
            var cimg = cgo.AddComponent<Image>();
            cimg.sprite = cloudSprites[i % cloudSprites.Count];
            cimg.preserveAspect = true;
            cimg.raycastTarget = false;
            cimg.color = new Color(1, 1, 1, 0.85f);
            cgo.AddComponent<CloudDrifter>();
        }

        // Hills
        if (hillsLargeSprite != null)
            CreateSpriteLayer(root, "HillsLarge", hillsLargeSprite, new Vector2(0, 0.20f), new Vector2(1, 0.55f), DayHillsLarge);
        if (hillsSprite != null)
            CreateSpriteLayer(root, "Hills", hillsSprite, new Vector2(0, 0.25f), new Vector2(1, 0.5f), DayHills);

        // Ground
        if (groundLayer1Sprite != null)
            CreateSpriteLayer(root, "GroundBack", groundLayer1Sprite, new Vector2(0, 0), new Vector2(1, 0.45f), DayGroundBack);
        if (groundLayer2Sprite != null)
            CreateSpriteLayer(root, "GroundFront", groundLayer2Sprite, new Vector2(0, 0), new Vector2(1, 0.25f), DayGroundFront);

        // ── Safe Area ──
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(root, false);
        var safeRT = safeGO.AddComponent<RectTransform>();
        Full(safeRT);
        safeGO.AddComponent<SafeAreaHandler>();

        // ── Top Bar ──
        var bar = new GameObject("TopBar");
        bar.transform.SetParent(safeGO.transform, false);
        var barRT = bar.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1);
        barRT.anchorMax = new Vector2(1, 1);
        barRT.pivot = new Vector2(0.5f, 1);
        barRT.sizeDelta = new Vector2(0, TopBarHeight);
        bar.AddComponent<Image>().color = HeaderColor;
        bar.GetComponent<Image>().raycastTarget = false;
        bar.AddComponent<ThemeHeader>();

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(bar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(110, 0);
        titleRT.offsetMax = new Vector2(-110, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05DE\u05D9\u05D9\u05DF \u05D1\u05D2\u05D3\u05D9\u05DD"); // מיין בגדים
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(1f, 0.96f, 0.88f, 1f);
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = IconBtn(bar.transform, "HomeButton", homeIcon, 16, -20, 90);

        // ── Play Area (below header) ──
        var playGO = new GameObject("PlayArea");
        playGO.transform.SetParent(safeGO.transform, false);
        var playRT = playGO.AddComponent<RectTransform>();
        Full(playRT);
        playRT.offsetMax = new Vector2(0, -TopBarHeight);

        // ── Washing Machine (left side, on grass) ──
        var machineSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Washing Machine.png");
        if (machineSprite == null)
            Debug.LogError("LaundrySortingSetup: Washing Machine.png not found or not imported as Sprite!");

        var machineGO = new GameObject("WashingMachine");
        machineGO.transform.SetParent(playRT, false);
        var machineRT = machineGO.AddComponent<RectTransform>();
        machineRT.anchorMin = new Vector2(0.02f, 0.02f);
        machineRT.anchorMax = new Vector2(0.02f, 0.02f);
        machineRT.pivot = new Vector2(0, 0);
        machineRT.sizeDelta = new Vector2(700, 700);
        machineRT.anchoredPosition = new Vector2(0, 0);

        var machineImg = machineGO.AddComponent<Image>();
        machineImg.sprite = machineSprite;
        machineImg.preserveAspect = true;
        machineImg.raycastTarget = false;

        // ── Basket (right side, on grass) ──
        var basketSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Basket.png");
        if (basketSprite == null)
            Debug.LogError("LaundrySortingSetup: Basket.png not found!");

        var basketGO = new GameObject("Basket");
        basketGO.transform.SetParent(playRT, false);
        var basketRT2 = basketGO.AddComponent<RectTransform>();
        basketRT2.anchorMin = new Vector2(0.98f, 0.02f);
        basketRT2.anchorMax = new Vector2(0.98f, 0.02f);
        basketRT2.pivot = new Vector2(1, 0);
        basketRT2.sizeDelta = new Vector2(560, 560);
        basketRT2.anchoredPosition = new Vector2(0, 0);

        var basketImg = basketGO.AddComponent<Image>();
        basketImg.sprite = basketSprite;
        basketImg.preserveAspect = true;
        basketImg.raycastTarget = false;

        // ── Items Area (full width but items spawn only in safe zones) ──
        var itemsGO = new GameObject("ItemsArea");
        itemsGO.transform.SetParent(playRT, false);
        var itemsRT = itemsGO.AddComponent<RectTransform>();
        itemsRT.anchorMin = new Vector2(0.18f, 0.02f);
        itemsRT.anchorMax = new Vector2(0.82f, 0.98f);
        itemsRT.offsetMin = Vector2.zero;
        itemsRT.offsetMax = Vector2.zero;

        // ── Load sprite sheets ──
        var clothesSprites = LoadSpriteSheet("Assets/Art/Clothes/Clothes.png");
        var fruitSprites = LoadSpriteSheet("Assets/Art/Fruits/Fruits.png");

        // ── Controller ──
        var ctrl = canvasGO.AddComponent<LaundrySortingController>();
        ctrl.playArea = playRT;
        ctrl.itemsArea = itemsRT;
        ctrl.washingMachineImage = machineImg;
        ctrl.washingMachineRT = machineRT;
        ctrl.basketImage = basketImg;
        ctrl.basketRT = basketRT2;
        ctrl.clothesSprites = clothesSprites;
        ctrl.fruitSprites = fruitSprites;
        ctrl.circleSprite = circleSprite;
        ctrl.clothesCount = 10;
        ctrl.fruitsCount = 5;
        ctrl.itemSize = 140f;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/LaundrySorting.unity");
    }

    // ── Helpers ──

    private static Sprite[] LoadSpriteSheet(string path)
    {
        var sprites = new List<Sprite>();
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (allAssets != null)
        {
            foreach (var asset in allAssets)
            {
                if (asset is Sprite spr)
                    sprites.Add(spr);
            }
        }
        sprites.Sort((a, b) =>
        {
            int numA = 0, numB = 0;
            var partsA = a.name.Split('_');
            var partsB = b.name.Split('_');
            if (partsA.Length > 1) int.TryParse(partsA[partsA.Length - 1], out numA);
            if (partsB.Length > 1) int.TryParse(partsB[partsB.Length - 1], out numB);
            return numA.CompareTo(numB);
        });
        return sprites.ToArray();
    }

    private static GameObject Layer(Transform p, string name, Sprite spr,
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
        img.color = c;
        img.raycastTarget = false;
        return go;
    }

    private static GameObject IconBtn(Transform p, string name, Sprite icon, float x, float y, float sz)
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
        go.AddComponent<Button>().targetGraphic = img;
        return go;
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
        if (all != null) foreach (var o in all) if (o is Sprite sp) return sp;
        return null;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
