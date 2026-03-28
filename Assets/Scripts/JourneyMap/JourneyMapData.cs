using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fixed-position journey map path. Each node has an explicit position.
/// ONE continuous path, left to right, with gentle Y variation.
/// No grid. No rows. Just a flowing connected path.
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
        public bool hasDecoration;
        public int decoIndex;
        public float platformScale;
    }

    public const int TotalNodes = 12;

    private static readonly int[] SmallPlatforms = { 1, 2, 3, 4, 20, 21, 22 };
    private static readonly int[] MediumPlatforms = { 6, 7, 8, 9, 16, 17, 18, 19 };
    private static readonly int[] LargePlatforms = { 10, 11, 12, 13, 14 };
    private static readonly int[] DecoElements = { 1, 4, 7, 8, 9, 10 };

    /// <summary>
    /// 12 nodes at EXACT fixed positions.
    /// Multiply by 150 to convert to pixel space.
    /// </summary>
    private static readonly Vector2[] FixedPositions =
    {
        new Vector2(0f,    0f),
        new Vector2(1.2f,  0.3f),
        new Vector2(2.4f, -0.2f),
        new Vector2(3.6f,  0.4f),
        new Vector2(4.8f,  0f),
        new Vector2(6.0f, -0.3f),
        new Vector2(7.2f,  0.2f),
        new Vector2(8.4f, -0.1f),
        new Vector2(9.6f,  0.3f),
        new Vector2(10.8f, 0f),
        new Vector2(12.0f,-0.4f),
        new Vector2(13.2f, 0.2f),
    };

    public static List<MapNode> Generate()
    {
        var nodes = new List<MapNode>();
        var rng = new System.Random(42);
        float scale = 150f; // convert unit coords to pixels
        int lastPlat = -1;

        for (int i = 0; i < FixedPositions.Length; i++)
        {
            Vector2 pos = FixedPositions[i] * scale;

            // Node type
            NodeType type = NodeType.Regular;
            if (i > 0 && i % 15 == 0) type = NodeType.BigReward;
            else if (i > 0 && i % 5 == 0) type = NodeType.Gift;

            // Platform — vary types, no >2 repeats
            int platIdx;
            float pScale;
            if (type == NodeType.BigReward)
            {
                platIdx = LargePlatforms[rng.Next(LargePlatforms.Length)];
                pScale = 1.2f;
            }
            else if (type == NodeType.Gift)
            {
                platIdx = MediumPlatforms[rng.Next(MediumPlatforms.Length)];
                pScale = 1.05f;
            }
            else
            {
                int[] pool = rng.Next(3) < 2 ? SmallPlatforms : MediumPlatforms;
                platIdx = pool[rng.Next(pool.Length)];
                pScale = 0.95f;
            }
            if (platIdx == lastPlat) platIdx = SmallPlatforms[rng.Next(SmallPlatforms.Length)];
            lastPlat = platIdx;

            // Decoration: every 3 nodes, max 1
            bool hasDeco = type == NodeType.Regular && i % 3 == 1;
            int decoIdx = hasDeco ? DecoElements[rng.Next(DecoElements.Length)] : 0;

            nodes.Add(new MapNode
            {
                index = i,
                position = pos,
                type = type,
                platformIndex = platIdx,
                hasDecoration = hasDeco,
                decoIndex = decoIdx,
                platformScale = pScale,
            });
        }

        return nodes;
    }
}
