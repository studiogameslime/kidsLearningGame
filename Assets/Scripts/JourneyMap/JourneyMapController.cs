using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the journey map following STRICT build order:
/// 1. Path positions (fixed from data)
/// 2. ONE bridge between each consecutive pair
/// 3. ONE platform per node
/// 4. Sparse decorations (last)
///
/// NO extra connectors. NO extra islands. NO clutter.
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

    // Bridge: element 03 (simple wood logs) — clean connector
    private const int BridgeElement = 2; // 0-indexed

    // Platform is the DOMINANT visual. Bridge is SUBTLE.
    private const float PlatW = 200f;
    private const float PlatH = 165f;
    private const float BridgeH = 22f;

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

        // Bounds
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var n in nodes)
        {
            minX = Mathf.Min(minX, n.position.x); maxX = Mathf.Max(maxX, n.position.x);
            minY = Mathf.Min(minY, n.position.y); maxY = Mathf.Max(maxY, n.position.y);
        }
        float pad = 250f;
        float cW = (maxX - minX) + pad * 2;
        float cH = Mathf.Max((maxY - minY) + pad * 2, 1080f);
        mapContent.sizeDelta = new Vector2(cW, cH);
        float offX = -minX + pad;
        float offY = -minY + cH * 0.5f; // center vertically

        // ══════════════════════════════════
        //  STEP 2: BRIDGES (one per pair, behind everything)
        // ══════════════════════════════════
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            Vector2 a = ToUI(nodes[i].position, offX, offY);
            Vector2 b = ToUI(nodes[i + 1].position, offX, offY);

            Vector2 mid = (a + b) * 0.5f;
            Vector2 diff = b - a;
            float dist = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            // Bridge spans the gap between platform edges
            float bridgeLen = Mathf.Max(dist - PlatW * 0.4f, 30f);

            int bIdx = BridgeElement;
            if (elementSprites != null && bIdx < elementSprites.Length && elementSprites[bIdx] != null)
            {
                var bGO = MakeImg("Bridge", mapContent, elementSprites[bIdx], bridgeLen, BridgeH);
                var bRT = bGO.GetComponent<RectTransform>();
                bRT.anchoredPosition = mid;
                bRT.localRotation = Quaternion.Euler(0, 0, angle);
                bGO.GetComponent<Image>().color = new Color(1, 1, 1, 0.6f);
                bGO.transform.SetAsFirstSibling();
            }
        }

        // ══════════════════════════════════
        //  STEP 3: PLATFORMS (one per node)
        // ══════════════════════════════════
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            Vector2 pos = ToUI(node.position, offX, offY);
            float s = node.platformScale;
            bool done = i < currentNode;
            bool cur = i == currentNode;

            int pIdx = Mathf.Clamp(node.platformIndex - 1, 0, platformSprites.Length - 1);
            var platGO = MakeImg($"Node{i}", mapContent, platformSprites[pIdx], PlatW * s, PlatH * s);
            var rt = platGO.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;

            if (done) platGO.GetComponent<Image>().color = new Color(0.75f, 0.75f, 0.75f, 0.85f);
            if (cur) rt.localScale = Vector3.one * 1.12f;

            // Gift (step 5)
            if (node.type != JourneyMapData.NodeType.Regular && giftSprite != null)
            {
                float gs = node.type == JourneyMapData.NodeType.BigReward ? 55f : 42f;
                var g = MakeImg("Gift", platGO.transform, giftSprite, gs, gs);
                g.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 22 * s);
                if (done) g.GetComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            }
            else if (done && starSprite != null)
            {
                var st = MakeImg("Star", platGO.transform, starSprite, 30, 30);
                st.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 18 * s);
                st.GetComponent<Image>().color = new Color(1f, 0.9f, 0.3f, 0.85f);
            }

            // Decoration (step 4 — sparse, controlled)
            if (node.hasDecoration && node.decoIndex > 0 && elementSprites != null)
            {
                int eIdx = Mathf.Clamp(node.decoIndex - 1, 0, elementSprites.Length - 1);
                if (elementSprites[eIdx] != null)
                {
                    float dx = (i % 2 == 0) ? 35f : -35f;
                    var d = MakeImg("Deco", platGO.transform, elementSprites[eIdx], 50, 50);
                    d.GetComponent<RectTransform>().anchoredPosition = new Vector2(dx, -12);
                }
            }
        }

        // ══════════════════════════════════
        //  STEP 6: PLAYER (topmost)
        // ══════════════════════════════════
        if (playerSprite != null && currentNode < nodes.Count)
        {
            var cn = nodes[currentNode];
            var p = MakeImg("Player", mapContent, playerSprite, 55, 55);
            p.GetComponent<RectTransform>().anchoredPosition =
                ToUI(cn.position, offX, offY) + new Vector2(0, 52 * cn.platformScale);
        }
    }

    // ── Helpers ──

    private Vector2 ToUI(Vector2 world, float ox, float oy) =>
        new Vector2(world.x + ox, -(world.y - oy));

    private GameObject MakeImg(string name, Transform parent, Sprite spr, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f); // left-center anchor
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
