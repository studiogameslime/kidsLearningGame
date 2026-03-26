using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Smart flood fill for the coloring game's Area Fill mode.
/// Fills closed regions bounded by outline lines with tolerance for imprecise taps.
/// Handles: boundary-tap resolution, leak prevention, small-region retry.
/// </summary>
public static class FloodFiller
{
    private const float BoundaryLuminanceThreshold = 0.45f;
    private const float BoundaryAlphaThreshold = 0.25f;
    private const int SmallRegionThreshold = 200;

    /// <summary>
    /// Builds a boundary mask from outline pixels.
    /// A pixel is a boundary if it is dark (luminance &lt; 0.45) and visible (alpha &gt; 0.25).
    /// Works with both transparent-background and white-background outlines.
    /// </summary>
    public static bool[] BuildBoundaryMask(Color[] outlinePixels)
    {
        bool[] mask = new bool[outlinePixels.Length];
        for (int i = 0; i < outlinePixels.Length; i++)
        {
            Color c = outlinePixels[i];
            if (c.a > BoundaryAlphaThreshold)
            {
                float lum = (c.r + c.g + c.b) * 0.333f;
                if (lum < BoundaryLuminanceThreshold)
                    mask[i] = true;
            }
        }
        return mask;
    }

    /// <summary>
    /// Performs a smart flood fill and returns the indices of pixels to fill.
    /// Returns null if fill was aborted (leak) or nothing to fill.
    /// </summary>
    /// <param name="boundary">Boundary mask (same size as w*h).</param>
    /// <param name="w">Texture width.</param>
    /// <param name="h">Texture height.</param>
    /// <param name="tapX">Tap X in texture coords.</param>
    /// <param name="tapY">Tap Y in texture coords.</param>
    /// <param name="searchRadius">Max radius to search for a non-boundary pixel if tap lands on a line.</param>
    /// <param name="maxFillRatio">Max fraction of canvas that can be filled before aborting (leak prevention).</param>
    public static List<int> Fill(bool[] boundary, int w, int h,
                                  int tapX, int tapY,
                                  int searchRadius = 30, float maxFillRatio = 0.6f)
    {
        int totalPixels = w * h;
        int maxFillPixels = Mathf.RoundToInt(totalPixels * maxFillRatio);

        // Resolve tap point — if on boundary, search outward for nearest fillable pixel
        int originX, originY;
        if (!ResolveTapPoint(boundary, w, h, tapX, tapY, searchRadius, out originX, out originY))
            return null;

        // First fill attempt
        bool[] visited = new bool[totalPixels];
        List<int> filled = FloodFillBFS(boundary, visited, w, h, originX, originY, maxFillPixels);

        if (filled == null || filled.Count == 0)
            return null;

        // Small region retry — try to find a larger neighboring region
        if (filled.Count < SmallRegionThreshold)
        {
            int altX, altY;
            if (FindAlternateOrigin(boundary, visited, w, h, originX, originY, searchRadius * 2,
                                     out altX, out altY))
            {
                List<int> altFilled = FloodFillBFS(boundary, visited, w, h, altX, altY, maxFillPixels);
                if (altFilled != null && altFilled.Count > filled.Count)
                    filled = altFilled;
            }
        }

        return filled;
    }

    // ── Tap Resolution ──

    /// <summary>
    /// If the tap point is on a boundary, spiral outward to find the nearest non-boundary pixel.
    /// </summary>
    private static bool ResolveTapPoint(bool[] boundary, int w, int h,
                                         int x, int y, int radius,
                                         out int outX, out int outY)
    {
        // Clamp to texture bounds
        x = Mathf.Clamp(x, 0, w - 1);
        y = Mathf.Clamp(y, 0, h - 1);

        // Direct hit — not on a boundary
        if (!boundary[y * w + x])
        {
            outX = x;
            outY = y;
            return true;
        }

        // Spiral search outward
        for (int r = 1; r <= radius; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    // Only check perimeter of each ring
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;

                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;

                    if (!boundary[ny * w + nx])
                    {
                        outX = nx;
                        outY = ny;
                        return true;
                    }
                }
            }
        }

        outX = -1;
        outY = -1;
        return false;
    }

    // ── Core BFS Flood Fill ──

    private static List<int> FloodFillBFS(bool[] boundary, bool[] visited,
                                           int w, int h, int startX, int startY,
                                           int maxPixels)
    {
        int startIdx = startY * w + startX;
        if (startIdx < 0 || startIdx >= w * h) return null;
        if (boundary[startIdx] || visited[startIdx]) return null;

        var filled = new List<int>();
        var queue = new Queue<int>();

        queue.Enqueue(startIdx);
        visited[startIdx] = true;

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            filled.Add(idx);

            // Leak detection — abort if too many pixels
            if (filled.Count > maxPixels)
                return null;

            int cx = idx % w;
            int cy = idx / w;

            // 4-connected neighbors
            TryEnqueue(queue, visited, boundary, w, h, cx - 1, cy);
            TryEnqueue(queue, visited, boundary, w, h, cx + 1, cy);
            TryEnqueue(queue, visited, boundary, w, h, cx, cy - 1);
            TryEnqueue(queue, visited, boundary, w, h, cx, cy + 1);
        }

        return filled;
    }

    private static void TryEnqueue(Queue<int> queue, bool[] visited, bool[] boundary,
                                    int w, int h, int x, int y)
    {
        if (x < 0 || x >= w || y < 0 || y >= h) return;
        int idx = y * w + x;
        if (visited[idx] || boundary[idx]) return;
        visited[idx] = true;
        queue.Enqueue(idx);
    }

    // ── Small Region Retry ──

    /// <summary>
    /// After filling a small region, search outward for a non-boundary, non-visited pixel
    /// that may lead to a larger neighboring region.
    /// </summary>
    private static bool FindAlternateOrigin(bool[] boundary, bool[] visited,
                                             int w, int h, int cx, int cy, int radius,
                                             out int outX, out int outY)
    {
        for (int r = 1; r <= radius; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    int idx = ny * w + nx;
                    if (!boundary[idx] && !visited[idx])
                    {
                        outX = nx;
                        outY = ny;
                        return true;
                    }
                }
            }
        }

        outX = -1;
        outY = -1;
        return false;
    }
}
