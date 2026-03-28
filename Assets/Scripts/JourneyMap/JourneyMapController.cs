using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the journey map: ONE clear path flowing left→right with curves.
/// Horizontal scrolling. Path priority over decoration.
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

    [Header("Bridge")]
    public int bridgeSpriteIndex = 5; // consistent bridge style

    private const float PlatW = 160f;
    private const float PlatH = 130f;

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

        float padX = 200f, padY = 200f;
        float cW = (maxX - minX) + padX * 2;
        float cH = Mathf.Max((maxY - minY) + padY * 2, 1080f);
        mapContent.sizeDelta = new Vector2(cW, cH);

        float offX = -minX + padX;
        float offY = padY;

        // ── Bridges ──
        int bIdx = Mathf.Clamp(bridgeSpriteIndex - 1, 0,
            elementSprites != null ? elementSprites.Length - 1 : 0);

        for (int i = 0; i < nodes.Count - 1; i++)
        {
            Vector2 a = ToUI(nodes[i].position, offX, offY);
            Vector2 b = ToUI(nodes[i + 1].position, offX, offY);
            DrawBridge(a, b, bIdx);
        }

        // ── Nodes (depth sorted) ──
        var order = new List<int>();
        for (int i = 0; i < nodes.Count; i++) order.Add(i);
        order.Sort((a, b) => nodes[a].position.y.CompareTo(nodes[b].position.y));

        foreach (int i in order)
            DrawNode(nodes[i], ToUI(nodes[i].position, offX, offY), i);

        // ── Player ──
        if (playerSprite != null && currentNode < nodes.Count)
        {
            var cn = nodes[currentNode];
            var p = Img("Player", mapContent, playerSprite, 50, 50);
            p.GetComponent<RectTransform>().anchoredPosition =
                ToUI(cn.position, offX, offY) + new Vector2(0, 48 * cn.platformScale);
        }
    }

    private void DrawNode(JourneyMapData.MapNode node, Vector2 pos, int idx)
    {
        float s = node.platformScale;
        bool done = idx < currentNode;
        bool cur = idx == currentNode;

        int pIdx = Mathf.Clamp(node.platformIndex - 1, 0, platformSprites.Length - 1);
        var go = Img($"N{idx}", mapContent, platformSprites[pIdx], PlatW * s, PlatH * s);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;

        if (done) go.GetComponent<Image>().color = new Color(0.75f, 0.75f, 0.75f, 0.85f);
        if (cur) rt.localScale = Vector3.one * 1.1f;

        // Gift or star
        if (node.type != JourneyMapData.NodeType.Regular && giftSprite != null)
        {
            float gs = node.type == JourneyMapData.NodeType.BigReward ? 52f : 40f;
            var g = Img("G", go.transform, giftSprite, gs, gs);
            g.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 22 * s);
            if (done) g.GetComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        }
        else if (done && starSprite != null)
        {
            var st = Img("S", go.transform, starSprite, 28, 28);
            st.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 18 * s);
            st.GetComponent<Image>().color = new Color(1f, 0.9f, 0.3f, 0.85f);
        }

        // Sparse decoration
        if (node.hasDecoration && node.decoIndex > 0 && elementSprites != null)
        {
            int eIdx = Mathf.Clamp(node.decoIndex - 1, 0, elementSprites.Length - 1);
            if (elementSprites[eIdx] != null)
            {
                float dx = ((idx % 3) - 1) * 35f;
                var d = Img("D", go.transform, elementSprites[eIdx], 50, 50);
                d.GetComponent<RectTransform>().anchoredPosition = new Vector2(dx, -12);
            }
        }
    }

    private void DrawBridge(Vector2 from, Vector2 to, int sprIdx)
    {
        if (elementSprites == null || sprIdx >= elementSprites.Length || elementSprites[sprIdx] == null) return;

        Vector2 mid = (from + to) * 0.5f;
        Vector2 diff = to - from;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        float dist = diff.magnitude;

        var go = Img("Br", mapContent, elementSprites[sprIdx], dist * 0.45f, 26f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = mid;
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        go.GetComponent<Image>().color = new Color(1, 1, 1, 0.6f);
        go.transform.SetAsFirstSibling();
    }

    private Vector2 ToUI(Vector2 w, float ox, float oy) =>
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
        float minX = nodes[0].position.x;
        float maxX = float.MinValue;
        foreach (var n in nodes) maxX = Mathf.Max(maxX, n.position.x);
        float range = maxX - minX;
        if (range <= 0) return;

        scrollRect.horizontalNormalizedPosition = Mathf.Clamp01((nodeX - minX) / range);
    }

    public void OnBackPressed() => NavigationManager.GoToWorld();
}
