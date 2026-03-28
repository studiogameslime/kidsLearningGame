using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a 100-node journey map: dense but readable, clear path.
/// Snake path with soft curves, organized in clusters of ~6 nodes.
/// </summary>
public static class JourneyMapData
{
    public enum NodeType { Regular, Gift, BigReward }

    public struct MapNode
    {
        public int index;
        public Vector2 position;
        public NodeType type;
        public int platformIndex;     // 1-22
        public int decoIndex;         // 0=none, 1-10
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

        // Balanced spacing — not sparse, not stacked
        float stepX = 145f;
        float rowDrop = 180f;
        int nodesPerRow = 5;

        int row = 0;
        int col = 0;
        bool goingRight = true;
        int lastPlatform = -1;
        int sameCount = 0;

        for (int i = 0; i < TotalNodes; i++)
        {
            // Base position
            float baseX = col * stepX;
            float baseY = row * rowDrop;

            // Soft wave offset (organic, not chaotic)
            float waveX = Mathf.Sin(i * 0.5f) * 18f;
            float waveY = Mathf.Sin(i * 0.35f) * 12f;

            // Small random jitter
            float jitterX = (float)(rng.NextDouble() * 12 - 6);
            float jitterY = (float)(rng.NextDouble() * 10 - 5);

            float x = baseX + waveX + jitterX;
            float y = baseY + waveY + jitterY;

            // Add cluster gap every 6 nodes (slight extra Y spacing)
            if (i > 0 && i % 6 == 0)
                y += 25f;

            // Node type
            NodeType type = NodeType.Regular;
            if (i > 0 && i % 15 == 0) type = NodeType.BigReward;
            else if (i > 0 && i % 5 == 0) type = NodeType.Gift;

            // Platform selection
            int platformIdx;
            float pScale;

            if (type == NodeType.BigReward || (i > 0 && i % 12 == 0))
            {
                platformIdx = LargePlatforms[rng.Next(LargePlatforms.Length)];
                pScale = 1.15f;
            }
            else if (type == NodeType.Gift)
            {
                platformIdx = MediumPlatforms[rng.Next(MediumPlatforms.Length)];
                pScale = 1.0f;
            }
            else
            {
                int[] pool = (rng.Next(3) < 2) ? SmallPlatforms : MediumPlatforms;
                platformIdx = pool[rng.Next(pool.Length)];
                pScale = 0.85f + (float)rng.NextDouble() * 0.15f;
            }

            // Prevent >2 same in a row
            if (platformIdx == lastPlatform)
            {
                sameCount++;
                if (sameCount >= 2)
                {
                    int[] any = (rng.Next(2) == 0) ? SmallPlatforms : MediumPlatforms;
                    while (platformIdx == lastPlatform)
                        platformIdx = any[rng.Next(any.Length)];
                    sameCount = 0;
                }
            }
            else sameCount = 0;
            lastPlatform = platformIdx;

            // Decoration: sparse — every 3-4 nodes, never on gift/reward nodes
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
                position = new Vector2(x, y),
                type = type,
                platformIndex = platformIdx,
                decoIndex = decoIdx,
                platformScale = pScale,
                hasDecoration = hasDeco,
            });

            // Snake movement
            if (goingRight)
            {
                col++;
                if (col >= nodesPerRow) { col = nodesPerRow - 1; row++; goingRight = false; }
            }
            else
            {
                col--;
                if (col < 0) { col = 0; row++; goingRight = true; }
            }
        }

        return nodes;
    }
}
