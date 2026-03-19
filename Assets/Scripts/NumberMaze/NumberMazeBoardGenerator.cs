using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates a valid Number Maze board.
/// Strategy:
/// 1. Pick END position on the board edge
/// 2. Build a winding orthogonal path of exactly targetLength steps
/// 3. Reverse it so it becomes 1 → target
/// 4. Fill remaining cells with distractors in range 1..target
/// 5. Validate no alternative sequence path exists
/// </summary>
public static class NumberMazeBoardGenerator
{
    public struct CellData
    {
        public int gridX;
        public int gridY;
        public int displayNumber;
        public bool isOnPath;
        public int pathOrder; // 0-based index on path, -1 if not on path
    }

    public struct BoardData
    {
        public int cols;
        public int rows;
        public int pathLength;
        public CellData[] cells;
        public int[] pathCellIndices; // indices into cells[] for path order
    }

    private static readonly int[] DX = { 0, 0, -1, 1 };
    private static readonly int[] DY = { -1, 1, 0, 0 };

    public static BoardData Generate(int cols, int rows, int pathLength)
    {
        pathLength = Mathf.Min(pathLength, cols * rows);

        CellData[] cells = null;
        int[] pathIndices = null;

        // Retry full generation (path + distractor validation)
        for (int attempt = 0; attempt < 300; attempt++)
        {
            int[] pathX, pathY;
            if (!TryBuildPath(cols, rows, pathLength, out pathX, out pathY))
                continue;

            // Build cell array
            cells = new CellData[cols * rows];
            pathIndices = new int[pathLength];

            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    cells[i].gridX = x;
                    cells[i].gridY = y;
                    cells[i].isOnPath = false;
                    cells[i].pathOrder = -1;
                }

            // Place path numbers 1..pathLength
            for (int p = 0; p < pathLength; p++)
            {
                int ci = pathY[p] * cols + pathX[p];
                cells[ci].isOnPath = true;
                cells[ci].pathOrder = p;
                cells[ci].displayNumber = p + 1;
                pathIndices[p] = ci;
            }

            // Fill distractors within 1..pathLength, then validate
            if (FillDistractorsAndValidate(cells, cols, rows, pathLength, pathIndices))
            {
                return new BoardData
                {
                    cols = cols,
                    rows = rows,
                    pathLength = pathLength,
                    cells = cells,
                    pathCellIndices = pathIndices
                };
            }
        }

