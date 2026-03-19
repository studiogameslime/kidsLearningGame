using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural pattern generator for Pattern Copy game.
/// Produces varied, recognizable, child-friendly grid patterns.
/// Uses a library of template shapes + transformations for maximum variety.
/// </summary>
public static class PatternGenerator
{
    public struct PatternData
    {
        public int gridSize;
        public bool[,] cells;
        public int filledCount;
    }

    public static PatternData Generate(int difficulty)
    {
        int gridSize = GetGridSize(difficulty);
        float density = GetDensity(difficulty);
        int targetFilled = Mathf.Max(2, Mathf.RoundToInt(gridSize * gridSize * density));

        bool[,] cells;
        if (difficulty <= 3)
            cells = GenerateSimple(gridSize, targetFilled);
        else if (difficulty <= 6)
            cells = GenerateMedium(gridSize, targetFilled);
        else
            cells = GenerateHard(gridSize, targetFilled);

        int filled = CountFilled(cells);
        return new PatternData { gridSize = gridSize, cells = cells, filledCount = filled };
    }

    public static int GetGridSize(int difficulty)
    {
        if (difficulty <= 2) return 3;
        if (difficulty <= 4) return 4;
        if (difficulty <= 6) return 5;
        if (difficulty <= 8) return 6;
        return 7;
    }

    public static float GetDensity(int difficulty)
    {
        return Mathf.Lerp(0.30f, 0.50f, (difficulty - 1) / 9f);
    }

    // ═══════════════════════════════════════════════════════════
    //  TEMPLATE LIBRARY — small shapes stamped onto the grid
    // ═══════════════════════════════════════════════════════════

    // Each template is a list of (row, col) offsets from an origin.
    // They get randomly placed, rotated, and flipped onto the grid.

    // ── 2-cell shapes ──
    private static readonly int[][] Domino_H = { new[]{0,0}, new[]{0,1} };
    private static readonly int[][] Domino_V = { new[]{0,0}, new[]{1,0} };

    // ── 3-cell shapes ──
    private static readonly int[][] L_Small  = { new[]{0,0}, new[]{1,0}, new[]{1,1} };
    private static readonly int[][] Line3_H  = { new[]{0,0}, new[]{0,1}, new[]{0,2} };
    private static readonly int[][] Line3_V  = { new[]{0,0}, new[]{1,0}, new[]{2,0} };
    private static readonly int[][] Corner3  = { new[]{0,0}, new[]{0,1}, new[]{1,0} };

    // ── 4-cell shapes ──
    private static readonly int[][] Square2  = { new[]{0,0}, new[]{0,1}, new[]{1,0}, new[]{1,1} };
    private static readonly int[][] T_Shape  = { new[]{0,0}, new[]{0,1}, new[]{0,2}, new[]{1,1} };
    private static readonly int[][] S_Shape  = { new[]{0,1}, new[]{0,2}, new[]{1,0}, new[]{1,1} };
    private static readonly int[][] Z_Shape  = { new[]{0,0}, new[]{0,1}, new[]{1,1}, new[]{1,2} };
    private static readonly int[][] L_Shape  = { new[]{0,0}, new[]{1,0}, new[]{2,0}, new[]{2,1} };
    private static readonly int[][] Line4_H  = { new[]{0,0}, new[]{0,1}, new[]{0,2}, new[]{0,3} };
    private static readonly int[][] Stairs4  = { new[]{0,0}, new[]{1,1}, new[]{2,2}, new[]{3,3} };

    // ── 5-cell shapes ──
    private static readonly int[][] Plus     = { new[]{0,1}, new[]{1,0}, new[]{1,1}, new[]{1,2}, new[]{2,1} };
    private static readonly int[][] U_Shape  = { new[]{0,0}, new[]{1,0}, new[]{2,0}, new[]{2,1}, new[]{2,2} };
    private static readonly int[][] Arrow_Up = { new[]{0,1}, new[]{1,0}, new[]{1,1}, new[]{1,2}, new[]{2,0} };
    private static readonly int[][] C_Shape  = { new[]{0,0}, new[]{0,1}, new[]{1,0}, new[]{2,0}, new[]{2,1} };
    private static readonly int[][] Zigzag5  = { new[]{0,0}, new[]{0,1}, new[]{1,1}, new[]{1,2}, new[]{2,2} };

