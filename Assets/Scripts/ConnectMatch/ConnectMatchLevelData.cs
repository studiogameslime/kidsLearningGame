using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Static level definitions for Connect and Match.
/// Each level defines a grid size and a target path the child must recreate.
/// No ScriptableObject needed — patterns are generated from difficulty.
/// </summary>
public static class ConnectMatchLevelData
{
    [System.Serializable]
    public struct LevelConfig
    {
        public int gridCols;
        public int gridRows;
        public Vector2Int[] targetPath; // ordered grid coordinates
        public bool requireExactOrder;
        public bool allowDiagonals;
    }

    /// <summary>
    /// Generate a level config for the given difficulty.
    /// </summary>
    public static LevelConfig Generate(int difficulty)
    {
        int cols, rows, pathLen;
        bool diagonals, exactOrder;
        GetDifficultyParams(difficulty, out cols, out rows, out pathLen, out diagonals, out exactOrder);

        var path = GeneratePath(cols, rows, pathLen, diagonals);

        return new LevelConfig
        {
            gridCols = cols,
            gridRows = rows,
            targetPath = path,
            requireExactOrder = exactOrder,
            allowDiagonals = diagonals
        };
    }

    private static void GetDifficultyParams(int difficulty,
        out int cols, out int rows, out int pathLen,
        out bool diagonals, out bool exactOrder)
    {
        if (difficulty <= 2)      { cols = 2; rows = 2; pathLen = 3;  diagonals = false; exactOrder = false; }
        else if (difficulty <= 4) { cols = 3; rows = 3; pathLen = 4;  diagonals = false; exactOrder = false; }
        else if (difficulty <= 6) { cols = 3; rows = 3; pathLen = 5;  diagonals = false; exactOrder = true; }
        else if (difficulty <= 8) { cols = 4; rows = 4; pathLen = 6;  diagonals = true;  exactOrder = true; }
        else                      { cols = 4; rows = 4; pathLen = 8;  diagonals = true;  exactOrder = true; }
    }

    private static readonly int[] DX4 = { 0, 0, -1, 1 };
    private static readonly int[] DY4 = { -1, 1, 0, 0 };
    private static readonly int[] DX8 = { 0, 0, -1, 1, -1, -1, 1, 1 };
    private static readonly int[] DY8 = { -1, 1, 0, 0, -1, 1, -1, 1 };

    private static Vector2Int[] GeneratePath(int cols, int rows, int length, bool diags)
    {
        int[] dx = diags ? DX8 : DX4;
        int[] dy = diags ? DY8 : DY4;
        int dirs = dx.Length;

        for (int attempt = 0; attempt < 200; attempt++)
        {
            var path = new Vector2Int[length];
            bool[,] visited = new bool[cols, rows];

            // Start from random cell
            path[0] = new Vector2Int(Random.Range(0, cols), Random.Range(0, rows));
            visited[path[0].x, path[0].y] = true;

            bool ok = true;
            for (int step = 1; step < length; step++)
            {
                var cur = path[step - 1];
                var neighbors = new System.Collections.Generic.List<int>();

                for (int d = 0; d < dirs; d++)
                {
                    int nx = cur.x + dx[d];
                    int ny = cur.y + dy[d];
                    if (nx >= 0 && nx < cols && ny >= 0 && ny < rows && !visited[nx, ny])
                        neighbors.Add(d);
                }

                if (neighbors.Count == 0) { ok = false; break; }

                // Prefer turns for interesting shapes
                int chosen;
                if (step >= 2 && neighbors.Count > 1)
                {
                    var prevDir = path[step - 1] - path[step - 2];
                    var turns = new System.Collections.Generic.List<int>();
                    int straight = -1;
                    foreach (int d in neighbors)
                    {
                        if (dx[d] == prevDir.x && dy[d] == prevDir.y) straight = d;
                        else turns.Add(d);
                    }
                    if (turns.Count > 0 && (straight < 0 || Random.value < 0.6f))
                        chosen = turns[Random.Range(0, turns.Count)];
                    else if (straight >= 0) chosen = straight;
                    else chosen = neighbors[Random.Range(0, neighbors.Count)];
                }
                else chosen = neighbors[Random.Range(0, neighbors.Count)];

                int nextX = cur.x + dx[chosen];
                int nextY = cur.y + dy[chosen];
                path[step] = new Vector2Int(nextX, nextY);
                visited[nextX, nextY] = true;
            }

            if (ok) return path;
        }

        // Fallback: simple L-shape
        var fallback = new Vector2Int[length];
        for (int i = 0; i < length; i++)
        {
            if (i < cols) fallback[i] = new Vector2Int(i, 0);
            else fallback[i] = new Vector2Int(cols - 1, i - cols + 1);
        }
        return fallback;
    }
}
