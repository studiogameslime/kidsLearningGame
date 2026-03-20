using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared wood-table background builder for game scenes.
/// Creates a warm wooden play-table surface with planks, grain, vignettes, and board rim.
/// Used by MemoryGame, PatternCopy, LetterGame, NumberMaze, OddOneOut, QuantityMatch, ConnectMatch.
/// </summary>
public static class WoodTableBackground
{
    // Wood palette
    public static readonly Color TableBaseColor = HexColor("#5C3D2E");
    public static readonly Color BoardWoodA = HexColor("#8B6B4A");
    public static readonly Color BoardWoodB = HexColor("#7E6042");
    public static readonly Color PlankSepColor = HexColor("#5A4030");
    public static readonly Color BoardEdgeColor = HexColor("#6B4D38");
    public static readonly Color BoardInnerRimColor = HexColor("#A08060");
    public static readonly Color HeaderColor = new Color(0.30f, 0.20f, 0.12f, 0.75f);
    public static readonly Color TitleTextColor = new Color(1f, 0.96f, 0.88f, 1f);

    /// <summary>
    /// Creates the full wood background layers on the canvas: base color + vignettes.
    /// Call this right after creating the canvas, before SafeArea.
    /// </summary>
    public static void CreateBackground(Transform canvasRoot)
    {
        var bg = StretchImg(canvasRoot, "Background", TableBaseColor);
        bg.GetComponent<Image>().raycastTarget = false;
        CreateVignette(canvasRoot, "VignetteTop", true);
        CreateVignette(canvasRoot, "VignetteBottom", false);
    }

    /// <summary>
    /// Creates a wood board panel with rim, planks, and grain.
    /// Returns the inner content transform where gameplay elements should be placed.
    /// </summary>
    public static RectTransform CreateBoardPanel(Transform parent, Sprite roundedRect,
        float anchorMinX = 0.02f, float anchorMinY = 0.02f,
        float anchorMaxX = 0.98f, float anchorMaxY = 0.88f,
        int contentPadding = 10)
    {
        var boardGO = new GameObject("BoardPanel");
        boardGO.transform.SetParent(parent, false);
        var boardRT = boardGO.AddComponent<RectTransform>();
        boardRT.anchorMin = new Vector2(anchorMinX, anchorMinY);
        boardRT.anchorMax = new Vector2(anchorMaxX, anchorMaxY);
        boardRT.offsetMin = Vector2.zero;
        boardRT.offsetMax = Vector2.zero;

        // Outer rim
        var boardImg = boardGO.AddComponent<Image>();
        boardImg.sprite = roundedRect;
        boardImg.type = Image.Type.Sliced;
        boardImg.color = BoardEdgeColor;
        boardImg.raycastTarget = false;

        // Shadow
        var shadow = boardGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.12f, 0.06f, 0.02f, 0.5f);
        shadow.effectDistance = new Vector2(5, -5);

        // Inner rim highlight
        var rimGO = new GameObject("InnerRim");
        rimGO.transform.SetParent(boardGO.transform, false);
        var rimRT = rimGO.AddComponent<RectTransform>();
        Full(rimRT);
        rimRT.offsetMin = new Vector2(3, 3);
        rimRT.offsetMax = new Vector2(-3, -3);
        var rimImg = rimGO.AddComponent<Image>();
        rimImg.sprite = roundedRect;
        rimImg.type = Image.Type.Sliced;
        rimImg.color = BoardInnerRimColor;
        rimImg.raycastTarget = false;

        // Wood plank surface
        var woodSurface = new GameObject("WoodSurface");
        woodSurface.transform.SetParent(boardGO.transform, false);
        var woodSurfaceRT = woodSurface.AddComponent<RectTransform>();
        Full(woodSurfaceRT);
        woodSurfaceRT.offsetMin = new Vector2(6, 6);
        woodSurfaceRT.offsetMax = new Vector2(-6, -6);
        CreatePlanks(woodSurface.transform);

        // Content area on top of wood
        var contentGO = new GameObject("BoardContent");
        contentGO.transform.SetParent(boardGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        Full(contentRT);
        contentRT.offsetMin = new Vector2(contentPadding, contentPadding);
        contentRT.offsetMax = new Vector2(-contentPadding, -contentPadding);

        return contentRT;
    }

    private static void CreatePlanks(Transform parent)
    {
        int plankCount = 6;
        Color[] colors = {
            BoardWoodA, BoardWoodB,
            Color.Lerp(BoardWoodA, BoardWoodB, 0.3f),
            BoardWoodA,
            Color.Lerp(BoardWoodB, BoardWoodA, 0.4f),
            BoardWoodB
        };

        for (int i = 0; i < plankCount; i++)
        {
            float yMin = (float)i / plankCount;
            float yMax = (float)(i + 1) / plankCount;

            var plank = new GameObject($"Plank_{i}");
            plank.transform.SetParent(parent, false);
            var rt = plank.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, yMin);
            rt.anchorMax = new Vector2(1, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = plank.AddComponent<Image>();
            img.color = colors[i % colors.Length];
            img.raycastTarget = false;

            // Grain line (dark)
            var grain = new GameObject($"Grain_{i}");
            grain.transform.SetParent(plank.transform, false);
            var grt = grain.AddComponent<RectTransform>();
            grt.anchorMin = new Vector2(0.02f, 0.4f);
            grt.anchorMax = new Vector2(0.98f, 0.45f);
            grt.offsetMin = Vector2.zero;
            grt.offsetMax = Vector2.zero;
            grain.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.06f);
            grain.GetComponent<Image>().raycastTarget = false;

            // Grain line (highlight)
            var grain2 = new GameObject($"Grain2_{i}");
            grain2.transform.SetParent(plank.transform, false);
            var g2rt = grain2.AddComponent<RectTransform>();
            g2rt.anchorMin = new Vector2(0.05f, 0.65f);
            g2rt.anchorMax = new Vector2(0.95f, 0.69f);
            g2rt.offsetMin = Vector2.zero;
            g2rt.offsetMax = Vector2.zero;
            grain2.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.04f);
            grain2.GetComponent<Image>().raycastTarget = false;

            // Separator
            if (i > 0)
            {
                var sep = new GameObject($"Sep_{i}");
                sep.transform.SetParent(parent, false);
                var srt = sep.AddComponent<RectTransform>();
                srt.anchorMin = new Vector2(0, yMin);
                srt.anchorMax = new Vector2(1, yMin);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.sizeDelta = new Vector2(0, 2f);
                srt.anchoredPosition = Vector2.zero;
                sep.AddComponent<Image>().color = PlankSepColor;
                sep.GetComponent<Image>().raycastTarget = false;
            }
        }
    }

    private static void CreateVignette(Transform parent, string name, bool isTop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        if (isTop)
        {
            rt.anchorMin = new Vector2(0, 0.88f);
            rt.anchorMax = new Vector2(1, 1);
        }
        else
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0.12f);
        }
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.08f, 0.03f, 0.2f);
        img.raycastTarget = false;
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

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