    // ── 6+ cell shapes ──
    private static readonly int[][] Ring3    = { new[]{0,0}, new[]{0,1}, new[]{0,2}, new[]{1,0}, new[]{1,2}, new[]{2,0}, new[]{2,1}, new[]{2,2} };
    private static readonly int[][] Diamond  = { new[]{0,1}, new[]{1,0}, new[]{1,2}, new[]{2,1} };
    private static readonly int[][] BigL     = { new[]{0,0}, new[]{1,0}, new[]{2,0}, new[]{3,0}, new[]{3,1}, new[]{3,2} };
    private static readonly int[][] Cross5   = { new[]{0,2}, new[]{1,1}, new[]{1,2}, new[]{1,3}, new[]{2,0}, new[]{2,1}, new[]{2,2}, new[]{2,3}, new[]{2,4}, new[]{3,1}, new[]{3,2}, new[]{3,3}, new[]{4,2} };
    private static readonly int[][] Heart    = { new[]{0,1}, new[]{0,3}, new[]{1,0}, new[]{1,1}, new[]{1,2}, new[]{1,3}, new[]{1,4}, new[]{2,0}, new[]{2,1}, new[]{2,2}, new[]{2,3}, new[]{2,4}, new[]{3,1}, new[]{3,2}, new[]{3,3}, new[]{4,2} };

    // Shape pools per difficulty tier
    private static readonly int[][][] SimpleShapes =
    {
        Domino_H, Domino_V, L_Small, Line3_H, Line3_V, Corner3,
        Square2, T_Shape, Diamond
    };

    private static readonly int[][][] MediumShapes =
    {
        T_Shape, S_Shape, Z_Shape, L_Shape, Line4_H, Stairs4,
        Plus, U_Shape, Arrow_Up, C_Shape, Zigzag5, Square2, Diamond
    };

    private static readonly int[][][] HardShapes =
    {
        Plus, U_Shape, Arrow_Up, C_Shape, Zigzag5, BigL, Ring3,
        T_Shape, S_Shape, Z_Shape, L_Shape
    };

    // ═══════════════════════════════════════════════════════════
    //  GENERATION BY DIFFICULTY
    // ═══════════════════════════════════════════════════════════

    private static bool[,] GenerateSimple(int size, int target)
    {
        // Pick from diverse strategies
        int strategy = Random.Range(0, 8);
        switch (strategy)
        {
            case 0: return StampSingle(size, target, SimpleShapes);
            case 1: return StampMultiple(size, target, SimpleShapes);
            case 2: return ScatteredDots(size, target);
            case 3: return DiagonalPattern(size, target);
            case 4: return CornerShape(size, target);
            case 5: return RowWithGap(size, target);
            case 6: return TwoCorners(size, target);
            default: return StampSingle(size, target, SimpleShapes);
        }
    }

    private static bool[,] GenerateMedium(int size, int target)
    {
        int strategy = Random.Range(0, 8);
        switch (strategy)
        {
            case 0: return StampMultiple(size, target, MediumShapes);
            case 1: return SymmetricVertical(size, target);
            case 2: return SymmetricHorizontal(size, target);
            case 3: return BorderPartial(size, target);
            case 4: return CheckerRegion(size, target);
            case 5: return StampSingle(size, target, MediumShapes);
            case 6: return CrossVariant(size, target);
            default: return StairsPattern(size, target);
        }
    }

