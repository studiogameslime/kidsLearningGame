using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates the 100-node journey map layout as a snake path.
/// Nodes zig-zag: right → down → left → down → right...
/// Each node has a position, platform type, and node type.
/// </summary>
public static class JourneyMapData
{
    public enum NodeType { Regular, Gift, BigReward }

    public struct MapNode
    {
        public int index;
        public Vector2 position;       // world position
        public NodeType type;
        public int platformIndex;      // 1-22 (platform sprite)
        public int elementIndex;       // 0 = none, 1-10 = decoration
        public int bridgeIndex;        // connector to next node (element sprite)
    }

    public const int TotalNodes = 100;
    public const int NodesPerRow = 8;

    // Platform categories
    // Small: 01,02,03,04,20 | Medium: 06,07,08,09,16,17,18,19 | Large (landmark): 10,11,12,13,14
    // Bridges: 05,15 (nets/bridges) — used as connectors
    private static readonly int[] SmallPlatforms = { 1, 2, 3, 4, 20, 21, 22 };
    private static readonly int[] MediumPlatforms = { 6, 7, 8, 9, 16, 17, 18, 19 };
    private static readonly int[] LargePlatforms = { 10, 11, 12, 13, 14 };
    private static readonly int[] BridgeElements = { 2, 3, 5, 6 }; // rope, bridge, net connectors
    private static readonly int[] DecoElements = { 1, 4, 7, 8, 9, 10 }; // cones, rocks, barrels

    public static List<MapNode> Generate()
    {
        var nodes = new List<MapNode>();
        var rng = new System.Random(42); // fixed seed for consistent layout

        float nodeSpacingX = 180f;
        float nodeSpacingY = 200f;
        float rowDropY = 250f;

        int row = 0;
        int col = 0;
        bool goingRight = true;
        int lastPlatform = -1;
        int repeatCount = 0;

        for (int i = 0; i < TotalNodes; i++)
        {
            // Position with slight random offset for organic feel
            float x = col * nodeSpacingX + (float)(rng.NextDouble() * 30 - 15);
            float y = -row * rowDropY + (float)(rng.NextDouble() * 20 - 10);

            // Node type
            NodeType type = NodeType.Regular;
            if (i > 0 && i % 15 == 0) type = NodeType.BigReward;
            else if (i > 0 && i % 5 == 0) type = NodeType.Gift;

            // Platform selection
            int platformIdx;
            if (type == NodeType.BigReward)
            {
                platformIdx = LargePlatforms[rng.Next(LargePlatforms.Length)];
            }
            else if (i % 10 == 0 && i > 0)
            {
                platformIdx = LargePlatforms[rng.Next(LargePlatforms.Length)];
            }
            else
            {
                // Alternate small/medium, avoid >2 repeats
                int[] pool = rng.Next(2) == 0 ? SmallPlatforms : MediumPlatforms;
                platformIdx = pool[rng.Next(pool.Length)];

                if (platformIdx == lastPlatform)
                {
                    repeatCount++;
                    if (repeatCount >= 2)
                    {
                        // Force different
                        while (platformIdx == lastPlatform)
                            platformIdx = pool[rng.Next(pool.Length)];
                        repeatCount = 0;
                    }
                }
                else
                {
                    repeatCount = 0;
                }
            }
            lastPlatform = platformIdx;

            // Decorative elements (every 3-4 nodes)
            int elemIdx = 0;
            if (i % 3 == 1 && type == NodeType.Regular)
                elemIdx = DecoElements[rng.Next(DecoElements.Length)];

            // Bridge to next node
            int bridgeIdx = BridgeElements[rng.Next(BridgeElements.Length)];

            nodes.Add(new MapNode
            {
                index = i,
                position = new Vector2(x, y),
                type = type,
                platformIndex = platformIdx,
                elementIndex = elemIdx,
                bridgeIndex = bridgeIdx,
            });

            // Snake movement
            if (goingRight)
            {
                col++;
                if (col >= NodesPerRow)
                {
                    col = NodesPerRow - 1;
                    row++;
                    goingRight = false;
                }
            }
            else
            {
                col--;
                if (col < 0)
                {
                    col = 0;
                    row++;
                    goingRight = true;
                }
            }
        }

        return nodes;
    }
}
