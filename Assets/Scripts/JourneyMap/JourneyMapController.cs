using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a BIG, CENTERED, READABLE journey path.
///
/// Build order:
/// 1. Bridges (behind) — thick, visible connectors
/// 2. Platforms (big, dominant)
/// 3. Gifts/stars (on platforms)
/// 4. Player marker (on top)
///
/// NO decorations. Path clarity is everything.
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

    // Visual sizes — LARGE for readability
    private const float PlatW = 260f;
    private const float PlatH = 210f;
    private const float BridgeH = 40f;    // thick, clearly visible bridge
    private const float GiftSize = 65f;
    private const float StarSize = 42f;
    private const float PlayerSize = 70f;

    // Bridge: element 06 (simple rope bridge — clean and readable)
    private const int BridgeElement = 5;

    private List<JourneyMapData.MapNode> nodes;
    private int currentNode;

    private void Start()
    {
        var profile = ProfileManager.ActiveProfile;
        currentNode = profile?.journey?.totalGamesCompleted ?? 0;
        currentNode = Mathf.Clamp(currentNode, 0, JourneyMapData.TotalNodes - 1);

        nodes = JourneyMapData.Generate();
        BuildMap();
        ScrollToPlayer();
    }

    private void BuildMap()
    {
        if (mapContent == null || platformSprites == null) return;

        // ── Calculate bounds ──
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var n in nodes)
        {
            minX = Mathf.Min(minX, n.position.x); maxX = Mathf.Max(maxX, n.position.x);
            minY = Mathf.Min(minY, n.position.y); maxY = Mathf.Max(maxY, n.position.y);
        }

        float padX = 300f;
        float contentW = (maxX - minX) + padX * 2;

        // Content fills viewport height — path centered vertically
        float viewH = scrollRect != null
            ? scrollRect.GetComponent<RectTransform>().rect.height
            : 900f;
        if (viewH <= 0) viewH = 900f;

        mapContent.sizeDelta = new Vector2(contentW, viewH);

        // Offsets to center the path in the viewport
        float offX = -minX + padX;
        float centerY = (minY + maxY) * 0.5f;

        // ══════════════════════════════════
        //  STEP 1: BRIDGES — thick, visible, behind platforms
        // ══════════════════════════════════
        int bIdx = (elementSprites != null && BridgeElement < elementSprites.Length)
            ? BridgeElement : 0;

        for (int i = 0; i < nodes.Count - 1; i++)
        {
            Vector2 a = ToUI(nodes[i].position, offX, centerY, viewH);
            Vector2 b = ToUI(nodes[i + 1].position, offX, centerY, viewH);

            Vector2 mid = (a + b) * 0.5f;
            Vector2 diff = b - a;
            float dist = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            // Bridge spans gap between platform edges
            float bridgeLen = Mathf.Max(dist - PlatW * 0.35f, 40f);

            if (elementSprites != null && bIdx < elementSprites.Length && elementSprites[bIdx] != null)
            {
                var bGO = MakeImg("Bridge", mapContent, elementSprites[bIdx], bridgeLen, BridgeH);
                var bRT = bGO.GetComponent<RectTransform>();
                bRT.anchoredPosition = mid;
                bRT.localRotation = Quaternion.Euler(0, 0, angle);
                bGO.GetComponent<Image>().color = new Color(1, 1, 1, 0.7f);
                bGO.transform.SetAsFirstSibling(); // behind platforms
            }
        }

        // ══════════════════════════════════
        //  STEP 2: PLATFORMS — big, dominant, clear
        // ══════════════════════════════════
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            Vector2 pos = ToUI(node.position, offX, centerY, viewH);
            float s = node.platformScale;
            bool done = i < currentNode;
            bool cur = i == currentNode;

            int pIdx = Mathf.Clamp(node.platformIndex - 1, 0, platformSprites.Length - 1);
            var platGO = MakeImg($"Node{i}", mapContent, platformSprites[pIdx], PlatW * s, PlatH * s);
            var rt = platGO.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;

            if (done) platGO.GetComponent<Image>().color = new Color(0.72f, 0.72f, 0.72f, 0.8f);
            if (cur) rt.localScale = Vector3.one * 1.15f;

            // ── STEP 3: Gifts / Stars ──
            if (node.type != JourneyMapData.NodeType.Regular && giftSprite != null)
            {
                float gs = node.type == JourneyMapData.NodeType.BigReward ? GiftSize * 1.2f : GiftSize;
                var g = MakeImg("Gift", platGO.transform, giftSprite, gs, gs);
                g.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 30 * s);
                if (done) g.GetComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            }
            else if (done && starSprite != null)
            {
                var st = MakeImg("Star", platGO.transform, starSprite, StarSize, StarSize);
                st.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 24 * s);
                st.GetComponent<Image>().color = new Color(1f, 0.9f, 0.3f, 0.85f);
            }
        }

        // ══════════════════════════════════
        //  STEP 4: PLAYER — big, on top, clearly visible
        // ══════════════════════════════════
        if (playerSprite != null && currentNode < nodes.Count)
        {
            var cn = nodes[currentNode];
            Vector2 pp = ToUI(cn.position, offX, centerY, viewH);
            var pGO = MakeImg("Player", mapContent, playerSprite, PlayerSize, PlayerSize);
            pGO.GetComponent<RectTransform>().anchoredPosition = pp + new Vector2(0, 65 * cn.platformScale);
        }
    }

    // ── Coordinate conversion: world → UI ──
    // Centers path vertically in the viewport
    private Vector2 ToUI(Vector2 world, float offX, float centerY, float viewH)
    {
        float x = world.x + offX;
        float y = viewH * 0.5f - (world.y - centerY); // center of viewport + offset from path center
        return new Vector2(x, -y + viewH); // flip for UI top-left origin
    }

    private GameObject MakeImg(string name, Transform parent, Sprite spr, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        var img = go.AddComponent<Image>();
        img.sprite = spr; img.preserveAspect = true; img.raycastTarget = false;
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
