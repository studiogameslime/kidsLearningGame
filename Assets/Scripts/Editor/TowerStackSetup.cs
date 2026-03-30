using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the TowerStack scene for landscape orientation (1920×1080).
/// Run via Tools > Kids Learning Game > Setup Tower Stack.
/// </summary>
public class TowerStackSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private static readonly int HeaderHeight = SetupConstants.HeaderHeight;
    private static readonly Color HeaderColor = new Color(0.30f, 0.65f, 0.85f, 0.80f);

    [MenuItem("Tools/Kids Learning Game/Setup Tower Stack")]
    public static void ShowWindow()
    {
        RunSetupSilent();
        Debug.Log("Tower Stack scene created!");
    }

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Tower Stack Setup", "Building scene…", 0.3f);
            var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
            CreateScene(roundedRect);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
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
        cam.backgroundColor = new Color(0.68f, 0.88f, 0.98f);
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

        // Canvas — LANDSCAPE
        var canvasGO = new GameObject("TowerStackCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Safe area
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        StretchFull(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // ═══ HEADER ═══
        var topBar = CreateStretchImage(safeArea.transform, "TopBar", HeaderColor);
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
        HebrewText.SetText(titleTMP, "\u05D1\u05E0\u05D4 \u05D0\u05EA \u05D4\u05DE\u05D2\u05D3\u05DC");
        titleTMP.fontSize = 42;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var homeIcon = UISheetHelper.HomeIcon;
        var homeGO = CreateIconButton(topBar.transform, "HomeButton", homeIcon,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(24, 0), new Vector2(90, 90));

        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = CreateIconButton(topBar.transform, "TrophyButton", trophyIcon,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-16, -20), new Vector2(70, 70));

        // ═══ PLAY AREA ═══
        var playArea = new GameObject("PlayArea");
        playArea.transform.SetParent(safeArea.transform, false);
        var playAreaRT = playArea.AddComponent<RectTransform>();
        StretchFull(playAreaRT);
        playAreaRT.offsetMax = new Vector2(0, -HeaderHeight);
        playAreaRT.offsetMin = Vector2.zero;

        // ═══ CONTROLLER ═══
        var controller = canvasGO.AddComponent<TowerStackController>();
        controller.playArea = playAreaRT;
        controller.roundedRectSprite = roundedRect;

        // Load sprites for background, blocks, and particles
        LoadAllSprites(controller);

        // Wire home button
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, controller.OnHomePressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = roundedRect;
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "towerstack";

        // Tutorial hand
        TutorialHandHelper.Create(safeArea.transform, TutorialHandHelper.Anim.Tap,
            new Vector2(0, 0), new Vector2(450, 450), "towerstack");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/TowerStack.unity");
    }

    // ═══ SPRITE LOADING ═══

    private static void LoadAllSprites(TowerStackController controller)
    {
        controller.spriteKeys = new List<string>();
        controller.spriteValues = new List<Sprite>();

        // Sprite map: key → asset path (with fallbacks)
        var spriteMap = new (string key, string[] paths)[]
        {
            // Wood block for stacking pieces (try organized folder first)
            ("wood_block", new[]
            {
                "Assets/Art/TowerGame/TowerBlocks/Wood_Block_Wide.png",
                "Assets/Art/TowerGame/Wood elements/elementWood014.png"
            }),
            // Stone block for alternating material
            ("stone_block", new[]
            {
                "Assets/Art/TowerGame/TowerBlocks/Stone_Block_Wide.png",
                "Assets/Art/TowerGame/Stone elements/elementStone014.png"
            }),

            // Background layers (matching TowerBuilder landscape style)
            ("bg_cloudB1", new[]
            {
                "Assets/Art/World/cloudLayerB1.png",
                "Assets/Art/World/cloudLayer1.png"
            }),
            ("bg_cloudB2", new[]
            {
                "Assets/Art/World/cloudLayerB2.png",
                "Assets/Art/World/cloudLayer2.png"
            }),
            ("bg_mountain", new[]
            {
                "Assets/Art/World/mountains.png",
                "Assets/Art/World/mountainA.png"
            }),
            ("bg_hills", new[]
            {
                "Assets/Art/World/hillsLarge.png",
                "Assets/Art/World/hills.png"
            }),
            ("bg_ground1", new[]
            {
                "Assets/Art/World/groundLayer1.png"
            }),
            ("bg_ground2", new[]
            {
                "Assets/Art/World/groundLayer2.png"
            }),

            // Debris particles
            ("debris_wood", new[]
            {
                "Assets/Art/TowerGame/Debris/debrisWood_1.png"
            }),
        };

        foreach (var (key, paths) in spriteMap)
        {
            Sprite sprite = null;
            foreach (var path in paths)
            {
                sprite = LoadSprite(path);
                if (sprite != null) break;
            }

            if (sprite != null)
            {
                controller.spriteKeys.Add(key);
                controller.spriteValues.Add(sprite);
            }
            else
            {
                Debug.LogWarning($"TowerStackSetup: Could not load sprite '{key}'");
            }
        }

        Debug.Log($"TowerStackSetup: Loaded {controller.spriteKeys.Count}/{spriteMap.Length} sprites");
    }

    // ═══ HELPERS ═══

    private static GameObject CreateStretchImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        StretchFull(rt);
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static GameObject CreateIconButton(Transform parent, string name, Sprite icon,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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
}
