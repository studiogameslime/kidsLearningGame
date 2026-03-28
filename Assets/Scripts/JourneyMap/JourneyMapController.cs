using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds and displays the journey map with 100 island nodes connected by bridges.
/// Shows player progress, gifts, and handles camera focus on current position.
/// </summary>
public class JourneyMapController : MonoBehaviour
{
    [Header("Map Container")]
    public RectTransform mapContent;
    public ScrollRect scrollRect;

    [Header("Assets")]
    public Sprite[] platformSprites;   // index 0-21 → Platforms 01-22
    public Sprite[] elementSprites;    // index 0-9  → Elements 01-10
    public Sprite giftSprite;
    public Sprite starSprite;
    public Sprite playerSprite;

    [Header("Sizing")]
    public float platformScale = 0.8f;
    public float giftScale = 0.5f;
    public float bridgeScale = 0.4f;
    public float playerScale = 0.6f;

    private List<JourneyMapData.MapNode> nodes;
    private List<GameObject> nodeObjects = new List<GameObject>();
    private GameObject playerMarker;
    private int currentNodeIndex;

    private void Start()
    {
        var profile = ProfileManager.ActiveProfile;
        currentNodeIndex = profile?.journey?.totalGamesCompleted ?? 0;
        currentNodeIndex = Mathf.Clamp(currentNodeIndex, 0, JourneyMapData.TotalNodes - 1);

        nodes = JourneyMapData.Generate();
        BuildMap();
        ScrollToNode(currentNodeIndex);
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

        float padding = 300f;
        float contentW = (maxX - minX) + padding * 2;
        float contentH = (maxY - minY) + padding * 2;
        mapContent.sizeDelta = new Vector2(contentW, contentH);

        // Center offset
        float offsetX = -minX + padding;
        float offsetY = -minY + padding;

        // Build bridges first (behind platforms)
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            var from = nodes[i];
            var to = nodes[i + 1];
            Vector2 posA = new Vector2(from.position.x + offsetX, from.position.y + offsetY);
            Vector2 posB = new Vector2(to.position.x + offsetX, to.position.y + offsetY);
            CreateBridge(posA, posB, from.bridgeIndex);
        }

