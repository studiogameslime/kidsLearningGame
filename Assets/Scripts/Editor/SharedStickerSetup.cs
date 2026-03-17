using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Builds the SharedSticker ("Spot It / Dobble") scene — landscape layout.
/// Two large circular cards on a wooden table background.
/// Generates a high-resolution circle sprite for crisp card visuals.
/// </summary>
public class SharedStickerSetup : EditorWindow
{
    private static readonly Vector2 Ref = new Vector2(1920, 1080);
    private const int TopBarHeight = 130;

    // Warm wood table palette
    private static readonly Color WoodDark    = HexColor("#8B6914");
    private static readonly Color WoodMid     = HexColor("#B08838");
    private static readonly Color WoodLight   = HexColor("#C9A44E");
    private static readonly Color WoodHighlight = HexColor("#D4B96A");
    private static readonly Color CardColor   = new Color(0.98f, 0.97f, 0.94f); // warm cream
    private static readonly Color CardOutline = new Color(0.25f, 0.22f, 0.18f, 0.7f);

    // High-res circle sprite path (generated once, reused)
    private const string HiResCirclePath = "Assets/UI/Sprites/CircleHiRes.png";

    public static void RunSetupSilent()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Shared Sticker", "Generating circle sprite...", 0.3f);
            var cardCircle = GenerateHiResCircle();

