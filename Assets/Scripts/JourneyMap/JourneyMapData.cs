using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates 100 nodes along ONE continuous flowing path.
/// Build order: path positions → connections → platforms → decorations.
/// Path flows LEFT→RIGHT in smooth curves with gentle turns.
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

        // ── STEP 1: Generate path positions ──
        // Walk one node at a time, flowing right then curving down
        float x = 100f;
        float y = 100f;
        float dir = 1f;              // 1=right, -1=left
        float stepDist = 115f;       // distance between nodes (tight)
        float bandWidth = 1200f;     // how far right before turning
        float turnDropTotal = 180f;  // vertical drop during turn
        int turnSteps = 3;           // how many nodes to spend on the turn curve
        int straightCount = 0;
        bool inTurn = false;
        int turnProgress = 0;

        int lastPlatform = -1;
        int sameCount = 0;

        for (int i = 0; i < TotalNodes; i++)
        {
            // Gentle wave wobble
            float wobble = Mathf.Sin(i * 0.55f) * 22f;
            float jX = (float)(rng.NextDouble() * 10 - 5);
            float jY = (float)(rng.NextDouble() * 8 - 4);

            float nodeX = x + jX;
            float nodeY = y + wobble + jY;

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
                pScale = 0.9f + (float)rng.NextDouble() * 0.1f;
            }

            if (platIdx == lastPlatform) { sameCount++; if (sameCount >= 2) { int[] a = SmallPlatforms; while (platIdx == lastPlatform) platIdx = a[rng.Next(a.Length)]; sameCount = 0; } }
            else sameCount = 0;
            lastPlatform = platIdx;

            // Sparse decoration
            bool hasDeco = type == NodeType.Regular && i % 4 == 2 && rng.Next(3) != 0;
            int decoIdx = hasDeco ? DecoElements[rng.Next(DecoElements.Length)] : 0;

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

            // ── Advance along path ──
            if (inTurn)
            {
                // During a turn: move diagonally down while reversing
                float turnFrac = (float)(turnProgress + 1) / turnSteps;
                x += stepDist * 0.3f * (-dir); // slight horizontal shift in new direction
                y += turnDropTotal / turnSteps;
                turnProgress++;
                if (turnProgress >= turnSteps)
                {
                    inTurn = false;
                    turnProgress = 0;
                    dir = -dir;
                    straightCount = 0;
                }
            }
            else
            {
                // Straight section: move in current direction
                x += stepDist * dir;
                straightCount++;

                // Check if we need to start a turn
                bool hitRight = dir > 0 && x > bandWidth;
                bool hitLeft = dir < 0 && x < 100f;
                if (hitRight || hitLeft)
                {
                    inTurn = true;
                    turnProgress = 0;
                }
            }
        }

        return nodes;
    }
}
