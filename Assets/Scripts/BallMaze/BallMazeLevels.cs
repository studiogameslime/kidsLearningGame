using UnityEngine;

/// <summary>
/// Block placement in a ball maze level.
/// Position is center-based in grid units from board bottom-left.
/// </summary>
[System.Serializable]
public class MazeBlockDef
{
    public string type;      // "square","small","large","narrow","corner","corner_large"
    public float x, y;
    public float rotation;

    public MazeBlockDef(string t, float px, float py, float rot = 0f)
    { type = t; x = px; y = py; rotation = rot; }

    public Vector2 Size
    {
        get
        {
            switch (type)
            {
                case "square":       return new Vector2(1f, 1f);
                case "small":        return new Vector2(0.65f, 0.65f);
                case "large":        return new Vector2(1f, 2f);
                case "narrow":       return new Vector2(0.5f, 2f);
                case "corner":       return new Vector2(1f, 1f);
                case "corner_large": return new Vector2(2f, 2f);
                default:             return new Vector2(1f, 1f);
            }
        }
    }

    public string SpriteName => "block_" + type;
}

/// <summary>
/// Rotating block that spins continuously around its center.
/// </summary>
[System.Serializable]
public class MazeRotatingDef
{
    public string type;      // "rotate_large" or "rotate_narrow"
    public float x, y;
    public float speed;      // degrees per second

    public MazeRotatingDef(string t, float px, float py, float spd)
    { type = t; x = px; y = py; speed = spd; }

    public Vector2 Size
    {
        get { return type == "rotate_large" ? new Vector2(1.2f, 2.5f) : new Vector2(0.6f, 2f); }
    }

    public string SpriteName => "block_" + type;
}

[System.Serializable]
public class BallMazeLevel
{
    public int boardW, boardH;
    public float ballX, ballY;
    public float holeX, holeY;
    public string ballSprite;   // e.g. "ball_red_large"
    public float ballRadius;    // grid units
    public float holeRadius;    // grid units
    public MazeBlockDef[] blocks;
    public MazeRotatingDef[] rotating;
}

