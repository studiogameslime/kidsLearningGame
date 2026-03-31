using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class SockMatchSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly Color BgColor = HexColor("#E8F4FD");
    private static readonly Color TopBarColor = HexColor("#7BAFD4");
    private static readonly Color LineColor = HexColor("#8B6B4A");

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Sock Match", "Data...", 0.2f);
            UpdateGameData();
            EditorUtility.DisplayProgressBar("Sock Match", "Scene...", 0.6f);
            BuildScene();
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static void UpdateGameData()
    {
        EnsureFolder("Assets/Data"); EnsureFolder("Assets/Data/Games");
        var path = "Assets/Data/Games/SockMatch.asset";
        var data = AssetDatabase.LoadAssetAtPath<GameItemData>(path);
        if (data == null) { data = ScriptableObject.CreateInstance<GameItemData>(); AssetDatabase.CreateAsset(data, path); }
        data.id = "sockmatch"; data.title = "Sock Match";
        data.targetSceneName = "SockMatch"; data.hasSubItems = false;
        data.cardColor = HexColor("#80DEEA");
        EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
    }

    private static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");

        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 5;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = BgColor;
        camGO.AddComponent<AudioListener>(); camGO.tag = "MainCamera";

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>(); esGO.AddComponent<StandaloneInputModule>();

        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref; scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        var bg = Fill(canvasGO.transform, "Background", BgColor);
        bg.transform.SetAsFirstSibling();

        // SafeArea
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(canvasGO.transform, false);
        Full(safeGO.AddComponent<RectTransform>());
        safeGO.AddComponent<SafeAreaHandler>();

        // TopBar
        var topBar = new GameObject("TopBar");
        topBar.transform.SetParent(safeGO.transform, false);
        var tbRT = topBar.AddComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0, 1); tbRT.anchorMax = Vector2.one;
        tbRT.pivot = new Vector2(0.5f, 1); tbRT.sizeDelta = new Vector2(0, SetupConstants.HeaderHeight);
        topBar.AddComponent<Image>().color = TopBarColor;
        topBar.AddComponent<ThemeHeader>();

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(topBar.transform, false);
        Full(titleGO.AddComponent<RectTransform>());
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D2\u05E8\u05D1\u05D9\u05D9\u05DD"); // גרביים
        titleTMP.fontSize = 52; titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white; titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeGO = CreateIconButton(topBar.transform, "HomeButton", UISheetHelper.HomeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        // Clothesline area (top portion)
        var lineGO = new GameObject("ClotheslineArea");
        lineGO.transform.SetParent(safeGO.transform, false);
        var lineRT = lineGO.AddComponent<RectTransform>();
        lineRT.anchorMin = new Vector2(0.05f, 0.65f);
        lineRT.anchorMax = new Vector2(0.95f, 0.88f);
        lineRT.offsetMin = Vector2.zero; lineRT.offsetMax = Vector2.zero;

        // Visual clothesline (thin horizontal bar)
        var ropeGO = new GameObject("Rope");
        ropeGO.transform.SetParent(lineGO.transform, false);
        var ropeRT = ropeGO.AddComponent<RectTransform>();
        ropeRT.anchorMin = new Vector2(0, 0.9f); ropeRT.anchorMax = new Vector2(1, 0.95f);
        ropeRT.offsetMin = Vector2.zero; ropeRT.offsetMax = Vector2.zero;
        ropeGO.AddComponent<Image>().color = LineColor;

        // Socks area (bottom portion)
        var socksGO = new GameObject("SocksArea");
        socksGO.transform.SetParent(safeGO.transform, false);
        var socksRT = socksGO.AddComponent<RectTransform>();
        socksRT.anchorMin = new Vector2(0.05f, 0.03f);
        socksRT.anchorMax = new Vector2(0.95f, 0.60f);
        socksRT.offsetMin = Vector2.zero; socksRT.offsetMax = Vector2.zero;

        // Load sprites
        var sockSprites = LoadAllSprites("Assets/Art/SocksSorting/Socks.png");
        var pinSprites = LoadAllSprites("Assets/Art/SocksSorting/Clothespins.png");

        // Controller
        var ctrl = canvasGO.AddComponent<SockMatchController>();
        ctrl.clotheslineArea = lineRT;
        ctrl.socksArea = socksRT;
        ctrl.sockSprites = sockSprites;
        ctrl.clothespinSprites = pinSprites;
        ctrl.circleSprite = circleSprite;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        var trophyIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Icons/trophy.png");
        if (trophyIcon != null)
        {
            var trophyGO = CreateIconButton(topBar.transform, "TrophyButton", trophyIcon,
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(-24, 0), new Vector2(80, 80));
            var lb = canvasGO.AddComponent<InGameLeaderboard>();
            lb.gameId = "sockmatch"; lb.trophyButton = trophyGO.GetComponent<Button>();
        }

        TutorialHandHelper.Create(safeGO.transform, TutorialHandHelper.Anim.Tap,
            Vector2.zero, new Vector2(400, 400), "sockmatch");

        EnsureFolder("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SockMatch.unity");
        var builds = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        string sp = "Assets/Scenes/SockMatch.unity";
        bool found = false;
        foreach (var s in builds) if (s.path == sp) { found = true; break; }
        if (!found) { builds.Add(new EditorBuildSettingsScene(sp, true)); EditorBuildSettings.scenes = builds.ToArray(); }
    }

    // ── Helpers ──
    private static Sprite[] LoadAllSprites(string path)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        var list = new List<Sprite>();
        foreach (var o in all) if (o is Sprite s) list.Add(s);
        list.Sort((a, b) => string.Compare(a.name, b.name));
        return list.ToArray();
    }
    private static GameObject Fill(Transform p, string n, Color c)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        Full(go.AddComponent<RectTransform>());
        var img = go.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
        return go;
    }
    private static void Full(RectTransform rt)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    private static GameObject CreateIconButton(Transform p, string n, Sprite icon,
        Vector2 amin, Vector2 amax, Vector2 piv, Vector2 pos, Vector2 sz)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax; rt.pivot = piv;
        rt.anchoredPosition = pos; rt.sizeDelta = sz;
        var img = go.AddComponent<Image>();
        img.sprite = icon; img.preserveAspect = true; img.color = Color.white; img.raycastTarget = true;
        go.AddComponent<Button>().targetGraphic = img;
        return go;
    }
    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(
                System.IO.Path.GetDirectoryName(path).Replace("\\", "/"),
                System.IO.Path.GetFileName(path));
    }
    private static Color HexColor(string hex) { ColorUtility.TryParseHtmlString(hex, out Color c); return c; }
}
