using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds and displays a dense, connected isometric journey map.
/// Islands overlap to form a continuous world. Vertical scrolling.
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

    // Base sizes (large — islands should dominate, not float in water)
    private const float BasePlatformW = 220f;
    private const float BasePlatformH = 180f;
    private const float GiftSize = 65f;
    private const float StarSize = 40f;
    private const float DecoSize = 75f;
    private const float BridgeThickness = 35f;
    private const float PlayerSize = 55f;

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

        // Calculate content bounds
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var n in nodes)
        {
            if (n.position.x < minX) minX = n.position.x;
            if (n.position.x > maxX) maxX = n.position.x;
            if (n.position.y < minY) minY = n.position.y;
            if (n.position.y > maxY) maxY = n.position.y;
        }

        float padX = 250f, padY = 300f;
        float contentW = (maxX - minX) + padX * 2;
        float contentH = (maxY - minY) + padY * 2;
        mapContent.sizeDelta = new Vector2(Mathf.Max(contentW, 1920), contentH);

        float offsetX = -minX + padX + (Mathf.Max(contentW, 1920) - contentW) * 0.5f;
        float offsetY = -minY + padY;

        // ── Layer 1: Bridges (behind everything) ──
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            var from = nodes[i];
            var to = nodes[i + 1];
            Vector2 a = NodeToUI(from.position, offsetX, offsetY);
            Vector2 b = NodeToUI(to.position, offsetX, offsetY);
            CreateBridge(a, b, from.bridgeIndex);
        }

        // ── Layer 2: Platforms + decorations + gifts (sorted by Y for depth) ──
        // Higher Y (further away) renders first — depth sorting
        var sortedIndices = new List<int>();
        for (int i = 0; i < nodes.Count; i++) sortedIndices.Add(i);
        sortedIndices.Sort((a, b) =>
        {
            // Render top-of-screen (high Y = small -position) first
            return nodes[a].position.y.CompareTo(nodes[b].position.y);
        });

        foreach (int i in sortedIndices)
        {
            var node = nodes[i];
            Vector2 pos = NodeToUI(node.position, offsetX, offsetY);
            CreateNode(node, pos, i);
        }

        // ── Layer 3: Player marker (on top) ──
        if (playerSprite != null && currentNodeIndex < nodes.Count)
        {
            var curNode = nodes[currentNodeIndex];
            Vector2 pPos = NodeToUI(curNode.position, offsetX, offsetY);
            var pGO = CreateImage("PlayerMarker", mapContent, playerSprite, PlayerSize, PlayerSize);
            var pRT = pGO.GetComponent<RectTransform>();
            pRT.anchoredPosition = pPos + new Vector2(0, 55 * curNode.platformScale);
        }
    }

    private void CreateNode(JourneyMapData.MapNode node, Vector2 pos, int index)
    {
        float s = node.platformScale;
        bool completed = index < currentNodeIndex;
        bool current = index == currentNodeIndex;

        // ── Platform ──
        int pIdx = Mathf.Clamp(node.platformIndex - 1, 0, platformSprites.Length - 1);
        float pw = BasePlatformW * s;
        float ph = BasePlatformH * s;

        var platGO = CreateImage($"Node_{index}", mapContent,
            pIdx < platformSprites.Length ? platformSprites[pIdx] : null, pw, ph);
        var platRT = platGO.GetComponent<RectTransform>();
        platRT.anchoredPosition = pos;
        platRT.localRotation = Quaternion.Euler(0, 0, node.rotation);

        var platImg = platGO.GetComponent<Image>();
        if (completed)
            platImg.color = new Color(0.75f, 0.75f, 0.75f, 0.85f);
        else if (current)
            platRT.localScale = Vector3.one * 1.12f;

        // ── Gift / Star overlay ──
        if ((node.type == JourneyMapData.NodeType.Gift || node.type == JourneyMapData.NodeType.BigReward)
            && giftSprite != null)
        {
            float gs = (node.type == JourneyMapData.NodeType.BigReward) ? GiftSize * 1.4f : GiftSize;
            var gGO = CreateImage("Gift", platGO.transform, giftSprite, gs, gs);
            gGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 30 * s);
            if (completed)
                gGO.GetComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        }
        else if (completed && starSprite != null)
        {
            var sGO = CreateImage("Star", platGO.transform, starSprite, StarSize, StarSize);
            sGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 25 * s);
            sGO.GetComponent<Image>().color = new Color(1f, 0.9f, 0.3f, 0.9f);
        }

        // ── Node number (on current + nearby) ──
        if (current || (index > currentNodeIndex && index <= currentNodeIndex + 3))
        {
            var numGO = new GameObject("Num");
            numGO.transform.SetParent(platGO.transform, false);
            var numRT = numGO.AddComponent<RectTransform>();
            numRT.anchoredPosition = new Vector2(0, 35 * s);
            numRT.sizeDelta = new Vector2(40, 30);
            var numTMP = numGO.AddComponent<TMPro.TextMeshProUGUI>();
            numTMP.text = (index + 1).ToString();
            numTMP.fontSize = current ? 22 : 16;
            numTMP.fontStyle = TMPro.FontStyles.Bold;
            numTMP.color = current ? Color.white : new Color(1, 1, 1, 0.7f);
            numTMP.alignment = TMPro.TextAlignmentOptions.Center;
            numTMP.raycastTarget = false;
        }

        // ── Decoration ──
        if (node.elementIndex > 0 && elementSprites != null)
        {
            int eIdx = Mathf.Clamp(node.elementIndex - 1, 0, elementSprites.Length - 1);
            if (eIdx < elementSprites.Length && elementSprites[eIdx] != null)
            {
                // Offset to side (alternate left/right asymmetrically)
                float sideX = ((index * 7 + 3) % 5 - 2) * 35f;
                float sideY = -20f + ((index * 3) % 4) * 8f;
                var dGO = CreateImage("Deco", platGO.transform, elementSprites[eIdx], DecoSize, DecoSize);
                dGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(sideX, sideY);
            }
        }
    }

    private void CreateBridge(Vector2 from, Vector2 to, int bridgeIdx)
    {
        if (elementSprites == null) return;
        int eIdx = Mathf.Clamp(bridgeIdx - 1, 0, elementSprites.Length - 1);
        if (eIdx >= elementSprites.Length || elementSprites[eIdx] == null) return;

        Vector2 mid = (from + to) * 0.5f;
        Vector2 diff = to - from;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        float dist = diff.magnitude;

        var go = CreateImage("Bridge", mapContent, elementSprites[eIdx],
            dist * 0.45f, BridgeThickness);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = mid;
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        go.GetComponent<Image>().color = new Color(1, 1, 1, 0.75f);
        go.transform.SetAsFirstSibling(); // behind platforms
    }

    // ── Helpers ──

    private Vector2 NodeToUI(Vector2 worldPos, float offsetX, float offsetY)
    {
        return new Vector2(worldPos.x + offsetX, -(worldPos.y + offsetY));
    }

    private GameObject CreateImage(string name, Transform parent, Sprite sprite, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
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

        float norm = 1f - Mathf.Clamp01((nodeY - minY) / range);
        scrollRect.verticalNormalizedPosition = norm;
    }

    public void OnBackPressed()
    {
        NavigationManager.GoToWorld();
    }
}
