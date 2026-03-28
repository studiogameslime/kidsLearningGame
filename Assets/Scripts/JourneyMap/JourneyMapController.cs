using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the journey map following strict build order:
/// 1. Path positions (from JourneyMapData)
/// 2. Bridges between consecutive nodes
/// 3. Platform islands on each node
/// 4. Decorations (sparse, last)
///
/// Bridges are SMALL and SUBTLE. Platforms are BIG and dominant.
/// </summary>
public class JourneyMapController : MonoBehaviour
{
    [Header("Map Container")]
    public RectTransform mapContent;
    public ScrollRect scrollRect;

    [Header("Assets")]
    public Sprite[] platformSprites;
    public Sprite[] elementSprites;
    public Sprite giftSprite;
    public Sprite starSprite;
    public Sprite playerSprite;

    // Use element 03 (wood logs) — clean, simple, doesn't dominate
    private const int BridgeSpriteIdx = 2; // 0-indexed → element 03

    // Platforms: BIG (dominant visual). Bridges: SMALL (subtle connector).
    private const float PlatW = 195f;
    private const float PlatH = 160f;
    private const float BridgeMaxW = 70f;
    private const float BridgeH = 20f;

    private List<JourneyMapData.MapNode> nodes;
    private int currentNode;

    private void Start()
    {
        var profile = ProfileManager.ActiveProfile;
        currentNode = profile?.journey?.totalGamesCompleted ?? 0;
        currentNode = Mathf.Clamp(currentNode, 0, JourneyMapData.TotalNodes - 1);

        nodes = JourneyMapData.Generate();
        Build();
        ScrollToPlayer();
    }

    private void Build()
    {
        if (mapContent == null || platformSprites == null) return;

        // ── Bounds ──
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var n in nodes)
        {
            minX = Mathf.Min(minX, n.position.x); maxX = Mathf.Max(maxX, n.position.x);
            minY = Mathf.Min(minY, n.position.y); maxY = Mathf.Max(maxY, n.position.y);
        }
        float padX = 250f, padY = 250f;
        float cW = (maxX - minX) + padX * 2;
        float cH = Mathf.Max((maxY - minY) + padY * 2, 1080f);
        mapContent.sizeDelta = new Vector2(cW, cH);
        float offX = -minX + padX;
        float offY = padY;

        // ════════════════════════════════════
        //  BUILD ORDER 1: BRIDGES (behind)
        // ════════════════════════════════════
        int bIdx = (elementSprites != null && BridgeSpriteIdx < elementSprites.Length)
            ? BridgeSpriteIdx : 0;

        for (int i = 0; i < nodes.Count - 1; i++)
        {
            Vector2 a = UI(nodes[i].position, offX, offY);
            Vector2 b = UI(nodes[i + 1].position, offX, offY);
            PlaceBridge(a, b, bIdx);
        }

        // ════════════════════════════════════
        //  BUILD ORDER 2: PLATFORMS (depth sorted)
        // ════════════════════════════════════
        var order = new List<int>();
        for (int i = 0; i < nodes.Count; i++) order.Add(i);
        order.Sort((a, b) => nodes[a].position.y.CompareTo(nodes[b].position.y));

        foreach (int i in order)
            PlaceNode(nodes[i], UI(nodes[i].position, offX, offY), i);

        // ════════════════════════════════════
        //  BUILD ORDER 3: PLAYER (topmost)
        // ════════════════════════════════════
        if (playerSprite != null && currentNode < nodes.Count)
        {
            var cn = nodes[currentNode];
            var p = Img("Player", mapContent, playerSprite, 55, 55);
            p.GetComponent<RectTransform>().anchoredPosition =
                UI(cn.position, offX, offY) + new Vector2(0, 52 * cn.platformScale);
        }
    }

    // ── Build Step 1: Bridge ──
    private void PlaceBridge(Vector2 from, Vector2 to, int sprIdx)
    {
        if (elementSprites == null || sprIdx >= elementSprites.Length || elementSprites[sprIdx] == null) return;

        Vector2 mid = (from + to) * 0.5f;
        Vector2 diff = to - from;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        float dist = diff.magnitude;

        // Bridge sized to fill the gap — but capped to stay subtle
        float w = Mathf.Min(dist * 0.55f, BridgeMaxW);

        var go = Img("Br", mapContent, elementSprites[sprIdx], w, BridgeH);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = mid;
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        go.GetComponent<Image>().color = new Color(1, 1, 1, 0.55f);
        go.transform.SetAsFirstSibling(); // behind everything
    }

    // ── Build Step 2: Node ──
    private void PlaceNode(JourneyMapData.MapNode node, Vector2 pos, int idx)
    {
        float s = node.platformScale;
        bool done = idx < currentNode;
        bool cur = idx == currentNode;

        // Platform (dominant visual)
        int pIdx = Mathf.Clamp(node.platformIndex - 1, 0, platformSprites.Length - 1);
        var go = Img($"N{idx}", mapContent, platformSprites[pIdx], PlatW * s, PlatH * s);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;

        if (done) go.GetComponent<Image>().color = new Color(0.75f, 0.75f, 0.75f, 0.85f);
        if (cur) rt.localScale = Vector3.one * 1.1f;

        // Gift or star
        if (node.type != JourneyMapData.NodeType.Regular && giftSprite != null)
        {
            float gs = node.type == JourneyMapData.NodeType.BigReward ? 55f : 42f;
            var g = Img("G", go.transform, giftSprite, gs, gs);
            g.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 22 * s);
            if (done) g.GetComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        }
        else if (done && starSprite != null)
        {
            var st = Img("S", go.transform, starSprite, 30, 30);
            st.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 18 * s);
            st.GetComponent<Image>().color = new Color(1f, 0.9f, 0.3f, 0.85f);
        }

        // Build Step 4: Sparse decoration (last priority)
        if (node.hasDecoration && node.decoIndex > 0 && elementSprites != null)
        {
            int eIdx = Mathf.Clamp(node.decoIndex - 1, 0, elementSprites.Length - 1);
            if (elementSprites[eIdx] != null)
            {
                float dx = ((idx % 3) - 1) * 38f;
                var d = Img("D", go.transform, elementSprites[eIdx], 50, 50);
                d.GetComponent<RectTransform>().anchoredPosition = new Vector2(dx, -14);
            }
        }
    }

    // ── Helpers ──

    private Vector2 UI(Vector2 w, float ox, float oy) =>
        new Vector2(w.x + ox, -(w.y + oy));

    private GameObject Img(string n, Transform p, Sprite s, float w, float h)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        var img = go.AddComponent<Image>();
        img.sprite = s; img.preserveAspect = true; img.raycastTarget = false;
        return go;
    }

    private void ScrollToPlayer()
    {
        if (scrollRect == null || nodes == null || currentNode >= nodes.Count) return;
        float totalW = mapContent.sizeDelta.x;
        float viewW = scrollRect.GetComponent<RectTransform>().rect.width;
        if (totalW <= viewW) return;

        float nodeX = nodes[currentNode].position.x;
        float minX = float.MaxValue, maxX = float.MinValue;
        foreach (var n in nodes) { minX = Mathf.Min(minX, n.position.x); maxX = Mathf.Max(maxX, n.position.x); }
        float range = maxX - minX;
        if (range <= 0) return;

        scrollRect.horizontalNormalizedPosition = Mathf.Clamp01((nodeX - minX) / range);
    }

    public void OnBackPressed() => NavigationManager.GoToWorld();
}
