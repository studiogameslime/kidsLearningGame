using UnityEngine;

/// <summary>
/// Procedurally generates sprites for the 7 classic tangram pieces.
/// Each shape fills its bounding box well for clear visual identity.
/// Sprites are cached after first generation.
/// </summary>
public static class TangramShapeGen
{
    private static Sprite[] _cache;
    private const int TexSize = 256;

    // Piece colors — vibrant, kid-friendly
    public static readonly Color[] PieceColors =
    {
        new Color(0.93f, 0.26f, 0.26f), // 0 Large Tri A — red
        new Color(0.98f, 0.55f, 0.16f), // 1 Large Tri B — orange
        new Color(0.98f, 0.80f, 0.18f), // 2 Medium Tri  — yellow
        new Color(0.30f, 0.77f, 0.35f), // 3 Small Tri A — green
        new Color(0.15f, 0.75f, 0.85f), // 4 Small Tri B — cyan
        new Color(0.25f, 0.47f, 0.85f), // 5 Square      — blue
        new Color(0.65f, 0.38f, 0.85f), // 6 Parallelogram — purple
    };

    public static Sprite Get(int pieceIndex)
    {
        if (_cache == null)
            GenerateAll();
        return _cache[Mathf.Clamp(pieceIndex, 0, 6)];
    }

    /// <summary>Get polygon vertices in normalized 0-1 space for a piece.</summary>
    public static Vector2[] GetVertices(int pieceIndex)
    {
        switch (pieceIndex)
        {
            case 0: // Large triangle A — fills full square diagonal
            case 1: // Large triangle B
                return new[] {
                    new Vector2(0.02f, 0.02f),
                    new Vector2(0.98f, 0.02f),
                    new Vector2(0.02f, 0.98f)
                };
            case 2: // Medium triangle — right triangle filling bottom-left half
                return new[] {
                    new Vector2(0.02f, 0.02f),
                    new Vector2(0.98f, 0.02f),
                    new Vector2(0.02f, 0.98f)
                };
            case 3: // Small triangle A
            case 4: // Small triangle B
                return new[] {
                    new Vector2(0.02f, 0.02f),
                    new Vector2(0.98f, 0.02f),
                    new Vector2(0.50f, 0.98f)
                };
            case 5: // Square — rotated 45° (diamond shape, fills the box better)
                return new[] {
                    new Vector2(0.50f, 0.02f),
                    new Vector2(0.98f, 0.50f),
                    new Vector2(0.50f, 0.98f),
                    new Vector2(0.02f, 0.50f)
                };
            case 6: // Parallelogram — wide and flat
                return new[] {
                    new Vector2(0.02f, 0.15f),
                    new Vector2(0.65f, 0.15f),
                    new Vector2(0.98f, 0.85f),
                    new Vector2(0.35f, 0.85f)
                };
            default:
                return new[] { Vector2.zero, Vector2.right, Vector2.up };
        }
    }

    private static void GenerateAll()
    {
        _cache = new Sprite[7];
        for (int i = 0; i < 7; i++)
            _cache[i] = GeneratePiece(i);
    }

    private static Sprite GeneratePiece(int index)
    {
        int size = TexSize;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        var verts = GetVertices(index);
        var baseColor = PieceColors[index];

        // Slightly lighter version for top gradient
        var lightColor = Color.Lerp(baseColor, Color.white, 0.2f);
        // Darker version for outline
        var darkColor = Color.Lerp(baseColor, Color.black, 0.3f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size;
                float ny = (y + 0.5f) / size;

                float dist = DistToPolygon(nx, ny, verts);
                bool inside = PointInPolygon(nx, ny, verts);

                if (inside)
                {
                    // Gradient: lighter at top, darker at bottom
                    Color c = Color.Lerp(baseColor, lightColor, ny * 0.5f);

                    // Darken near edges for outline effect
                    float edgePx = dist * size;
                    if (edgePx < 3f)
                    {
                        float t = edgePx / 3f;
                        c = Color.Lerp(darkColor, c, t);
                    }
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
                else
                {
                    // Anti-alias outside
                    float edgePx = dist * size;
                    if (edgePx < 1.5f)
                    {
                        float alpha = 1f - edgePx / 1.5f;
                        Color c = darkColor;
                        c.a = alpha * 0.5f;
                        pixels[y * size + x] = c;
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static bool PointInPolygon(float px, float py, Vector2[] verts)
    {
        bool inside = false;
        int n = verts.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if (((verts[i].y > py) != (verts[j].y > py)) &&
                (px < (verts[j].x - verts[i].x) * (py - verts[i].y) / (verts[j].y - verts[i].y) + verts[i].x))
                inside = !inside;
        }
        return inside;
    }

    private static float DistToPolygon(float px, float py, Vector2[] verts)
    {
        float minDist = float.MaxValue;
        int n = verts.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float d = DistToSegment(px, py, verts[i].x, verts[i].y, verts[j].x, verts[j].y);
            if (d < minDist) minDist = d;
        }
        return minDist;
    }

    private static float DistToSegment(float px, float py, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay;
        float lenSq = dx * dx + dy * dy;
        if (lenSq < 0.0001f) return Mathf.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));
        float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / lenSq);
        float cx = ax + t * dx, cy = ay + t * dy;
        return Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
    }
}
