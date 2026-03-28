using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates 100 nodes along ONE continuous flowing path.
/// Path flows LEFT → RIGHT in gentle curves, with soft downward drift.
/// NOT a grid. NOT rows. A single organic river-like path.
/// </summary>
public static class JourneyMapData
{
    public enum NodeType { Regular, Gift, BigReward }

    public struct MapNode
    {
        public int index;
        public Vector2 position;
        public NodeType type;
        public int platformIndex;
        public int decoIndex;
        public float platformScale;
        public bool hasDecoration;
    }

    public const int TotalNodes = 100;

    private static readonly int[] SmallPlatforms = { 1, 2, 3, 4, 20, 21, 22 };
    private static readonly int[] MediumPlatforms = { 6, 7, 8, 9, 16, 17, 18, 19 };
    private static readonly int[] LargePlatforms = { 10, 11, 12, 13, 14 };
    private static readonly int[] DecoElements = { 1, 4, 7, 8, 9, 10 };

    public static List<MapNode> Generate()
    {
        var nodes = new List<MapNode>();
        var rng = new System.Random(42);

        // Path parameters
        float stepSize = 135f;        // distance between nodes along the path
        float pathWidth = 1400f;      // horizontal span before curving back
        float curveAmplitude = 40f;   // vertical wave amplitude
        float rowGap = 220f;          // vertical drop between left→right passes

        // Walk along a flowing path
        float x = 80f;               // start near left edge
        float y = 0f;
        float direction = 1f;        // 1 = going right, -1 = going left
        int lastPlatform = -1;
        int sameCount = 0;

        for (int i = 0; i < TotalNodes; i++)
        {
            // Organic Y wobble (sine wave + random jitter)
            float wobbleY = Mathf.Sin(i * 0.6f) * curveAmplitude;
            float jitterY = (float)(rng.NextDouble() * 20 - 10);
            float jitterX = (float)(rng.NextDouble() * 15 - 7);

            float nodeX = x + jitterX;
            float nodeY = y + wobbleY + jitterY;

            // Node type
            NodeType type = NodeType.Regular;
            if (i > 0 && i % 15 == 0) type = NodeType.BigReward;
            else if (i > 0 && i % 5 == 0) type = NodeType.Gift;

            // Platform
            int platIdx;
            float pScale;
            if (type == NodeType.BigReward || (i > 0 && i % 12 == 0))
            {
                platIdx = LargePlatforms[rng.Next(LargePlatforms.Length)];
                pScale = 1.15f;
            }
            else if (type == NodeType.Gift)
            {
                platIdx = MediumPlatforms[rng.Next(MediumPlatforms.Length)];
                pScale = 1.0f;
            }
            else
            {
                int[] pool = rng.Next(3) < 2 ? SmallPlatforms : MediumPlatforms;
                platIdx = pool[rng.Next(pool.Length)];
                pScale = 0.85f + (float)rng.NextDouble() * 0.15f;
            }

            // No >2 repeats
            if (platIdx == lastPlatform) { sameCount++; if (sameCount >= 2) { int[] any = SmallPlatforms; while (platIdx == lastPlatform) platIdx = any[rng.Next(any.Length)]; sameCount = 0; } }
            else sameCount = 0;
            lastPlatform = platIdx;

            // Sparse decoration
            bool hasDeco = false;
            int decoIdx = 0;
            if (type == NodeType.Regular && i % 4 == 2 && rng.Next(3) != 0)
            {
                hasDeco = true;
                decoIdx = DecoElements[rng.Next(DecoElements.Length)];
            }

            nodes.Add(new MapNode
            {
                index = i,
                position = new Vector2(nodeX, nodeY),
                type = type,
                platformIndex = platIdx,
                decoIndex = decoIdx,
                platformScale = pScale,
                hasDecoration = hasDeco,
            });

            // Advance along the path
            x += stepSize * direction;

            // Check if we need to curve to the next "row"
            if (direction > 0 && x > pathWidth)
            {
                // Reached right edge → curve down, reverse direction
                direction = -1f;
                y += rowGap;
                // Don't snap — keep x where it is, just reverse
            }
            else if (direction < 0 && x < 80f)
            {
                // Reached left edge → curve down, reverse direction
                direction = 1f;
                y += rowGap;
            }
        }

        return nodes;
    }
}
