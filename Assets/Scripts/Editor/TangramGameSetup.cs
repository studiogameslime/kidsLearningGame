using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the TangramGame scene with warm wooden table background.
/// Run via Tools menu or ProjectSetup.
/// </summary>
public class TangramGameSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly Color TableBase = HexColor("#5C3D2E");
    private static readonly Color TopBarColor = new Color(0.30f, 0.20f, 0.12f, 0.75f);

    [MenuItem("Tools/Kids Learning Game/Setup Tangram Game")]
    public static void RunSetup() => RunSetupSilent();

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Tangram Game", "Data...", 0.2f);
            UpdateGameData();
            EditorUtility.DisplayProgressBar("Tangram Game", "Scene...", 0.6f);
            BuildScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TangramSetup] Setup complete.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TangramSetup] Failed: {e}");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static void UpdateGameData()
    {
        EnsureFolder("Assets/Data"); EnsureFolder("Assets/Data/Games");
        var path = "Assets/Data/Games/Tangram.asset";
        var data = AssetDatabase.LoadAssetAtPath<GameItemData>(path);
        if (data == null) { data = ScriptableObject.CreateInstance<GameItemData>(); AssetDatabase.CreateAsset(data, path); }
        data.id = "tangram"; data.title = "Tangram";
        data.targetSceneName = "TangramGame"; data.hasSubItems = false;
        data.cardColor = HexColor("#8D6E63"); // warm brown
        EditorUtility.SetDirty(data);
    }

    private static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 5;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = TableBase;
        camGO.AddComponent<AudioListener>(); camGO.tag = "MainCamera";

        // EventSystem
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>(); esGO.AddComponent<StandaloneInputModule>();

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref; scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background (TangramController builds the wooden table at runtime)
        var bgGO = Fill(canvasGO.transform, "Background", TableBase);
        bgGO.transform.SetAsFirstSibling();

        // SafeArea
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(canvasGO.transform, false);
        Full(safeGO.AddComponent<RectTransform>());
        safeGO.AddComponent<SafeAreaHandler>();

        float headerFrac = (float)SetupConstants.HeaderHeight / Ref.y;

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
        HebrewText.SetText(titleTMP, "\u05D8\u05E0\u05D2\u05E8\u05DD"); // טנגרם
        titleTMP.fontSize = 52; titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white; titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeGO = CreateIconButton(topBar.transform, "HomeButton",
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        // Board area (center, where silhouette sits)
        var boardGO = new GameObject("BoardArea");
        boardGO.transform.SetParent(safeGO.transform, false);
        var boardRT = boardGO.AddComponent<RectTransform>();
        boardRT.anchorMin = new Vector2(0.1f, 0.28f);
        boardRT.anchorMax = new Vector2(0.9f, 0.90f);
        boardRT.sizeDelta = Vector2.zero;
        boardRT.anchoredPosition = Vector2.zero;

        // Pieces area (bottom strip, where scattered pieces start)
        var piecesGO = new GameObject("PiecesArea");
        piecesGO.transform.SetParent(safeGO.transform, false);
        var piecesRT = piecesGO.AddComponent<RectTransform>();
        piecesRT.anchorMin = new Vector2(0.05f, 0.03f);
        piecesRT.anchorMax = new Vector2(0.95f, 0.25f);
        piecesRT.sizeDelta = Vector2.zero;
        piecesRT.anchoredPosition = Vector2.zero;

        // Controller
        var ctrl = canvasGO.AddComponent<TangramController>();
        ctrl.boardArea = boardRT;
        ctrl.piecesArea = piecesRT;

        // Home button wired at runtime by TangramController
        EnsureFolder("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/TangramGame.unity");
        Debug.Log("[TangramSetup] Scene saved to Assets/Scenes/TangramGame.unity");
    }

    // ── Helpers ─────────────────────────────────────────────────

    private static GameObject Fill(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color; img.raycastTarget = false;
        return go;
    }

    private static void Full(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
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
    }

    private static GameObject CreateIconButton(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = true;

        // Try loading home icon sprite
        var homeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Arrow.png");
        if (homeSprite != null) img.sprite = homeSprite;

        go.AddComponent<Button>();
        return go;
    }

    private static Color HexColor(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString(hex, out c);
        return c;
    }
}
