using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Extracts a clean ordered contour from a sprite's alpha channel,
/// then samples curvature-aware points at various difficulty levels.
///
/// Pipeline:
/// 1. Read texture pixels, build binary alpha mask
/// 2. Trace outer contour using Moore neighborhood
/// 3. Smooth the raw contour
/// 4. Simplify with Ramer-Douglas-Peucker
/// 5. Sample curvature-aware points at requested count
/// 6. Normalize to 0–1 relative to visible bounds
/// </summary>
public static class SpriteContourExtractor
{
    private const float AlphaThreshold = 0.15f;

    // Moore neighborhood — 8 directions, clockwise from right
    private static readonly int[] DX = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] DY = { 0, -1, -1, -1, 0, 1, 1, 1 };

    /// <summary>
    /// Result of contour extraction: points + complexity metrics.
    /// </summary>
    public struct ExtractionResult
    {
        public Vector2[] points;
        public float complexityScore;
        public int complexityLevel; // 0=Low, 1=Medium, 2=High
    }

    /// <summary>
    /// Extract contour points from a sprite at the given target point count.
    /// Returns normalized 0–1 points (relative to visible content bounds)
    /// centered in the play area.
    /// </summary>
    public static Vector2[] Extract(Sprite sprite, int targetPointCount)
    {
        return ExtractWithComplexity(sprite, targetPointCount).points;
    }

    /// <summary>
    /// Extract contour points and also compute complexity metrics.
    /// Call this once (e.g. at medium point count) to get the complexity.
    /// </summary>
    public static ExtractionResult ExtractWithComplexity(Sprite sprite, int targetPointCount)
    {
        var result = new ExtractionResult();

        if (sprite == null || sprite.texture == null)
            return result;

        var tex = MakeReadable(sprite.texture);
        if (tex == null) return result;

        int w = tex.width;
        int h = tex.height;

        // 1. Build alpha mask
        bool[,] mask = BuildAlphaMask(tex, w, h);

        // 2. Trace outer contour
        List<Vector2Int> rawContour = TraceContour(mask, w, h);
        if (rawContour == null || rawContour.Count < 10)
        {
            Debug.LogWarning($"Contour too small ({rawContour?.Count ?? 0} pixels) for {sprite.name}");
            return result;
        }

        // 3. Find visible bounds
        int minX = w, maxX = 0, minY = h, maxY = 0;
        foreach (var p in rawContour)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }
        float bw = maxX - minX;
        float bh = maxY - minY;
        if (bw < 2 || bh < 2) return result;

        // 4. Convert to float, normalize to 0–1 within bounds
        List<Vector2> contourF = new List<Vector2>(rawContour.Count);
        foreach (var p in rawContour)
        {
            float nx = (p.x - minX) / bw;
            float ny = (p.y - minY) / bh;
            contourF.Add(new Vector2(nx, ny));
        }

        // 5. Subsample raw contour to manageable size (skip near-duplicate neighbors)
        contourF = SubsampleContour(contourF, 0.003f);

        // 6. Smooth the contour
        contourF = SmoothContour(contourF, 3);

        // ── Compute complexity from the smoothed contour ──
        result.complexityScore = ComputeComplexity(contourF);
        result.complexityLevel = ClassifyComplexity(result.complexityScore);

        // 7. Simplify with RDP to get a clean polygon
        float rdpEpsilon = EstimateRDPEpsilon(contourF, targetPointCount);
        List<Vector2> simplified = RamerDouglasPeucker(contourF, rdpEpsilon);

        // 8. If we got too many or too few, iteratively adjust epsilon
        int iterations = 0;
        while (iterations < 20)
        {
            if (simplified.Count > targetPointCount + 3)
            {
                rdpEpsilon *= 1.3f;
                simplified = RamerDouglasPeucker(contourF, rdpEpsilon);
            }
            else if (simplified.Count < targetPointCount - 2 && rdpEpsilon > 0.001f)
            {
                rdpEpsilon *= 0.7f;
                simplified = RamerDouglasPeucker(contourF, rdpEpsilon);
            }
            else break;
            iterations++;
        }

        // 9. Curvature-aware resampling to exact target count
        result.points = CurvatureAwareResample(simplified, targetPointCount);

        // 10. Center and scale to fit 0.15–0.85 range (leave margins for dots)
        result.points = FitToPlayArea(result.points);

        return result;
    }

    // ══════════════════════════════════════════
    //  COMPLEXITY ANALYSIS
    // ══════════════════════════════════════════

    /// <summary>
    /// Compute a complexity score from a smoothed contour.
    /// Combines multiple metrics:
    /// - Perimeter-to-bounding-box ratio (how much outline relative to size)
    /// - Number of significant direction changes (corners/landmarks)
    /// - Curvature variance (how irregular the shape is)
    /// - Concavity depth (how many deep indentations)
    /// </summary>
    private static float ComputeComplexity(List<Vector2> contour)
    {
        int n = contour.Count;
        if (n < 10) return 0f;

        // 1. Perimeter
        float perimeter = 0f;
        for (int i = 0; i < n; i++)
            perimeter += Vector2.Distance(contour[i], contour[(i + 1) % n]);

        // Bounding box
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in contour)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }
        float boxW = maxX - minX;
        float boxH = maxY - minY;
        float boxPerimeter = 2f * (boxW + boxH);
        if (boxPerimeter < 0.01f) return 0f;

        // Perimeter ratio: circle ≈ π/4 ≈ 0.78, complex shapes >> 1.0
        float perimRatio = perimeter / boxPerimeter;

        // 2. Count significant direction changes (corners)
        // Sample direction at intervals, count when angle changes > threshold
        float[] angles = new float[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 prev = contour[(i - 1 + n) % n];
            Vector2 next = contour[(i + 1) % n];
            Vector2 dir = next - prev;
            angles[i] = Mathf.Atan2(dir.y, dir.x);
        }

        int significantTurns = 0;
        float turnThreshold = 0.4f; // ~23 degrees
        for (int i = 0; i < n; i++)
        {
            float diff = Mathf.Abs(Mathf.DeltaAngle(
                angles[i] * Mathf.Rad2Deg,
                angles[(i + 1) % n] * Mathf.Rad2Deg));
            if (diff > turnThreshold * Mathf.Rad2Deg)
                significantTurns++;
        }
        // Normalize: turns per unit perimeter
        float turnsPerPerimeter = significantTurns / Mathf.Max(perimeter, 0.01f);

        // 3. Curvature variance
        float[] curvatures = new float[n];
        float curvSum = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 prev = contour[(i - 1 + n) % n];
            Vector2 curr = contour[i];
            Vector2 next = contour[(i + 1) % n];
            Vector2 d1 = (curr - prev).normalized;
            Vector2 d2 = (next - curr).normalized;
            float dot = Mathf.Clamp(Vector2.Dot(d1, d2), -1f, 1f);
            curvatures[i] = Mathf.Acos(dot);
            curvSum += curvatures[i];
        }
        float curvMean = curvSum / n;
        float curvVariance = 0f;
        for (int i = 0; i < n; i++)
        {
            float diff = curvatures[i] - curvMean;
            curvVariance += diff * diff;
        }
        curvVariance /= n;

        // 4. Concavity: count how many RDP-significant points needed at tight epsilon
        float tightEpsilon = 0.015f;
        int rdpPoints = RamerDouglasPeucker(contour, tightEpsilon).Count;
        // Normalize to a 0–1-ish range: simple shapes ≈ 4-8, complex ≈ 30-60
        float rdpNorm = rdpPoints / 50f;

        // ── Combine into final score ──
        // Weights tuned so: circle ≈ 0.2, fish ≈ 0.35, cat ≈ 0.55, giraffe ≈ 0.8
        float score = 0f;
        score += Mathf.Clamp01((perimRatio - 0.7f) / 0.8f) * 0.25f;  // 0–0.25
        score += Mathf.Clamp01(turnsPerPerimeter * 0.3f) * 0.20f;      // 0–0.20
        score += Mathf.Clamp01(curvVariance * 5f) * 0.20f;             // 0–0.20
        score += Mathf.Clamp01(rdpNorm) * 0.35f;                       // 0–0.35

        return score;
    }

    /// <summary>
    /// Classify complexity score into Low/Medium/High.
    /// </summary>
    public static int ClassifyComplexity(float score)
    {
        if (score < 0.35f) return 0; // Low
        if (score < 0.55f) return 1; // Medium
        return 2;                     // High
    }

    private static bool[,] BuildAlphaMask(Texture2D tex, int w, int h)
    {
        var pixels = tex.GetPixels32();
        bool[,] mask = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask[x, y] = pixels[y * w + x].a > (byte)(AlphaThreshold * 255);
        return mask;
    }

    /// <summary>
    /// Moore neighborhood contour tracing.
    /// Finds the outermost contour of the largest connected region.
    /// </summary>
    private static List<Vector2Int> TraceContour(bool[,] mask, int w, int h)
    {
        // Find start pixel — bottom-left scan
        Vector2Int start = Vector2Int.zero;
        bool found = false;
        for (int y = 0; y < h && !found; y++)
            for (int x = 0; x < w && !found; x++)
                if (mask[x, y]) { start = new Vector2Int(x, y); found = true; }

        if (!found) return null;

        var contour = new List<Vector2Int>();
        Vector2Int current = start;
        int dir = 0; // start looking right

        int maxSteps = w * h;
        int steps = 0;

        do
        {
            contour.Add(current);
            // Search neighbors starting from (dir + 5) % 8 for Moore tracing
            int searchDir = (dir + 5) % 8;
            bool foundNext = false;

            for (int i = 0; i < 8; i++)
            {
                int d = (searchDir + i) % 8;
                int nx = current.x + DX[d];
                int ny = current.y + DY[d];

                if (nx >= 0 && nx < w && ny >= 0 && ny < h && mask[nx, ny])
                {
                    dir = d;
                    current = new Vector2Int(nx, ny);
                    foundNext = true;
                    break;
                }
            }

            if (!foundNext) break;
            steps++;
        }
        while ((current != start || steps < 3) && steps < maxSteps);

        return contour;
    }

    /// <summary>Remove near-duplicate consecutive points.</summary>
    private static List<Vector2> SubsampleContour(List<Vector2> contour, float minDist)
    {
        if (contour.Count < 3) return contour;

        var result = new List<Vector2> { contour[0] };
        for (int i = 1; i < contour.Count; i++)
        {
            if (Vector2.Distance(contour[i], result[result.Count - 1]) >= minDist)
                result.Add(contour[i]);
        }
        return result;
    }

    /// <summary>Simple moving-average smoothing (wraps around for closed contour).</summary>
    private static List<Vector2> SmoothContour(List<Vector2> contour, int passes)
    {
        var pts = new List<Vector2>(contour);
        int n = pts.Count;
        if (n < 5) return pts;

        for (int pass = 0; pass < passes; pass++)
        {
            var smoothed = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                Vector2 prev2 = pts[(i - 2 + n) % n];
                Vector2 prev1 = pts[(i - 1 + n) % n];
                Vector2 curr = pts[i];
                Vector2 next1 = pts[(i + 1) % n];
                Vector2 next2 = pts[(i + 2) % n];
                smoothed.Add((prev2 + prev1 + curr + next1 + next2) / 5f);
            }
            pts = smoothed;
        }
        return pts;
    }

    /// <summary>Estimate a starting RDP epsilon for the target point count.</summary>
    private static float EstimateRDPEpsilon(List<Vector2> contour, int targetPoints)
    {
        // Rough: total perimeter / target count gives average segment length
        // Epsilon should be a fraction of that
        float perim = 0f;
        for (int i = 1; i < contour.Count; i++)
            perim += Vector2.Distance(contour[i], contour[i - 1]);
        perim += Vector2.Distance(contour[contour.Count - 1], contour[0]);

        return perim / (targetPoints * 3f);
    }

    /// <summary>Ramer-Douglas-Peucker line simplification for closed polygon.</summary>
    private static List<Vector2> RamerDouglasPeucker(List<Vector2> pts, float epsilon)
    {
        if (pts.Count < 3) return new List<Vector2>(pts);

        // For closed polygon: find the two farthest points, split there
        int n = pts.Count;
        int splitA = 0, splitB = 0;
        float maxDist = 0;
        for (int i = 0; i < n; i++)
        {
            for (int j = i + n / 4; j < n; j++)
            {
                float d = Vector2.Distance(pts[i], pts[j]);
                if (d > maxDist) { maxDist = d; splitA = i; splitB = j; }
            }
        }

        // Build two halves
        var half1 = new List<Vector2>();
        for (int i = splitA; i <= splitB; i++) half1.Add(pts[i]);
        var half2 = new List<Vector2>();
        for (int i = splitB; i < n; i++) half2.Add(pts[i]);
        for (int i = 0; i <= splitA; i++) half2.Add(pts[i]);

        var s1 = RDPSimplify(half1, epsilon);
        var s2 = RDPSimplify(half2, epsilon);

        // Combine, avoiding duplicate endpoints
        var result = new List<Vector2>(s1);
        for (int i = 1; i < s2.Count - 1; i++)
            result.Add(s2[i]);

        return result;
    }

    private static List<Vector2> RDPSimplify(List<Vector2> pts, float epsilon)
    {
        if (pts.Count < 3)
            return new List<Vector2>(pts);

        // Find point with max distance from line (first->last)
        float maxDist = 0;
        int index = 0;
        Vector2 first = pts[0], last = pts[pts.Count - 1];

        for (int i = 1; i < pts.Count - 1; i++)
        {
            float d = PerpendicularDistance(pts[i], first, last);
            if (d > maxDist) { maxDist = d; index = i; }
        }

        if (maxDist > epsilon)
        {
            var left = RDPSimplify(pts.GetRange(0, index + 1), epsilon);
            var right = RDPSimplify(pts.GetRange(index, pts.Count - index), epsilon);

            var result = new List<Vector2>(left);
            result.AddRange(right.Skip(1));
            return result;
        }
        else
        {
            return new List<Vector2> { first, last };
        }
    }

    private static float PerpendicularDistance(Vector2 pt, Vector2 lineStart, Vector2 lineEnd)
    {
        Vector2 line = lineEnd - lineStart;
        float len = line.magnitude;
        if (len < 0.0001f) return Vector2.Distance(pt, lineStart);
        return Mathf.Abs((pt.x - lineStart.x) * line.y - (pt.y - lineStart.y) * line.x) / len;
    }

    /// <summary>
    /// Resample a simplified polygon to exactly targetCount points,
    /// placing more points where curvature is higher.
    /// </summary>
    private static Vector2[] CurvatureAwareResample(List<Vector2> polygon, int targetCount)
    {
        int n = polygon.Count;
        if (n <= 2) return polygon.ToArray();
        if (n <= targetCount) return polygon.ToArray();

        // Calculate curvature at each vertex
        float[] curvatures = new float[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 prev = polygon[(i - 1 + n) % n];
            Vector2 curr = polygon[i];
            Vector2 next = polygon[(i + 1) % n];

            Vector2 d1 = (curr - prev).normalized;
            Vector2 d2 = (next - curr).normalized;
            float dot = Vector2.Dot(d1, d2);
            curvatures[i] = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)); // angle in radians
        }

        // Calculate edge lengths and assign curvature-weighted importance
        float[] edgeLengths = new float[n];
        float[] edgeWeights = new float[n];
        float totalWeight = 0f;

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            edgeLengths[i] = Vector2.Distance(polygon[i], polygon[next]);

            // Weight = length * (1 + curvature boost)
            float curvAvg = (curvatures[i] + curvatures[next]) * 0.5f;
            float curvBoost = 1f + curvAvg * 2f; // more points on curves
            edgeWeights[i] = edgeLengths[i] * curvBoost;
            totalWeight += edgeWeights[i];
        }

        // Distribute target points proportional to edge weights
        float pointsPerWeight = (float)targetCount / totalWeight;

        // Walk edges, placing points
        var result = new List<Vector2>();
        float accumulated = 0f;

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            float edgeAlloc = edgeWeights[i] * pointsPerWeight;
            accumulated += edgeAlloc;

            int pointsOnEdge = Mathf.FloorToInt(accumulated) - result.Count;
            if (pointsOnEdge < 0) pointsOnEdge = 0;

            // First point on this edge is always the vertex itself
            if (pointsOnEdge > 0)
            {
                result.Add(polygon[i]);
                pointsOnEdge--;
            }

            // Additional intermediate points along this edge
            for (int j = 0; j < pointsOnEdge; j++)
            {
                float t = (float)(j + 1) / (pointsOnEdge + 1);
                result.Add(Vector2.Lerp(polygon[i], polygon[next], t));
            }
        }

        // Adjust to exact count
        while (result.Count > targetCount)
        {
            // Remove the point that changes the shape least
            float minCost = float.MaxValue;
            int removeIdx = 1;
            for (int i = 1; i < result.Count - 1; i++)
            {
                float cost = PerpendicularDistance(result[i],
                    result[(i - 1 + result.Count) % result.Count],
                    result[(i + 1) % result.Count]);
                if (cost < minCost) { minCost = cost; removeIdx = i; }
            }
            result.RemoveAt(removeIdx);
        }

        while (result.Count < targetCount)
        {
            // Add point on longest edge
            float maxLen = 0;
            int maxIdx = 0;
            for (int i = 0; i < result.Count; i++)
            {
                int ni = (i + 1) % result.Count;
                float d = Vector2.Distance(result[i], result[ni]);
                if (d > maxLen) { maxLen = d; maxIdx = i; }
            }
            int insertIdx = (maxIdx + 1) % result.Count;
            Vector2 mid = (result[maxIdx] + result[insertIdx]) * 0.5f;
            result.Insert(insertIdx, mid);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Scale and center points to fit 0.15–0.85 range,
    /// preserving aspect ratio and centering in the play area.
    /// </summary>
    private static Vector2[] FitToPlayArea(Vector2[] points)
    {
        if (points.Length == 0) return points;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in points)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        float w = maxX - minX;
        float h = maxY - minY;
        if (w < 0.001f || h < 0.001f) return points;

        // Target range: 0.15 to 0.85 (70% of play area)
        float targetRange = 0.70f;
        float scale = targetRange / Mathf.Max(w, h);

        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;

        var result = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            float x = (points[i].x - centerX) * scale + 0.5f;
            float y = (points[i].y - centerY) * scale + 0.5f;
            result[i] = new Vector2(x, y);
        }
        return result;
    }

    /// <summary>
    /// Create a readable CPU copy of any texture via RenderTexture blit.
    /// Works regardless of isReadable or compression settings.
    /// </summary>
    private static Texture2D MakeReadable(Texture2D source)
    {
        // Blit to a temporary RenderTexture, then read back
        RenderTexture tmp = RenderTexture.GetTemporary(source.width, source.height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(source, tmp);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = tmp;

        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false);
        readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readable.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(tmp);

        return readable;
    }
}
