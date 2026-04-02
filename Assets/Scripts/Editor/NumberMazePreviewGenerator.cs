using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Generates a polished preview image for the Number Maze game.
/// Run via: Tools > Kids Learning Game > Generate Number Maze Preview
/// </summary>
public class NumberMazePreviewGenerator : EditorWindow
{
    public static void Generate()
    {
        int width = 1024;
        int height = 576; // 16:9 landscape
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        // ── Background gradient (warm pastel) ──
        for (int y = 0; y < height; y++)
        {
            float t = (float)y / height;
            Color bg = Color.Lerp(Hex("#E8F5E9"), Hex("#E3F2FD"), t);
            // Add subtle warmth
            bg = Color.Lerp(bg, Hex("#FFF8E1"), 0.15f);
            for (int x = 0; x < width; x++)
                tex.SetPixel(x, y, bg);
        }

        // ── Sparkle dots ──
        var rng = new System.Random(42);
        for (int i = 0; i < 60; i++)
        {
            int sx = rng.Next(width);
            int sy = rng.Next(height);
            int sr = rng.Next(2, 5);
            Color sparkle = new Color(1f, 1f, 1f, 0.35f + (float)rng.NextDouble() * 0.25f);
            FillCircle(tex, sx, sy, sr, sparkle);
        }

        // ── Grid configuration ──
        int cols = 6;
        int rows = 4;
        int pathLen = 10;
        float cellSize = 80f;
        float spacing = 10f;
        float gridW = cols * cellSize + (cols - 1) * spacing;
        float gridH = rows * cellSize + (rows - 1) * spacing;
        float offsetX = (width - gridW) / 2f;
        float offsetY = (height - gridH) / 2f - 10f;

        // ── Generate a valid path ──
        int[] pathX, pathY;
        GeneratePath(cols, rows, pathLen, rng, out pathX, out pathY);

        // Mark path cells
        int[,] pathOrder = new int[cols, rows];
        for (int x = 0; x < cols; x++)
            for (int y = 0; y < rows; y++)
                pathOrder[x, y] = -1;

        for (int i = 0; i < pathLen; i++)
            pathOrder[pathX[i], pathY[i]] = i + 1; // 1-based

        // ── Fill board numbers ──
        int[,] displayNumbers = new int[cols, rows];
        for (int gx = 0; gx < cols; gx++)
        {
            for (int gy = 0; gy < rows; gy++)
            {
                if (pathOrder[gx, gy] > 0)
                    displayNumbers[gx, gy] = pathOrder[gx, gy];
                else
                    displayNumbers[gx, gy] = rng.Next(1, pathLen + 1); // distractor in same range
            }
        }

        // ── Color palette for cells ──
        Color[] pastelColors = {
            Hex("#FFCDD2"), Hex("#F8BBD0"), Hex("#E1BEE7"), Hex("#D1C4E9"),
            Hex("#C5CAE9"), Hex("#BBDEFB"), Hex("#B3E5FC"), Hex("#B2EBF2"),
            Hex("#B2DFDB"), Hex("#C8E6C9"), Hex("#DCEDC8"), Hex("#F0F4C3"),
            Hex("#FFF9C4"), Hex("#FFECB3"), Hex("#FFE0B2"), Hex("#FFCCBC"),
        };

        Color pathHighlight = Hex("#A5D6A7"); // soft green for path
        Color pathBorder = Hex("#66BB6A");
        Color normalBorder = Hex("#E0E0E0");

        // ── Draw cells ──
        for (int gx = 0; gx < cols; gx++)
        {
            for (int gy = 0; gy < rows; gy++)
            {
                int cx = (int)(offsetX + gx * (cellSize + spacing) + cellSize / 2f);
                int cy = (int)(offsetY + (rows - 1 - gy) * (cellSize + spacing) + cellSize / 2f);
                int half = (int)(cellSize / 2f);
                int r = 12; // corner radius

                bool isPath = pathOrder[gx, gy] > 0;
                Color cellColor;
                Color borderColor;

                if (isPath)
                {
                    // Path cells: soft green with subtle variation
                    float pathT = (pathOrder[gx, gy] - 1f) / (pathLen - 1f);
                    cellColor = Color.Lerp(Hex("#C8E6C9"), Hex("#A5D6A7"), pathT);
                    borderColor = pathBorder;
                }
                else
                {
                    cellColor = pastelColors[(gx + gy * cols) % pastelColors.Length];
                    // Desaturate distractors slightly
                    cellColor = Color.Lerp(cellColor, Color.white, 0.3f);
                    borderColor = normalBorder;
                }

                // Draw border (slightly larger rounded rect)
                FillRoundedRect(tex, cx - half - 2, cy - half - 2, (int)cellSize + 4, (int)cellSize + 4, r + 1, borderColor);
                // Draw cell background
                FillRoundedRect(tex, cx - half, cy - half, (int)cellSize, (int)cellSize, r, cellColor);

                // Draw number
                int num = displayNumbers[gx, gy];
                DrawNumber(tex, cx, cy, num, isPath ? Hex("#2E7D32") : Hex("#546E7A"), isPath);
            }
        }

        // ── Draw subtle path connections ──
        for (int i = 0; i < pathLen - 1; i++)
        {
            int x1 = (int)(offsetX + pathX[i] * (cellSize + spacing) + cellSize / 2f);
            int y1 = (int)(offsetY + (rows - 1 - pathY[i]) * (cellSize + spacing) + cellSize / 2f);
            int x2 = (int)(offsetX + pathX[i + 1] * (cellSize + spacing) + cellSize / 2f);
            int y2 = (int)(offsetY + (rows - 1 - pathY[i + 1]) * (cellSize + spacing) + cellSize / 2f);

            // Subtle dotted line between connected path cells
            DrawDottedLine(tex, x1, y1, x2, y2, new Color(0.4f, 0.73f, 0.42f, 0.3f), 3);
        }

        // ── Soft vignette ──
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (x - width * 0.5f) / (width * 0.5f);
                float dy = (y - height * 0.5f) / (height * 0.5f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > 0.7f)
                {
                    float fade = Mathf.Clamp01((dist - 0.7f) / 0.6f) * 0.15f;
                    Color c = tex.GetPixel(x, y);
                    tex.SetPixel(x, y, Color.Lerp(c, Hex("#E8EAF6"), fade));
                }
            }
        }

        tex.Apply();

        // ── Save ──
        string folder = "Assets/Art/Games Preview";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets/Art", "Games Preview");
        }

        string path = folder + "/NumberMaze.png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        // Set texture import settings
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        Debug.Log($"[NumberMazePreview] Generated preview at {path}");
        EditorUtility.DisplayDialog("Done", $"Number Maze preview saved to:\n{path}", "OK");
    }

    // ── Path Generation ──

    static readonly int[] DX = { 0, 0, -1, 1 };
    static readonly int[] DY = { -1, 1, 0, 0 };

    static void GeneratePath(int cols, int rows, int len, System.Random rng, out int[] px, out int[] py)
    {
        for (int attempt = 0; attempt < 500; attempt++)
        {
            px = new int[len];
            py = new int[len];
            bool[,] visited = new bool[cols, rows];

            px[0] = rng.Next(cols);
            py[0] = rng.Next(rows);
            visited[px[0], py[0]] = true;

            bool ok = true;
            for (int step = 1; step < len; step++)
            {
                var neighbors = new List<int>();
                for (int d = 0; d < 4; d++)
                {
                    int nx = px[step - 1] + DX[d];
                    int ny = py[step - 1] + DY[d];
                    if (nx >= 0 && nx < cols && ny >= 0 && ny < rows && !visited[nx, ny])
                        neighbors.Add(d);
                }
                if (neighbors.Count == 0) { ok = false; break; }
                int dir = neighbors[rng.Next(neighbors.Count)];
                px[step] = px[step - 1] + DX[dir];
                py[step] = py[step - 1] + DY[dir];
                visited[px[step], py[step]] = true;
            }
            if (ok) return;
        }

        // Fallback: snake
        px = new int[len]; py = new int[len];
        int idx = 0;
        for (int y = 0; y < rows && idx < len; y++)
        {
            if (y % 2 == 0)
                for (int x = 0; x < cols && idx < len; x++) { px[idx] = x; py[idx] = y; idx++; }
            else
                for (int x = cols - 1; x >= 0 && idx < len; x--) { px[idx] = x; py[idx] = y; idx++; }
        }
    }

    // ── Drawing Helpers ──

    static void FillRoundedRect(Texture2D tex, int x0, int y0, int w, int h, int r, Color c)
    {
        for (int dy = 0; dy < h; dy++)
        {
            for (int dx = 0; dx < w; dx++)
            {
                int px = x0 + dx;
                int py = y0 + dy;
                if (px < 0 || px >= tex.width || py < 0 || py >= tex.height) continue;

                // Check corners for rounding
                bool inside = true;
                if (dx < r && dy < r)
                    inside = (dx - r) * (dx - r) + (dy - r) * (dy - r) <= r * r;
                else if (dx >= w - r && dy < r)
                    inside = (dx - (w - r - 1)) * (dx - (w - r - 1)) + (dy - r) * (dy - r) <= r * r;
                else if (dx < r && dy >= h - r)
                    inside = (dx - r) * (dx - r) + (dy - (h - r - 1)) * (dy - (h - r - 1)) <= r * r;
                else if (dx >= w - r && dy >= h - r)
                    inside = (dx - (w - r - 1)) * (dx - (w - r - 1)) + (dy - (h - r - 1)) * (dy - (h - r - 1)) <= r * r;

                if (inside)
                {
                    Color existing = tex.GetPixel(px, py);
                    tex.SetPixel(px, py, Color.Lerp(existing, c, c.a));
                }
            }
        }
    }

    static void FillCircle(Texture2D tex, int cx, int cy, int r, Color c)
    {
        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy <= r * r)
                {
                    int px = cx + dx, py = cy + dy;
                    if (px >= 0 && px < tex.width && py >= 0 && py < tex.height)
                    {
                        Color existing = tex.GetPixel(px, py);
                        tex.SetPixel(px, py, Color.Lerp(existing, new Color(c.r, c.g, c.b, 1f), c.a));
                    }
                }
            }
        }
    }

    static void DrawDottedLine(Texture2D tex, int x1, int y1, int x2, int y2, Color c, int thickness)
    {
        int dx = x2 - x1, dy = y2 - y1;
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        if (steps == 0) return;
        float xInc = dx / (float)steps;
        float yInc = dy / (float)steps;

        for (int i = 0; i < steps; i += 6) // dotted: draw every 6 pixels, skip 6
        {
            for (int j = 0; j < 3 && i + j < steps; j++)
            {
                int px = (int)(x1 + (i + j) * xInc);
                int py = (int)(y1 + (i + j) * yInc);
                for (int t = -thickness / 2; t <= thickness / 2; t++)
                {
                    if (dx == 0) // vertical line
                    {
                        if (px + t >= 0 && px + t < tex.width && py >= 0 && py < tex.height)
                        {
                            Color existing = tex.GetPixel(px + t, py);
                            tex.SetPixel(px + t, py, Color.Lerp(existing, new Color(c.r, c.g, c.b, 1f), c.a));
                        }
                    }
                    else // horizontal line
                    {
                        if (px >= 0 && px < tex.width && py + t >= 0 && py + t < tex.height)
                        {
                            Color existing = tex.GetPixel(px, py + t);
                            tex.SetPixel(px, py + t, Color.Lerp(existing, new Color(c.r, c.g, c.b, 1f), c.a));
                        }
                    }
                }
            }
        }
    }

    // ── Simple bitmap digit rendering ──

    static readonly bool[][,] Digits = BuildDigitBitmaps();

    static bool[][,] BuildDigitBitmaps()
    {
        // 5x7 pixel font for digits 0-9
        string[] patterns = {
            // 0
            " ### ," +
            "#   #," +
            "#   #," +
            "#   #," +
            "#   #," +
            "#   #," +
            " ### ",
            // 1
            "  #  ," +
            " ##  ," +
            "  #  ," +
            "  #  ," +
            "  #  ," +
            "  #  ," +
            " ### ",
            // 2
            " ### ," +
            "#   #," +
            "    #," +
            "  ## ," +
            " #   ," +
            "#    ," +
            "#####",
            // 3
            " ### ," +
            "#   #," +
            "    #," +
            "  ## ," +
            "    #," +
            "#   #," +
            " ### ",
            // 4
            "   # ," +
            "  ## ," +
            " # # ," +
            "#  # ," +
            "#####," +
            "   # ," +
            "   # ",
            // 5
            "#####," +
            "#    ," +
            "#### ," +
            "    #," +
            "    #," +
            "#   #," +
            " ### ",
            // 6
            " ### ," +
            "#    ," +
            "#    ," +
            "#### ," +
            "#   #," +
            "#   #," +
            " ### ",
            // 7
            "#####," +
            "    #," +
            "   # ," +
            "  #  ," +
            "  #  ," +
            "  #  ," +
            "  #  ",
            // 8
            " ### ," +
            "#   #," +
            "#   #," +
            " ### ," +
            "#   #," +
            "#   #," +
            " ### ",
            // 9
            " ### ," +
            "#   #," +
            "#   #," +
            " ####," +
            "    #," +
            "    #," +
            " ### ",
        };

        var bitmaps = new bool[10][,];
        for (int d = 0; d < 10; d++)
        {
            bitmaps[d] = new bool[5, 7];
            string[] rowStrs = patterns[d].Split(',');
            for (int row = 0; row < 7; row++)
            {
                string r = rowStrs[row];
                for (int col = 0; col < 5 && col < r.Length; col++)
                    bitmaps[d][col, row] = r[col] == '#';
            }
        }
        return bitmaps;
    }

    static void DrawNumber(Texture2D tex, int cx, int cy, int number, Color color, bool bold)
    {
        string numStr = number.ToString();
        int scale = bold ? 5 : 4;
        int charW = 5 * scale + scale; // char width + spacing
        int totalW = numStr.Length * charW - scale;
        int startX = cx - totalW / 2;
        int startY = cy - (7 * scale) / 2;

        for (int ci = 0; ci < numStr.Length; ci++)
        {
            int digit = numStr[ci] - '0';
            if (digit < 0 || digit > 9) continue;
            var bm = Digits[digit];

            int ox = startX + ci * charW;
            for (int bx = 0; bx < 5; bx++)
            {
                for (int by = 0; by < 7; by++)
                {
                    if (!bm[bx, by]) continue;
                    for (int sy = 0; sy < scale; sy++)
                    {
                        for (int sx = 0; sx < scale; sx++)
                        {
                            int px = ox + bx * scale + sx;
                            int py = startY + (6 - by) * scale + sy;
                            if (px >= 0 && px < tex.width && py >= 0 && py < tex.height)
                                tex.SetPixel(px, py, color);
                        }
                    }
                }
            }
        }
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
