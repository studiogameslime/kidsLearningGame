using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the DiscoveryReveal scene — scratch-to-reveal screen for new discoveries.
/// Run via Tools > Kids Learning Game > Setup Discovery Reveal.
/// </summary>
public class DiscoveryRevealSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1080, 1920);
    private static readonly Color BgColor = HexColor("#FFF8E8"); // warm gold-ish

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Discovery Reveal Setup", "Building scene…", 0.5f);
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
        var canvasGO = new GameObject("DiscoveryCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Ref;
        scaler.matchWidthOrHeight = 0f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        var bgGO = CreateStretchImage(canvasGO.transform, "Background", BgColor);
        bgGO.GetComponent<Image>().raycastTarget = false;
        var bgImg = bgGO.GetComponent<Image>();

        // Safe area
        var safeArea = new GameObject("SafeArea");
        safeArea.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeArea.AddComponent<RectTransform>();
        StretchFull(safeRT);
        safeArea.AddComponent<SafeAreaHandler>();

        // ── Reveal content (centered, large) ──
        var revealGO = new GameObject("RevealImage");
        revealGO.transform.SetParent(safeArea.transform, false);
        var revealRT = revealGO.AddComponent<RectTransform>();
        revealRT.anchorMin = new Vector2(0.5f, 0.5f);
        revealRT.anchorMax = new Vector2(0.5f, 0.5f);
        revealRT.sizeDelta = new Vector2(500, 500);
        revealRT.anchoredPosition = Vector2.zero;
        var revealImg = revealGO.AddComponent<Image>();
        revealImg.preserveAspect = true;
        revealImg.raycastTarget = false;
        revealImg.color = Color.white;

        // ── Gold scratch overlay (covers the reveal area) ──
        var overlayGO = new GameObject("ScratchOverlay");
        overlayGO.transform.SetParent(safeArea.transform, false);
        var overlayRT = overlayGO.AddComponent<RectTransform>();
        overlayRT.anchorMin = new Vector2(0.5f, 0.5f);
        overlayRT.anchorMax = new Vector2(0.5f, 0.5f);
        overlayRT.sizeDelta = new Vector2(540, 960);
        overlayRT.anchoredPosition = Vector2.zero;
        var overlayRawImg = overlayGO.AddComponent<RawImage>();
        overlayRawImg.color = Color.white;
        overlayRawImg.raycastTarget = true; // receives touch input

        // ── Name text (below reveal, hidden initially) ──
        var nameGO = new GameObject("NameText");
        nameGO.transform.SetParent(safeArea.transform, false);
        var nameRT = nameGO.AddComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0.5f, 0.5f);
        nameRT.anchorMax = new Vector2(0.5f, 0.5f);
        nameRT.sizeDelta = new Vector2(800, 100);
        nameRT.anchoredPosition = new Vector2(0, -350);
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "";
        nameTMP.fontSize = 64;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = HexColor("#333333");
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.raycastTarget = false;
        nameGO.SetActive(false);

        // ── Controller ──
        var controller = canvasGO.AddComponent<DiscoveryRevealController>();
        controller.overlayImage = overlayRawImg;
        controller.revealImage = revealImg;
        controller.nameText = nameTMP;
        controller.backgroundImage = bgImg;

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/DiscoveryReveal.unity");
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