    private static bool[,] GenerateHard(int size, int target)
    {
        int strategy = Random.Range(0, 8);
        switch (strategy)
        {
            case 0: return StampMultiple(size, target, HardShapes);
            case 1: return SymmetricBoth(size, target);
            case 2: return FrameVariant(size, target);
            case 3: return MultiCluster(size, target);
            case 4: return StampSingle(size, target, HardShapes);
            case 5: return DiagonalBand(size, target);
            case 6: return InvertedRegion(size, target);
            default: return SymmetricVertical(size, target);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  PATTERN STRATEGIES
    // ═══════════════════════════════════════════════════════════

    /// <summary>Stamp one template shape, randomly rotated and placed.</summary>
    private static bool[,] StampSingle(int size, int target, int[][][] pool)
    {
        var cells = new bool[size, size];
        var shape = pool[Random.Range(0, pool.Length)];
        var transformed = TransformShape(shape);
        PlaceShape(cells, size, transformed);
        AdjustToTarget(cells, size, target);
        return cells;
    }

    /// <summary>Stamp 2-3 different template shapes, building up to target.</summary>
    private static bool[,] StampMultiple(int size, int target, int[][][] pool)
    {
        var cells = new bool[size, size];
        int stamps = Random.Range(2, 4);
        for (int i = 0; i < stamps; i++)
        {
            var shape = pool[Random.Range(0, pool.Length)];
            var transformed = TransformShape(shape);
            PlaceShape(cells, size, transformed);
        }
        AdjustToTarget(cells, size, target);
        return cells;
    }

    /// <summary>Spread individual dots in a visually interesting pattern (not clumped).</summary>
    private static bool[,] ScatteredDots(int size, int target)
    {
        var cells = new bool[size, size];
        // Place dots with minimum spacing of 1 cell apart
        var placed = new List<(int r, int c)>();
        int attempts = 0;
        while (placed.Count < target && attempts < 300)
        {
            attempts++;
            int r = Random.Range(0, size);
            int c = Random.Range(0, size);
            if (cells[r, c]) continue;

            // Check spacing
            bool tooClose = false;
            foreach (var p in placed)
            {
                if (Mathf.Abs(p.r - r) <= 1 && Mathf.Abs(p.c - c) <= 1 && !(p.r == r && p.c == c))
                {
                    tooClose = true;
                    break;
                }
            }
            // Allow closer placement after many attempts
            if (tooClose && attempts < 150) continue;

            cells[r, c] = true;
            placed.Add((r, c));
        }
        return cells;
    }

    /// <summary>Diagonal line or staircase pattern.</summary>
    private static bool[,] DiagonalPattern(int size, int target)
    {
        var cells = new bool[size, size];
        bool ascending = Random.value > 0.5f;
        bool thick = target > size; // double diagonal

        for (int i = 0; i < size && CountFilled(cells) < target; i++)
        {
            int c = ascending ? i : (size - 1 - i);
            if (i < size && c >= 0 && c < size) cells[i, c] = true;
            // Thicken: add adjacent cell
            if (thick && c + 1 < size) cells[i, c + 1] = true;
            else if (thick && c - 1 >= 0) cells[i, c - 1] = true;
        }
        AdjustToTarget(cells, size, target);
        return cells;
    }

    /// <summary>Shape anchored to a corner — L, square, triangle feel.</summary>
    private static bool[,] CornerShape(int size, int target)
    {
        var cells = new bool[size, size];
        int corner = Random.Range(0, 4);

        // Build a triangle/stair from the corner
        int placed = 0;
        for (int layer = 0; placed < target && layer < size; layer++)
        {
            int cellsInLayer = Mathf.Min(layer + 1, target - placed);
            for (int i = 0; i < cellsInLayer && placed < target; i++)
            {
                int r, c;
                switch (corner)
                {
                    case 0: r = layer; c = i; break;       // top-left
                    case 1: r = layer; c = size-1-i; break; // top-right
                    case 2: r = size-1-layer; c = i; break; // bottom-left
                    default: r = size-1-layer; c = size-1-i; break; // bottom-right
                }
                if (r >= 0 && r < size && c >= 0 && c < size && !cells[r, c])
                {
                    cells[r, c] = true;
                    placed++;
                }
            }
        }
        return cells;
    }

    /// <summary>A row with one or two gaps — teaches attention to detail.</summary>
    private static bool[,] RowWithGap(int size, int target)
    {
        var cells = new bool[size, size];
        // Fill 1-2 rows, then remove a random cell from each
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)target / size));
        int startRow = Random.Range(0, Mathf.Max(1, size - rows));

