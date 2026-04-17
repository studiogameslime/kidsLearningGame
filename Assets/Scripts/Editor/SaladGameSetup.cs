using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the SaladGame scene — cute kitchen counter background.
/// </summary>
public class SaladGameSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly int HeaderHeight = SetupConstants.HeaderHeight;

    // Kitchen palette
    private static readonly Color WallColor    = HexColor("#F0E8DD");
    private static readonly Color CounterColor = HexColor("#D4C4AA");
    private static readonly Color CounterEdge  = HexColor("#B8A888");
    private static readonly Color SplashColor  = HexColor("#C5DDE8");
    private static readonly Color HeaderColor  = new Color(0.45f, 0.72f, 0.45f, 0.88f);

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Salad Game Setup", "Building scene...", 0.5f);
            var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
            CreateScene(roundedRect);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static void CreateScene(Sprite roundedRect)
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = WallColor;
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
        var canvasGO = new GameObject("SaladGameCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        CreateKitchenBg(canvasGO.transform, roundedRect);

        // SafeArea
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        StretchFull(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // Header
        var topBar = CreateImg(safeArea.transform, "TopBar", HeaderColor);
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.sizeDelta = new Vector2(0, HeaderHeight);
        topBar.GetComponent<Image>().raycastTarget = false;
        topBar.AddComponent<ThemeHeader>();

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        StretchFull(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D4\u05DB\u05D9\u05E0\u05D5 \u05E1\u05DC\u05D8!"); // הכינו סלט!
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = CreateIconBtn(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(90, 90));

        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = CreateIconBtn(topBar.transform, "TrophyButton", trophyIcon,
            new Vector2(1, 1), new Vector2(-16, -20), new Vector2(70, 70));

        // Play area
        var playArea = new GameObject("PlayArea");
        playArea.transform.SetParent(safeArea.transform, false);
        var playAreaRT = playArea.AddComponent<RectTransform>();
        StretchFull(playAreaRT);
        playAreaRT.offsetMax = new Vector2(0, -HeaderHeight);

        // Controller
        var controller = canvasGO.AddComponent<SaladGameController>();
        controller.playArea = playAreaRT;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnExitPressed);

        // Leaderboard
        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = roundedRect;
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "saladgame";

        // Alin Guide
        var alinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/AlinGuide.prefab");
        if (alinPrefab != null)
        {
            var alinGO = (GameObject)PrefabUtility.InstantiatePrefab(alinPrefab, safeArea.transform);
            var alinRT = alinGO.GetComponent<RectTransform>();
            alinRT.anchorMin = new Vector2(0, 0);
            alinRT.anchorMax = new Vector2(0, 0);
            alinRT.pivot = new Vector2(0, 0);
            alinRT.anchoredPosition = new Vector2(20, 20);
            alinRT.sizeDelta = new Vector2(200, 300);
            var alinGuide = alinGO.GetComponent<AlinGuide>();
            if (alinGuide != null) alinGuide.startVisible = false;
        }

        // Tutorial hand
        TutorialHandHelper.Create(safeArea.transform, TutorialHandHelper.Anim.Tap,
            Vector2.zero, new Vector2(450, 450), "saladgame");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SaladGame.unity");
        Debug.Log("[SaladGameSetup] Scene created");
    }

    private static void CreateKitchenBg(Transform parent, Sprite roundedRect)
    {
        // Wall
        var wall = CreateImg(parent, "Background", WallColor);
        wall.GetComponent<Image>().raycastTarget = false;
        wall.transform.SetAsFirstSibling();

        // Backsplash
        var splash = new GameObject("Backsplash");
        splash.transform.SetParent(wall.transform, false);
        var splashRT = splash.AddComponent<RectTransform>();
        splashRT.anchorMin = new Vector2(0, 0.30f);
        splashRT.anchorMax = new Vector2(1, 0.40f);
        splashRT.offsetMin = Vector2.zero;
        splashRT.offsetMax = Vector2.zero;
        splash.AddComponent<Image>().color = SplashColor;
        splash.GetComponent<Image>().raycastTarget = false;

        // Counter
        var counter = new GameObject("Counter");
        counter.transform.SetParent(wall.transform, false);
        var counterRT = counter.AddComponent<RectTransform>();
        counterRT.anchorMin = Vector2.zero;
        counterRT.anchorMax = new Vector2(1, 0.30f);
        counterRT.offsetMin = Vector2.zero;
        counterRT.offsetMax = Vector2.zero;
        counter.AddComponent<Image>().color = CounterColor;
        counter.GetComponent<Image>().raycastTarget = false;

        // Counter edge
        var edge = new GameObject("Edge");
        edge.transform.SetParent(counter.transform, false);
        var edgeRT = edge.AddComponent<RectTransform>();
        edgeRT.anchorMin = new Vector2(0, 1);
        edgeRT.anchorMax = new Vector2(1, 1);
        edgeRT.pivot = new Vector2(0.5f, 0.5f);
        edgeRT.sizeDelta = new Vector2(0, 5);
        edge.AddComponent<Image>().color = CounterEdge;
        edge.GetComponent<Image>().raycastTarget = false;
    }

    // Helpers

    private static GameObject CreateImg(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        StretchFull(go.AddComponent<RectTransform>());
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static GameObject CreateIconBtn(Transform parent, string name, Sprite icon,
        Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = icon; img.preserveAspect = true; img.color = Color.white;
        go.AddComponent<Button>().targetGraphic = img;
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/'); string cur = parts[0];
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