        // Build nodes
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            Vector2 pos = new Vector2(node.position.x + offsetX, node.position.y + offsetY);
            var nodeGO = CreateNode(node, pos, i);
            nodeObjects.Add(nodeGO);
        }

        // Create player marker
        CreatePlayerMarker(offsetX, offsetY);
    }

    private GameObject CreateNode(JourneyMapData.MapNode node, Vector2 pos, int index)
    {
        var go = new GameObject($"Node_{index}");
        go.transform.SetParent(mapContent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); // top-left anchor
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(pos.x, -pos.y); // flip Y for UI

        // Platform sprite
        int pIdx = Mathf.Clamp(node.platformIndex - 1, 0, platformSprites.Length - 1);
        float scale = platformScale;
        if (node.type == JourneyMapData.NodeType.BigReward) scale *= 1.3f;

        var platformGO = new GameObject("Platform");
        platformGO.transform.SetParent(go.transform, false);
        var prt = platformGO.AddComponent<RectTransform>();
        prt.sizeDelta = new Vector2(200 * scale, 160 * scale);
        var pimg = platformGO.AddComponent<Image>();
        if (pIdx < platformSprites.Length && platformSprites[pIdx] != null)
            pimg.sprite = platformSprites[pIdx];
        pimg.preserveAspect = true;
        pimg.raycastTarget = false;

        // Visual state based on progress
        bool completed = index < currentNodeIndex;
        bool current = index == currentNodeIndex;
        bool future = index > currentNodeIndex;

        if (completed)
            pimg.color = new Color(0.7f, 0.7f, 0.7f, 0.8f); // dimmed
        else if (current)
            prt.localScale = Vector3.one * 1.15f; // highlighted
        // future = normal

        // Gift on gift/reward nodes
        if ((node.type == JourneyMapData.NodeType.Gift || node.type == JourneyMapData.NodeType.BigReward)
            && giftSprite != null)
        {
            var giftGO = new GameObject("Gift");
            giftGO.transform.SetParent(go.transform, false);
            var grt = giftGO.AddComponent<RectTransform>();
            float gs = giftScale;
            if (node.type == JourneyMapData.NodeType.BigReward) gs *= 1.4f;
            grt.sizeDelta = new Vector2(100 * gs, 100 * gs);
            grt.anchoredPosition = new Vector2(0, 40 * scale); // on top of platform
            var gimg = giftGO.AddComponent<Image>();
            gimg.sprite = giftSprite;
            gimg.preserveAspect = true;
            gimg.raycastTarget = false;

            if (completed)
                gimg.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        }

        // Star on regular completed nodes
        if (node.type == JourneyMapData.NodeType.Regular && completed && starSprite != null)
        {
            var starGO = new GameObject("Star");
            starGO.transform.SetParent(go.transform, false);
            var srt = starGO.AddComponent<RectTransform>();
            srt.sizeDelta = new Vector2(40, 40);
            srt.anchoredPosition = new Vector2(0, 30 * scale);
            var simg = starGO.AddComponent<Image>();
            simg.sprite = starSprite;
            simg.preserveAspect = true;
            simg.raycastTarget = false;
            simg.color = new Color(1f, 0.9f, 0.3f, 0.9f);
        }

        // Decorative element
        if (node.elementIndex > 0 && elementSprites != null)
        {
            int eIdx = Mathf.Clamp(node.elementIndex - 1, 0, elementSprites.Length - 1);
            if (eIdx < elementSprites.Length && elementSprites[eIdx] != null)
            {
                var decoGO = new GameObject("Deco");
                decoGO.transform.SetParent(go.transform, false);
                var drt = decoGO.AddComponent<RectTransform>();
                drt.sizeDelta = new Vector2(70, 70);
                // Offset to side of platform
                float sideX = (index % 2 == 0) ? 80f : -80f;
                drt.anchoredPosition = new Vector2(sideX, -10);
                var dimg = decoGO.AddComponent<Image>();
                dimg.sprite = elementSprites[eIdx];
                dimg.preserveAspect = true;
                dimg.raycastTarget = false;
            }
        }

        return go;
    }

    private void CreateBridge(Vector2 from, Vector2 to, int bridgeIdx)
    {
        if (elementSprites == null) return;
        int eIdx = Mathf.Clamp(bridgeIdx - 1, 0, elementSprites.Length - 1);
        if (eIdx >= elementSprites.Length || elementSprites[eIdx] == null) return;

        Vector2 mid = (from + to) * 0.5f;
        Vector2 diff = to - from;
        float angle = Mathf.Atan2(-diff.y, diff.x) * Mathf.Rad2Deg; // flip Y for UI coords
        float dist = diff.magnitude;

        var go = new GameObject("Bridge");
        go.transform.SetParent(mapContent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(mid.x, -mid.y);
        rt.sizeDelta = new Vector2(dist * 0.6f * bridgeScale, 60 * bridgeScale);
        rt.localRotation = Quaternion.Euler(0, 0, angle);

        var img = go.AddComponent<Image>();
        img.sprite = elementSprites[eIdx];
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = new Color(1, 1, 1, 0.7f);

        // Ensure bridges render behind nodes
        go.transform.SetAsFirstSibling();
    }

    private void CreatePlayerMarker(float offsetX, float offsetY)
    {
        if (playerSprite == null || currentNodeIndex >= nodes.Count) return;

        var node = nodes[currentNodeIndex];
        Vector2 pos = new Vector2(node.position.x + offsetX, node.position.y + offsetY);

        playerMarker = new GameObject("PlayerMarker");
        playerMarker.transform.SetParent(mapContent, false);
        var rt = playerMarker.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(pos.x, -pos.y + 50); // above platform
        rt.sizeDelta = new Vector2(80 * playerScale, 80 * playerScale);

        var img = playerMarker.AddComponent<Image>();
        img.sprite = playerSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
    }

    private void ScrollToNode(int nodeIndex)
    {
        if (scrollRect == null || nodes == null || nodeIndex >= nodes.Count) return;

        // Calculate vertical scroll position to center on current node
        float totalH = mapContent.sizeDelta.y;
        float viewH = scrollRect.GetComponent<RectTransform>().rect.height;
        if (totalH <= viewH) return;

        float nodeY = nodes[nodeIndex].position.y;
        // Normalize: 0 = top, 1 = bottom
        float minY = nodes[0].position.y;
        float maxY = nodes[nodes.Count - 1].position.y;
        float range = maxY - minY;
        if (range <= 0) return;

        float norm = 1f - Mathf.Clamp01((nodeY - minY) / range);
        scrollRect.verticalNormalizedPosition = norm;
    }

    // ── Navigation ──

    public void OnBackPressed()
    {
        NavigationManager.GoToWorld();
    }
}
