using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the HomeScene — the main entry point after profile selection.
/// Layout: profile avatar (top-left), pulsing Play button (center), World button, All Games text.
/// Run via Tools > Kids Learning Game > Setup Home Scene.
/// </summary>
public class HomeSceneSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1080, 1920);

    private static readonly Color BgColor = HexColor("#F0F4FF");       // soft blue-white
    private static readonly Color PlayColor = HexColor("#4CAF50");      // green
    private static readonly Color WorldColor = HexColor("#42A5F5");     // blue
    private static readonly Color AllGamesColor = HexColor("#999999");  // muted gray

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Home Scene Setup", "Building scene…", 0.5f);
            CreateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void CreateScene()
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgColor;
        cam.orthographic = true;
        var urpType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpType != null) camGO.AddComponent(urpType);

        // EventSystem
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inputType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputType != null) esGO.AddComponent(inputType);
        else esGO.AddComponent<StandaloneInputModule>();

        // Canvas
        var canvasGO = new GameObject("HomeCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        var bg = CreateStretchImage(canvasGO.transform, "Background", BgColor);
        bg.GetComponent<Image>().raycastTarget = false;

        // Safe area
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        StretchFull(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // ── Profile Avatar (top-left) ──
        var profileBtn = new GameObject("ProfileButton");
        profileBtn.transform.SetParent(safeArea.transform, false);
        var profileBtnRT = profileBtn.AddComponent<RectTransform>();
        profileBtnRT.anchorMin = new Vector2(0, 1);
        profileBtnRT.anchorMax = new Vector2(0, 1);
        profileBtnRT.pivot = new Vector2(0, 1);
        profileBtnRT.anchoredPosition = new Vector2(30, -30);
        profileBtnRT.sizeDelta = new Vector2(100, 100);

        var profileBtnImg = profileBtn.AddComponent<Image>();
        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");
        if (circleSprite != null) profileBtnImg.sprite = circleSprite;
        profileBtnImg.color = HexColor("#90CAF9");
        var profileBtnButton = profileBtn.AddComponent<Button>();
        profileBtnButton.targetGraphic = profileBtnImg;

        // Profile initial text
        var initialGO = new GameObject("Initial");
        initialGO.transform.SetParent(profileBtn.transform, false);
        var initialRT = initialGO.AddComponent<RectTransform>();
        StretchFull(initialRT);
        var initialTMP = initialGO.AddComponent<TextMeshProUGUI>();
        initialTMP.text = "?";
        initialTMP.fontSize = 42;
        initialTMP.fontStyle = FontStyles.Bold;
        initialTMP.color = Color.white;
        initialTMP.alignment = TextAlignmentOptions.Center;
        initialTMP.raycastTarget = false;

        // ── Play Button (center, large) ──
        var playBtn = new GameObject("PlayButton");
        playBtn.transform.SetParent(safeArea.transform, false);
        var playBtnRT = playBtn.AddComponent<RectTransform>();
        playBtnRT.anchorMin = new Vector2(0.5f, 0.5f);
        playBtnRT.anchorMax = new Vector2(0.5f, 0.5f);
        playBtnRT.sizeDelta = new Vector2(320, 320);
        playBtnRT.anchoredPosition = new Vector2(0, 100);

        var playBtnImg = playBtn.AddComponent<Image>();
        if (circleSprite != null) playBtnImg.sprite = circleSprite;
        playBtnImg.color = PlayColor;
        var playBtnButton = playBtn.AddComponent<Button>();
        playBtnButton.targetGraphic = playBtnImg;

        // Play icon (triangle/text)
        var playLabel = new GameObject("PlayLabel");
        playLabel.transform.SetParent(playBtn.transform, false);
        var playLabelRT = playLabel.AddComponent<RectTransform>();
        StretchFull(playLabelRT);
        var playLabelTMP = playLabel.AddComponent<TextMeshProUGUI>();
        playLabelTMP.text = "\u25B6"; // ▶
        playLabelTMP.fontSize = 100;
        playLabelTMP.color = Color.white;
        playLabelTMP.alignment = TextAlignmentOptions.Center;
        playLabelTMP.raycastTarget = false;

        // ── World Button (below play) ──
        var worldBtn = new GameObject("WorldButton");
        worldBtn.transform.SetParent(safeArea.transform, false);
        var worldBtnRT = worldBtn.AddComponent<RectTransform>();
        worldBtnRT.anchorMin = new Vector2(0.5f, 0.5f);
        worldBtnRT.anchorMax = new Vector2(0.5f, 0.5f);
        worldBtnRT.sizeDelta = new Vector2(260, 80);
        worldBtnRT.anchoredPosition = new Vector2(0, -120);

        var worldBtnImg = worldBtn.AddComponent<Image>();
        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
        if (roundedRect != null)
        {
            worldBtnImg.sprite = roundedRect;
            worldBtnImg.type = Image.Type.Sliced;
        }
        worldBtnImg.color = WorldColor;
        var worldBtnButton = worldBtn.AddComponent<Button>();
        worldBtnButton.targetGraphic = worldBtnImg;

        var worldLabel = new GameObject("WorldLabel");
        worldLabel.transform.SetParent(worldBtn.transform, false);
        var worldLabelRT = worldLabel.AddComponent<RectTransform>();
        StretchFull(worldLabelRT);
        var worldLabelTMP = worldLabel.AddComponent<TextMeshProUGUI>();
        worldLabelTMP.text = "\uD83C\uDF0D My World"; // 🌍 My World
        worldLabelTMP.fontSize = 32;
        worldLabelTMP.fontStyle = FontStyles.Bold;
        worldLabelTMP.color = Color.white;
        worldLabelTMP.alignment = TextAlignmentOptions.Center;
        worldLabelTMP.raycastTarget = false;

        // ── All Games (bottom, small text) ──
        var allGamesBtn = new GameObject("AllGamesButton");
        allGamesBtn.transform.SetParent(safeArea.transform, false);
        var allGamesBtnRT = allGamesBtn.AddComponent<RectTransform>();
        allGamesBtnRT.anchorMin = new Vector2(0.5f, 0);
        allGamesBtnRT.anchorMax = new Vector2(0.5f, 0);
        allGamesBtnRT.pivot = new Vector2(0.5f, 0);
        allGamesBtnRT.anchoredPosition = new Vector2(0, 60);
        allGamesBtnRT.sizeDelta = new Vector2(300, 60);

        var allGamesTMP = allGamesBtn.AddComponent<TextMeshProUGUI>();
        allGamesTMP.text = "All Games";
        allGamesTMP.fontSize = 24;
        allGamesTMP.color = AllGamesColor;
        allGamesTMP.alignment = TextAlignmentOptions.Center;
        var allGamesButton = allGamesBtn.AddComponent<Button>();
        allGamesButton.targetGraphic = allGamesTMP;

        // ── Controller ──
        var controller = canvasGO.AddComponent<HomeController>();
        controller.gameDatabase = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/Games/GameDatabase.asset");
        controller.playButton = playBtnButton;
        controller.worldButton = worldBtnButton;
        controller.profileButton = profileBtnButton;
        controller.allGamesButton = allGamesButton;
        controller.profileAvatar = profileBtnImg;
        controller.profileInitial = initialTMP;

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/HomeScene.unity");
    }

    // ── Helpers ──

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

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
