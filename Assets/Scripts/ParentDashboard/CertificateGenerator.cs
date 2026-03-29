using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Generates a beautiful certificate/diploma PNG image from dashboard data,
/// then triggers the native share sheet with the image.
/// Uses an off-screen Camera + Canvas → RenderTexture → PNG pipeline.
/// </summary>
public static class CertificateGenerator
{
    private const int Width = 1080;
    private const int Height = 1920;

    // Colors
    private static readonly Color Gold = HexColor("#F1C40F");
    private static readonly Color GoldDark = HexColor("#D4AC0D");
    private static readonly Color TextDark = HexColor("#2D3436");
    private static readonly Color TextMedium = HexColor("#636E72");
    private static readonly Color BrandColor = HexColor("#3498DB");


    /// <summary>
    /// Generates a certificate PNG and shares it via the native OS share sheet.
    /// Call via StartCoroutine from a MonoBehaviour.
    /// </summary>
    public static IEnumerator GenerateAndShare(ParentDashboardData data, Sprite roundedRect)
    {
        if (data == null) yield break;

        // ── Create off-screen rendering setup ──
        var rootGO = new GameObject("CertificateRoot");
        rootGO.SetActive(false); // build everything before rendering

        // Camera: orthographic, sized to exactly frame the canvas
        // Canvas height in world units = 2 * orthographicSize = 10
        // Canvas width in world units = 10 * (1080/1920) = 5.625
        float orthoSize = 5f;
        float worldHeight = orthoSize * 2f;
        float worldWidth = worldHeight * ((float)Width / Height);

        var camGO = new GameObject("CertCam");
        camGO.transform.SetParent(rootGO.transform, false);
        camGO.transform.localPosition = new Vector3(0, 0, -10);
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.white;
        cam.cullingMask = 1 << 31; // use layer 31 to avoid conflicts
        cam.depth = -100;
        cam.enabled = false; // we render manually
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 20f;

        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        cam.targetTexture = rt;

        // ── Canvas (WorldSpace — decoupled from actual screen size) ──
        var canvasGO = new GameObject("CertCanvas");
        canvasGO.layer = 31;
        canvasGO.transform.SetParent(rootGO.transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        canvas.sortingOrder = 1000;

        // Size the canvas rect to match the camera's view exactly
        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(Width, Height);
        canvasRT.localPosition = Vector3.zero; // centered in front of camera
        canvasRT.localScale = new Vector3(worldWidth / Width, worldHeight / Height, 1f);

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 1f;

        // ── Profile color ──
        Color profileColor = ThemeManager.HeaderColor;

        // ── Build certificate layout ──
        BuildCertificate(canvasGO.transform, data, roundedRect, profileColor);

        // Set all children to layer 31
        SetLayerRecursive(rootGO, 31);
        rootGO.SetActive(true);

        // Wait a frame for layout to compute
        yield return null;
        // Force canvas update
        Canvas.ForceUpdateCanvases();
        yield return null;

        // ── Render to texture ──
        cam.Render();

        // ── Read pixels → PNG → cleanup (with guaranteed cleanup) ──
        string filePath = null;
        Texture2D tex2D = null;
        try
        {
            var prevRT = RenderTexture.active;
            RenderTexture.active = rt;
            tex2D = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex2D.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex2D.Apply();
            RenderTexture.active = prevRT;

            byte[] pngData = tex2D.EncodeToPNG();
            filePath = Path.Combine(Application.temporaryCachePath, "certificate.png");
            File.WriteAllBytes(filePath, pngData);
            Debug.Log($"[Certificate] Saved to: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Certificate] Failed to generate: {e.Message}");
        }
        finally
        {
            if (tex2D != null) Object.Destroy(tex2D);
            Object.Destroy(rootGO);
            rt.Release();
            Object.Destroy(rt);
        }

        if (string.IsNullOrEmpty(filePath)) yield break;

        // ── Share ──
        string fallbackText = BuildFallbackText(data);

        #if UNITY_ANDROID && !UNITY_EDITOR
        ShareImageAndroid(filePath, fallbackText);
        #elif UNITY_IOS && !UNITY_EDITOR
        ShareImageIOS(filePath, fallbackText);
        #else
        // Editor: copy path to clipboard for testing
        GUIUtility.systemCopyBuffer = filePath;
        Debug.Log($"[Certificate] Editor mode — image saved at: {filePath}");
        Application.OpenURL("file://" + filePath);
        #endif
    }

    // ════════════════════════════════════════════════════════════════
    //  CERTIFICATE LAYOUT
    // ════════════════════════════════════════════════════════════════

    private static void BuildCertificate(Transform canvasRoot, ParentDashboardData data,
        Sprite roundedRect, Color profileColor)
    {
        // ── Outer white background (full canvas) ──
        var bgGO = MakeRect(canvasRoot, "Background", Vector2.zero, Vector2.one, Color.white, null);

        // ── Decorative border fill ──
        float borderInset = 40f / Width;
        float borderInsetY = 40f / Height;
        var borderGO = MakeRect(bgGO.transform, "Border",
            new Vector2(borderInset, borderInsetY),
            new Vector2(1f - borderInset, 1f - borderInsetY),
            new Color(profileColor.r, profileColor.g, profileColor.b, 0.15f), roundedRect);

        // ── Inner border stroke (slightly inset, visible solid line) ──
        float strokeInset = 50f / Width;
        float strokeInsetY = 50f / Height;
        Color strokeColor = new Color(profileColor.r, profileColor.g, profileColor.b, 0.5f);

        // Top edge
        MakeRect(bgGO.transform, "StrokeTop",
            new Vector2(strokeInset, 1f - strokeInsetY - 3f / Height),
            new Vector2(1f - strokeInset, 1f - strokeInsetY),
            strokeColor, null);
        // Bottom edge
        MakeRect(bgGO.transform, "StrokeBottom",
            new Vector2(strokeInset, strokeInsetY),
            new Vector2(1f - strokeInset, strokeInsetY + 3f / Height),
            strokeColor, null);
        // Left edge
        MakeRect(bgGO.transform, "StrokeLeft",
            new Vector2(strokeInset, strokeInsetY),
            new Vector2(strokeInset + 3f / Width, 1f - strokeInsetY),
            strokeColor, null);
        // Right edge
        MakeRect(bgGO.transform, "StrokeRight",
            new Vector2(1f - strokeInset - 3f / Width, strokeInsetY),
            new Vector2(1f - strokeInset, 1f - strokeInsetY),
            strokeColor, null);

        // ── Content container (centered, with padding) ──
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(bgGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.08f, 0.04f);
        contentRT.anchorMax = new Vector2(0.92f, 0.96f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.spacing = 10;
        contentLayout.padding = new RectOffset(20, 20, 30, 20);

        AddSpacer(contentGO.transform, 30);

        // ── Title: תעודת הצטיינות ──
        AddCenteredText(contentGO.transform, "\u05EA\u05E2\u05D5\u05D3\u05EA \u05D4\u05E6\u05D8\u05D9\u05D9\u05E0\u05D5\u05EA", 68, Gold, true);

        // ── Gold divider line under title ──
        AddDividerLine(contentGO.transform, GoldDark, 0.4f);

        AddSpacer(contentGO.transform, 20);

        // ── Animal sprite ──
        var profile = ProfileManager.ActiveProfile;
        string favoriteAnimalId = profile != null ? profile.favoriteAnimalId : null;
        bool hasAnimal = false;

        if (!string.IsNullOrEmpty(favoriteAnimalId))
        {
            var animData = AnimalAnimData.Load(favoriteAnimalId);
            if (animData != null && animData.idleFrames != null && animData.idleFrames.Length > 0)
            {
                var animalGO = new GameObject("Animal");
                animalGO.transform.SetParent(contentGO.transform, false);
                animalGO.AddComponent<RectTransform>();

                var animalImg = animalGO.AddComponent<Image>();
                animalImg.sprite = animData.idleFrames[0];
                animalImg.preserveAspect = true;
                animalImg.raycastTarget = false;

                var le = animalGO.AddComponent<LayoutElement>();
                le.preferredHeight = 400;
                le.preferredWidth = 400;
                hasAnimal = true;
            }
        }

        if (!hasAnimal)
        {
            AddSpacer(contentGO.transform, 60);
        }

        AddSpacer(contentGO.transform, 15);

        // ── Child name (auto-size for long names) ──
        string childName = !string.IsNullOrEmpty(data.profileName) && data.profileName != "---"
            ? data.profileName : "\u05D4\u05D9\u05DC\u05D3 \u05E9\u05DC\u05D9"; // הילד שלי
        var nameTMP = AddCenteredText(contentGO.transform, childName, 80, profileColor, true);
        nameTMP.enableAutoSizing = true;
        nameTMP.fontSizeMin = 40;
        nameTMP.fontSizeMax = 80;

        AddSpacer(contentGO.transform, 25);

        // ── Divider line + הישגים מרשימים ──
        AddDividerLine(contentGO.transform, profileColor, 0.6f);
        AddSpacer(contentGO.transform, 8);
        AddCenteredText(contentGO.transform,
            "\u05D4\u05D9\u05E9\u05D2\u05D9\u05DD \u05DE\u05E8\u05E9\u05D9\u05DE\u05D9\u05DD",
            32, TextMedium, true);
        AddSpacer(contentGO.transform, 8);
        AddDividerLine(contentGO.transform, profileColor, 0.6f);

        AddSpacer(contentGO.transform, 20);

        // ── Stats grid (2×2) ──
        bool hasData = data.totalSessions > 0;
        if (hasData)
        {
            BuildStatsGrid(contentGO.transform, data, profileColor);
        }
        else
        {
            AddCenteredText(contentGO.transform,
                "\u05DE\u05EA\u05D7\u05D9\u05DC \u05D0\u05EA \u05D4\u05DE\u05E1\u05E2!",
                36, TextMedium, true);
        }

        AddSpacer(contentGO.transform, 20);

        // ── Overall score ──
        if (hasData)
        {
            int score = Mathf.RoundToInt(data.overallScore);
            AddCenteredText(contentGO.transform,
                $"{score}/100 :\u05E6\u05D9\u05D5\u05DF \u05DB\u05DC\u05DC\u05D9",
                44, TextDark, true);
        }

        AddSpacer(contentGO.transform, 30);

        // ── Bottom divider ──
        AddDividerLine(contentGO.transform, TextMedium, 0.5f);
        AddSpacer(contentGO.transform, 12);

        // ── App branding ──
        AddCenteredText(contentGO.transform, "\u05DC\u05D5\u05DE\u05D3\u05D9\u05DD \u05E2\u05DD \u05D0\u05DC\u05D9\u05DF", 34, BrandColor, true); // לומדים עם אלין
    }

    private static void BuildStatsGrid(Transform parent, ParentDashboardData data, Color accentColor)
    {
        // 2×2 grid using a vertical + horizontal layout
        var gridGO = new GameObject("StatsGrid");
        gridGO.transform.SetParent(parent, false);
        gridGO.AddComponent<RectTransform>();
        var gridLayout = gridGO.AddComponent<VerticalLayoutGroup>();
        gridLayout.childAlignment = TextAnchor.MiddleCenter;
        gridLayout.childControlWidth = true;
        gridLayout.childControlHeight = true;
        gridLayout.childForceExpandWidth = true;
        gridLayout.childForceExpandHeight = false;
        gridLayout.spacing = 10;
        gridGO.AddComponent<LayoutElement>().preferredHeight = 200;

        // Row 1: sessions + animals
        var row1 = MakeHRow(gridGO.transform);
        AddStatCell(row1.transform, $"{data.totalSessions}", "\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD", accentColor); // משחקים
        AddStatCell(row1.transform, $"{data.discoveredAnimals}", "\u05D7\u05D9\u05D5\u05EA", accentColor); // חיות

        // Row 2: colors + stickers
        var row2 = MakeHRow(gridGO.transform);
        AddStatCell(row2.transform, $"{data.discoveredColors}", "\u05E6\u05D1\u05E2\u05D9\u05DD", accentColor); // צבעים
        AddStatCell(row2.transform, $"{data.collectedStickers}", "\u05DE\u05D3\u05D1\u05E7\u05D5\u05EA", accentColor); // מדבקות
    }

    private static GameObject MakeHRow(Transform parent)
    {
        var rowGO = new GameObject("Row");
        rowGO.transform.SetParent(parent, false);
        rowGO.AddComponent<RectTransform>();
        var hLayout = rowGO.AddComponent<HorizontalLayoutGroup>();
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = true;
        hLayout.spacing = 40;
        rowGO.AddComponent<LayoutElement>().preferredHeight = 80;
        return rowGO;
    }

    private static void AddStatCell(Transform parent, string value, string label, Color accentColor)
    {
        var cellGO = new GameObject("StatCell");
        cellGO.transform.SetParent(parent, false);
        cellGO.AddComponent<RectTransform>();
        var cellLayout = cellGO.AddComponent<VerticalLayoutGroup>();
        cellLayout.childAlignment = TextAnchor.MiddleCenter;
        cellLayout.childControlWidth = true;
        cellLayout.childControlHeight = true;
        cellLayout.childForceExpandWidth = true;
        cellLayout.childForceExpandHeight = false;
        cellLayout.spacing = 4;
        cellGO.AddComponent<LayoutElement>().flexibleWidth = 1;

        // Big number
        var valTMP = AddTMP(cellGO.transform, value, 48, accentColor, TextAlignmentOptions.Center, true);
        valTMP.gameObject.GetComponent<LayoutElement>().preferredHeight = 55;

        // Label below
        var labelTMP = AddTMP(cellGO.transform, label, 26, TextMedium, TextAlignmentOptions.Center, false);
        labelTMP.gameObject.GetComponent<LayoutElement>().preferredHeight = 32;
    }

    // ════════════════════════════════════════════════════════════════
    //  SHARING
    // ════════════════════════════════════════════════════════════════

    #if UNITY_ANDROID && !UNITY_EDITOR
    private static void ShareImageAndroid(string imagePath, string fallbackText)
    {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var intentClass = new AndroidJavaClass("android.content.Intent"))
        using (var intent = new AndroidJavaObject("android.content.Intent"))
        {
            intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
            intent.Call<AndroidJavaObject>("setType", "image/*");

            // Get content URI via FileProvider
            using (var file = new AndroidJavaObject("java.io.File", imagePath))
            using (var fileProviderClass = new AndroidJavaClass("androidx.core.content.FileProvider"))
            {
                string authority = activity.Call<string>("getPackageName") + ".fileprovider";
                using (var uri = fileProviderClass.CallStatic<AndroidJavaObject>("getUriForFile", activity, authority, file))
                {
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), uri);
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), fallbackText);
                    intent.Call<AndroidJavaObject>("addFlags", intentClass.GetStatic<int>("FLAG_GRANT_READ_URI_PERMISSION"));

                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser",
                        intent, "\u05E9\u05EA\u05E4\u05D5 \u05E2\u05DD \u05D7\u05D1\u05E8\u05D9\u05DD")) // שתפו עם חברים
                    {
                        activity.Call("startActivity", chooser);
                    }
                }
            }
        }
    }
    #endif

    #if UNITY_IOS && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void _ShareImageWithText(string imagePath, string text);

    private static void ShareImageIOS(string imagePath, string text)
    {
        _ShareImageWithText(imagePath, text);
    }
    #endif

    // ════════════════════════════════════════════════════════════════
    //  PUBLIC SHARE WRAPPERS (used by drawing share, certificate share)
    // ════════════════════════════════════════════════════════════════

    public static void ShareImageWithTextAndroid(string imagePath, string text)
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        ShareImageAndroid(imagePath, text);
        #endif
    }

    public static void ShareImageWithTextIOS(string imagePath, string text)
    {
        #if UNITY_IOS && !UNITY_EDITOR
        ShareImageIOS(imagePath, text);
        #endif
    }

    // ════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════

    private static string BuildFallbackText(ParentDashboardData data)
    {
        string name = data.profileName ?? "";
        int sessions = data.totalSessions;
        int animals = data.discoveredAnimals;
        return $"\u05D4\u05D9\u05DC\u05D3 \u05E9\u05DC\u05D9 {name} \u05DC\u05D5\u05DE\u05D3 \u05D5\u05DE\u05E9\u05D7\u05E7 \u05E2\u05DD Alin Teaching Kids!\n" +
               $"\u05DB\u05D1\u05E8 \u05E9\u05D9\u05D7\u05E7 \u05D1-{sessions} \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D5\u05D2\u05D9\u05DC\u05D4 {animals} \u05D7\u05D9\u05D5\u05EA!\n" +
               $"https://play.google.com/store/apps/details?id={Application.identifier}";
    }

    private static TextMeshProUGUI AddCenteredText(Transform parent, string text, int fontSize, Color color, bool bold)
    {
        return AddTMP(parent, text, fontSize, color, TextAlignmentOptions.Center, bold);
    }

    private static TextMeshProUGUI AddTMP(Transform parent, string text, int fontSize, Color color,
        TextAlignmentOptions align, bool bold)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        go.AddComponent<LayoutElement>().preferredHeight = fontSize * 1.5f;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(tmp, text);
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.enableWordWrapping = false;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        return tmp;
    }

    /// <summary>Adds a colored horizontal line as a visual divider (Image-based, no font dependency).</summary>
    private static void AddDividerLine(Transform parent, Color color, float widthFraction)
    {
        // Container to hold the centered line
        var containerGO = new GameObject("DividerContainer");
        containerGO.transform.SetParent(parent, false);
        containerGO.AddComponent<RectTransform>();
        containerGO.AddComponent<LayoutElement>().preferredHeight = 3;

        // The actual line, anchored to center with a fraction of parent width
        var lineGO = new GameObject("DividerLine");
        lineGO.transform.SetParent(containerGO.transform, false);
        var lineRT = lineGO.AddComponent<RectTransform>();
        float pad = (1f - widthFraction) / 2f;
        lineRT.anchorMin = new Vector2(pad, 0f);
        lineRT.anchorMax = new Vector2(1f - pad, 1f);
        lineRT.offsetMin = Vector2.zero;
        lineRT.offsetMax = Vector2.zero;

        var img = lineGO.AddComponent<Image>();
        img.color = new Color(color.r, color.g, color.b, 0.4f);
        img.raycastTarget = false;
    }

    private static void AddSpacer(Transform parent, float height)
    {
        var go = new GameObject("Spacer");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<LayoutElement>().preferredHeight = height;
    }

    private static GameObject MakeRect(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Color color, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
        }
        return go;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
