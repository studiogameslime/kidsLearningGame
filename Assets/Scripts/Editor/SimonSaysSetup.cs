using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the SimonSays scene — landscape layout.
/// Wooden table background with a raised wooden toy board
/// holding 4 large colored buttons in a 2x2 grid.
/// </summary>
public class SimonSaysSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private const int TopBarHeight = 130;

    // Warm wood table palette (matching SharedSticker)
    private static readonly Color WoodDark      = HexColor("#8B6914");
    private static readonly Color WoodMid       = HexColor("#B08838");
    private static readonly Color WoodLight     = HexColor("#C9A44E");
    private static readonly Color WoodHighlight = HexColor("#D4B96A");

    // Board
    private static readonly Color BoardColor    = HexColor("#A67C52"); // lighter wood panel
    private static readonly Color BoardShadow   = new Color(0.2f, 0.15f, 0.08f, 0.35f);
    private static readonly Color BoardEdge     = HexColor("#8B6238"); // darker rim

    // Buttons
    private static readonly Color RedBtn        = new Color(0.90f, 0.22f, 0.21f);
    private static readonly Color YellowBtn     = new Color(0.98f, 0.80f, 0.18f);
    private static readonly Color BlueBtn       = new Color(0.25f, 0.47f, 0.85f);
    private static readonly Color GreenBtn      = new Color(0.30f, 0.69f, 0.31f);

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Simon Says", "Building scene...", 0.5f);
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

        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/CircleHiRes.png");
        if (circleSprite == null)
            circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");

        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");

        // ── Camera ──
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = WoodMid;
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
        scaler.matchWidthOrHeight = 1f;
        canvasGO.AddComponent<GraphicRaycaster>();
        var root = canvasGO.transform;

        // ═══════════════════════════════════════
        //  WOODEN TABLE BACKGROUND
        // ═══════════════════════════════════════

        Layer(root, "WoodBase", null, 0, 0, 1, 1, WoodMid);
        Layer(root, "WoodTop", null, 0, 0.88f, 1, 1, WoodDark);
        Layer(root, "WoodBottom", null, 0, 0, 1, 0.06f, WoodDark);

        // Wood grain lines
        for (int i = 0; i < 6; i++)
        {
            float y = 0.10f + i * 0.14f;
            Layer(root, $"Grain{i}", null, 0, y, 1, y + 0.008f,
                new Color(WoodLight.r, WoodLight.g, WoodLight.b, 0.18f));
        }

        // Subtle center highlight
        Layer(root, "WoodHighlight", null, 0, 0.30f, 1, 0.70f,
            new Color(WoodHighlight.r, WoodHighlight.g, WoodHighlight.b, 0.12f));

        // ═══════════════════════════════════════
        //  SAFE AREA + TOP BAR
        // ═══════════════════════════════════════

        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(root, false);
        var safeRT = safeGO.AddComponent<RectTransform>();
        Full(safeRT);
        safeGO.AddComponent<SafeAreaHandler>();

        var bar = CreateBar(safeGO.transform);

        // Title: זכרו את הצבעים
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(bar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var tmp = titleGO.AddComponent<TextMeshProUGUI>();
        tmp.text = HebrewFixer.Fix("\u05D6\u05DB\u05E8\u05D5 \u05D0\u05EA \u05D4\u05E6\u05D1\u05E2\u05D9\u05DD");
        tmp.isRightToLeftText = true;
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = Btn(bar.transform, "HomeButton", homeIcon, 16, -20, 90);

        // Trophy button (top-right)
        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = BtnRight(bar.transform, "TrophyButton", trophyIcon, -16, -20, 70);

        // ═══════════════════════════════════════
        //  PLAY AREA
        // ═══════════════════════════════════════

        var playGO = new GameObject("PlayArea");
        playGO.transform.SetParent(safeGO.transform, false);
        var playRT = playGO.AddComponent<RectTransform>();
        playRT.anchorMin = new Vector2(0, 0);
        playRT.anchorMax = new Vector2(1, 1);
        playRT.offsetMin = Vector2.zero;
        playRT.offsetMax = new Vector2(0, -TopBarHeight);

        // ═══════════════════════════════════════
        //  BOARD SHADOW (under the board)
        // ═══════════════════════════════════════

        var shadowGO = new GameObject("BoardShadow");
        shadowGO.transform.SetParent(playRT, false);
        var shadowRT = shadowGO.AddComponent<RectTransform>();
        shadowRT.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRT.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRT.sizeDelta = new Vector2(680, 680);
        shadowRT.anchoredPosition = new Vector2(4, -6);
        var shadowImg = shadowGO.AddComponent<Image>();
        if (roundedRect != null) shadowImg.sprite = roundedRect;
        shadowImg.type = roundedRect != null ? Image.Type.Sliced : Image.Type.Simple;
        shadowImg.color = BoardShadow;
        shadowImg.raycastTarget = false;

        // ═══════════════════════════════════════
        //  BOARD (raised wooden panel)
        // ═══════════════════════════════════════

        var boardGO = new GameObject("Board");
        boardGO.transform.SetParent(playRT, false);
        var boardRT = boardGO.AddComponent<RectTransform>();
        boardRT.anchorMin = new Vector2(0.5f, 0.5f);
        boardRT.anchorMax = new Vector2(0.5f, 0.5f);
        boardRT.sizeDelta = new Vector2(670, 670);
        boardRT.anchoredPosition = Vector2.zero;
        var boardImg = boardGO.AddComponent<Image>();
        if (roundedRect != null) boardImg.sprite = roundedRect;
        boardImg.type = roundedRect != null ? Image.Type.Sliced : Image.Type.Simple;
        boardImg.color = BoardColor;
        boardImg.raycastTarget = false;

        // Board edge/rim (slightly larger, behind)
        var edgeGO = new GameObject("BoardEdge");
        edgeGO.transform.SetParent(boardGO.transform, false);
        var edgeRT = edgeGO.AddComponent<RectTransform>();
        Full(edgeRT);
        edgeRT.offsetMin = new Vector2(-8, -8);
        edgeRT.offsetMax = new Vector2(8, 8);
        edgeGO.transform.SetAsFirstSibling();
        var edgeImg = edgeGO.AddComponent<Image>();
        if (roundedRect != null) edgeImg.sprite = roundedRect;
        edgeImg.type = roundedRect != null ? Image.Type.Sliced : Image.Type.Simple;
        edgeImg.color = BoardEdge;
        edgeImg.raycastTarget = false;

        // Board wood grain overlay
        var grainGO = new GameObject("BoardGrain");
        grainGO.transform.SetParent(boardGO.transform, false);
        var grainRT = grainGO.AddComponent<RectTransform>();
        Full(grainRT);
        var grainImg = grainGO.AddComponent<Image>();
        grainImg.color = new Color(WoodLight.r, WoodLight.g, WoodLight.b, 0.08f);
        grainImg.raycastTarget = false;

        // ═══════════════════════════════════════
        //  ROUND COUNTER (centered above board)
        // ═══════════════════════════════════════

        var roundGO = new GameObject("RoundText");
        roundGO.transform.SetParent(playRT, false);
        var roundRT = roundGO.AddComponent<RectTransform>();
        roundRT.anchorMin = new Vector2(0.5f, 0.5f);
        roundRT.anchorMax = new Vector2(0.5f, 0.5f);
        roundRT.sizeDelta = new Vector2(200, 60);
        roundRT.anchoredPosition = new Vector2(0, 380);
        var roundTMP = roundGO.AddComponent<TextMeshProUGUI>();
        roundTMP.text = "1";
        roundTMP.fontSize = 44;
        roundTMP.fontStyle = FontStyles.Bold;
        roundTMP.color = new Color(1f, 1f, 1f, 0.85f);
        roundTMP.alignment = TextAlignmentOptions.Center;
        roundTMP.raycastTarget = false;

        // ═══════════════════════════════════════
        //  COLOR BUTTONS (2x2 on the board)
        // ═══════════════════════════════════════

        Color[] btnColors = { RedBtn, YellowBtn, BlueBtn, GreenBtn };
        // Positions: Red=top-left, Yellow=top-right, Blue=bottom-left, Green=bottom-right
        Vector2[] positions =
        {
            new Vector2(-135, 135),   // Red (top-left)
            new Vector2(135, 135),    // Yellow (top-right)
            new Vector2(-135, -135),  // Blue (bottom-left)
            new Vector2(135, -135),   // Green (bottom-right)
        };

        float buttonSize = 240f;
        var buttons = new Image[4];
        var buttonComponents = new Button[4];
        var glows = new Image[4];

        for (int i = 0; i < 4; i++)
        {
            // Button container
            var btnGO = new GameObject($"ColorButton_{i}");
            btnGO.transform.SetParent(boardGO.transform, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.sizeDelta = new Vector2(buttonSize, buttonSize);
            btnRT.anchoredPosition = positions[i];

            // Button base (circle)
            var btnImg = btnGO.AddComponent<Image>();
            if (circleSprite != null) btnImg.sprite = circleSprite;
            btnImg.color = btnColors[i];
            btnImg.raycastTarget = true;
            buttons[i] = btnImg;

            // Glossy highlight (upper half, subtle)
            var glossGO = new GameObject("Gloss");
            glossGO.transform.SetParent(btnGO.transform, false);
            var glossRT = glossGO.AddComponent<RectTransform>();
            glossRT.anchorMin = new Vector2(0.15f, 0.45f);
            glossRT.anchorMax = new Vector2(0.85f, 0.90f);
            glossRT.offsetMin = Vector2.zero;
            glossRT.offsetMax = Vector2.zero;
            var glossImg = glossGO.AddComponent<Image>();
            if (circleSprite != null) glossImg.sprite = circleSprite;
            glossImg.color = new Color(1f, 1f, 1f, 0.22f);
            glossImg.raycastTarget = false;

            // Glow overlay (full circle, starts transparent)
            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(btnGO.transform, false);
            var glowRT = glowGO.AddComponent<RectTransform>();
            Full(glowRT);
            glowRT.offsetMin = new Vector2(-20, -20);
            glowRT.offsetMax = new Vector2(20, 20);
            var glowImg = glowGO.AddComponent<Image>();
            if (circleSprite != null) glowImg.sprite = circleSprite;
            glowImg.color = new Color(1f, 1f, 1f, 0f);
            glowImg.raycastTarget = false;
            glows[i] = glowImg;

            // Button component
            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            // No color transition — we handle glow manually
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f);
            btn.colors = colors;

            buttonComponents[i] = btn;
        }

        // ═══════════════════════════════════════
        //  CONTROLLER
        // ═══════════════════════════════════════

        var ctrl = canvasGO.AddComponent<SimonGameController>();
        ctrl.boardRT = boardRT;
        ctrl.boardImage = boardImg;
        ctrl.playArea = playRT;
        ctrl.circleSprite = circleSprite;
        ctrl.roundText = roundTMP;
        ctrl.colorButtons = buttons;
        ctrl.colorButtonComponents = buttonComponents;
        ctrl.glowOverlays = glows;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        // Wire trophy / leaderboard
        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = LoadSprite("Assets/UI/Sprites/RoundedRect.png");
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "simonsays";

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SimonSays.unity");
    }

    // ── HELPERS ──────────────────────────────────────────────────

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
        img.type = Image.Type.Simple;
        img.color = c;
        img.raycastTarget = false;
        return go;
    }

    private static GameObject CreateBar(Transform parent)
    {
        var bar = new GameObject("TopBar");
        bar.transform.SetParent(parent, false);
        var barRT = bar.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1);
        barRT.anchorMax = new Vector2(1, 1);
        barRT.pivot = new Vector2(0.5f, 1);
        barRT.sizeDelta = new Vector2(0, TopBarHeight);
        var barImg = bar.AddComponent<Image>();
        barImg.color = HexColor("#5D4037");
        barImg.raycastTarget = false;
        bar.AddComponent<ThemeHeader>();
        return bar;
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
        if (all != null)
            foreach (var o in all)
                if (o is Sprite sp) return sp;
        return null;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
