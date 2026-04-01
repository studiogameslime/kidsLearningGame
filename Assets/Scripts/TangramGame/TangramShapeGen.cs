using UnityEngine;

/// <summary>
/// Procedurally generates sprites for the 7 classic tangram pieces.
/// All pieces are defined on a unit grid where 1 unit = half the large triangle hypotenuse.
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

    /// <summary>
    /// Get sprite for piece index 0-6.
    /// 0,1 = large triangles, 2 = medium, 3,4 = small, 5 = square, 6 = parallelogram.
    /// </summary>
    public static Sprite Get(int pieceIndex)
    {
        if (_cache == null)
            GenerateAll();
        return _cache[Mathf.Clamp(pieceIndex, 0, 6)];
    }

    /// <summary>Get the polygon vertices for a piece (in normalized 0-1 space).</summary>
    public static Vector2[] GetVertices(int pieceIndex)
    {
        switch (pieceIndex)
        {
            case 0: // Large triangle A
            case 1: // Large triangle B (same shape, different color)
                return new[] {
                    new Vector2(0.05f, 0.05f),
                    new Vector2(0.95f, 0.05f),
                    new Vector2(0.05f, 0.95f)
                };
            case 2: // Medium triangle
                return new[] {
                    new Vector2(0.05f, 0.05f),
                    new Vector2(0.95f, 0.05f),
                    new Vector2(0.50f, 0.50f)
                };
            case 3: // Small triangle A
            case 4: // Small triangle B
                return new[] {
                    new Vector2(0.05f, 0.05f),
                    new Vector2(0.95f, 0.05f),
                    new Vector2(0.50f, 0.50f)
                };
            case 5: // Square
                return new[] {
                    new Vector2(0.10f, 0.10f),
                    new Vector2(0.90f, 0.10f),
                    new Vector2(0.90f, 0.90f),
                    new Vector2(0.10f, 0.90f)
                };
            case 6: // Parallelogram
                return new[] {
                    new Vector2(0.05f, 0.10f),
                    new Vector2(0.55f, 0.10f),
                    new Vector2(0.95f, 0.90f),
                    new Vector2(0.45f, 0.90f)
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
        // Piece-specific texture size (small triangles get smaller tex)
        int size = (index == 3 || index == 4) ? TexSize / 2 : TexSize;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        var verts = GetVertices(index);
        var baseColor = PieceColors[index];

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
                    // Subtle gradient: lighter at top
                    float gradient = 1f + 0.12f * ny;
                    Color c = baseColor * gradient;
                    c.a = 1f;

                    // Anti-aliased edge (smooth 2px border)
                    float edgeDist = dist * size;
                    if (edgeDist < 2f)
                    {
                        // Darken near edges for subtle outline
                        float darken = Mathf.Lerp(0.7f, 1f, edgeDist / 2f);
                        c.r *= darken;
                        c.g *= darken;
                        c.b *= darken;
                    }

                    pixels[y * size + x] = c;
                }
                else
                {
                    // Anti-alias: smooth alpha falloff outside edge
                    float edgeDist = dist * size;
                    if (edgeDist < 1.5f)
                    {
                        float alpha = Mathf.Clamp01(1f - edgeDist / 1.5f);
                        Color c = baseColor * 0.8f;
                        c.a = alpha * 0.6f;
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

    /// <summary>Approximate distance from point to nearest polygon edge (in normalized coords).</summary>
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
