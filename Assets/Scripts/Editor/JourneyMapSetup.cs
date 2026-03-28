using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the JourneyMap scene: scrollable island path with 100 nodes.
/// </summary>
public class JourneyMapSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;
    private static readonly Color WaterColor = HexColor("#7DD3E8");
    private static readonly Color TopBarColor = new Color(0.35f, 0.75f, 0.88f, 0.9f);

    private const string JourneyArt = "Assets/Art/Journey";

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Journey Map Setup", "Building scene…", 0.5f);
            BuildScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void BuildScene()
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera ──
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera"; camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = WaterColor;
        cam.orthographic = true;
        var urp = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urp != null) camGO.AddComponent(urp);

        // ── EventSystem ──
        var esGO = new GameObject("EventSystem"); esGO.AddComponent<EventSystem>();
        var inp = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inp != null) esGO.AddComponent(inp); else esGO.AddComponent<StandaloneInputModule>();

        // ── Canvas ──
        var canvasGO = new GameObject("JourneyMapCanvas");
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

        // ── Water background ──
        var bgGO = new GameObject("WaterBg");
        bgGO.transform.SetParent(safeGO.transform, false);
        Full(bgGO.AddComponent<RectTransform>());
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = WaterColor;
        bgImg.raycastTarget = false;

        // ── Header ──
        var bar = new GameObject("TopBar");
        bar.transform.SetParent(safeGO.transform, false);
        var barRT = bar.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1); barRT.anchorMax = new Vector2(1, 1);
        barRT.pivot = new Vector2(0.5f, 1); barRT.sizeDelta = new Vector2(0, TopBarHeight);
        var barImg = bar.AddComponent<Image>();
        barImg.color = TopBarColor; barImg.raycastTarget = false;
        bar.AddComponent<ThemeHeader>();

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(bar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT); titleRT.offsetMin = new Vector2(100, 0); titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05DE\u05E1\u05E2"); // מסע
        titleTMP.fontSize = 36; titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white; titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Back button
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = IconBtn(bar.transform, "BackButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(90, 90));

        // ── Scroll View (vertical, map scrolls up/down) ──
        var scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(safeGO.transform, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero; scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = Vector2.zero; scrollRT.offsetMax = new Vector2(0, -TopBarHeight);
        scrollGO.AddComponent<Image>().color = Color.clear;
        scrollGO.GetComponent<Image>().raycastTarget = true;
        var scrollView = scrollGO.AddComponent<ScrollRect>();
        scrollView.horizontal = false;
        scrollView.vertical = true;
        scrollView.movementType = ScrollRect.MovementType.Elastic;
        scrollView.elasticity = 0.1f;
        scrollView.scrollSensitivity = 30f;
        scrollGO.AddComponent<RectMask2D>();

        // Content container (sized dynamically by controller)
        var contentGO = new GameObject("MapContent");
        contentGO.transform.SetParent(scrollGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.5f, 1); // top-center
        contentRT.anchorMax = new Vector2(0.5f, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.sizeDelta = new Vector2(1920, 5000); // will be resized by controller

        scrollView.content = contentRT;

        // ── Load all sprites ──
        var platformSprites = new List<Sprite>();
        for (int i = 1; i <= 22; i++)
        {
            var spr = LoadSprite($"{JourneyArt}/Platforms/{i:D2}.png");
            platformSprites.Add(spr);
        }

        var elementSprites = new List<Sprite>();
        for (int i = 1; i <= 10; i++)
        {
            var spr = LoadSprite($"{JourneyArt}/Elements/{i:D2}.png");
            elementSprites.Add(spr);
        }

        var giftSprite = LoadSprite($"{JourneyArt}/Gift.png");
        var starSprite = LoadSprite($"{JourneyArt}/Star.png");
        var playerSprite = LoadSprite($"{JourneyArt}/User01.png");

        // ── Controller ──
        var ctrl = canvasGO.AddComponent<JourneyMapController>();
        ctrl.mapContent = contentRT;
        ctrl.scrollRect = scrollView;
        ctrl.platformSprites = platformSprites.ToArray();
        ctrl.elementSprites = elementSprites.ToArray();
        ctrl.giftSprite = giftSprite;
        ctrl.starSprite = starSprite;
        ctrl.playerSprite = playerSprite;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnBackPressed);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/JourneyMap.unity");
    }

    // ── Helpers ──

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
