using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Builds the BakeryGame scene from scratch.
/// Bakery-themed shape matching: drag cookies into matching tray slots.
/// </summary>
public class BakeryGameSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    // Warm bakery palette
    private static readonly Color BgColor     = HexColor("#F5E6D0"); // warm beige
    private static readonly Color CounterColor = HexColor("#D2B48C"); // tan wood
    private static readonly Color TrayColor   = HexColor("#8B6B4A"); // dark wood tray
    private static readonly Color TopBarColor = HexColor("#A0785A"); // warm brown header

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

        // Camera
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgColor;
        camGO.AddComponent<AudioListener>();
        camGO.tag = "MainCamera";

        // EventSystem
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background (full screen, behind SafeArea)
        var bg = Fill(canvasGO.transform, "Background", BgColor);
        bg.transform.SetAsFirstSibling();

        // Counter stripe at bottom
        var counter = Fill(canvasGO.transform, "Counter", CounterColor);
        var counterRT = counter.GetComponent<RectTransform>();
        counterRT.anchorMin = new Vector2(0, 0);
        counterRT.anchorMax = new Vector2(1, 0.25f);

        // SafeArea
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeGO.AddComponent<RectTransform>();
        Full(safeRT);
        safeGO.AddComponent<SafeAreaHandler>();

        // TopBar
        var topBar = new GameObject("TopBar");
        topBar.transform.SetParent(safeGO.transform, false);
        var tbRT = topBar.AddComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0, 1);
        tbRT.anchorMax = Vector2.one;
        tbRT.pivot = new Vector2(0.5f, 1);
        tbRT.sizeDelta = new Vector2(0, TopBarHeight);
        var tbImg = topBar.AddComponent<Image>();
        tbImg.color = TopBarColor;
        topBar.AddComponent<ThemeHeader>();

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05DE\u05D0\u05E4\u05D9\u05D9\u05D4"); // מאפייה
        titleTMP.fontSize = 52;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button
        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        // Tray area (centered, above counter)
        var trayGO = new GameObject("TrayArea");
        trayGO.transform.SetParent(safeGO.transform, false);
        var trayRT = trayGO.AddComponent<RectTransform>();
        trayRT.anchorMin = new Vector2(0.15f, 0.28f);
        trayRT.anchorMax = new Vector2(0.85f, 0.88f);
        trayRT.offsetMin = Vector2.zero;
        trayRT.offsetMax = Vector2.zero;
        var trayImg = trayGO.AddComponent<Image>();
        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
        if (roundedRect != null) { trayImg.sprite = roundedRect; trayImg.type = Image.Type.Sliced; }
        trayImg.color = TrayColor;
        trayImg.raycastTarget = false;
        trayGO.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.25f);

        // Cookies area (bottom strip, on the counter)
        var cookiesGO = new GameObject("CookiesArea");
        cookiesGO.transform.SetParent(safeGO.transform, false);
        var cookiesRT = cookiesGO.AddComponent<RectTransform>();
        cookiesRT.anchorMin = new Vector2(0.05f, 0.02f);
        cookiesRT.anchorMax = new Vector2(0.95f, 0.26f);
        cookiesRT.offsetMin = Vector2.zero;
        cookiesRT.offsetMax = Vector2.zero;

        // Load cookie sprites
        var cookieSprites = LoadAllSprites("Assets/Art/BakeryGame/Cookies.png");

        // Controller
        var ctrl = canvasGO.AddComponent<BakeryGameController>();
        ctrl.trayArea = trayRT;
        ctrl.cookiesArea = cookiesRT;
        ctrl.trayImage = trayImg;
        ctrl.cookieSprites = cookieSprites;
        ctrl.roundedRect = roundedRect;

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

        // Save scene
        EnsureFolder("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/BakeryGame.unity");

        // Build settings
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
        foreach (var o in all)
            if (o is Sprite s) sprites.Add(s);
        sprites.Sort((a, b) => string.Compare(a.name, b.name));
        return sprites.ToArray();
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject Fill(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        Full(rt);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return go;
    }

    private static void Full(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject CreateIconButton(Transform parent, string name, Sprite icon,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
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
            var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            var folder = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
