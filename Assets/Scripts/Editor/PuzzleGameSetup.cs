using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Builds the PuzzleGame scene and updates Puzzle.asset with animal sub-items.
///
/// Layout (portrait):
///   Top bar (80px, matches Shadow Match / World)
///   Right side — 3x3 puzzle board with faded preview image
///   Left side  — scattered puzzle pieces
///
/// Background: warm desert/sand theme (distinct from other mini-games)
/// </summary>
public class PuzzleGameSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1080, 1920);

    // Desert palette — warm pastel tones
    private static readonly Color SkyColor       = HexColor("#E8D8C8");  // warm sandy sky
    private static readonly Color SkyTopColor    = HexColor("#D4C4B0");  // slightly darker sky top
    private static readonly Color HillFarColor   = HexColor("#D5C0A8");  // distant sandy hills
    private static readonly Color HillNearColor  = HexColor("#CCAB8A");  // nearer warm hills
    private static readonly Color GroundColor    = HexColor("#E0C9A0");  // soft sand floor
    private static readonly Color TopBarColor    = HexColor("#C4A882");  // warm brown header

    private const int TopBarHeight = 130;

    // Animal colors for cards
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
            EditorUtility.DisplayProgressBar("Puzzle Game Setup", "Updating puzzle data...", 0.2f);
            UpdatePuzzleData();

            EditorUtility.DisplayProgressBar("Puzzle Game Setup", "Building scene...", 0.5f);
            CreatePuzzleScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ─────────────────────────────────────────
    //  UPDATE PUZZLE DATA
    // ─────────────────────────────────────────

    private static void UpdatePuzzleData()
    {
        var puzzle = AssetDatabase.LoadAssetAtPath<GameItemData>("Assets/Data/Games/Puzzle.asset");
        if (puzzle == null)
        {
            Debug.LogError("Puzzle.asset not found. Run Setup Project first.");
            return;
        }

        puzzle.hasSubItems = true;
        puzzle.selectionScreenTitle = "\u05D1\u05D7\u05E8\u05D5 \u05E4\u05D0\u05D6\u05DC"; // בחרו פאזל
        puzzle.targetSceneName = "PuzzleGame";

        string[] animals = {
            "Bear", "Bird", "Cat", "Chicken", "Cow", "Dog", "Donkey", "Duck",
            "Elephant", "Fish", "Frog", "Giraffe", "Horse", "Lion", "Monkey",
            "Sheep", "Snake", "Turtle", "Zebra"
        };

        if (puzzle.subItems == null)
            puzzle.subItems = new List<SubItemData>();

        puzzle.subItems.Clear();

        // Gallery import option — first item, outlined style
        puzzle.subItems.Add(new SubItemData
        {
            id = "puzzle_gallery",
            title = "\u05DE\u05D4\u05D2\u05DC\u05E8\u05D9\u05D4", // מהגלריה
            cardColor = new Color(0.85f, 0.75f, 0.95f),
            categoryKey = "gallery",
            targetSceneName = "PuzzleGame",
            contentAsset = null,
            thumbnail = null
        });

        for (int i = 0; i < animals.Length; i++)
        {
            string name = animals[i];
            string mainPath = $"Assets/Art/Animals/{name}/Art/Puzzle/{name} Main.png";
            var mainSprite = LoadSprite(mainPath);

            Sprite thumbSprite = null;
            string[] thumbPaths = {
                $"Assets/Art/Animals/{name}/Art/{name}Sprite.png",
                $"Assets/Art/Animals/{name}/Art/{name}.png"
            };
            foreach (var tp in thumbPaths)
            {
                thumbSprite = LoadSprite(tp);
                if (thumbSprite != null) break;
            }

            if (mainSprite == null)
            {
                Debug.LogWarning($"Puzzle main image not found: {mainPath}");
                continue;
            }

            puzzle.subItems.Add(new SubItemData
            {
                id = $"puzzle_{name.ToLower()}",
                title = name,
                cardColor = AnimalColors[i % AnimalColors.Length],
                categoryKey = name.ToLower(),
                targetSceneName = "PuzzleGame",
                contentAsset = mainSprite,
                thumbnail = thumbSprite != null ? thumbSprite : mainSprite
            });
        }

        EditorUtility.SetDirty(puzzle);
        Debug.Log($"Puzzle data updated with {puzzle.subItems.Count} animals.");
    }

    // ─────────────────────────────────────────
    //  SCENE
    // ─────────────────────────────────────────

    private static void CreatePuzzleScene()
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string artPath = "Assets/Art/World";

        var hillsSpr = LoadSprite($"{artPath}/hillsLarge.png");
        var grassSpr = LoadSprite($"{artPath}/groundLayer1.png");

        // ── Camera ──
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SkyColor;
        cam.orthographic = true;
        var urpType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpType != null) camGO.AddComponent(urpType);

        // ── EventSystem ──
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inputType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputType != null) esGO.AddComponent(inputType);
        else esGO.AddComponent<StandaloneInputModule>();

        // ── Canvas ──
        var canvasGO = new GameObject("PuzzleCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0f;
        canvasGO.AddComponent<GraphicRaycaster>();
        var root = canvasGO.transform;

        // ═══════════════════════════════════
        //  BACKGROUND — warm desert/sand theme
        //
        //  Top ~55%  = warm sandy sky
        //  Middle    = sandy hills
        //  Bottom ~40% = sand floor
        // ═══════════════════════════════════

        // 1. SKY — warm sandy gradient base
        Layer(root, "Sky", null, 0, 0, 1, 1, SkyColor);

        // 2. SKY TOP — slightly darker warm tone at top
        Layer(root, "SkyTop", null, 0, 0.75f, 1, 1, SkyTopColor);

        // 3. DISTANT HILLS — sandy dunes
        Layer(root, "HillsFar", hillsSpr, 0, 0.38f, 1, 0.55f, HillFarColor);

        // 4. NEAR HILLS — warmer brown dunes
        Layer(root, "HillsNear", hillsSpr, 0, 0.33f, 1, 0.48f, HillNearColor);

        // 5. GROUND — soft sand floor (bottom 40%)
        Layer(root, "Ground", grassSpr, 0, 0, 1, 0.40f, GroundColor);

        // ═══════════════════════════════════
        //  SAFE AREA + TOP BAR
        // ═══════════════════════════════════

        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(root, false);
        var safeRT = safeGO.AddComponent<RectTransform>();
        Full(safeRT);
        safeGO.AddComponent<SafeAreaHandler>();

        // Top bar — matches Shadow Match / World header
        var bar = FillGO(safeGO.transform, "TopBar", TopBarColor);
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
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = HebrewFixer.Fix("\u05E4\u05D0\u05D6\u05DC"); // פאזל
        titleTMP.isRightToLeftText = false;
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button — matches Shadow Match exactly
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = Btn(bar.transform, "HomeButton", homeIcon, 16, -20, 90);

        // Trophy button (top-right)
        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = BtnRight(bar.transform, "TrophyButton", trophyIcon, -16, -20, 70);

        // ═══════════════════════════════════
        //  GAMEPLAY ZONES (portrait — left/right split)
        //
        //  Left side:  scattered puzzle pieces
        //    anchors: x 0.02–0.48, y 0.03–0.92 (below bar)
        //
        //  Right side: puzzle board with faded reference
        //    anchors: x 0.52–0.98, y 0.03–0.92
        //
        //  Vertical divider gap (0.48–0.52)
        // ═══════════════════════════════════

        // Pieces area (left side)
        var piecesGO = new GameObject("PiecesArea");
        piecesGO.transform.SetParent(safeGO.transform, false);
        var piecesRT = piecesGO.AddComponent<RectTransform>();
        piecesRT.anchorMin = new Vector2(0.02f, 0.03f);
        piecesRT.anchorMax = new Vector2(0.48f, 0.92f);
        piecesRT.offsetMin = Vector2.zero;
        piecesRT.offsetMax = Vector2.zero;

        // Board area (right side)
        var boardGO = new GameObject("BoardArea");
        boardGO.transform.SetParent(safeGO.transform, false);
        var boardRT = boardGO.AddComponent<RectTransform>();
        boardRT.anchorMin = new Vector2(0.52f, 0.03f);
        boardRT.anchorMax = new Vector2(0.98f, 0.92f);
        boardRT.offsetMin = Vector2.zero;
        boardRT.offsetMax = Vector2.zero;

        // Reference image (centered in board area, resized by controller)
        var refGO = new GameObject("ReferenceImage");
        refGO.transform.SetParent(boardGO.transform, false);
        var refRT = refGO.AddComponent<RectTransform>();
        refRT.anchorMin = new Vector2(0.5f, 0.5f);
        refRT.anchorMax = new Vector2(0.5f, 0.5f);
        refRT.sizeDelta = new Vector2(400, 400); // resized by controller at runtime
        var refRaw = refGO.AddComponent<RawImage>();
        refRaw.color = new Color(1f, 1f, 1f, 0.25f);
        refRaw.raycastTarget = false;

        // ═══════════════════════════════════
        //  CONTROLLER
        // ═══════════════════════════════════

        var controller = canvasGO.AddComponent<PuzzleGameController>();
        controller.boardArea = boardRT;
        controller.piecesArea = piecesRT;
        controller.referenceImage = refRaw;
        controller.referenceAlpha = 0.25f;

        // Wire home button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        // Wire trophy / leaderboard
        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = LoadSprite("Assets/UI/Sprites/RoundedRect.png");
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "puzzle";

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/PuzzleGame.unity");
    }

    // ─────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────

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

    private static GameObject FillGO(Transform p, string name, Color c)
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

    private static GameObject BtnRight(Transform p, string name, Sprite icon, float x, float y, float sz)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
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
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;

        var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (allAssets != null)
        {
            foreach (var asset in allAssets)
                if (asset is Sprite s) return s;
        }
        return null;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