            EditorUtility.DisplayProgressBar("Shared Sticker", "Building scene...", 0.6f);
            BuildScene(cardCircle);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    /// <summary>
    /// Generate a 512x512 anti-aliased circle sprite — sharp at any UI size.
    /// </summary>
    private static Sprite GenerateHiResCircle()
    {
        // Check if already exists
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(HiResCirclePath);
        if (existing != null) return existing;

        EnsureFolder("Assets/UI/Sprites");

        int size = 512;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        float center = (size - 1) / 2f;
        float radius = center - 1f; // 1px inset so AA doesn't clip at edge

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                // 1.5px anti-aliasing band for crisp but smooth edge
                float alpha = Mathf.Clamp01((radius - dist) / 1.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        File.WriteAllBytes(HiResCirclePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(HiResCirclePath);

        var importer = AssetImporter.GetAtPath(HiResCirclePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(HiResCirclePath);
    }

    private static void BuildScene(Sprite cardCircle)
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Small circle for glow effects (existing low-res is fine for glows)
        var glowCircle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Circle.png");

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
        scaler.matchWidthOrHeight = 1f; // match height for landscape
        canvasGO.AddComponent<GraphicRaycaster>();
        var root = canvasGO.transform;

        // ═══════════════════════════════════════
        //  WOODEN TABLE BACKGROUND
        // ═══════════════════════════════════════

        Layer(root, "WoodBase", null, 0, 0, 1, 1, WoodMid);
        Layer(root, "WoodTop", null, 0, 0.85f, 1, 1, WoodDark);
        Layer(root, "WoodBottom", null, 0, 0, 1, 0.08f, WoodDark);

        for (int i = 0; i < 6; i++)
        {
            float y = 0.12f + i * 0.14f;
            Layer(root, $"Grain{i}", null, 0, y, 1, y + 0.01f,
                new Color(WoodLight.r, WoodLight.g, WoodLight.b, 0.2f));
        }

        Layer(root, "WoodHighlight", null, 0, 0.35f, 1, 0.65f,
            new Color(WoodHighlight.r, WoodHighlight.g, WoodHighlight.b, 0.15f));

        // ═══════════════════════════════════════
        //  SAFE AREA + TOP BAR
        // ═══════════════════════════════════════

        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(root, false);
        var safeRT = safeGO.AddComponent<RectTransform>();
        Full(safeRT);
        safeGO.AddComponent<SafeAreaHandler>();

        var bar = CreateBar(safeGO.transform);

        // Title: מצא את המשותף
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(bar.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        Full(titleRT);
        titleRT.offsetMin = new Vector2(100, 0);
        titleRT.offsetMax = new Vector2(-100, 0);
        var tmp = titleGO.AddComponent<TextMeshProUGUI>();
        tmp.text = HebrewFixer.Fix("\u05DE\u05E6\u05D0 \u05D0\u05EA \u05D4\u05DE\u05E9\u05D5\u05EA\u05E3");
        tmp.isRightToLeftText = false;
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        var homeIcon = LoadSprite("Assets/Art/Icons/home.png");
        var homeGO = Btn(bar.transform, "HomeButton", homeIcon, 16, -20, 90);

        var trophyIcon = LoadSprite("Assets/Art/Icons/trophy.png");
        var trophyGO = BtnRight(bar.transform, "TrophyButton", trophyIcon, -16, -20, 70);

        // ═══════════════════════════════════════
        //  TWO CIRCULAR CARDS (using hi-res circle)
        // ═══════════════════════════════════════

        var leftCardGO = CreateCard(safeGO.transform, "LeftCard",
            new Vector2(0.03f, 0.03f), new Vector2(0.48f, 0.88f), cardCircle);
        var leftCardBg = leftCardGO.transform.Find("CardBg").GetComponent<Image>();
        var leftCardArea = leftCardGO.transform.Find("StickerArea").GetComponent<RectTransform>();

        var rightCardGO = CreateCard(safeGO.transform, "RightCard",
            new Vector2(0.52f, 0.03f), new Vector2(0.97f, 0.88f), cardCircle);
        var rightCardBg = rightCardGO.transform.Find("CardBg").GetComponent<Image>();
        var rightCardArea = rightCardGO.transform.Find("StickerArea").GetComponent<RectTransform>();

        // ═══════════════════════════════════════
        //  CONTROLLER
        // ═══════════════════════════════════════

        var ctrl = canvasGO.AddComponent<SharedStickerGameController>();
        ctrl.leftCardArea = leftCardArea;
        ctrl.rightCardArea = rightCardArea;
        ctrl.leftCardBg = leftCardBg;
        ctrl.rightCardBg = rightCardBg;
        ctrl.circleSprite = glowCircle; // low-res circle is fine for glow effects
        ctrl.stickerSprites = LoadStickerSprites();

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            homeGO.GetComponent<Button>().onClick, ctrl.OnHomePressed);

        var leaderboard = canvasGO.AddComponent<InGameLeaderboard>();
        leaderboard.roundedRect = LoadSprite("Assets/UI/Sprites/RoundedRect.png");
        leaderboard.trophySprite = trophyIcon;
        leaderboard.trophyButton = trophyGO.GetComponent<Button>();
        leaderboard.gameId = "sharedsticker";

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SharedSticker.unity");
    }

    // ── CARD BUILDER ─────────────────────────────────────────────

    private static GameObject CreateCard(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Sprite circleSprite)
    {
        var cardGO = new GameObject(name);
        cardGO.transform.SetParent(parent, false);
        var cardRT = cardGO.AddComponent<RectTransform>();
        cardRT.anchorMin = anchorMin;
        cardRT.anchorMax = anchorMax;
        cardRT.offsetMin = Vector2.zero;
        cardRT.offsetMax = Vector2.zero;

        float areaW = (anchorMax.x - anchorMin.x) * Ref.x;
        float areaH = (anchorMax.y - anchorMin.y) * Ref.y;
        float diameter = Mathf.Min(areaW, areaH) - 20f;

        // Shadow (offset dark circle)
        var shadowGO = new GameObject("Shadow");
        shadowGO.transform.SetParent(cardGO.transform, false);
        var shadowRT = shadowGO.AddComponent<RectTransform>();
        shadowRT.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRT.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRT.pivot = new Vector2(0.5f, 0.5f);
        shadowRT.anchoredPosition = new Vector2(5, -7);
        shadowRT.sizeDelta = new Vector2(diameter + 16, diameter + 16);
        var shadowImg = shadowGO.AddComponent<Image>();
        if (circleSprite != null) shadowImg.sprite = circleSprite;
        shadowImg.color = new Color(0, 0, 0, 0.18f);
        shadowImg.raycastTarget = false;

        // Outline (slightly larger circle)
        var outlineGO = new GameObject("Outline");
        outlineGO.transform.SetParent(cardGO.transform, false);
        var outlineRT = outlineGO.AddComponent<RectTransform>();
        outlineRT.anchorMin = new Vector2(0.5f, 0.5f);
        outlineRT.anchorMax = new Vector2(0.5f, 0.5f);
        outlineRT.pivot = new Vector2(0.5f, 0.5f);
        outlineRT.anchoredPosition = Vector2.zero;
        outlineRT.sizeDelta = new Vector2(diameter + 10, diameter + 10);
        var outlineImg = outlineGO.AddComponent<Image>();
        if (circleSprite != null) outlineImg.sprite = circleSprite;
        outlineImg.color = CardOutline;
        outlineImg.raycastTarget = false;

        // Card background (cream fill)
        var bgGO = new GameObject("CardBg");
        bgGO.transform.SetParent(cardGO.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0.5f, 0.5f);
        bgRT.anchorMax = new Vector2(0.5f, 0.5f);
        bgRT.pivot = new Vector2(0.5f, 0.5f);
        bgRT.anchoredPosition = Vector2.zero;
        bgRT.sizeDelta = new Vector2(diameter, diameter);
        var bgImg = bgGO.AddComponent<Image>();
        if (circleSprite != null) bgImg.sprite = circleSprite;
        bgImg.color = CardColor;
        bgImg.raycastTarget = false;

        // Sticker area (invisible container centered on card)
        var stickerGO = new GameObject("StickerArea");
        stickerGO.transform.SetParent(cardGO.transform, false);
        var stickerRT = stickerGO.AddComponent<RectTransform>();
        stickerRT.anchorMin = new Vector2(0.5f, 0.5f);
        stickerRT.anchorMax = new Vector2(0.5f, 0.5f);
        stickerRT.pivot = new Vector2(0.5f, 0.5f);
        stickerRT.anchoredPosition = Vector2.zero;
        stickerRT.sizeDelta = new Vector2(diameter, diameter);

        return cardGO;
    }

    // ── STICKER LOADING ──────────────────────────────────────────

    private static Sprite[] LoadStickerSprites()
    {
        var stickerSprites = new List<Sprite>();
        var allAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Stickers/Sticker.png");
        if (allAssets != null)
        {
            foreach (var asset in allAssets)
            {
                if (asset is Sprite spr)
                    stickerSprites.Add(spr);
            }
        }
        stickerSprites.Sort((a, b) =>
        {
            int numA = 0, numB = 0;
            var partsA = a.name.Split('_');
            var partsB = b.name.Split('_');
            if (partsA.Length > 1) int.TryParse(partsA[partsA.Length - 1], out numA);
            if (partsB.Length > 1) int.TryParse(partsB[partsB.Length - 1], out numB);
            return numA.CompareTo(numB);
        });
        return stickerSprites.ToArray();
    }

    // ── HELPERS ──────────────────────────────────────────────────

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
        barImg.color = HexColor("#6D4C41");
        barImg.raycastTarget = false;
        bar.AddComponent<ThemeHeader>();
        return bar;
    }

    private static GameObject Layer(Transform p, string name, Sprite spr,
        float x0, float y0, float x1, float y1, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        if (spr != null) img.sprite = spr;
        img.color = c;
        img.preserveAspect = false;
        img.raycastTarget = false;
        return go;
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
        img.sprite = icon; img.preserveAspect = true; img.color = Color.white; img.raycastTarget = true;
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
