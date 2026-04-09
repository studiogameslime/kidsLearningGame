using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the FruitPuzzle scene — wood table background, fruit puzzle game.
/// </summary>
public class FruitPuzzleSetup
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    private static readonly Color TableBaseColor = HexColor("#5C3D2E");
    private static readonly Color BoardWoodA    = HexColor("#8B6B4A");
    private static readonly Color BoardWoodB    = HexColor("#7E6042");
    private static readonly Color BoardEdgeColor= HexColor("#6B4D38");
    private static readonly Color BoardInnerRim = HexColor("#A08060");
    private static readonly Color HeaderColor   = new Color(0.35f, 0.22f, 0.12f, 0.75f);
    private static readonly int TopBarHeight     = SetupConstants.HeaderHeight;

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Fruit Puzzle Setup", "Copying assets…", 0.3f);
            CopyAssets();
            EditorUtility.DisplayProgressBar("Fruit Puzzle Setup", "Building scene…", 0.6f);
            CreateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void CopyAssets()
    {
        EnsureFolder("Assets/Resources/VehiclePuzzle");
        Copy("Assets/Art/Cars/cars.png", "Assets/Resources/VehiclePuzzle/cars.png");
        CopyMeta("Assets/Art/Cars/cars.png.meta", "Assets/Resources/VehiclePuzzle/cars.png.meta");
    }

    private static void CreateScene()
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = TableBaseColor;
        cam.orthographic = true;
        var urpType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpType != null) camGO.AddComponent(urpType);

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inputType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputType != null) esGO.AddComponent(inputType);
        else esGO.AddComponent<StandaloneInputModule>();

        var canvasGO = new GameObject("FruitPuzzleCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");

        // ═══ WOOD TABLE BACKGROUND ═══

        var bgGO = StretchImg(canvasGO.transform, "Background", TableBaseColor);
        bgGO.GetComponent<Image>().raycastTarget = false;

        var boardGO = new GameObject("BoardPanel");
        boardGO.transform.SetParent(canvasGO.transform, false);
        var boardRT = boardGO.AddComponent<RectTransform>();
        boardRT.anchorMin = new Vector2(0.005f, 0.01f);
        boardRT.anchorMax = new Vector2(0.995f, 0.90f);
        boardRT.offsetMin = Vector2.zero;
        boardRT.offsetMax = Vector2.zero;
        var boardImg = boardGO.AddComponent<Image>();
        if (roundedRect != null) { boardImg.sprite = roundedRect; boardImg.type = Image.Type.Sliced; }
        boardImg.color = BoardEdgeColor;
        boardImg.raycastTarget = false;
        boardGO.AddComponent<Shadow>().effectColor = new Color(0.12f, 0.06f, 0.02f, 0.5f);

        var rimGO = new GameObject("InnerRim");
        rimGO.transform.SetParent(boardGO.transform, false);
        var rimRT = rimGO.AddComponent<RectTransform>();
        Full(rimRT);
        rimRT.offsetMin = new Vector2(2, 2);
        rimRT.offsetMax = new Vector2(-2, -2);
        var rimImg = rimGO.AddComponent<Image>();
        if (roundedRect != null) { rimImg.sprite = roundedRect; rimImg.type = Image.Type.Sliced; }
        rimImg.color = BoardInnerRim;
        rimImg.raycastTarget = false;

        var woodGO = new GameObject("WoodSurface");
        woodGO.transform.SetParent(boardGO.transform, false);
        var woodRT = woodGO.AddComponent<RectTransform>();
        Full(woodRT);
        woodRT.offsetMin = new Vector2(3, 3);
        woodRT.offsetMax = new Vector2(-3, -3);
        CreateWoodPlanks(woodGO.transform);

        // ═══ GAME AREAS ═══

        // Silhouette area (lower half of board)
        var silhouetteAreaGO = new GameObject("SilhouetteArea");
        silhouetteAreaGO.transform.SetParent(canvasGO.transform, false);
        var silhouetteAreaRT = silhouetteAreaGO.AddComponent<RectTransform>();
        silhouetteAreaRT.anchorMin = new Vector2(0.03f, 0.03f);
        silhouetteAreaRT.anchorMax = new Vector2(0.97f, 0.42f);
        silhouetteAreaRT.offsetMin = Vector2.zero;
        silhouetteAreaRT.offsetMax = Vector2.zero;

        // Pieces area (upper half of board)
        var piecesAreaGO = new GameObject("PiecesArea");
        piecesAreaGO.transform.SetParent(canvasGO.transform, false);
        var piecesAreaRT = piecesAreaGO.AddComponent<RectTransform>();
        piecesAreaRT.anchorMin = new Vector2(0.03f, 0.44f);
        piecesAreaRT.anchorMax = new Vector2(0.97f, 0.88f);
        piecesAreaRT.offsetMin = Vector2.zero;
        piecesAreaRT.offsetMax = Vector2.zero;

        // ═══ SAFE AREA + UI ═══

        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        Full(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        var topBar = StretchImg(safeArea.transform, "TopBar", HeaderColor);
        var topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.sizeDelta = new Vector2(0, TopBarHeight);
        topBar.GetComponent<Image>().raycastTarget = false;
        topBar.AddComponent<ThemeHeader>();

        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(topBar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05E4\u05D0\u05D6\u05DC \u05E8\u05DB\u05D1\u05D9\u05DD"); // פאזל רכבים
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(1f, 0.96f, 0.88f);
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = IconBtn(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = IconBtn(topBar.transform, "TrophyButton", trophyIcon,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-16, 0), new Vector2(70, 70));

        // ═══ CONTROLLER ═══

        var controller = canvasGO.AddComponent<FruitPuzzleController>();
        controller.silhouetteArea = silhouetteAreaRT;
        controller.piecesArea = piecesAreaRT;
        controller.titleText = titleTMP;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = LoadSprite("Assets/UI/Sprites/RoundedRect.png");
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "vehiclepuzzle";

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/FruitPuzzle.unity");
        Debug.Log("[FruitPuzzleSetup] Scene created: Assets/Scenes/FruitPuzzle.unity");
    }

    // ═══ HELPERS ═══

    private static void CreateWoodPlanks(Transform parent)
    {
        int plankCount = 8;
        for (int i = 0; i < plankCount; i++)
        {
            float yMin = (float)i / plankCount;
            float yMax = (float)(i + 1) / plankCount;
            var plankGO = new GameObject($"Plank_{i}");
            plankGO.transform.SetParent(parent, false);
            var prt = plankGO.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0, yMin);
            prt.anchorMax = new Vector2(1, yMax);
            prt.offsetMin = new Vector2(0, i > 0 ? 1 : 0);
            prt.offsetMax = Vector2.zero;
            var pimg = plankGO.AddComponent<Image>();
            pimg.color = (i % 2 == 0) ? BoardWoodA : BoardWoodB;
            pimg.raycastTarget = false;
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void Copy(string src, string dst)
    {
        if (!System.IO.File.Exists(src)) return;
        if (System.IO.File.Exists(dst)) System.IO.File.Delete(dst);
        System.IO.File.Copy(src, dst, true);
        AssetDatabase.ImportAsset(dst);
    }

    private static void CopyMeta(string src, string dst)
    {
        if (!System.IO.File.Exists(src)) return;
        if (System.IO.File.Exists(dst)) System.IO.File.Delete(dst);
        System.IO.File.Copy(src, dst, true);
    }

    private static GameObject StretchImg(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Full(go.AddComponent<RectTransform>());
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static void Full(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject IconBtn(Transform parent, string name, Sprite icon,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        if (icon != null) img.sprite = icon;
        img.preserveAspect = true;
        img.color = Color.white;
        go.AddComponent<Button>();
        return go;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
