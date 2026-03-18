using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.IO;

/// <summary>
/// Builds the HomeScene — the main entry point after profile selection.
/// Layout: profile avatar (top-left), pulsing Play button (center), World button, All Games text.
/// Run via Tools > Kids Learning Game > Setup Home Scene.
/// </summary>
public class HomeSceneSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);

    private static readonly Color BgColor = HexColor("#F0F4FF");       // soft blue-white
    private static readonly Color PlayColor = HexColor("#4CAF50");      // green (placeholder, colored at runtime)
    private static readonly Color WorldColor = HexColor("#42A5F5");     // blue
    private static readonly Color AllGamesColor = HexColor("#999999");  // muted gray

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Home Scene Setup", "Building scene…", 0.5f);
            CreateArrowSprite();
            CreateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static Sprite CreateArrowSprite()
    {
        EnsureFolder("Assets/UI/Sprites");
        string path = "Assets/UI/Sprites/Arrow.png";

        int w = 64, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];

        float cx = w / 2f;
        int headHeight = 36;
        int shaftHalfWidth = 5;
        int headHalfWidth = 28;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x - cx;
                bool inside = false;

                if (y < h - headHeight)
                {
                    // Shaft
                    inside = Mathf.Abs(dx) <= shaftHalfWidth;
                }
                else
                {
                    // Triangle head — wider at bottom of head, narrows to tip at top
                    float progress = (float)(y - (h - headHeight)) / headHeight;
                    float halfW = headHalfWidth * (1f - progress);
                    inside = Mathf.Abs(dx) <= halfW;
                }

                // Soft edge anti-aliasing
                if (inside)
                {
                    float edgeDist;
                    if (y < h - headHeight)
                        edgeDist = shaftHalfWidth - Mathf.Abs(dx);
                    else
                    {
                        float progress = (float)(y - (h - headHeight)) / headHeight;
                        edgeDist = headHalfWidth * (1f - progress) - Mathf.Abs(dx);
                    }
                    float alpha = Mathf.Clamp01(edgeDist * 2f);
                    pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    pixels[y * w + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void CreateScene()
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");
        var roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedRect.png");
        var arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Arrow.png");

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
        scaler.matchWidthOrHeight = 0.5f;
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
        profileBtnRT.anchoredPosition = new Vector2(24, -20);
        profileBtnRT.sizeDelta = new Vector2(80, 80);

        var profileBtnImg = profileBtn.AddComponent<Image>();
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
        initialTMP.fontSize = 34;
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
        playBtnRT.sizeDelta = new Vector2(260, 260);
        playBtnRT.anchoredPosition = new Vector2(0, 40);

        var playBtnImg = playBtn.AddComponent<Image>();
        if (circleSprite != null) playBtnImg.sprite = circleSprite;
        playBtnImg.color = PlayColor;
        var playBtnButton = playBtn.AddComponent<Button>();
        playBtnButton.targetGraphic = playBtnImg;

        // Play shadow
        var playShadow = playBtn.AddComponent<Shadow>();
        playShadow.effectColor = new Color(0, 0, 0, 0.25f);
        playShadow.effectDistance = new Vector2(3, -4);

        // Play icon (triangle/text)
        var playLabel = new GameObject("PlayLabel");
        playLabel.transform.SetParent(playBtn.transform, false);
        var playLabelRT = playLabel.AddComponent<RectTransform>();
        StretchFull(playLabelRT);
        var playLabelTMP = playLabel.AddComponent<TextMeshProUGUI>();
        playLabelTMP.text = "\u25B6"; // ▶
        playLabelTMP.fontSize = 80;
        playLabelTMP.color = Color.white;
        playLabelTMP.alignment = TextAlignmentOptions.Center;
        playLabelTMP.raycastTarget = false;

        // ── Arrows pointing TO the play button ──

        // Arrow from profile area → play (rotated, pointing down-right)
        var arrow1 = CreateArrowImage(safeArea.transform, "ArrowFromProfile", arrowSprite,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-130, 250), new Vector2(32, 64),
            PlayColor, -35f);

        // Arrow from world → play (pointing up)
        var arrow2 = CreateArrowImage(safeArea.transform, "ArrowFromWorld", arrowSprite,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -30), new Vector2(30, 60),
            PlayColor, 0f);

        // ── World Button (below play) ──
        var worldBtn = new GameObject("WorldButton");
        worldBtn.transform.SetParent(safeArea.transform, false);
        var worldBtnRT = worldBtn.AddComponent<RectTransform>();
        worldBtnRT.anchorMin = new Vector2(0.5f, 0.5f);
        worldBtnRT.anchorMax = new Vector2(0.5f, 0.5f);
        worldBtnRT.sizeDelta = new Vector2(240, 70);
        worldBtnRT.anchoredPosition = new Vector2(0, -120);

        var worldBtnImg = worldBtn.AddComponent<Image>();
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
        HebrewText.SetText(worldLabelTMP, "\u05D4\u05E2\u05D5\u05DC\u05DD \u05E9\u05DC\u05D9"); // העולם שלי
        worldLabelTMP.fontSize = 28;
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
        allGamesBtnRT.anchoredPosition = new Vector2(0, 40);
        allGamesBtnRT.sizeDelta = new Vector2(300, 60);

        var allGamesTMP = allGamesBtn.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(allGamesTMP, "\u05DB\u05DC \u05D4\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD"); // כל המשחקים
        allGamesTMP.fontSize = 24;
        allGamesTMP.color = AllGamesColor;
        allGamesTMP.alignment = TextAlignmentOptions.Center;
        var allGamesButton = allGamesBtn.AddComponent<Button>();
        allGamesButton.targetGraphic = allGamesTMP;

        // ── Parent Area (bottom-left, small subtle text) ──
        var parentBtn = new GameObject("ParentAreaButton");
        parentBtn.transform.SetParent(safeArea.transform, false);
        var parentBtnRT = parentBtn.AddComponent<RectTransform>();
        parentBtnRT.anchorMin = new Vector2(0, 0);
        parentBtnRT.anchorMax = new Vector2(0, 0);
        parentBtnRT.pivot = new Vector2(0, 0);
        parentBtnRT.anchoredPosition = new Vector2(20, 40);
        parentBtnRT.sizeDelta = new Vector2(200, 40);

        var parentBtnTMP = parentBtn.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(parentBtnTMP, "\u05D0\u05D6\u05D5\u05E8 \u05D4\u05D5\u05E8\u05D9\u05DD"); // אזור הורים
        parentBtnTMP.fontSize = 18;
        parentBtnTMP.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        parentBtnTMP.alignment = TextAlignmentOptions.Left;
        var parentAreaButton = parentBtn.AddComponent<Button>();
        parentAreaButton.targetGraphic = parentBtnTMP;

        // ── Controller ──
        var controller = canvasGO.AddComponent<HomeController>();
        controller.gameDatabase = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/Games/GameDatabase.asset");
        controller.playButton = playBtnButton;
        controller.playButtonImage = playBtnImg;
        controller.worldButton = worldBtnButton;
        controller.profileButton = profileBtnButton;
        controller.allGamesButton = allGamesButton;
        controller.parentAreaButton = parentAreaButton;
        controller.profileAvatar = profileBtnImg;
        controller.profileInitial = initialTMP;
        controller.arrowImages = new Image[] {
            arrow1.GetComponent<Image>(),
            arrow2.GetComponent<Image>()
        };

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/HomeScene.unity");
    }

    // ── Helpers ──

    private static GameObject CreateArrowImage(Transform parent, string name, Sprite arrowSprite,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color color, float zRotation)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localEulerAngles = new Vector3(0, 0, zRotation);
        var img = go.AddComponent<Image>();
        if (arrowSprite != null) img.sprite = arrowSprite;
        img.color = new Color(color.r, color.g, color.b, 0.5f);
        img.preserveAspect = true;
        img.raycastTarget = false;
        return go;
    }

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
