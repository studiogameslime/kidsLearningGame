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
/// Levels increase in brick count and structural complexity.
/// </summary>
public static class TowerLevels
{
    public static readonly TowerLevel[] All = new TowerLevel[]
    {
        // Level 0: Easy (3 bricks) — Simple stack
        new TowerLevel(
            new BrickDef("brick_medium_4", "Red",    0, 0f),
            new BrickDef("brick_medium_4", "Blue",   0, 1f),
            new BrickDef("brick_medium_2", "Yellow", 1, 2f)
        ),

        // Level 1: Easy (4 bricks) — Pyramid
        new TowerLevel(
            new BrickDef("brick_medium_6", "Red",    0, 0f),
            new BrickDef("brick_medium_4", "Blue",   1, 1f),
            new BrickDef("brick_medium_4", "Green",  1, 2f),
            new BrickDef("brick_medium_2", "Yellow", 2, 3f)
        ),

        // Level 2: Medium (6 bricks) — Tall pyramid
        new TowerLevel(
            new BrickDef("brick_medium_6", "Red",    0, 0f),
            new BrickDef("brick_medium_6", "Blue",   0, 1f),
            new BrickDef("brick_medium_4", "Green",  1, 2f),
            new BrickDef("brick_medium_4", "Yellow", 1, 3f),
            new BrickDef("brick_medium_2", "White",  2, 4f),
            new BrickDef("brick_medium_2", "Red",    2, 5f)
        ),

        // Level 3: Medium (8 bricks) — Castle with pillars
        new TowerLevel(
            new BrickDef("brick_medium_6", "Red",    0, 0f),
            new BrickDef("brick_medium_2", "Blue",   0, 1f),
            new BrickDef("brick_medium_2", "Blue",   4, 1f),
            new BrickDef("brick_medium_2", "Green",  0, 2f),
            new BrickDef("brick_medium_2", "Green",  4, 2f),
            new BrickDef("brick_medium_6", "Yellow", 0, 3f),
            new BrickDef("brick_medium_4", "White",  1, 4f),
            new BrickDef("brick_medium_2", "Red",    2, 5f)
        ),

        // Level 4: Hard (11 bricks) — Tall castle
        new TowerLevel(
            new BrickDef("brick_medium_6", "Red",    0, 0f),
            new BrickDef("brick_medium_6", "Red",    0, 1f),
            new BrickDef("brick_medium_2", "Blue",   0, 2f),
            new BrickDef("brick_medium_2", "Blue",   4, 2f),
            new BrickDef("brick_medium_2", "Green",  0, 3f),
            new BrickDef("brick_medium_2", "Green",  4, 3f),
            new BrickDef("brick_medium_6", "Yellow", 0, 4f),
            new BrickDef("brick_medium_4", "White",  1, 5f),
            new BrickDef("brick_medium_4", "Blue",   1, 6f),
            new BrickDef("brick_medium_2", "Yellow", 2, 7f),
            new BrickDef("brick_medium_2", "Green",  2, 8f)
        ),

        // Level 5: Very Hard (15 bricks) — Wide fortress
        new TowerLevel(
            new BrickDef("brick_medium_6", "Red",    0, 0f),
            new BrickDef("brick_medium_4", "Red",    6, 0f),
            new BrickDef("brick_medium_6", "Blue",   0, 1f),
            new BrickDef("brick_medium_4", "Blue",   6, 1f),
            new BrickDef("brick_medium_4", "Green",  1, 2f),
            new BrickDef("brick_medium_4", "Green",  5, 2f),
            new BrickDef("brick_medium_4", "Yellow", 1, 3f),
            new BrickDef("brick_medium_4", "Yellow", 5, 3f),
            new BrickDef("brick_medium_6", "White",  2, 4f),
            new BrickDef("brick_medium_6", "Red",    2, 5f),
            new BrickDef("brick_medium_4", "Blue",   3, 6f),
            new BrickDef("brick_medium_4", "Green",  3, 7f),
            new BrickDef("brick_medium_2", "Yellow", 4, 8f),
            new BrickDef("brick_medium_2", "White",  4, 9f),
            new BrickDef("brick_medium_2", "Red",    4, 10f)
        ),
    };

    /// <summary>
    /// Returns all unique sprite asset keys (Color/brickType) used across all levels.
    /// The setup script uses this to pre-load sprites.
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
    /// 0=Easy, 1=Medium, 2=Hard, 3=Very Hard.
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
