using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the journey map: clear readable path, balanced density.
/// Priority: 1) Path clarity 2) Player + gifts 3) Decorations.
/// Uses ONE consistent bridge style. Sparse decorations.
/// </summary>
public class JourneyMapController : MonoBehaviour
{
    [Header("Map Container")]
    public RectTransform mapContent;
    public ScrollRect scrollRect;

    [Header("Assets")]
    public Sprite[] platformSprites;   // 0-21 → Platforms 01-22
    public Sprite[] elementSprites;    // 0-9  → Elements 01-10
    public Sprite giftSprite;
    public Sprite starSprite;
    public Sprite playerSprite;

    [Header("Bridge Style")]
    public int mainBridgeIndex = 5;    // element index used for most bridges (05 = rope bridge)

    private const float PlatformW = 170f;
    private const float PlatformH = 140f;
    private const float BridgeW = 55f;
    private const float BridgeH = 28f;

    private List<JourneyMapData.MapNode> nodes;
    private int currentNodeIndex;

    private void Start()
    {
        var profile = ProfileManager.ActiveProfile;
        currentNodeIndex = profile?.journey?.totalGamesCompleted ?? 0;
        currentNodeIndex = Mathf.Clamp(currentNodeIndex, 0, JourneyMapData.TotalNodes - 1);

        nodes = JourneyMapData.Generate();
        BuildMap();
        ScrollToCurrentNode();
    }

    private void BuildMap()
    {
        if (mapContent == null || platformSprites == null) return;

        // Content bounds
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var n in nodes)
        {
            if (n.position.x < minX) minX = n.position.x;
            if (n.position.x > maxX) maxX = n.position.x;
            if (n.position.y < minY) minY = n.position.y;
            if (n.position.y > maxY) maxY = n.position.y;
        }

        float padX = 200f, padY = 250f;
        float contentW = Mathf.Max((maxX - minX) + padX * 2, 1920f);
        float contentH = (maxY - minY) + padY * 2;
        mapContent.sizeDelta = new Vector2(contentW, contentH);

        float offsetX = -minX + (contentW - (maxX - minX)) * 0.5f;
        float offsetY = padY;

        // ── PASS 1: Bridges (ONE consistent style, behind everything) ──
        int bridgeSpriteIdx = Mathf.Clamp(mainBridgeIndex - 1, 0,
            elementSprites != null ? elementSprites.Length - 1 : 0);

        for (int i = 0; i < nodes.Count - 1; i++)
        {
            Vector2 a = ToUI(nodes[i].position, offsetX, offsetY);
            Vector2 b = ToUI(nodes[i + 1].position, offsetX, offsetY);
            CreateBridge(a, b, bridgeSpriteIdx);
        }

        // ── PASS 2: Platforms (depth-sorted: higher Y first) ──
        var sorted = new List<int>();
        for (int i = 0; i < nodes.Count; i++) sorted.Add(i);
        sorted.Sort((a, b) => nodes[a].position.y.CompareTo(nodes[b].position.y));

        foreach (int i in sorted)
        {
            Vector2 pos = ToUI(nodes[i].position, offsetX, offsetY);
            CreateNode(nodes[i], pos, i);
        }

        // ── PASS 3: Player marker (topmost) ──
        if (playerSprite != null && currentNodeIndex < nodes.Count)
        {
            var cn = nodes[currentNodeIndex];
            Vector2 pp = ToUI(cn.position, offsetX, offsetY);
            var pGO = MakeImg("Player", mapContent, playerSprite, 50, 50);
            pGO.GetComponent<RectTransform>().anchoredPosition = pp + new Vector2(0, 50 * cn.platformScale);
        }
    }

    private void CreateNode(JourneyMapData.MapNode node, Vector2 pos, int index)
    {
        float s = node.platformScale;
        bool completed = index < currentNodeIndex;
        bool current = index == currentNodeIndex;

        // Platform
        int pIdx = Mathf.Clamp(node.platformIndex - 1, 0, platformSprites.Length - 1);
        var platGO = MakeImg($"N{index}", mapContent,
            platformSprites[pIdx], PlatformW * s, PlatformH * s);
        var rt = platGO.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;

        var img = platGO.GetComponent<Image>();
        if (completed) img.color = new Color(0.75f, 0.75f, 0.75f, 0.85f);
        if (current) rt.localScale = Vector3.one * 1.1f;

        // Gift or star
        if (node.type != JourneyMapData.NodeType.Regular && giftSprite != null)
        {
            float gs = node.type == JourneyMapData.NodeType.BigReward ? 55f : 42f;
            var gGO = MakeImg("Gift", platGO.transform, giftSprite, gs, gs);
            gGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 25 * s);
            if (completed) gGO.GetComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        }
        else if (completed && starSprite != null)
        {
            var sGO = MakeImg("Star", platGO.transform, starSprite, 30, 30);
            sGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 20 * s);
            sGO.GetComponent<Image>().color = new Color(1f, 0.9f, 0.3f, 0.85f);
        }

        // Sparse decoration (max 1 per island, only some islands)
        if (node.hasDecoration && node.decoIndex > 0 && elementSprites != null)
        {
            int eIdx = Mathf.Clamp(node.decoIndex - 1, 0, elementSprites.Length - 1);
            if (elementSprites[eIdx] != null)
            {
                float dx = ((index % 3) - 1) * 40f; // left/center/right
                var dGO = MakeImg("Deco", platGO.transform, elementSprites[eIdx], 55, 55);
                dGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(dx, -15);
            }
        }
    }

    private void CreateBridge(Vector2 from, Vector2 to, int spriteIdx)
    {
        if (elementSprites == null || spriteIdx >= elementSprites.Length) return;
        if (elementSprites[spriteIdx] == null) return;

        Vector2 mid = (from + to) * 0.5f;
        Vector2 diff = to - from;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        float dist = diff.magnitude;

        // Scale bridge length to distance, keep consistent thickness
        float w = Mathf.Max(dist * 0.5f, BridgeW);
        var go = MakeImg("Br", mapContent, elementSprites[spriteIdx], w, BridgeH);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = mid;
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        go.GetComponent<Image>().color = new Color(1, 1, 1, 0.65f);
        go.transform.SetAsFirstSibling();
    }

    // ── Helpers ──

    private Vector2 ToUI(Vector2 world, float offX, float offY)
    {
        return new Vector2(world.x + offX, -(world.y + offY));
    }

    private GameObject MakeImg(string name, Transform parent, Sprite spr, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        var img = go.AddComponent<Image>();
        img.sprite = spr;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return go;
    }

    private void ScrollToCurrentNode()
    {
        if (scrollRect == null || nodes == null || currentNodeIndex >= nodes.Count) return;
        float totalH = mapContent.sizeDelta.y;
        float viewH = scrollRect.GetComponent<RectTransform>().rect.height;
        if (totalH <= viewH) return;

        float nodeY = nodes[currentNodeIndex].position.y;
        float minY = nodes[0].position.y;
        float maxY = nodes[nodes.Count - 1].position.y;
        float range = maxY - minY;
        if (range <= 0) return;

        scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01((nodeY - minY) / range);
    }

    public void OnBackPressed() => NavigationManager.GoToWorld();
}
