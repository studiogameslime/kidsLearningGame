using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the LettersGame scene — landscape split layout.
/// Left half: answer buttons in bordered container.
/// Right half: animal image on top, word tiles below.
/// </summary>
public class LetterGameSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly int TopBarHeight = SetupConstants.HeaderHeight;

    private static readonly Color BgColor  = WoodTableBackground.TableBaseColor;
    private static readonly Color BarColor = WoodTableBackground.HeaderColor;
    private static readonly Color ContainerBg    = HexColor("#FEFCF9");
    private static readonly Color ContainerBorder = HexColor("#D7CFC7");
    private static readonly Color LabelColor     = HexColor("#5D4037");
    private static readonly Color BtnBg          = HexColor("#E3F2FD");
    private static readonly Color BtnBorder      = HexColor("#90CAF9");

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Letter Game", "Building scene...", 0.5f);
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
        cam.backgroundColor = BgColor;
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

        // ── Wood Background ──
        WoodTableBackground.CreateBackground(root);

        // ── Safe Area ──
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(root, false);
        var safeRT = safeGO.AddComponent<RectTransform>();
        Full(safeRT);
        safeGO.AddComponent<SafeAreaHandler>();

        // ══════════════════════════════════════════
        //  TOP BAR
        // ══════════════════════════════════════════
        var bar = CreateBar(safeGO.transform);

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(bar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(110, 0);
        titleRT.offsetMax = new Vector2(-110, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D4\u05D0\u05D5\u05EA \u05D4\u05D7\u05E1\u05E8\u05D4"); // האות החסרה
        titleTMP.fontSize = 34;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = WoodTableBackground.TitleTextColor;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        // Home button
        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = Btn(bar.transform, "HomeButton", homeIcon, 16, -12, 76);

        // Trophy button
        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = BtnRight(bar.transform, "TrophyButton", trophyIcon, -16, -12, 62);

        // ══════════════════════════════════════════
        //  PLAY AREA (below top bar)
        // ══════════════════════════════════════════

        var boardContent = WoodTableBackground.CreateBoardPanel(safeGO.transform, roundedRect,
            0.01f, 0.01f, 0.99f, 1f - (float)TopBarHeight / Ref.y - 0.01f);

        var playGO = new GameObject("PlayArea");
        playGO.transform.SetParent(boardContent, false);
        var playRT = playGO.AddComponent<RectTransform>();
        playRT.anchorMin = new Vector2(0, 0);
        playRT.anchorMax = new Vector2(1, 1);
        playRT.offsetMin = Vector2.zero;
        playRT.offsetMax = Vector2.zero;

        // (score display removed)

        // ══════════════════════════════════════════
        //  LEFT HALF — Answer buttons container (x: 0.01–0.48)
        // ══════════════════════════════════════════

        // Outer container with border
        var answerContainerGO = new GameObject("AnswerContainer");
        answerContainerGO.transform.SetParent(playRT, false);
        var acRT = answerContainerGO.AddComponent<RectTransform>();
        acRT.anchorMin = new Vector2(0.01f, 0.04f);
        acRT.anchorMax = new Vector2(0.48f, 0.96f);
        acRT.offsetMin = Vector2.zero;
        acRT.offsetMax = Vector2.zero;

        // Container border (behind)
        var acBorderGO = new GameObject("ContainerBorder");
        acBorderGO.transform.SetParent(answerContainerGO.transform, false);
        var acbRT = acBorderGO.AddComponent<RectTransform>();
        Full(acbRT);
        acbRT.offsetMin = new Vector2(-4, -4);
        acbRT.offsetMax = new Vector2(4, 4);
        acBorderGO.transform.SetAsFirstSibling();
        var acbImg = acBorderGO.AddComponent<Image>();
        if (roundedRect != null) { acbImg.sprite = roundedRect; acbImg.type = Image.Type.Sliced; }
        acbImg.color = ContainerBorder;
        acbImg.raycastTarget = false;

        // Container background
        var acBgImg = answerContainerGO.AddComponent<Image>();
        if (roundedRect != null) { acBgImg.sprite = roundedRect; acBgImg.type = Image.Type.Sliced; }
        acBgImg.color = ContainerBg;
        acBgImg.raycastTarget = false;

        // Label: "בחרו את האות הראשונה" (inside container, top)
        var labelGO = new GameObject("ChooseLabel");
        labelGO.transform.SetParent(answerContainerGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.05f, 0.72f);
        labelRT.anchorMax = new Vector2(0.95f, 0.90f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(labelTMP, "\u05D1\u05D7\u05E8\u05D5 \u05D0\u05EA \u05D4\u05D0\u05D5\u05EA \u05D4\u05E8\u05D0\u05E9\u05D5\u05E0\u05D4"); // בחרו את האות הראשונה
        labelTMP.fontSize = 26;
        labelTMP.color = LabelColor;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.raycastTarget = false;

        // Button area (inside container, centered below label)
        var btnAreaGO = new GameObject("ButtonArea");
        btnAreaGO.transform.SetParent(answerContainerGO.transform, false);
        var btnAreaRT = btnAreaGO.AddComponent<RectTransform>();
        btnAreaRT.anchorMin = new Vector2(0.05f, 0.10f);
        btnAreaRT.anchorMax = new Vector2(0.95f, 0.68f);
        btnAreaRT.offsetMin = Vector2.zero;
        btnAreaRT.offsetMax = Vector2.zero;

        // ══════════════════════════════════════════
        //  RIGHT HALF — Image top + word tiles bottom (x: 0.50–0.98)
        // ══════════════════════════════════════════

        // ── Animal/Color Image (no card bg, just the image, bigger) ──
        var animalGO = new GameObject("AnimalImage");
        animalGO.transform.SetParent(playRT, false);
        var animalRT = animalGO.AddComponent<RectTransform>();
        animalRT.anchorMin = new Vector2(0.52f, 0.32f);
        animalRT.anchorMax = new Vector2(0.99f, 0.98f);
        animalRT.offsetMin = Vector2.zero;
        animalRT.offsetMax = Vector2.zero;
        var animalImg = animalGO.AddComponent<Image>();
        animalImg.preserveAspect = true;
        animalImg.raycastTarget = false;
        animalImg.color = Color.white;

        // Image area reference (for controller)
        var imgAreaGO = new GameObject("ImageArea");
        imgAreaGO.transform.SetParent(playRT, false);
        var imgAreaRT = imgAreaGO.AddComponent<RectTransform>();
        imgAreaRT.anchorMin = new Vector2(0.52f, 0.32f);
        imgAreaRT.anchorMax = new Vector2(0.99f, 0.98f);
        imgAreaRT.offsetMin = Vector2.zero;
        imgAreaRT.offsetMax = Vector2.zero;

        // ── Word tiles (below the image, right half, bigger) ──
        var tileAreaGO = new GameObject("TileArea");
        tileAreaGO.transform.SetParent(playRT, false);
        var tileAreaRT = tileAreaGO.AddComponent<RectTransform>();
        tileAreaRT.anchorMin = new Vector2(0.50f, 0.04f);
        tileAreaRT.anchorMax = new Vector2(0.99f, 0.30f);
        tileAreaRT.offsetMin = Vector2.zero;
        tileAreaRT.offsetMax = Vector2.zero;

        // ══════════════════════════════════════════
        //  CONTROLLER
        // ══════════════════════════════════════════
        var ctrl = canvasGO.AddComponent<LetterGameController>();
        ctrl.imageArea = imgAreaRT;
        ctrl.tileArea = tileAreaRT;
        ctrl.buttonArea = btnAreaRT;
        ctrl.animalImage = animalImg;
        ctrl.titleText = titleTMP;
        ctrl.scoreText = null;
        ctrl.replayButton = null;
        ctrl.cellSprite = roundedRect;
        ctrl.circleSprite = circleSprite;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        // Wire trophy / leaderboard
        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = roundedRect;
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "letters";

        // Tutorial hand
        TutorialHandHelper.Create(safeGO.transform, TutorialHandHelper.Anim.Tap,
            new Vector2(0, 0), new Vector2(450, 450), "letters");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/LettersGame.unity");
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
        barImg.color = BarColor;
        barImg.raycastTarget = false;
        bar.AddComponent<ThemeHeader>();
        return bar;
    }

    private static GameObject Btn(Transform p, string name, Sprite icon, float x, float y, float sz)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
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
        go.AddComponent<Button>().targetGraphic = img;
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
