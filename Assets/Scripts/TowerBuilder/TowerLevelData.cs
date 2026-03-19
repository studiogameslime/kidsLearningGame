using System.Collections.Generic;

/// <summary>
/// Defines a single brick in a tower layout.
/// gridX/gridY are in stud units from the tower's bottom-left.
/// brickType encodes both height and width: "brick_{low|medium|high}_{1|2|4|6}".
/// </summary>
[System.Serializable]
public class BrickDef
{
    public string brickType;
    public string color;
    public int gridX;
    public float gridY;

    public BrickDef(string type, string col, int x, float y)
    {
        brickType = type;
        color = col;
        gridX = x;
        gridY = y;
    }

    public int StudWidth
    {
        get
        {
            string[] parts = brickType.Split('_');
            int w;
            if (int.TryParse(parts[parts.Length - 1], out w)) return w;
            return 2;
        }
    }

    public float HeightUnits
    {
        get
        {
            if (brickType.Contains("_low_")) return 0.5f;
            if (brickType.Contains("_high_")) return 1.5f;
            return 1f;
        }
    }

    public string SpriteKey => color + "/" + brickType;
}

[System.Serializable]
public class TowerLevel
{
    public BrickDef[] bricks;
    public TowerLevel(params BrickDef[] b) { bricks = b; }
}

/// <summary>
/// Static level definitions for the Tower Builder game.
/// 24 levels across 4 difficulty tiers.
/// Each level is a unique tower shape with varied colors and structure.
/// </summary>
public static class TowerLevels
{
    // Shorthand helpers
    private static BrickDef B(string type, string col, int x, float y) => new BrickDef(type, col, x, y);
    private const string M2 = "brick_medium_2";
    private const string M4 = "brick_medium_4";
    private const string M6 = "brick_medium_6";
    private const string L2 = "brick_low_2";
    private const string L4 = "brick_low_4";
    private const string H2 = "brick_high_2";

