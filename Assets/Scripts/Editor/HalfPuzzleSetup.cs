using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the HalfPuzzle scene — wooden board theme (like Memory Game).
/// Uses fruit sprites from Resources/Tractor/Fruits.png sprite sheet.
/// </summary>
public class HalfPuzzleSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    // Warm wood palette (matching Memory Game)
    private static readonly Color TableBaseColor    = HexColor("#5C3D2E");
    private static readonly Color BoardWoodA        = HexColor("#8B6B4A");
    private static readonly Color BoardWoodB        = HexColor("#7E6042");
    private static readonly Color PlankSepColor     = HexColor("#5A4030");
    private static readonly Color BoardEdgeColor    = HexColor("#6B4D38");
    private static readonly Color BoardInnerRimColor = HexColor("#A08060");
    private static readonly Color HeaderColor       = new Color(0.30f, 0.20f, 0.12f, 0.75f);

    private static readonly int HeaderHeight = SetupConstants.HeaderHeight;

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
        cam.backgroundColor = TableBaseColor;
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

        // ── Background (dark warm brown table) ──
        var bgGO = CreateStretchImage(canvasGO.transform, "Background", TableBaseColor);
        bgGO.GetComponent<Image>().raycastTarget = false;

        // Warm vignette
        CreateWarmVignette(canvasGO.transform, "VignetteTop", true);
        CreateWarmVignette(canvasGO.transform, "VignetteBottom", false);

        // ── SafeArea ──
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        StretchFull(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // ── Header (warm brown) ──
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
        titleTMP.color = new Color(1f, 0.96f, 0.88f, 1f); // warm white
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button
        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        // Trophy button
        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = CreateIconButton(topBar.transform, "TrophyButton", trophyIcon,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-16, -20), new Vector2(70, 70));

        // ── Board (wood plank surface) ──
        var boardGO = new GameObject("BoardPanel");
        boardGO.transform.SetParent(safeArea.transform, false);
        var boardRT = boardGO.AddComponent<RectTransform>();
        boardRT.anchorMin = new Vector2(0.005f, 0.01f);
        boardRT.anchorMax = new Vector2(0.995f, 0.90f);
        boardRT.offsetMin = Vector2.zero;
        boardRT.offsetMax = Vector2.zero;

        var boardImg = boardGO.AddComponent<Image>();
        if (roundedRect != null) { boardImg.sprite = roundedRect; boardImg.type = Image.Type.Sliced; }
        boardImg.color = BoardEdgeColor;
        boardImg.raycastTarget = false;

        var boardShadow = boardGO.AddComponent<Shadow>();
        boardShadow.effectColor = new Color(0.12f, 0.06f, 0.02f, 0.5f);
        boardShadow.effectDistance = new Vector2(5, -5);

        // Inner rim
        var rimGO = new GameObject("InnerRim");
        rimGO.transform.SetParent(boardGO.transform, false);
        var rimRT = rimGO.AddComponent<RectTransform>();
        StretchFull(rimRT);
        rimRT.offsetMin = new Vector2(2, 2);
        rimRT.offsetMax = new Vector2(-2, -2);
        var rimImg = rimGO.AddComponent<Image>();
        if (roundedRect != null) { rimImg.sprite = roundedRect; rimImg.type = Image.Type.Sliced; }
        rimImg.color = BoardInnerRimColor;
        rimImg.raycastTarget = false;

        // Wood planks
        var woodSurface = new GameObject("WoodSurface");
        woodSurface.transform.SetParent(boardGO.transform, false);
        var woodSurfaceRT = woodSurface.AddComponent<RectTransform>();
        StretchFull(woodSurfaceRT);
        woodSurfaceRT.offsetMin = new Vector2(3, 3);
        woodSurfaceRT.offsetMax = new Vector2(-3, -3);
        CreateWoodPlanks(woodSurface.transform);

        // Play area (on top of wood — controller builds pieces here)
        var playArea = new GameObject("PlayArea");
        playArea.transform.SetParent(boardGO.transform, false);
        var playAreaRT = playArea.AddComponent<RectTransform>();
        StretchFull(playAreaRT);
        playAreaRT.offsetMin = new Vector2(4, 4);
        playAreaRT.offsetMax = new Vector2(-4, -4);

        // ── Controller ──
        var controller = canvasGO.AddComponent<HalfPuzzleController>();
        controller.boardArea = playAreaRT;

        // Wire home button
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

    // ── Wood planks ──

    private static void CreateWoodPlanks(Transform parent)
    {
        int plankCount = 6;
        Color[] plankColors = {
            BoardWoodA, BoardWoodB,
            LerpColor(BoardWoodA, BoardWoodB, 0.3f),
            BoardWoodA,
            LerpColor(BoardWoodB, BoardWoodA, 0.4f),
            BoardWoodB
        };

        for (int i = 0; i < plankCount; i++)
        {
            float yMin = (float)i / plankCount;
            float yMax = (float)(i + 1) / plankCount;

            var plankGO = new GameObject($"Plank_{i}");
            plankGO.transform.SetParent(parent, false);
            var plankRT = plankGO.AddComponent<RectTransform>();
            plankRT.anchorMin = new Vector2(0, yMin);
            plankRT.anchorMax = new Vector2(1, yMax);
            plankRT.offsetMin = Vector2.zero;
            plankRT.offsetMax = Vector2.zero;
            var plankImg = plankGO.AddComponent<Image>();
            plankImg.color = plankColors[i % plankColors.Length];
            plankImg.raycastTarget = false;

            // Separator line
            if (i < plankCount - 1)
            {
                var sep = new GameObject($"Sep_{i}");
                sep.transform.SetParent(parent, false);
                var sepRT = sep.AddComponent<RectTransform>();
                sepRT.anchorMin = new Vector2(0, yMax);
                sepRT.anchorMax = new Vector2(1, yMax);
                sepRT.pivot = new Vector2(0.5f, 0.5f);
                sepRT.sizeDelta = new Vector2(0, 2);
                var sepImg = sep.AddComponent<Image>();
                sepImg.color = PlankSepColor;
                sepImg.raycastTarget = false;
            }
        }
    }

    // ── Vignette ──

    private static void CreateWarmVignette(Transform parent, string name, bool isTop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        if (isTop)
        {
            rt.anchorMin = new Vector2(0, 0.8f);
            rt.anchorMax = Vector2.one;
        }
        else
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1, 0.2f);
        }
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.08f, 0.03f, isTop ? 0.3f : 0.4f);
        img.raycastTarget = false;
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

    private static Color LerpColor(Color a, Color b, float t) => Color.Lerp(a, b, t);

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
