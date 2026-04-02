#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Creates a HebrewValidation scene with test strings to verify RTL rendering.
/// Run via Tools > Kids Learning Game > Create Hebrew Validation Scene.
/// </summary>
public static class HebrewValidationSetup
{
    private static readonly string[] TestStrings = new string[]
    {
        "\u05DE\u05D9 \u05DE\u05E9\u05D7\u05E7?",                     // מי משחק?
        "\u05DE\u05EA\u05DF",                                           // מתן
        "\u05D4\u05D5\u05E1\u05E3",                                     // הוסף
        "\u05D0\u05D6\u05D5\u05E8 \u05D4\u05D5\u05E8\u05D9\u05DD",     // אזור הורים
        "\u05E4\u05EA\u05E8\u05D5 \u05D0\u05EA \u05D4\u05EA\u05E8\u05D2\u05D9\u05DC", // פתרו את התרגיל
        "? = 7 + 4",                                                    // ? = 7 + 4
        "\u05D9\u05E9 \u05DC\u05DA 2 \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD", // יש לך 2 משחקים
        "\u05D1\u05D7\u05E8 \u05E6\u05D1\u05E2: \u05D0\u05D3\u05D5\u05DD", // בחר צבע: אדום
        "\u05DC\u05D7\u05E5 Play \u05DB\u05D3\u05D9 \u05DC\u05D4\u05EA\u05D7\u05D9\u05DC", // לחץ Play כדי להתחיל
        "\u05D4\u05EA\u05E7\u05D3\u05DE\u05D5\u05EA 25%",              // התקדמות 25%
    };

    private static readonly string[] TestLabels = new string[]
    {
        "Pure Hebrew + punctuation",
        "Short Hebrew name",
        "Single Hebrew word",
        "Two Hebrew words",
        "Hebrew phrase (instruction)",
        "Pure math (LTR)",
        "Hebrew + number + Hebrew",
        "Hebrew + colon + Hebrew",
        "Hebrew + English + Hebrew",
        "Hebrew + number + percent",
    };

    public static void Create()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.95f, 0.95f, 0.97f);
        camGO.tag = "MainCamera";

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Scroll view
        var scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(canvasGO.transform, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(40, 40);
        scrollRT.offsetMax = new Vector2(-40, -40);
        scrollGO.AddComponent<Image>().color = Color.white;
        scrollGO.AddComponent<Mask>().showMaskGraphic = true;
        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // Content
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(scrollGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;
        var layout = contentGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 20;
        layout.padding = new RectOffset(20, 20, 20, 40);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRT;

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(contentGO.transform, false);
        titleGO.AddComponent<RectTransform>();
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "Hebrew RTL Validation";
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(0.2f, 0.2f, 0.2f);
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleGO.AddComponent<LayoutElement>().preferredHeight = 60;

        // Load Hebrew font if available
        TMP_FontAsset hebrewFont = null;
        var fontGuids = AssetDatabase.FindAssets("Rubik-Hebrew-Regular SDF t:TMP_FontAsset");
        if (fontGuids.Length > 0)
            hebrewFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(fontGuids[0]));

        // Test rows
        for (int i = 0; i < TestStrings.Length; i++)
        {
            CreateTestRow(contentGO.transform, i + 1, TestLabels[i], TestStrings[i], hebrewFont);
        }

        // Save
        string scenePath = "Assets/Scenes/HebrewValidation.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"Hebrew Validation scene created at {scenePath}");
        Debug.Log("Open in Editor and verify all Hebrew strings render correctly.");
        Debug.Log("Then build to Android/iOS and verify on device.");
    }

    private static void CreateTestRow(Transform parent, int index, string label, string testText, TMP_FontAsset font)
    {
        // Row container
        var rowGO = new GameObject($"Test_{index}");
        rowGO.transform.SetParent(parent, false);
        rowGO.AddComponent<RectTransform>();
        var rowLayout = rowGO.AddComponent<VerticalLayoutGroup>();
        rowLayout.spacing = 4;
        rowLayout.padding = new RectOffset(16, 16, 12, 12);
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        var rowImg = rowGO.AddComponent<Image>();
        rowImg.color = new Color(0.97f, 0.97f, 1f);

        // Label (description)
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(rowGO.transform, false);
        labelGO.AddComponent<RectTransform>();
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = $"#{index}: {label}";
        labelTMP.fontSize = 18;
        labelTMP.color = new Color(0.5f, 0.5f, 0.5f);
        labelTMP.alignment = TextAlignmentOptions.Left;
        labelGO.AddComponent<LayoutElement>().preferredHeight = 28;

        // Hebrew text (the actual test)
        var textGO = new GameObject("HebrewText");
        textGO.transform.SetParent(rowGO.transform, false);
        textGO.AddComponent<RectTransform>();
        var textTMP = textGO.AddComponent<TextMeshProUGUI>();
        if (font != null) textTMP.font = font;
        textTMP.fontSize = 40;
        textTMP.fontStyle = FontStyles.Bold;
        textTMP.color = new Color(0.15f, 0.15f, 0.15f);
        textTMP.alignment = TextAlignmentOptions.Center;
        textGO.AddComponent<LayoutElement>().preferredHeight = 60;

        // Use HebrewText.SetText — the ONLY text assignment API
        HebrewText.SetText(textTMP, testText);

        // Expected text (raw string for comparison)
        var expectedGO = new GameObject("Expected");
        expectedGO.transform.SetParent(rowGO.transform, false);
        expectedGO.AddComponent<RectTransform>();
        var expectedTMP = expectedGO.AddComponent<TextMeshProUGUI>();
        expectedTMP.text = $"Expected: {testText}";
        expectedTMP.fontSize = 14;
        expectedTMP.color = new Color(0.6f, 0.6f, 0.6f);
        expectedTMP.alignment = TextAlignmentOptions.Left;
        expectedGO.AddComponent<LayoutElement>().preferredHeight = 22;
    }
}
#endif
