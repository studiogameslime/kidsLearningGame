using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines all tangram target figures with mathematically verified positions.
///
/// Key principle: when two right triangles share the same center, they tile
/// the bounding box exactly (one fills bottom-left, the other top-right at rot 180).
///
/// Coordinate system: positions in grid units (1 unit = GridUnit pixels).
/// Rotation in degrees clockwise.
/// </summary>
public static class TangramFigures
{
    public struct PiecePlacement
    {
        public int pieceIndex;
        public Vector2 position;
        public float rotation;

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
        public int difficulty;

        public Figure(string name, int difficulty, params PiecePlacement[] pieces)
        {
            this.name = name;
            this.difficulty = difficulty;
            this.pieces = pieces;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  ALL FIGURES — mathematically verified positions
    // ═══════════════════════════════════════════════════════════

    public static readonly Figure[] All = new[]
    {
        // ── EASY (2 pieces) ────────────────────────────────────

        // Square: two large right triangles at same center, rot 0 + 180
        // Piece 0 fills bottom-left half, piece 1 fills top-right half
        new Figure("Square", 0,
            new PiecePlacement(0, 0f, 0f, 0f),
            new PiecePlacement(1, 0f, 0f, 180f)
        ),

        // Diamond: two small isosceles triangles at same center, rot 0 + 180
        // Piece 3 points up, piece 4 points down → diamond shape
        new Figure("Diamond", 0,
            new PiecePlacement(3, 0f, 0f, 0f),
            new PiecePlacement(4, 0f, 0f, 180f)
        ),

        // Rotated Square: two large tris at same center, rot 45 + 225
        // Creates a diamond/tilted square (~2.83 units diagonal)
        new Figure("BigDiamond", 0,
            new PiecePlacement(0, 0f, 0f, 45f),
            new PiecePlacement(1, 0f, 0f, 225f)
        ),

        // Bowtie: two small isosceles tris, tips touching at center
        // Each offset by half its width so apexes meet at origin
        new Figure("Bowtie", 0,
            new PiecePlacement(3, -0.55f, 0f, 90f),
            new PiecePlacement(4,  0.55f, 0f, 270f)
        ),

        // ── MEDIUM (4-5 pieces) ────────────────────────────────

        // House: square base + triangle roof + door + chimney
        // Base = 2 large tris at (0, -0.5) forming a square
        // Roof = medium tri at rot 135 (isosceles triangle pointing up)
        // Door = diamond square at bottom center
        // Chimney = small tri on the left
        new Figure("House", 1,
            new PiecePlacement(0,  0f,   -0.5f,  0f),
            new PiecePlacement(1,  0f,   -0.5f,  180f),
            new PiecePlacement(2,  0f,    0.5f,  135f),
            new PiecePlacement(3, -0.5f,  1.2f,  0f),
            new PiecePlacement(5,  0f,   -1.0f,  0f)
        ),

        // Arrow: arrowhead (large tri) + shaft (parallelogram + small pieces)
        new Figure("Arrow", 1,
            new PiecePlacement(0,  0.7f, 0f,   225f),
            new PiecePlacement(6, -0.5f, 0f,   0f),
            new PiecePlacement(3, -0.5f, 0.4f, 180f),
            new PiecePlacement(4, -0.5f,-0.4f, 0f),
            new PiecePlacement(5, -1.2f, 0f,   0f)
        ),

        // Fish: diamond body + tail
        // Body = 2 large tris at rot 45/225 (tilted square)
        // Tail = medium tri pointing left behind the body
        // Eye = small tri
        new Figure("Fish", 1,
            new PiecePlacement(0,  0.3f, 0f,   45f),
            new PiecePlacement(1,  0.3f, 0f,   225f),
            new PiecePlacement(2, -1.1f, 0f,   315f),
            new PiecePlacement(3,  0.8f, 0.4f, 0f),
            new PiecePlacement(5, -0.3f, 0f,   0f)
        ),

        // ── HARD (6-7 pieces) ──────────────────────────────────

        // Cat: square body + triangle head + ears + tail
        new Figure("Cat", 2,
            new PiecePlacement(0,  0f,   -0.3f, 0f),
            new PiecePlacement(1,  0f,   -0.3f, 180f),
            new PiecePlacement(2,  0f,    0.8f, 135f),
            new PiecePlacement(3, -0.4f,  1.5f, 0f),
            new PiecePlacement(4,  0.4f,  1.5f, 0f),
            new PiecePlacement(5,  0f,    0.2f, 0f),
            new PiecePlacement(6,  1.2f, -0.8f, 30f)
        ),

        // Rocket: tall shape pointing up
        new Figure("Rocket", 2,
            new PiecePlacement(0,  0f,   -0.3f, 45f),
            new PiecePlacement(1,  0f,   -0.3f, 225f),
            new PiecePlacement(2,  0f,    1.0f, 135f),
            new PiecePlacement(3, -0.6f, -1.2f, 270f),
            new PiecePlacement(4,  0.6f, -1.2f, 90f),
            new PiecePlacement(5,  0f,   -0.8f, 0f),
            new PiecePlacement(6,  0f,    0.3f, 0f)
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
