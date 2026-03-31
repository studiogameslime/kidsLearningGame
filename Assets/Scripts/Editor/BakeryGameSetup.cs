using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the BakeryGame scene from scratch.
/// Layout: tray on LEFT, draggable cookies on RIGHT, warm bakery atmosphere.
/// </summary>
public class BakeryGameSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    // Warm bakery palette
    private static readonly Color BgColor       = HexColor("#FDF6EC"); // soft cream
    private static readonly Color TopBarColor   = HexColor("#A0785A"); // warm brown header
    private static readonly Color TrayColor     = HexColor("#C9A87C"); // light wood tray surface
    private static readonly Color TrayRimColor  = HexColor("#8B6B4A"); // darker tray rim
    private static readonly Color SlotColor     = HexColor("#8A6840"); // indented slot (noticeably darker than tray)
    private static readonly Color SlotEdgeLight = new Color(1f, 1f, 1f, 0.22f); // top-left highlight edge
    private static readonly Color SlotEdgeDark  = new Color(0f, 0f, 0f, 0.35f); // bottom-right inner shadow
    private static readonly Color CookiesPanel  = HexColor("#FFF8EF"); // light cream for cookies area

    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Bakery Game", "Data...", 0.2f);
            UpdateGameData();
            EditorUtility.DisplayProgressBar("Bakery Game", "Scene...", 0.6f);
            BuildScene();
            EditorUtility.DisplayProgressBar("Bakery Game", "Done", 1f);
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static void UpdateGameData()
    {
        EnsureFolder("Assets/Data");
        EnsureFolder("Assets/Data/Games");

        var path = "Assets/Data/Games/Bakery.asset";
        var data = AssetDatabase.LoadAssetAtPath<GameItemData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<GameItemData>();
            AssetDatabase.CreateAsset(data, path);
        }
        data.id = "bakery";
        data.title = "Bakery";
        data.targetSceneName = "BakeryGame";
        data.hasSubItems = false;
        data.cardColor = HexColor("#FFAB91");
        data.thumbnail = LoadSprite("Assets/Art/Games Preview/Bakery.png");
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
    }

    private static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");

        // ── Camera ──
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgColor;
        camGO.AddComponent<AudioListener>();
        camGO.tag = "MainCamera";

        // ── EventSystem ──
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();

        // ── Canvas ──
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Background (single warm screen fill — clean, no gradient mess) ──
        var bgGO = Fill(canvasGO.transform, "Background", BgColor);
        bgGO.transform.SetAsFirstSibling();

        // ── SafeArea ──
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(canvasGO.transform, false);
        Full(safeGO.AddComponent<RectTransform>());
        safeGO.AddComponent<SafeAreaHandler>();

        // ── TopBar ──
        var topBar = new GameObject("TopBar");
        topBar.transform.SetParent(safeGO.transform, false);
        var tbRT = topBar.AddComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0, 1);
        tbRT.anchorMax = Vector2.one;
        tbRT.pivot = new Vector2(0.5f, 1);
        tbRT.sizeDelta = new Vector2(0, TopBarHeight);
        topBar.AddComponent<Image>().color = TopBarColor;
        topBar.AddComponent<ThemeHeader>();

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(topBar.transform, false);
        Full(titleGO.AddComponent<RectTransform>());
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05DE\u05D0\u05E4\u05D9\u05D9\u05D4"); // מאפייה
        titleTMP.fontSize = 52;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", UISheetHelper.HomeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        // ══════════════════════════════════════════════════════════════
        //  GAME AREA — LEFT: Tray | RIGHT: Cookies
        // ══════════════════════════════════════════════════════════════
        // Content area below header
        float headerFrac = (float)TopBarHeight / Ref.y; // ~0.093

        // ── LEFT: Tray panel ──
        var trayPanelGO = new GameObject("TrayPanel");
        trayPanelGO.transform.SetParent(safeGO.transform, false);
        var tpRT = trayPanelGO.AddComponent<RectTransform>();
        tpRT.anchorMin = new Vector2(0.02f, 0.03f);
        tpRT.anchorMax = new Vector2(0.52f, 1f - headerFrac - 0.02f);
        tpRT.offsetMin = Vector2.zero;
        tpRT.offsetMax = Vector2.zero;

        // Tray rim (outer frame — darker wood)
        var trayRimImg = trayPanelGO.AddComponent<Image>();
        if (roundedRect != null) { trayRimImg.sprite = roundedRect; trayRimImg.type = Image.Type.Sliced; }
        trayRimImg.color = TrayRimColor;
        trayRimImg.raycastTarget = false;
        trayPanelGO.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.3f);

        // Tray surface (inner — lighter, inset from rim)
        var traySurfGO = new GameObject("TraySurface");
        traySurfGO.transform.SetParent(trayPanelGO.transform, false);
        var tsRT = traySurfGO.AddComponent<RectTransform>();
        tsRT.anchorMin = Vector2.zero; tsRT.anchorMax = Vector2.one;
        tsRT.offsetMin = new Vector2(14, 14); tsRT.offsetMax = new Vector2(-14, -14);
        var tsImg = traySurfGO.AddComponent<Image>();
        if (roundedRect != null) { tsImg.sprite = roundedRect; tsImg.type = Image.Type.Sliced; }
        tsImg.color = TrayColor;
        tsImg.raycastTarget = false;

        // TrayArea (actual slot container inside the surface)
        var trayGO = new GameObject("TrayArea");
        trayGO.transform.SetParent(traySurfGO.transform, false);
        var trayRT = trayGO.AddComponent<RectTransform>();
        trayRT.anchorMin = new Vector2(0.05f, 0.05f);
        trayRT.anchorMax = new Vector2(0.95f, 0.95f);
        trayRT.offsetMin = Vector2.zero;
        trayRT.offsetMax = Vector2.zero;

        // ── RIGHT: Cookies panel ──
        var cookiesPanelGO = new GameObject("CookiesPanel");
        cookiesPanelGO.transform.SetParent(safeGO.transform, false);
        var cpRT = cookiesPanelGO.AddComponent<RectTransform>();
        cpRT.anchorMin = new Vector2(0.54f, 0.03f);
        cpRT.anchorMax = new Vector2(0.98f, 1f - headerFrac - 0.02f);
        cpRT.offsetMin = Vector2.zero;
        cpRT.offsetMax = Vector2.zero;

        // Soft panel background for cookies area
        var cpImg = cookiesPanelGO.AddComponent<Image>();
        if (roundedRect != null) { cpImg.sprite = roundedRect; cpImg.type = Image.Type.Sliced; }
        cpImg.color = CookiesPanel;
        cpImg.raycastTarget = false;
        var cpShadow = cookiesPanelGO.AddComponent<Shadow>();
        cpShadow.effectColor = new Color(0, 0, 0, 0.08f);

        // CookiesArea (actual cookie container)
        var cookiesGO = new GameObject("CookiesArea");
        cookiesGO.transform.SetParent(cookiesPanelGO.transform, false);
        var cookiesRT = cookiesGO.AddComponent<RectTransform>();
        cookiesRT.anchorMin = new Vector2(0.05f, 0.05f);
        cookiesRT.anchorMax = new Vector2(0.95f, 0.95f);
        cookiesRT.offsetMin = Vector2.zero;
        cookiesRT.offsetMax = Vector2.zero;

        // ── Load cookie sprites ──
        var cookieSprites = LoadAllSprites("Assets/Art/BakeryGame/Cookies.png");

        // ── Controller ──
        var ctrl = canvasGO.AddComponent<BakeryGameController>();
        ctrl.trayArea = trayRT;
        ctrl.cookiesArea = cookiesRT;
        ctrl.trayImage = tsImg;
        ctrl.cookieSprites = cookieSprites;
        ctrl.roundedRect = roundedRect;
        ctrl.circleSprite = circleSprite;
        ctrl.slotColor = SlotColor;
        ctrl.slotEdgeLight = SlotEdgeLight;
        ctrl.slotEdgeDark = SlotEdgeDark;

        // Wire home button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        // Leaderboard
        var trophyIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Icons/trophy.png");
        if (trophyIcon != null)
        {
            var trophyGO = CreateIconButton(topBar.transform, "TrophyButton", trophyIcon,
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(-24, 0), new Vector2(80, 80));
            var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
            leaderboard.gameId = "bakery";
            leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        }

        // Tutorial hand
        TutorialHandHelper.Create(safeGO.transform, TutorialHandHelper.Anim.SlideRight,
            Vector2.zero, new Vector2(400, 400), "bakery");

        // Save
        EnsureFolder("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/BakeryGame.unity");
        var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        string scenePath = "Assets/Scenes/BakeryGame.unity";
        bool found = false;
        foreach (var s in buildScenes) if (s.path == scenePath) { found = true; break; }
        if (!found)
        {
            buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
        }
    }

    // ── Helpers ──

    private static Sprite[] LoadAllSprites(string path)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        var sprites = new List<Sprite>();
        foreach (var o in all) if (o is Sprite s) sprites.Add(s);
        sprites.Sort((a, b) => string.Compare(a.name, b.name));
        return sprites.ToArray();
    }

    private static Sprite LoadSprite(string p) => AssetDatabase.LoadAssetAtPath<Sprite>(p);

    private static GameObject Fill(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Full(go.AddComponent<RectTransform>());
        var img = go.AddComponent<Image>();
        img.color = color; img.raycastTarget = false;
        return go;
    }

    private static void Full(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static GameObject CreateIconButton(Transform parent, string name, Sprite icon,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = icon; img.preserveAspect = true;
        img.color = Color.white; img.raycastTarget = true;
        go.AddComponent<Button>().targetGraphic = img;
        return go;
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var p = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            AssetDatabase.CreateFolder(p, System.IO.Path.GetFileName(path));
        }
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c); return c;
    }
}