    public static readonly TowerLevel[] All = new TowerLevel[]
    {
        // ═══════════════════════════════════════════════════════════
        //  EASY (3-4 bricks) — 6 levels
        // ═══════════════════════════════════════════════════════════

        // Level 0: Simple 3-stack
        new TowerLevel(
            B(M4, "Red",    0, 0f),
            B(M4, "Blue",   0, 1f),
            B(M2, "Yellow", 1, 2f)
        ),

        // Level 1: Pyramid
        new TowerLevel(
            B(M6, "Red",    0, 0f),
            B(M4, "Blue",   1, 1f),
            B(M2, "Green",  2, 2f)
        ),

        // Level 2: Short wide tower
        new TowerLevel(
            B(M6, "Green",  0, 0f),
            B(M6, "Yellow", 0, 1f),
            B(M4, "Red",    1, 2f)
        ),

        // Level 3: Colorful stack
        new TowerLevel(
            B(M4, "Blue",   0, 0f),
            B(M4, "Green",  0, 1f),
            B(M4, "Red",    0, 2f),
            B(M2, "Yellow", 1, 3f)
        ),

        // Level 4: Small house
        new TowerLevel(
            B(M6, "Yellow",  0, 0f),
            B(M4, "Red",    1, 1f),
            B(M2, "Red",    2, 2f)
        ),

        // Level 5: 4-brick column
        new TowerLevel(
            B(M2, "Red",    0, 0f),
            B(M2, "Blue",   0, 1f),
            B(M2, "Green",  0, 2f),
            B(M2, "Yellow", 0, 3f)
        ),

        // ═══════════════════════════════════════════════════════════
        //  MEDIUM (5-8 bricks) — 6 levels
        // ═══════════════════════════════════════════════════════════

        // Level 6: Tall pyramid
        new TowerLevel(
            B(M6, "Red",    0, 0f),
            B(M6, "Blue",   0, 1f),
            B(M4, "Green",  1, 2f),
            B(M4, "Yellow", 1, 3f),
            B(M2, "Yellow",  2, 4f)
        ),

        // Level 7: Twin pillars
        new TowerLevel(
            B(M6, "Red",    0, 0f),
            B(M2, "Blue",   0, 1f),
            B(M2, "Blue",   4, 1f),
            B(M2, "Green",  0, 2f),
            B(M2, "Green",  4, 2f),
            B(M6, "Yellow", 0, 3f)
        ),

        // Level 8: Castle base
        new TowerLevel(
            B(M6, "Red",    0, 0f),
            B(M2, "Blue",   0, 1f),
            B(M2, "Blue",   4, 1f),
            B(M2, "Green",  0, 2f),
            B(M2, "Green",  4, 2f),
            B(M6, "Yellow", 0, 3f),
            B(M4, "Yellow",  1, 4f),
            B(M2, "Red",    2, 5f)
        ),

        // Level 9: Staircase right
        new TowerLevel(
            B(M2, "Red",    0, 0f),
            B(M4, "Blue",   0, 1f),
            B(M2, "Green",  2, 2f),
            B(M4, "Yellow", 2, 3f),
            B(M2, "Yellow",  4, 4f)
        ),

        // Level 10: Wide base, narrow top
        new TowerLevel(
            B(M6, "Blue",   0, 0f),
            B(M6, "Green",  0, 1f),
            B(M4, "Red",    1, 2f),
            B(M4, "Yellow", 1, 3f),
            B(M2, "Blue",   2, 4f),
            B(M2, "Yellow",  2, 5f)
        ),

        // Level 11: Cross shape base
        new TowerLevel(
            B(M2, "Red",    2, 0f),
            B(M6, "Blue",   0, 1f),
            B(M2, "Green",  2, 2f),
            B(M2, "Yellow", 2, 3f),
            B(M2, "Yellow",  2, 4f)
        ),

        // ═══════════════════════════════════════════════════════════
        //  HARD (9-12 bricks) — 6 levels
        // ═══════════════════════════════════════════════════════════

        // Level 12: Tall castle
        new TowerLevel(
            B(M6, "Red",    0, 0f),
            B(M6, "Red",    0, 1f),
            B(M2, "Blue",   0, 2f),
            B(M2, "Blue",   4, 2f),
            B(M2, "Green",  0, 3f),
            B(M2, "Green",  4, 3f),
            B(M6, "Yellow", 0, 4f),
            B(M4, "Yellow",  1, 5f),
            B(M4, "Blue",   1, 6f),
            B(M2, "Yellow", 2, 7f),
            B(M2, "Green",  2, 8f)
        ),

        // Level 13: Double tower
        new TowerLevel(
            B(M4, "Red",    0, 0f),
            B(M4, "Red",    4, 0f),
            B(M4, "Blue",   0, 1f),
            B(M4, "Blue",   4, 1f),
            B(M4, "Green",  0, 2f),
            B(M4, "Green",  4, 2f),
            B(M6, "Yellow", 1, 3f),
            B(M4, "Yellow",  2, 4f),
            B(M2, "Red",    3, 5f)
        ),

        // Level 14: Zigzag tower
        new TowerLevel(
            B(M4, "Blue",   0, 0f),
            B(M4, "Green",  2, 1f),
            B(M4, "Red",    0, 2f),
            B(M4, "Yellow", 2, 3f),
            B(M4, "Blue",   0, 4f),
            B(M4, "Green",  2, 5f),
            B(M4, "Red",    0, 6f),
            B(M4, "Yellow", 2, 7f),
            B(M2, "Yellow",  2, 8f)
        ),

        // Level 15: Wide pyramid
        new TowerLevel(
            B(M6, "Red",    0, 0f),
            B(M4, "Red",    6, 0f),
            B(M4, "Blue",   1, 1f),
            B(M4, "Blue",   5, 1f),
            B(M4, "Green",  2, 2f),
            B(M4, "Yellow", 2, 3f),
            B(M4, "Yellow",  2, 4f),
            B(M2, "Red",    3, 5f),
            B(M2, "Blue",   3, 6f)
        ),

        // Level 16: Tiered cake
        new TowerLevel(
            B(M6, "Yellow",  0, 0f),
            B(M6, "Yellow",  0, 1f),
            B(M4, "Red",    1, 2f),
            B(M4, "Red",    1, 3f),
            B(M2, "Yellow", 2, 4f),
            B(M2, "Yellow", 2, 5f),
            B(M2, "Blue",   2, 6f),
            B(M2, "Green",  2, 7f),
            B(M2, "Red",    2, 8f)
        ),

        // Level 17: Arch shape
        new TowerLevel(
            B(M2, "Red",    0, 0f),
            B(M2, "Red",    4, 0f),
            B(M2, "Blue",   0, 1f),
            B(M2, "Blue",   4, 1f),
            B(M2, "Green",  0, 2f),
            B(M2, "Green",  4, 2f),
            B(M6, "Yellow", 0, 3f),
            B(M6, "Yellow", 0, 4f),
            B(M4, "Yellow",  1, 5f),
            B(M2, "Red",    2, 6f)
        ),

        // ═══════════════════════════════════════════════════════════
        //  VERY HARD (13+ bricks) — 6 levels
        // ═══════════════════════════════════════════════════════════

        // Level 18: Wide fortress
        new TowerLevel(
            B(M6, "Red",    0, 0f),
            B(M4, "Red",    6, 0f),
            B(M6, "Blue",   0, 1f),
            B(M4, "Blue",   6, 1f),
            B(M4, "Green",  1, 2f),
            B(M4, "Green",  5, 2f),
            B(M4, "Yellow", 1, 3f),
            B(M4, "Yellow", 5, 3f),
            B(M6, "Yellow",  2, 4f),
            B(M6, "Red",    2, 5f),
            B(M4, "Blue",   3, 6f),
            B(M4, "Green",  3, 7f),
            B(M2, "Yellow", 4, 8f),
            B(M2, "Yellow",  4, 9f),
            B(M2, "Red",    4, 10f)
        ),

        // Level 19: Triple tower
        new TowerLevel(
            B(M2, "Red",    0, 0f),
            B(M2, "Green",  3, 0f),
            B(M2, "Blue",   6, 0f),
            B(M2, "Red",    0, 1f),
            B(M2, "Green",  3, 1f),
            B(M2, "Blue",   6, 1f),
            B(M2, "Red",    0, 2f),
            B(M2, "Green",  3, 2f),
            B(M2, "Blue",   6, 2f),
            B(M6, "Yellow", 1, 3f),
            B(M6, "Yellow", 1, 4f),
            B(M4, "Yellow",  2, 5f),
            B(M2, "Red",    3, 6f)
        ),

        // Level 20: Tall spire
        new TowerLevel(
            B(M6, "Blue",   0, 0f),
            B(M6, "Green",  0, 1f),
            B(M6, "Red",    0, 2f),
            B(M4, "Yellow", 1, 3f),
            B(M4, "Blue",   1, 4f),
            B(M4, "Green",  1, 5f),
            B(M2, "Red",    2, 6f),
            B(M2, "Yellow", 2, 7f),
            B(M2, "Blue",   2, 8f),
            B(M2, "Green",  2, 9f),
            B(M2, "Red",    2, 10f),
            B(M2, "Yellow",  2, 11f),
            B(M2, "Yellow", 2, 12f)
        ),

        // Level 21: Castle with bridge
        new TowerLevel(
            B(M2, "Red",    0, 0f),
            B(M2, "Red",    6, 0f),
            B(M2, "Blue",   0, 1f),
            B(M2, "Blue",   6, 1f),
            B(M2, "Green",  0, 2f),
            B(M2, "Green",  6, 2f),
            B(M2, "Yellow", 0, 3f),
            B(M2, "Yellow", 6, 3f),
            B(M6, "Yellow",  1, 4f),
            B(M6, "Red",    1, 5f),
            B(M4, "Blue",   2, 6f),
            B(M4, "Green",  2, 7f),
            B(M2, "Yellow", 3, 8f),
            B(M2, "Yellow",  3, 9f)
        ),

        // Level 22: Stepped pyramid
        new TowerLevel(
            B(M6, "Red",    0, 0f),
            B(M4, "Red",    6, 0f),
            B(M6, "Blue",   0, 1f),
            B(M4, "Blue",   6, 1f),
            B(M4, "Green",  1, 2f),
            B(M4, "Green",  5, 2f),
            B(M4, "Yellow", 2, 3f),
            B(M6, "Yellow",  2, 4f),
            B(M6, "Red",    2, 5f),
            B(M4, "Blue",   3, 6f),
            B(M4, "Green",  3, 7f),
            B(M2, "Yellow", 4, 8f),
            B(M2, "Yellow",  4, 9f)
        ),

        // Level 23: Rainbow tower
        new TowerLevel(
            B(M4, "Red",    0, 0f),
            B(M4, "Red",    4, 0f),
            B(M4, "Blue",   0, 1f),
            B(M4, "Blue",   4, 1f),
            B(M2, "Green",  1, 2f),
            B(M2, "Green",  5, 2f),
            B(M2, "Yellow", 1, 3f),
            B(M2, "Yellow", 5, 3f),
            B(M4, "Yellow",  2, 4f),
            B(M4, "Red",    2, 5f),
            B(M4, "Blue",   2, 6f),
            B(M2, "Green",  3, 7f),
            B(M2, "Yellow", 3, 8f),
            B(M2, "Yellow",  3, 9f)
        ),
    };

    /// <summary>
    /// Returns all unique sprite asset keys (Color/brickType) used across all levels.
    /// </summary>
    public static HashSet<string> GetAllSpriteKeys()
    {
        var keys = new HashSet<string>();
        foreach (var level in All)
            foreach (var brick in level.bricks)
                keys.Add(brick.SpriteKey);
        return keys;
    }

    /// <summary>
    /// Returns indices of levels appropriate for the given difficulty.
    /// 0=Easy (3-4 bricks), 1=Medium (5-8), 2=Hard (9-12), 3=Very Hard (13+).
    /// </summary>
    public static List<int> GetLevelsForDifficulty(int difficulty)
    {
        var result = new List<int>();
        for (int i = 0; i < All.Length; i++)
        {
            int count = All[i].bricks.Length;
            int d;
            if (count <= 4) d = 0;
            else if (count <= 8) d = 1;
            else if (count <= 12) d = 2;
            else d = 3;

            if (d == difficulty) result.Add(i);
        }
        if (result.Count == 0) result.Add(0);
        return result;
    }
}