        for (int r = startRow; r < startRow + rows && r < size; r++)
            for (int c = 0; c < size; c++)
                cells[r, c] = true;

        // Remove 1-2 random cells to create gaps
        int gaps = Random.Range(1, 3);
        for (int g = 0; g < gaps; g++)
        {
            int r = Random.Range(startRow, Mathf.Min(startRow + rows, size));
            int c = Random.Range(0, size);
            cells[r, c] = false;
        }

        AdjustToTarget(cells, size, target);
        return cells;
    }

    /// <summary>Two small clusters in opposite corners.</summary>
    private static bool[,] TwoCorners(int size, int target)
    {
        var cells = new bool[size, size];
        int half = Mathf.Max(1, target / 2);

        // Pick two opposite corners
        bool topLeftFirst = Random.value > 0.5f;
        int r1 = topLeftFirst ? 0 : size - 1;
        int c1 = topLeftFirst ? 0 : size - 1;
        int r2 = topLeftFirst ? size - 1 : 0;
        int c2 = topLeftFirst ? size - 1 : 0;

        GrowCluster(cells, size, r1, c1, half);
        GrowCluster(cells, size, r2, c2, target - half);
        return cells;
    }

    /// <summary>Vertical mirror symmetry.</summary>
    private static bool[,] SymmetricVertical(int size, int target)
    {
        var cells = new bool[size, size];
        int half = (size + 1) / 2;
        int placed = 0;
        int attempts = 0;
        while (placed < target && attempts < 300)
        {
            attempts++;
            int r = Random.Range(0, size);
            int c = Random.Range(0, half);
            if (!cells[r, c])
            {
                cells[r, c] = true;
                int mc = size - 1 - c;
                cells[r, mc] = true;
                placed += (c == mc) ? 1 : 2;
            }
        }
        AdjustToTarget(cells, size, target);
        return cells;
    }

    /// <summary>Horizontal mirror symmetry.</summary>
    private static bool[,] SymmetricHorizontal(int size, int target)
    {
        var cells = new bool[size, size];
        int half = (size + 1) / 2;
        int placed = 0;
        int attempts = 0;
        while (placed < target && attempts < 300)
        {
            attempts++;
            int r = Random.Range(0, half);
            int c = Random.Range(0, size);
            if (!cells[r, c])
            {
                cells[r, c] = true;
                int mr = size - 1 - r;
                cells[mr, c] = true;
                placed += (r == mr) ? 1 : 2;
            }
        }
        AdjustToTarget(cells, size, target);
        return cells;
    }

    /// <summary>Partial border — 2-3 sides of a frame.</summary>
    private static bool[,] BorderPartial(int size, int target)
    {
        var cells = new bool[size, size];
        // Pick 2-3 sides to fill
        bool[] sides = { Random.value > 0.3f, Random.value > 0.3f, Random.value > 0.3f, Random.value > 0.3f };
        // Ensure at least 2 sides
        int sideCount = 0;
        for (int i = 0; i < 4; i++) if (sides[i]) sideCount++;
        while (sideCount < 2) { int i = Random.Range(0, 4); if (!sides[i]) { sides[i] = true; sideCount++; } }

        for (int i = 0; i < size; i++)
        {
            if (sides[0]) cells[0, i] = true;           // top
            if (sides[1]) cells[size - 1, i] = true;    // bottom
            if (sides[2]) cells[i, 0] = true;           // left
            if (sides[3]) cells[i, size - 1] = true;    // right
        }
        AdjustToTarget(cells, size, target);
        return cells;
    }

    /// <summary>Checkerboard in a region of the grid.</summary>
    private static bool[,] CheckerRegion(int size, int target)
    {
        var cells = new bool[size, size];
        int offset = Random.Range(0, 2); // 0 or 1 phase
        // Fill a rectangular region with checkerboard
        int r0 = Random.Range(0, size / 2);
        int c0 = Random.Range(0, size / 2);
        int r1 = Random.Range(r0 + 2, size + 1);
        int c1 = Random.Range(c0 + 2, size + 1);

        for (int r = r0; r < Mathf.Min(r1, size); r++)
            for (int c = c0; c < Mathf.Min(c1, size); c++)
                if ((r + c) % 2 == offset) cells[r, c] = true;

        AdjustToTarget(cells, size, target);
        return cells;
    }

    /// <summary>Plus/cross with variations — offset, thick, partial.</summary>
    private static bool[,] CrossVariant(int size, int target)
    {
        var cells = new bool[size, size];
        int centerR = Random.Range(size / 2 - 1, size / 2 + 1);
        int centerC = Random.Range(size / 2 - 1, size / 2 + 1);
        centerR = Mathf.Clamp(centerR, 1, size - 2);
        centerC = Mathf.Clamp(centerC, 1, size - 2);

        // Horizontal arm
        for (int c = 0; c < size; c++) cells[centerR, c] = true;
        // Vertical arm
        for (int r = 0; r < size; r++) cells[r, centerC] = true;

        // Randomly trim arms for variety
        int trimCount = Random.Range(1, 4);
        for (int t = 0; t < trimCount; t++)
        {
            if (Random.value > 0.5f)
                cells[Random.Range(0, size), centerC] = false;
            else
                cells[centerR, Random.Range(0, size)] = false;
        }
        // Always keep center
        cells[centerR, centerC] = true;

        AdjustToTarget(cells, size, target);
        return cells;
    }

    /// <summary>Staircase / zigzag going across the grid.</summary>
    private static bool[,] StairsPattern(int size, int target)
    {
        var cells = new bool[size, size];
        bool goRight = Random.value > 0.5f;
        int stepWidth = Random.Range(1, 3);
        int r = 0, c = goRight ? 0 : size - 1;
        int placed = 0;

        while (r < size && placed < target)
        {
            for (int w = 0; w < stepWidth && placed < target; w++)
            {
                int cc = Mathf.Clamp(c + (goRight ? w : -w), 0, size - 1);
                if (!cells[r, cc])
                {
                    cells[r, cc] = true;
                    placed++;
                }
            }
            r++;
            c += goRight ? 1 : -1;
            c = Mathf.Clamp(c, 0, size - 1);
        }
        AdjustToTarget(cells, size, target);
        return cells;
    }

    /// <summary>Both-axis symmetry for hard patterns.</summary>
    private static bool[,] SymmetricBoth(int size, int target)
    {
        var cells = new bool[size, size];
        int halfR = (size + 1) / 2;
        int halfC = (size + 1) / 2;
        int placed = 0;
        int attempts = 0;
        while (placed < target && attempts < 300)
        {
            attempts++;
            int r = Random.Range(0, halfR);
            int c = Random.Range(0, halfC);
            if (!cells[r, c])
            {
                int mr = size - 1 - r;
                int mc = size - 1 - c;
                cells[r, c] = true;
                cells[r, mc] = true;
                cells[mr, c] = true;
                cells[mr, mc] = true;
                var set = new HashSet<int> { r*size+c, r*size+mc, mr*size+c, mr*size+mc };
                placed += set.Count;
            }
        }
        AdjustToTarget(cells, size, target);
        return cells;
    }

    /// <summary>Frame/border with holes or partial fill.</summary>
    private static bool[,] FrameVariant(int size, int target)
    {
        var cells = new bool[size, size];
        // Full border
        for (int i = 0; i < size; i++)
        {
            cells[0, i] = true;
            cells[size-1, i] = true;
            cells[i, 0] = true;
            cells[i, size-1] = true;
        }
        // Punch holes in the frame
        int holes = CountFilled(cells) - target;
        if (holes > 0)
        {
            var border = new List<(int r, int c)>();
            for (int r = 0; r < size; r++)
                for (int c = 0; c < size; c++)
                    if (cells[r, c] && !IsCorner(r, c, size)) // keep corners
                        border.Add((r, c));
            Shuffle(border);
            for (int i = 0; i < holes && i < border.Count; i++)
                cells[border[i].r, border[i].c] = false;
        }
        AdjustToTarget(cells, size, target);
        return cells;
    }

    /// <summary>Multiple organic clusters spread across the grid.</summary>
    private static bool[,] MultiCluster(int size, int target)
    {
        var cells = new bool[size, size];
        int clusters = Random.Range(2, 4);
        int perCluster = Mathf.Max(2, target / clusters);

        for (int i = 0; i < clusters; i++)
        {
            // Try to space clusters apart
            int startR = (i < 2) ? Random.Range(0, size/2) : Random.Range(size/2, size);
            int startC = (i % 2 == 0) ? Random.Range(0, size/2) : Random.Range(size/2, size);
            GrowCluster(cells, size, startR, startC, perCluster);
        }
        AdjustToTarget(cells, size, target);
        return cells;
    }

    /// <summary>Thick diagonal band across the grid.</summary>
    private static bool[,] DiagonalBand(int size, int target)
    {
        var cells = new bool[size, size];
        bool ascending = Random.value > 0.5f;
        int bandwidth = Random.Range(2, 4);

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                int diag = ascending ? (r + c) : (r + (size - 1 - c));
                int center = size - 1;
                if (Mathf.Abs(diag - center) < bandwidth)
                    cells[r, c] = true;
            }
        }
        AdjustToTarget(cells, size, target);
        return cells;
    }

    /// <summary>Fill a region then carve out a shape (negative space).</summary>
    private static bool[,] InvertedRegion(int size, int target)
    {
        var cells = new bool[size, size];
        // Fill a rectangular region
        int margin = 1;
        for (int r = margin; r < size - margin; r++)
            for (int c = margin; c < size - margin; c++)
                cells[r, c] = true;

        // Carve out a shape from the center
        var shape = HardShapes[Random.Range(0, HardShapes.Length)];
        var transformed = TransformShape(shape);
        int midR = size / 2, midC = size / 2;
        foreach (var pt in transformed)
        {
            int r = midR + pt[0] - 1;
            int c = midC + pt[1] - 1;
            if (r >= 0 && r < size && c >= 0 && c < size)
                cells[r, c] = false;
        }
        AdjustToTarget(cells, size, target);
        return cells;
    }

    // ═══════════════════════════════════════════════════════════
    //  SHAPE TRANSFORM & PLACEMENT
    // ═══════════════════════════════════════════════════════════

    /// <summary>Randomly rotate (0/90/180/270) and mirror a shape template.</summary>
    private static int[][] TransformShape(int[][] shape)
    {
        var result = new int[shape.Length][];
        for (int i = 0; i < shape.Length; i++)
            result[i] = new int[] { shape[i][0], shape[i][1] };

        // Random rotation (0-3 times 90 degrees)
        int rotations = Random.Range(0, 4);
        for (int rot = 0; rot < rotations; rot++)
        {
            for (int i = 0; i < result.Length; i++)
            {
                int r = result[i][0], c = result[i][1];
                result[i][0] = c;
                result[i][1] = -r;
            }
        }

        // Random horizontal flip
        if (Random.value > 0.5f)
            for (int i = 0; i < result.Length; i++)
                result[i][1] = -result[i][1];

        // Normalize to positive coordinates
        int minR = int.MaxValue, minC = int.MaxValue;
        for (int i = 0; i < result.Length; i++)
        {
            if (result[i][0] < minR) minR = result[i][0];
            if (result[i][1] < minC) minC = result[i][1];
        }
        for (int i = 0; i < result.Length; i++)
        {
            result[i][0] -= minR;
            result[i][1] -= minC;
        }

        return result;
    }

    /// <summary>Place a transformed shape at a random valid position on the grid.</summary>
    private static void PlaceShape(bool[,] cells, int size, int[][] shape)
    {
        // Find bounding box of shape
        int maxR = 0, maxC = 0;
        for (int i = 0; i < shape.Length; i++)
        {
            if (shape[i][0] > maxR) maxR = shape[i][0];
            if (shape[i][1] > maxC) maxC = shape[i][1];
        }

        // Try random positions
        int bestR = 0, bestC = 0;
        int bestOverlap = int.MaxValue;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int offR = Random.Range(0, Mathf.Max(1, size - maxR));
            int offC = Random.Range(0, Mathf.Max(1, size - maxC));
            int overlap = 0;
            for (int i = 0; i < shape.Length; i++)
            {
                int r = offR + shape[i][0];
                int c = offC + shape[i][1];
                if (r >= 0 && r < size && c >= 0 && c < size && cells[r, c])
                    overlap++;
            }
            if (overlap < bestOverlap)
            {
                bestOverlap = overlap;
                bestR = offR;
                bestC = offC;
                if (overlap == 0) break;
            }
        }

        // Stamp shape
        for (int i = 0; i < shape.Length; i++)
        {
            int r = bestR + shape[i][0];
            int c = bestC + shape[i][1];
            if (r >= 0 && r < size && c >= 0 && c < size)
                cells[r, c] = true;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════

    private static bool IsCorner(int r, int c, int size)
    {
        return (r == 0 || r == size-1) && (c == 0 || c == size-1);
    }

    private static void GrowCluster(bool[,] cells, int size, int startR, int startC, int count)
    {
        startR = Mathf.Clamp(startR, 0, size - 1);
        startC = Mathf.Clamp(startC, 0, size - 1);
        cells[startR, startC] = true;
        int placed = 1;

        var frontier = new List<(int r, int c)>();
        AddNeighbors(frontier, cells, size, startR, startC);

        int attempts = 0;
        while (placed < count && frontier.Count > 0 && attempts < 300)
        {
            attempts++;
            int idx = Random.Range(0, frontier.Count);
            var (r, c) = frontier[idx];
            frontier.RemoveAt(idx);
            if (!cells[r, c])
            {
                cells[r, c] = true;
                placed++;
                AddNeighbors(frontier, cells, size, r, c);
            }
        }
    }

    private static void AddNeighbors(List<(int r, int c)> frontier, bool[,] cells, int size, int r, int c)
    {
        int[] dr = { -1, 1, 0, 0 };
        int[] dc = { 0, 0, -1, 1 };
        for (int d = 0; d < 4; d++)
        {
            int nr = r + dr[d];
            int nc = c + dc[d];
            if (nr >= 0 && nr < size && nc >= 0 && nc < size && !cells[nr, nc])
                frontier.Add((nr, nc));
        }
    }

    private static void AdjustToTarget(bool[,] cells, int size, int target)
    {
        int current = CountFilled(cells);
        if (current > target)
        {
            var filled = new List<(int r, int c)>();
            for (int r = 0; r < size; r++)
                for (int c = 0; c < size; c++)
                    if (cells[r, c]) filled.Add((r, c));
            Shuffle(filled);
            for (int i = 0; i < current - target && i < filled.Count; i++)
                cells[filled[i].r, filled[i].c] = false;
        }
        else if (current < target)
        {
            int placed = 0;
            int attempts = 0;
            while (placed < target - current && attempts < 300)
            {
                attempts++;
                int r = Random.Range(0, size);
                int c = Random.Range(0, size);
                if (!cells[r, c])
                {
                    cells[r, c] = true;
                    placed++;
                }
            }
        }
    }

    private static int CountFilled(bool[,] cells)
    {
        int count = 0;
        int rows = cells.GetLength(0);
        int cols = cells.GetLength(1);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (cells[r, c]) count++;
        return count;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}
