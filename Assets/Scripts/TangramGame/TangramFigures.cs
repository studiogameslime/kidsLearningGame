using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines all tangram target figures. Each figure specifies which pieces are used
/// and where they go (position + rotation) on a grid.
///
/// Coordinate system: unit grid where (0,0) is bottom-left of the play area.
/// Positions are in grid units (1 unit ≈ 100px at runtime).
/// Rotation is in degrees (clockwise).
/// </summary>
public static class TangramFigures
{
    public struct PiecePlacement
    {
        public int pieceIndex;   // 0-6 (which tangram piece)
        public Vector2 position; // center position in grid units
        public float rotation;   // degrees clockwise

        public PiecePlacement(int piece, float x, float y, float rot = 0f)
        {
            pieceIndex = piece;
            position = new Vector2(x, y);
            rotation = rot;
        }
    }

    public struct Figure
    {
        public string name;
        public PiecePlacement[] pieces;
        public int difficulty; // 0=easy, 1=medium, 2=hard

        public Figure(string name, int difficulty, params PiecePlacement[] pieces)
        {
            this.name = name;
            this.difficulty = difficulty;
            this.pieces = pieces;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  ALL FIGURES
    // ═══════════════════════════════════════════════════════════

    public static readonly Figure[] All = new[]
    {
        // ── EASY (3-4 pieces) ──────────────────────────────────

        // Square: two large triangles
        new Figure("Square", 0,
            new PiecePlacement(0, -0.5f, 0f, 0f),
            new PiecePlacement(1,  0.5f, 0f, 180f)
        ),

        // Big Triangle: two large + medium
        new Figure("BigTriangle", 0,
            new PiecePlacement(0, -0.7f, -0.3f, 0f),
            new PiecePlacement(1,  0.7f, -0.3f, 90f),
            new PiecePlacement(2,  0f, 0.5f, 180f)
        ),

        // Diamond: two small triangles
        new Figure("Diamond", 0,
            new PiecePlacement(3,  0f,  0.3f, 0f),
            new PiecePlacement(4,  0f, -0.3f, 180f)
        ),

        // Rectangle: two large triangles side by side
        new Figure("Rectangle", 0,
            new PiecePlacement(0, -0.5f, 0f, 45f),
            new PiecePlacement(1,  0.5f, 0f, 225f)
        ),

        // ── MEDIUM (5-6 pieces) ────────────────────────────────

        // House: square base + triangle roof
        new Figure("House", 1,
            new PiecePlacement(5,  0f, -0.4f, 0f),        // square base
            new PiecePlacement(0, -0.5f, -0.4f, 0f),      // large tri left wall
            new PiecePlacement(1,  0.5f, -0.4f, 90f),     // large tri right wall
            new PiecePlacement(2,  0f,  0.6f, 180f),       // medium tri roof
            new PiecePlacement(3,  0f,  1.0f, 0f)          // small tri chimney top
        ),

        // Arrow pointing right
        new Figure("Arrow", 1,
            new PiecePlacement(0, -0.8f,  0f, 0f),
            new PiecePlacement(1, -0.8f,  0f, 90f),
            new PiecePlacement(2,  0.3f,  0.3f, 270f),
            new PiecePlacement(3,  0.3f, -0.3f, 90f),
            new PiecePlacement(6, -0.2f,  0f, 0f)
        ),

        // Boat
        new Figure("Boat", 1,
            new PiecePlacement(0, -0.5f, -0.2f, 0f),
            new PiecePlacement(1,  0.5f, -0.2f, 90f),
            new PiecePlacement(6,  0f, -0.7f, 0f),         // parallelogram hull
            new PiecePlacement(2,  0f,  0.5f, 180f),       // medium tri sail
            new PiecePlacement(3, -0.4f, 0.3f, 0f)
        ),

        // Tree
        new Figure("Tree", 1,
            new PiecePlacement(0,  0f,  0.8f, 180f),       // large tri top
            new PiecePlacement(1,  0f,  0.2f, 0f),         // large tri middle
            new PiecePlacement(2,  0f, -0.2f, 180f),       // medium tri lower
            new PiecePlacement(5,  0f, -0.6f, 45f),        // square trunk
            new PiecePlacement(3, -0.3f, 0.5f, 90f),
            new PiecePlacement(4,  0.3f, 0.5f, 270f)
        ),

        // ── HARD (7 pieces, all used) ──────────────────────────

        // Cat sitting
        new Figure("Cat", 2,
            new PiecePlacement(0, -0.3f, -0.5f, 0f),      // large tri body left
            new PiecePlacement(1,  0.3f, -0.5f, 90f),      // large tri body right
            new PiecePlacement(2,  0f,  0.3f, 0f),          // medium tri head
            new PiecePlacement(3, -0.3f, 0.7f, 0f),         // small tri ear left
            new PiecePlacement(4,  0.3f, 0.7f, 90f),        // small tri ear right
            new PiecePlacement(5,  0f, -0.1f, 45f),         // square neck
            new PiecePlacement(6,  0.6f, -0.8f, 45f)        // parallelogram tail
        ),

        // Swan
        new Figure("Swan", 2,
            new PiecePlacement(0, -0.4f, -0.4f, 0f),
            new PiecePlacement(1,  0.4f, -0.4f, 90f),
            new PiecePlacement(2, -0.5f,  0.3f, 270f),
            new PiecePlacement(3, -0.5f,  0.8f, 180f),
            new PiecePlacement(4, -0.2f,  1.1f, 0f),
            new PiecePlacement(5,  0.3f,  0f, 45f),
            new PiecePlacement(6,  0f, -0.8f, 0f)
        ),

        // Runner
        new Figure("Runner", 2,
            new PiecePlacement(0,  0f,  0.5f, 45f),
            new PiecePlacement(1,  0.5f, -0.2f, 135f),
            new PiecePlacement(2, -0.3f,  0f, 0f),
            new PiecePlacement(3,  0f,  1.0f, 0f),
            new PiecePlacement(4, -0.6f, -0.5f, 270f),
            new PiecePlacement(5,  0.2f,  0.8f, 45f),
            new PiecePlacement(6,  0.6f, -0.7f, 135f)
        ),

        // Heart
        new Figure("Heart", 2,
            new PiecePlacement(0, -0.4f,  0.2f, 135f),
            new PiecePlacement(1,  0.4f,  0.2f, 225f),
            new PiecePlacement(2,  0f, -0.5f, 0f),
            new PiecePlacement(3, -0.5f,  0.6f, 45f),
            new PiecePlacement(4,  0.5f,  0.6f, 315f),
            new PiecePlacement(5,  0f,  0.2f, 45f),
            new PiecePlacement(6,  0f, -0.2f, 0f)
        ),

        // Candle
        new Figure("Candle", 2,
            new PiecePlacement(0, -0.2f, -0.6f, 0f),
            new PiecePlacement(1,  0.2f, -0.6f, 90f),
            new PiecePlacement(2,  0f,  0f, 0f),
            new PiecePlacement(3,  0f,  0.7f, 0f),
            new PiecePlacement(4,  0f,  1.0f, 180f),
            new PiecePlacement(5,  0f, -0.2f, 0f),
            new PiecePlacement(6, -0.3f, 0.3f, 90f)
        ),
    };

    /// <summary>Get figures filtered by difficulty tier (0=easy, 1=medium, 2=hard).</summary>
    public static List<Figure> GetByDifficulty(int tier)
    {
        var result = new List<Figure>();
        foreach (var fig in All)
            if (fig.difficulty <= tier)
                result.Add(fig);
        return result;
    }

    /// <summary>Map game difficulty 1-10 to figure tier 0-2.</summary>
    public static int DifficultyToTier(int difficulty)
    {
        if (difficulty <= 3) return 0;
        if (difficulty <= 6) return 1;
        return 2;
    }
}
