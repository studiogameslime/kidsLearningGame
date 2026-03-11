using UnityEngine;

/// <summary>
/// Generates a black outline texture from a colored sprite using edge detection.
/// Used to create coloring-book outlines from the animal art at runtime.
/// </summary>
public static class OutlineGenerator
{
    /// <summary>
    /// Create an outline texture from a source sprite.
    /// Returns a new Texture2D with black outlines on a transparent background.
    /// </summary>
    public static Texture2D Generate(Sprite sourceSprite, int outputWidth, int outputHeight, float threshold = 0.15f, int lineThickness = 2)
    {
        if (sourceSprite == null) return null;

        // Get readable pixels from the sprite
        Texture2D source = GetReadableTexture(sourceSprite);
        int srcW = source.width;
        int srcH = source.height;
        Color[] srcPixels = source.GetPixels();

        // Create output texture
        var outline = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
        outline.filterMode = FilterMode.Bilinear;
        var outPixels = new Color[outputWidth * outputHeight];

        // Fill with transparent
        Color transparent = new Color(0, 0, 0, 0);
        for (int i = 0; i < outPixels.Length; i++)
            outPixels[i] = transparent;

        Color outlineColor = new Color(0.2f, 0.2f, 0.2f, 1f); // dark gray

        // Sample the source texture at output resolution and detect edges
        for (int y = 0; y < outputHeight; y++)
        {
            for (int x = 0; x < outputWidth; x++)
            {
                // Map output position to source position
                float srcX = (float)x / outputWidth * srcW;
                float srcY = (float)y / outputHeight * srcH;

                bool isEdge = false;

                // Check neighboring pixels for color/alpha differences (Sobel-like)
                for (int dy = -lineThickness; dy <= lineThickness && !isEdge; dy++)
                {
                    for (int dx = -lineThickness; dx <= lineThickness && !isEdge; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        float nx = srcX + dx;
                        float ny = srcY + dy;

                        Color center = SamplePixel(srcPixels, srcW, srcH, srcX, srcY);
                        Color neighbor = SamplePixel(srcPixels, srcW, srcH, nx, ny);

                        // Edge = significant alpha difference (object boundary)
                        float alphaDiff = Mathf.Abs(center.a - neighbor.a);
                        if (alphaDiff > threshold)
                        {
                            isEdge = true;
                            break;
                        }

                        // Edge = significant color difference (within the object)
                        if (center.a > 0.5f && neighbor.a > 0.5f)
                        {
                            float colorDiff = ColorDifference(center, neighbor);
                            if (colorDiff > threshold * 2f)
                            {
                                isEdge = true;
                                break;
                            }
                        }
                    }
                }

                if (isEdge)
                    outPixels[y * outputWidth + x] = outlineColor;
            }
        }

        outline.SetPixels(outPixels);
        outline.Apply();

        // Clean up temporary texture
        if (source != sourceSprite.texture)
            Object.Destroy(source);

        return outline;
    }

    private static Color SamplePixel(Color[] pixels, int w, int h, float x, float y)
    {
        int px = Mathf.Clamp(Mathf.RoundToInt(x), 0, w - 1);
        int py = Mathf.Clamp(Mathf.RoundToInt(y), 0, h - 1);
        return pixels[py * w + px];
    }

    private static float ColorDifference(Color a, Color b)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }

    /// <summary>
    /// Creates a readable copy of a sprite's texture (handles compressed/non-readable textures).
    /// </summary>
    private static Texture2D GetReadableTexture(Sprite sprite)
    {
        var tex = sprite.texture;

        // Try to read directly
        try
        {
            tex.GetPixels();
            return tex;
        }
        catch
        {
            // Texture not readable — use RenderTexture to copy it
        }

        RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(tex, rt);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return readable;
    }
}
