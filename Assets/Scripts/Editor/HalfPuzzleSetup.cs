using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the HalfPuzzle scene — cute toy kitchen theme.
/// Layered kitchen background: wall tiles, counter, shelf details.
/// All built procedurally from shapes — no image assets needed for background.
/// </summary>
public class HalfPuzzleSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly int HeaderHeight = SetupConstants.HeaderHeight;

    // Kitchen palette
    private static readonly Color WallColor       = HexColor("#E8E0D8"); // warm beige wall
    private static readonly Color WallTileColor    = HexColor("#F2ECE4"); // lighter tile
    private static readonly Color CounterColor     = HexColor("#D4C4AA"); // warm wood counter
    private static readonly Color CounterEdgeColor = HexColor("#B8A888"); // darker counter edge
    private static readonly Color ShelfColor       = HexColor("#C8B898"); // shelf wood
    private static readonly Color BacksplashColor  = HexColor("#B5D4E8"); // soft blue backsplash
    private static readonly Color HeaderColor      = new Color(0.55f, 0.75f, 0.55f, 0.88f); // soft green

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Half Puzzle Setup", "Building scene...", 0.5f);
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

        // ── Camera ──
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

        // ── EventSystem ──
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inputType = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputType != null) esGO.AddComponent(inputType);
        else esGO.AddComponent<StandaloneInputModule>();

        // ── Canvas ──
        var canvasGO = new GameObject("HalfPuzzleCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Kitchen Background ──
        CreateKitchenBackground(canvasGO.transform, roundedRect);

        // ── SafeArea ──
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        StretchFull(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // ── Header ──
        var topBar = CreateStretchImage(safeArea.transform, "TopBar", HeaderColor);
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.sizeDelta = new Vector2(0, HeaderHeight);
        topBar.GetComponent<Image>().raycastTarget = false;
        topBar.AddComponent<ThemeHeader>();

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        StretchFull(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D7\u05D1\u05E8\u05D5 \u05D0\u05EA \u05D4\u05D7\u05E6\u05D0\u05D9\u05DD"); // חברו את החצאים
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button
        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        // Trophy
        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = CreateIconButton(topBar.transform, "TrophyButton", trophyIcon,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-16, -20), new Vector2(70, 70));

        // ── Play area ──
        var playArea = new GameObject("PlayArea");
        playArea.transform.SetParent(safeArea.transform, false);
        var playAreaRT = playArea.AddComponent<RectTransform>();
        StretchFull(playAreaRT);
        playAreaRT.offsetMax = new Vector2(0, -HeaderHeight);
        playAreaRT.offsetMin = Vector2.zero;

        // ── Controller ──
        var controller = canvasGO.AddComponent<HalfPuzzleController>();
        controller.boardArea = playAreaRT;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnExitPressed);

        // Leaderboard
        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = roundedRect;
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "halfpuzzle";

        // Tutorial hand
        TutorialHandHelper.Create(safeArea.transform, TutorialHandHelper.Anim.Tap,
            new Vector2(0, -50), new Vector2(450, 450), "halfpuzzle");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/HalfPuzzle.unity");
        Debug.Log("[HalfPuzzleSetup] Scene created: Assets/Scenes/HalfPuzzle.unity");
    }

    // ══════════════════════════════════════════════
    //  KITCHEN BACKGROUND
    // ══════════════════════════════════════════════

    private static void CreateKitchenBackground(Transform parent, Sprite roundedRect)
    {
        // ── 1. Wall (full screen, warm beige) ──
        var wallGO = CreateStretchImage(parent, "Wall", WallColor);
        wallGO.GetComponent<Image>().raycastTarget = false;
        wallGO.transform.SetAsFirstSibling();

        // ── 2. Tile pattern on wall (subtle grid of lighter rectangles, top 65%) ──
        var tilesParent = new GameObject("WallTiles");
        tilesParent.transform.SetParent(wallGO.transform, false);
        var tilesRT = tilesParent.AddComponent<RectTransform>();
        tilesRT.anchorMin = new Vector2(0, 0.35f);
        tilesRT.anchorMax = Vector2.one;
        tilesRT.offsetMin = Vector2.zero;
        tilesRT.offsetMax = Vector2.zero;

        // Create subtle tile grid
        int tileCols = 12, tileRows = 4;
        for (int r = 0; r < tileRows; r++)
        {
            for (int c = 0; c < tileCols; c++)
            {
                var tileGO = new GameObject($"Tile_{r}_{c}");
                tileGO.transform.SetParent(tilesParent.transform, false);
                var tileRT = tileGO.AddComponent<RectTransform>();
                float xMin = (float)c / tileCols + 0.002f;
                float xMax = (float)(c + 1) / tileCols - 0.002f;
                float yMin = (float)r / tileRows + 0.005f;
                float yMax = (float)(r + 1) / tileRows - 0.005f;
                tileRT.anchorMin = new Vector2(xMin, yMin);
                tileRT.anchorMax = new Vector2(xMax, yMax);
                tileRT.offsetMin = Vector2.zero;
                tileRT.offsetMax = Vector2.zero;
                var tileImg = tileGO.AddComponent<Image>();
                if (roundedRect != null) { tileImg.sprite = roundedRect; tileImg.type = Image.Type.Sliced; }
                // Alternate tile colors slightly
                float shade = ((r + c) % 2 == 0) ? 0f : 0.015f;
                tileImg.color = new Color(
                    WallTileColor.r + shade,
                    WallTileColor.g + shade,
                    WallTileColor.b + shade, 0.6f);
                tileImg.raycastTarget = false;
            }
        }

        // ── 3. Backsplash strip (soft blue band between wall and counter) ──
        var splashGO = new GameObject("Backsplash");
        splashGO.transform.SetParent(wallGO.transform, false);
        var splashRT = splashGO.AddComponent<RectTransform>();
        splashRT.anchorMin = new Vector2(0, 0.30f);
        splashRT.anchorMax = new Vector2(1, 0.38f);
        splashRT.offsetMin = Vector2.zero;
        splashRT.offsetMax = Vector2.zero;
        var splashImg = splashGO.AddComponent<Image>();
        splashImg.color = BacksplashColor;
        splashImg.raycastTarget = false;

        // ── 4. Counter (bottom 30%, warm wood) ──
        var counterGO = new GameObject("Counter");
        counterGO.transform.SetParent(wallGO.transform, false);
        var counterRT = counterGO.AddComponent<RectTransform>();
        counterRT.anchorMin = Vector2.zero;
        counterRT.anchorMax = new Vector2(1, 0.30f);
        counterRT.offsetMin = Vector2.zero;
        counterRT.offsetMax = Vector2.zero;
        var counterImg = counterGO.AddComponent<Image>();
        counterImg.color = CounterColor;
        counterImg.raycastTarget = false;

        // Counter top edge (darker line)
        var edgeGO = new GameObject("CounterEdge");
        edgeGO.transform.SetParent(counterGO.transform, false);
        var edgeRT = edgeGO.AddComponent<RectTransform>();
        edgeRT.anchorMin = new Vector2(0, 1);
        edgeRT.anchorMax = new Vector2(1, 1);
        edgeRT.pivot = new Vector2(0.5f, 0.5f);
        edgeRT.sizeDelta = new Vector2(0, 6);
        var edgeImg = edgeGO.AddComponent<Image>();
        edgeImg.color = CounterEdgeColor;
        edgeImg.raycastTarget = false;

        // Counter wood grain lines (horizontal)
        for (int i = 0; i < 3; i++)
        {
            var grainGO = new GameObject($"Grain_{i}");
            grainGO.transform.SetParent(counterGO.transform, false);
            var grainRT = grainGO.AddComponent<RectTransform>();
            float y = 0.25f + i * 0.25f;
            grainRT.anchorMin = new Vector2(0.02f, y);
            grainRT.anchorMax = new Vector2(0.98f, y);
            grainRT.sizeDelta = new Vector2(0, 1.5f);
            var grainImg = grainGO.AddComponent<Image>();
            grainImg.color = new Color(CounterEdgeColor.r, CounterEdgeColor.g, CounterEdgeColor.b, 0.2f);
            grainImg.raycastTarget = false;
        }

        // ── 5. Upper shelf (thin shelf line at ~68% height) ──
        var shelfGO = new GameObject("UpperShelf");
        shelfGO.transform.SetParent(wallGO.transform, false);
        var shelfRT = shelfGO.AddComponent<RectTransform>();
        shelfRT.anchorMin = new Vector2(0.05f, 0.60f);
        shelfRT.anchorMax = new Vector2(0.95f, 0.60f);
        shelfRT.pivot = new Vector2(0.5f, 0.5f);
        shelfRT.sizeDelta = new Vector2(0, 8);
        var shelfImg = shelfGO.AddComponent<Image>();
        if (roundedRect != null) { shelfImg.sprite = roundedRect; shelfImg.type = Image.Type.Sliced; }
        shelfImg.color = ShelfColor;
        shelfImg.raycastTarget = false;

        // Shelf shadow (below)
        var shelfShadowGO = new GameObject("ShelfShadow");
        shelfShadowGO.transform.SetParent(wallGO.transform, false);
        var shelfShadowRT = shelfShadowGO.AddComponent<RectTransform>();
        shelfShadowRT.anchorMin = new Vector2(0.05f, 0.57f);
        shelfShadowRT.anchorMax = new Vector2(0.95f, 0.60f);
        shelfShadowRT.offsetMin = Vector2.zero;
        shelfShadowRT.offsetMax = Vector2.zero;
        var shelfShadowImg = shelfShadowGO.AddComponent<Image>();
        shelfShadowImg.color = new Color(0.3f, 0.25f, 0.2f, 0.08f);
        shelfShadowImg.raycastTarget = false;
    }

    // ── Helpers ──

    private static GameObject CreateStretchImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        StretchFull(go.AddComponent<RectTransform>());
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static GameObject CreateIconButton(Transform parent, string name, Sprite icon,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = icon; img.preserveAspect = true; img.color = Color.white; img.raycastTarget = true;
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
