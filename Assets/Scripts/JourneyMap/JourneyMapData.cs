using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 12 nodes at fixed positions, scaled LARGE for readability.
/// ONE continuous path, left to right, centered on screen.
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
        public float platformScale;
    }

    public const int TotalNodes = 12;

    private static readonly int[] Platforms = { 1, 6, 3, 9, 2, 17, 4, 7, 1, 18, 3, 8 };

    // Fixed positions — scaled by 220px per unit for BIG readable layout
    // Y=0 is vertical center of screen. Positive=up, negative=down.
    private static readonly Vector2[] Positions =
    {
        new Vector2(0f,    0f),
        new Vector2(1.2f,  0.35f),
        new Vector2(2.4f, -0.25f),
        new Vector2(3.6f,  0.4f),
        new Vector2(4.8f,  0f),
        new Vector2(6.0f, -0.35f),
        new Vector2(7.2f,  0.2f),
        new Vector2(8.4f, -0.15f),
        new Vector2(9.6f,  0.3f),
        new Vector2(10.8f, 0f),
        new Vector2(12.0f,-0.4f),
        new Vector2(13.2f, 0.25f),
    };

    public static List<MapNode> Generate()
    {
        var nodes = new List<MapNode>();
        const float scale = 220f; // BIG spacing

        for (int i = 0; i < Positions.Length; i++)
        {
            NodeType type = NodeType.Regular;
            if (i > 0 && i % 10 == 0) type = NodeType.BigReward;
            else if (i > 0 && i % 5 == 0) type = NodeType.Gift;

            float pScale = type == NodeType.BigReward ? 1.25f
                         : type == NodeType.Gift ? 1.1f : 1.0f;

            nodes.Add(new MapNode
            {
                index = i,
                position = Positions[i] * scale,
                type = type,
                platformIndex = Platforms[i],
                platformScale = pScale,
            });
        }
        return nodes;
    }
}