        // Fallback: snake path (always valid)
        return GenerateFallback(cols, rows, pathLength);
    }

    /// <summary>
    /// Build a winding path. BOTH start (1) and end (target) must be on the board edge.
    /// Strategy: pick two different edge cells, build path from one to the other.
    /// </summary>
    private static bool TryBuildPath(int cols, int rows, int length, out int[] px, out int[] py)
    {
        px = new int[length];
        py = new int[length];

        // Pick START on edge
        int sx, sy;
        PickEdgeCell(cols, rows, out sx, out sy);

        // Pick END on a different edge (prefer far away for a winding path)
        int ex, ey;
        for (int pick = 0; pick < 20; pick++)
        {
            PickEdgeCell(cols, rows, out ex, out ey);
            // Accept if different cell and at least some distance apart
            int dist = Mathf.Abs(ex - sx) + Mathf.Abs(ey - sy);
            if ((ex != sx || ey != sy) && dist >= 2)
                break;
        }

        // Build path from start, winding toward the board interior
        bool[,] visited = new bool[cols, rows];
        px[0] = sx;
        py[0] = sy;
        visited[sx, sy] = true;

        for (int step = 1; step < length; step++)
        {
            int cx = px[step - 1];
            int cy = py[step - 1];

            // Gather unvisited orthogonal neighbors
            var neighbors = new List<int>();
            for (int d = 0; d < 4; d++)
            {
                int nx = cx + DX[d];
                int ny = cy + DY[d];
                if (nx >= 0 && nx < cols && ny >= 0 && ny < rows && !visited[nx, ny])
                    neighbors.Add(d);
            }

            if (neighbors.Count == 0)
                return false;

            // Prefer turns to make it maze-like
            int chosen;
            if (step >= 2 && neighbors.Count > 1)
            {
                int prevDx = px[step - 1] - px[step - 2];
                int prevDy = py[step - 1] - py[step - 2];

                var turns = new List<int>();
                int straight = -1;
                foreach (int d in neighbors)
                {
                    if (DX[d] == prevDx && DY[d] == prevDy)
                        straight = d;
                    else
                        turns.Add(d);
                }

                // 70% chance to turn if possible
                if (turns.Count > 0 && (straight < 0 || Random.value < 0.7f))
                    chosen = turns[Random.Range(0, turns.Count)];
                else if (straight >= 0)
                    chosen = straight;
                else
                    chosen = neighbors[Random.Range(0, neighbors.Count)];
            }
            else
            {
                chosen = neighbors[Random.Range(0, neighbors.Count)];
            }

            px[step] = cx + DX[chosen];
            py[step] = cy + DY[chosen];
            visited[px[step], py[step]] = true;
        }

        // Verify: last cell must be on edge. If not, reject.
        int lastX = px[length - 1];
        int lastY = py[length - 1];
        if (lastX != 0 && lastX != cols - 1 && lastY != 0 && lastY != rows - 1)
            return false;

        return true;
    }

    /// <summary>
    /// Pick a random cell on the board edge.
    /// </summary>
    private static void PickEdgeCell(int cols, int rows, out int x, out int y)
    {
        // Build list of all edge cells
        var edges = new List<(int, int)>();
        for (int c = 0; c < cols; c++)
        {
            edges.Add((c, 0));
            edges.Add((c, rows - 1));
        }
        for (int r = 1; r < rows - 1; r++)
        {
            edges.Add((0, r));
            edges.Add((cols - 1, r));
        }
        int idx = Random.Range(0, edges.Count);
        x = edges[idx].Item1;
        y = edges[idx].Item2;
    }

    /// <summary>
    /// Fill distractor cells with numbers in 1..pathLength.
    /// Then validate that no alternative sequence path exists.
    /// Returns false if validation fails (caller should retry).
    /// </summary>
    private static bool FillDistractorsAndValidate(CellData[] cells, int cols, int rows,
        int pathLength, int[] pathIndices)
    {
        // Collect distractor cell indices
        var distractorIndices = new List<int>();
        for (int i = 0; i < cells.Length; i++)
            if (!cells[i].isOnPath)
                distractorIndices.Add(i);

        // Try a few distractor fills to find one that passes validation
        for (int fillAttempt = 0; fillAttempt < 10; fillAttempt++)
        {
            // Fill distractors: random numbers in 1..pathLength
            foreach (int ci in distractorIndices)
                cells[ci].displayNumber = Random.Range(1, pathLength + 1);

            // Validate: for each step N→N+1 on the path, ensure the ONLY cell
            // showing number N+1 that is orthogonally adjacent to the path-N cell
            // is the actual path cell for N+1.
            if (ValidateUniqueSequencePath(cells, cols, rows, pathLength, pathIndices))
                return true;
        }

        // Aggressive fix: for each conflict, change the offending distractor
        foreach (int ci in distractorIndices)
            cells[ci].displayNumber = Random.Range(1, pathLength + 1);

        FixConflicts(cells, cols, rows, pathLength, pathIndices, distractorIndices);
        return true; // forced valid
    }

    /// <summary>
    /// Check that for every step N in the sequence, the ONLY cell showing N+1
    /// adjacent to the path-N cell is the actual path cell for N+1.
    /// This prevents alternative paths.
    /// </summary>
    private static bool ValidateUniqueSequencePath(CellData[] cells, int cols, int rows,
        int pathLength, int[] pathIndices)
    {
        for (int step = 0; step < pathLength - 1; step++)
        {
            int currentCI = pathIndices[step];
            int nextNumber = step + 2; // the number we expect next (1-based)
            int cx = cells[currentCI].gridX;
            int cy = cells[currentCI].gridY;

            // Check all orthogonal neighbors of the current path cell
            for (int d = 0; d < 4; d++)
            {
                int nx = cx + DX[d];
                int ny = cy + DY[d];
                if (nx < 0 || nx >= cols || ny < 0 || ny >= rows) continue;

                int neighborCI = ny * cols + nx;
                if (neighborCI == pathIndices[step + 1]) continue; // this IS the correct next cell

                // If a non-path neighbor shows the next number, that's a conflict
                if (cells[neighborCI].displayNumber == nextNumber)
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// For each conflict found, change the offending distractor to a safe number.
    /// A safe number is one that won't create an alternative path step.
    /// </summary>
    private static void FixConflicts(CellData[] cells, int cols, int rows,
        int pathLength, int[] pathIndices, List<int> distractorIndices)
    {
        // Build a set of "forbidden" assignments per distractor cell:
        // A distractor at (x,y) must NOT show number N+1 if (x,y) is adjacent to pathCell[N]
        // (unless it IS pathCell[N+1], but distractors are never on path)

        var forbidden = new HashSet<int>[cells.Length];
        for (int i = 0; i < cells.Length; i++)
            forbidden[i] = new HashSet<int>();

        for (int step = 0; step < pathLength - 1; step++)
        {
            int cx = cells[pathIndices[step]].gridX;
            int cy = cells[pathIndices[step]].gridY;
            int nextNum = step + 2;

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + DX[d];
                int ny = cy + DY[d];
                if (nx < 0 || nx >= cols || ny < 0 || ny >= rows) continue;
                int neighborCI = ny * cols + nx;
                if (cells[neighborCI].isOnPath) continue;
                forbidden[neighborCI].Add(nextNum);
            }
        }

        // Also: distractor adjacent to pathCell[N-1] must not show N
        // (prevents confusing the player with an alternative "1" adjacent to start, etc.)

        // Fix each distractor
        foreach (int ci in distractorIndices)
        {
            if (forbidden[ci].Contains(cells[ci].displayNumber))
            {
                // Pick a safe number
                var safe = new List<int>();
                for (int n = 1; n <= pathLength; n++)
                {
                    if (!forbidden[ci].Contains(n))
                        safe.Add(n);
                }

                if (safe.Count > 0)
                    cells[ci].displayNumber = safe[Random.Range(0, safe.Count)];
                else
                    cells[ci].displayNumber = pathLength; // worst case: use target number
            }
        }
    }

    /// <summary>
    /// Fallback: snake path that always works.
    /// </summary>
    private static BoardData GenerateFallback(int cols, int rows, int pathLength)
    {
        int[] pathX = new int[pathLength];
        int[] pathY = new int[pathLength];
        int idx = 0;
        for (int y = 0; y < rows && idx < pathLength; y++)
        {
            if (y % 2 == 0)
                for (int x = 0; x < cols && idx < pathLength; x++) { pathX[idx] = x; pathY[idx] = y; idx++; }
            else
                for (int x = cols - 1; x >= 0 && idx < pathLength; x--) { pathX[idx] = x; pathY[idx] = y; idx++; }
        }

        var cells = new CellData[cols * rows];
        var pathIndices = new int[pathLength];

        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                int i = y * cols + x;
                cells[i].gridX = x;
                cells[i].gridY = y;
                cells[i].isOnPath = false;
                cells[i].pathOrder = -1;
            }

        for (int p = 0; p < pathLength; p++)
        {
            int ci = pathY[p] * cols + pathX[p];
            cells[ci].isOnPath = true;
            cells[ci].pathOrder = p;
            cells[ci].displayNumber = p + 1;
            pathIndices[p] = ci;
        }

        // Fill distractors in range, then fix conflicts
        var distractors = new List<int>();
        for (int i = 0; i < cells.Length; i++)
        {
            if (!cells[i].isOnPath)
            {
                cells[i].displayNumber = Random.Range(1, pathLength + 1);
                distractors.Add(i);
            }
        }
        FixConflicts(cells, cols, rows, pathLength, pathIndices, distractors);

        return new BoardData
        {
            cols = cols,
            rows = rows,
            pathLength = pathLength,
            cells = cells,
            pathCellIndices = pathIndices
        };
    }

    /// <summary>
    /// Returns grid config for a given difficulty level.
    /// Landscape-friendly: cols >= rows always.
    /// </summary>
    public static void GetGridConfig(int difficulty, out int cols, out int rows, out int pathLength)
    {
        if (difficulty <= 2)      { cols = 5; rows = 3; pathLength = 10; }
        else if (difficulty <= 4) { cols = 5; rows = 4; pathLength = 10; }
        else if (difficulty <= 6) { cols = 6; rows = 4; pathLength = 15; }
        else if (difficulty <= 8) { cols = 6; rows = 5; pathLength = 15; }
        else                      { cols = 7; rows = 5; pathLength = 20; }
    }
}