/// <summary>
/// Hand-designed levels for the Ball Maze game.
/// Difficulty increases with more blocks, tighter gaps, and rotating obstacles.
/// </summary>
public static class BallMazeLevels
{
    public static readonly BallMazeLevel[] All = new BallMazeLevel[]
    {
        // ── Level 1: Easy — gentle introduction ──
        new BallMazeLevel
        {
            boardW = 9, boardH = 6,
            ballX = 1.5f, ballY = 4.5f,
            holeX = 7.5f, holeY = 1.5f,
            ballSprite = "ball_red_large", ballRadius = 0.4f, holeRadius = 0.55f,
            blocks = new[]
            {
                new MazeBlockDef("large",  3.5f, 4f, 90),   // horizontal bar top-left
                new MazeBlockDef("square", 5f,   3f),        // middle obstacle
                new MazeBlockDef("large",  6f,   1.5f, 90),  // horizontal bar bottom-right
            },
            rotating = new MazeRotatingDef[0],
        },

        // ── Level 2: Easy — more obstacles, one turn ──
        new BallMazeLevel
        {
            boardW = 10, boardH = 6,
            ballX = 1.5f, ballY = 4.5f,
            holeX = 8.5f, holeY = 1.5f,
            ballSprite = "ball_blue_large", ballRadius = 0.4f, holeRadius = 0.55f,
            blocks = new[]
            {
                new MazeBlockDef("large",  3f,   4f),         // vertical wall left
                new MazeBlockDef("square", 3f,   2f),         // gap at y=3
                new MazeBlockDef("large",  6f,   2f),         // vertical wall mid
                new MazeBlockDef("square", 6f,   4.5f),       // top block
                new MazeBlockDef("small",  8f,   3.5f),       // small obstacle
            },
            rotating = new MazeRotatingDef[0],
        },

        // ── Level 3: Medium — S-curve with rotating gate ──
        // Path: top-left → right through gated gap → down → left through open gap → right to hole
        new BallMazeLevel
        {
            boardW = 11, boardH = 7,
            ballX = 1.5f, ballY = 5.5f,
            holeX = 9.5f, holeY = 1.5f,
            ballSprite = "ball_red_large", ballRadius = 0.4f, holeRadius = 0.55f,
            blocks = new[]
            {
                // Upper wall (y=4.5) — extends x≈1.5-6.5, gap at x≈6.5-9.5
                new MazeBlockDef("large",  2.5f, 4.5f, 90),
                new MazeBlockDef("large",  4.5f, 4.5f, 90),
                new MazeBlockDef("square", 6f,   4.5f),
                // Right cap narrows the gap: wall at x≈9-10 → gap is ~2.5 units
                new MazeBlockDef("square", 9.5f, 4.5f),
                // Lower wall (y=2.5) — gap on left side at x≈1-4.5
                new MazeBlockDef("large",  6.5f, 2.5f, 90),
                new MazeBlockDef("large",  8.5f, 2.5f, 90),
                new MazeBlockDef("square", 5f,   2.5f),
                // Corner decoration
                new MazeBlockDef("corner", 1f,   1f, 180),
                new MazeBlockDef("corner", 10f,  6f),
            },
            rotating = new[]
            {
                // Gate in upper wall gap — sweeps across the passage the ball must cross
                new MazeRotatingDef("rotate_large", 7.75f, 4.5f, 35f),
            },
        },

        // ── Level 4: Medium — zigzag with 2 rotating gates ──
        // Path: right → down through gate 1 → left → down through gate 2 → right to hole
        new BallMazeLevel
        {
            boardW = 12, boardH = 7,
            ballX = 1.5f, ballY = 5.5f,
            holeX = 10.5f, holeY = 1.5f,
            ballSprite = "ball_blue_large", ballRadius = 0.4f, holeRadius = 0.55f,
            blocks = new[]
            {
                // Row 1 wall (y=4.5) from left — gap at right x≈6-8.5
                new MazeBlockDef("large",  2.5f, 4.5f, 90),
                new MazeBlockDef("large",  4.5f, 4.5f, 90),
                // Right cap to narrow gap
                new MazeBlockDef("square", 9f,   4.5f),
                // Row 2 wall (y=3) from right — gap at left x≈3.5-6
                new MazeBlockDef("large",  8.5f, 3f, 90),
                new MazeBlockDef("large",  10.5f, 3f, 90),
                // Left cap to narrow gap
                new MazeBlockDef("square", 3f,   3f),
                // Row 3 wall (y=1.5) from left — gap at right x≈6.5+
                new MazeBlockDef("large",  3f,   1.5f, 90),
                new MazeBlockDef("large",  5f,   1.5f, 90),
                // Corners
                new MazeBlockDef("corner", 0.5f, 0.5f, 180),
                new MazeBlockDef("corner", 11.5f, 6.5f),
            },
            rotating = new[]
            {
                // Gate 1: blocks the gap in row 1 wall (ball going right)
                new MazeRotatingDef("rotate_large",  7f, 4.5f, 32f),
                // Gate 2: blocks the gap in row 2 wall (ball going left)
                new MazeRotatingDef("rotate_narrow", 4.75f, 3f, -30f),
            },
        },

        // ── Level 5: Hard — winding path with 2 rotating gates ──
        // Path: right → through gate 1 down → left → down → right through gate 2 → to hole
        new BallMazeLevel
        {
            boardW = 13, boardH = 8,
            ballX = 1.5f, ballY = 6.5f,
            holeX = 11.5f, holeY = 1.5f,
            ballSprite = "ball_red_small", ballRadius = 0.3f, holeRadius = 0.5f,
            blocks = new[]
            {
                // Wall 1 (y=6) horizontal — gap at x≈7-9.5
                new MazeBlockDef("large",  3f,   6f, 90),
                new MazeBlockDef("large",  5f,   6f, 90),
                new MazeBlockDef("square", 10f,  6f),
                // Wall 2 (x=8) vertical — gap at y≈2.5-3.5
                new MazeBlockDef("large",  8f,   5f),
                new MazeBlockDef("square", 8f,   1.5f),
                // Wall 3 (y=3.5) horizontal — gap at x≈5.5-7.5
                new MazeBlockDef("large",  2.5f, 3.5f, 90),
                new MazeBlockDef("large",  4f,   3.5f, 90),
                new MazeBlockDef("square", 8f,   3.5f),
                // Wall 4 (y=2) right side blocks path to hole
                new MazeBlockDef("large",  10f,  3.5f, 90),
                new MazeBlockDef("square", 11.5f, 3.5f),
                // Extra obstacles
                new MazeBlockDef("square", 3f,   1.5f),
                new MazeBlockDef("corner_large", 11.5f, 6.5f),
                new MazeBlockDef("corner", 0.5f, 0.5f, 180),
            },
            rotating = new[]
            {
                // Gate 1: blocks the gap in wall 1 — must time passage right
                new MazeRotatingDef("rotate_large",  7.5f, 6f, 33f),
                // Gate 2: blocks the corridor between wall 3 and wall 4
                new MazeRotatingDef("rotate_large",  9f,  2f, -30f),
            },
        },

        // ── Level 6: Hard — tight maze with 3 rotating gates ──
        // Path: right through gate 1 → down → left through gate 2 → down → right through gate 3 → hole
        new BallMazeLevel
        {
            boardW = 14, boardH = 8,
            ballX = 1.5f, ballY = 6.5f,
            holeX = 12.5f, holeY = 1.5f,
            ballSprite = "ball_blue_small", ballRadius = 0.3f, holeRadius = 0.5f,
            blocks = new[]
            {
                // Wall A (y=6.5) — gap at x≈6.5-9
                new MazeBlockDef("large",  3f,   6.5f, 90),
                new MazeBlockDef("large",  5f,   6.5f, 90),
                new MazeBlockDef("square", 9.5f, 6.5f),
                new MazeBlockDef("narrow", 11f,  6.5f),
                // Wall B (y=4.5) — gap at x≈4.5-7
                new MazeBlockDef("large",  2f,   4.5f, 90),
                new MazeBlockDef("large",  8.5f, 4.5f, 90),
                new MazeBlockDef("large",  10.5f, 4.5f, 90),
                new MazeBlockDef("square", 12.5f, 4.5f),
                // Wall C (y=2.5) — gap at x≈9-11.5
                new MazeBlockDef("large",  3f,   2.5f, 90),
                new MazeBlockDef("large",  5f,   2.5f, 90),
                new MazeBlockDef("large",  7f,   2.5f, 90),
                new MazeBlockDef("square", 12f,  2.5f),
                // Extra obstacles
                new MazeBlockDef("small",  12f,  5.5f),
                new MazeBlockDef("corner_large", 12.5f, 6.5f),
                new MazeBlockDef("corner", 0.5f, 0.5f, 180),
            },
            rotating = new[]
            {
                // Gate 1: blocks gap in wall A
                new MazeRotatingDef("rotate_large",  7.5f, 6.5f, 35f),
                // Gate 2: blocks gap in wall B
                new MazeRotatingDef("rotate_narrow", 5.75f, 4.5f, -32f),
                // Gate 3: blocks gap in wall C near the hole
                new MazeRotatingDef("rotate_large",  10f, 2.5f, 38f),
            },
        },
    };

    public static int GetLevelForDifficulty(int difficulty)
    {
        switch (difficulty)
        {
            case 0: return Random.Range(0, 2);   // Easy: levels 0-1
            case 1: return Random.Range(2, 4);   // Medium: levels 2-3
            case 2: return Random.Range(4, 6);   // Hard: levels 4-5
            default: return 0;
        }
    }
}
