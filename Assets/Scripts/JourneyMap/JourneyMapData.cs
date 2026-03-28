using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates the 100-node journey map as a dense, organic isometric path.
/// The path snakes diagonally creating a connected island world.
/// Islands overlap and cluster — minimal water visible.
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
        public int elementIndex;      // 0=none, 1-10
        public int bridgeIndex;       // 1-10 element for connector
        public float platformScale;   // size multiplier
        public float rotation;        // slight tilt
    }

    public const int TotalNodes = 100;

    // Platform pools
    private static readonly int[] SmallPlatforms = { 1, 2, 3, 4, 20, 21, 22 };
    private static readonly int[] MediumPlatforms = { 6, 7, 8, 9, 16, 17, 18, 19 };
    private static readonly int[] LargePlatforms = { 10, 11, 12, 13, 14 };
    private static readonly int[] BridgeElements = { 2, 3, 5, 6 };
    private static readonly int[] DecoElements = { 1, 4, 7, 8, 9, 10 };

    public static List<MapNode> Generate()
    {
        var nodes = new List<MapNode>();
        var rng = new System.Random(42);

        // Dense isometric spacing — islands nearly touching
        float stepX = 105f;   // horizontal gap (tight)
        float stepY = 85f;    // vertical drop per step
        float rowDrop = 140f; // extra drop when changing direction

        int nodesPerRow = 6;
        int row = 0;
        int col = 0;
        bool goingRight = true;
        int lastPlatform = -1;
        int sameCount = 0;

        for (int i = 0; i < TotalNodes; i++)
        {
            // Base position — diagonal isometric feel
            float baseX = col * stepX;
            float baseY = row * rowDrop + col * stepY * 0.3f; // slight diagonal slope

            // Organic offset (wave + random)
            float waveX = Mathf.Sin(i * 0.4f) * 25f;
            float waveY = Mathf.Cos(i * 0.3f) * 15f;
            float randX = (float)(rng.NextDouble() * 20 - 10);
            float randY = (float)(rng.NextDouble() * 16 - 8);

            float x = baseX + waveX + randX;
            float y = baseY + waveY + randY;

            // Center the path horizontally
            float centerOffset = (nodesPerRow - 1) * stepX * 0.5f;
            x -= centerOffset * (goingRight ? 0 : 0) ; // already centered by col range

            // Node type
            NodeType type = NodeType.Regular;
            if (i > 0 && i % 15 == 0) type = NodeType.BigReward;
            else if (i > 0 && i % 5 == 0) type = NodeType.Gift;

            // Platform — landmark every 10-12, otherwise alternate small/medium
            int platformIdx;
            float pScale = 1.0f;

            if (type == NodeType.BigReward || (i % 12 == 0 && i > 0))
            {
                platformIdx = LargePlatforms[rng.Next(LargePlatforms.Length)];
                pScale = 1.3f;
            }
            else if (type == NodeType.Gift)
            {
                platformIdx = MediumPlatforms[rng.Next(MediumPlatforms.Length)];
                pScale = 1.1f;
            }
            else
            {
                int[] pool = (i % 3 < 2) ? SmallPlatforms : MediumPlatforms;
                platformIdx = pool[rng.Next(pool.Length)];
                pScale = 0.9f + (float)rng.NextDouble() * 0.2f;
            }

            // Prevent >2 same in a row
            if (platformIdx == lastPlatform) { sameCount++; if (sameCount >= 2) { while (platformIdx == lastPlatform) { int[] any = (rng.Next(2) == 0) ? SmallPlatforms : MediumPlatforms; platformIdx = any[rng.Next(any.Length)]; } sameCount = 0; } } else { sameCount = 0; }
            lastPlatform = platformIdx;

            // Decoration every 2-3 nodes (asymmetric)
            int elemIdx = 0;
            if (i % 2 == 1 && type == NodeType.Regular && rng.Next(3) != 0)
                elemIdx = DecoElements[rng.Next(DecoElements.Length)];

            // Bridge type
            int bridgeIdx = BridgeElements[rng.Next(BridgeElements.Length)];

            // Slight random rotation for organic feel
            float rot = (float)(rng.NextDouble() * 6 - 3);

            nodes.Add(new MapNode
            {
                index = i,
                position = new Vector2(x, y),
                type = type,
                platformIndex = platformIdx,
                elementIndex = elemIdx,
                bridgeIndex = bridgeIdx,
                platformScale = pScale,
                rotation = rot,
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
