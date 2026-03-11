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

        // Find bounding box of non-transparent pixels
        int minX = srcW, maxX = 0, minY = srcH, maxY = 0;
        for (int y = 0; y < srcH; y++)
        {
            for (int x = 0; x < srcW; x++)
            {
                if (srcPixels[y * srcW + x].a > 0.05f)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        // Fallback if no visible pixels
        if (maxX < minX || maxY < minY)
        {
            minX = 0; maxX = srcW - 1;
            minY = 0; maxY = srcH - 1;
        }

        // Content dimensions with a small margin
        int contentW = maxX - minX + 1;
        int contentH = maxY - minY + 1;
        float margin = 0.05f; // 5% margin on each side
        int marginPx = Mathf.RoundToInt(Mathf.Min(outputWidth, outputHeight) * margin);
        int availW = outputWidth - marginPx * 2;
        int availH = outputHeight - marginPx * 2;

        // Scale to fit while preserving aspect ratio
        float scale = Mathf.Min((float)availW / contentW, (float)availH / contentH);
        float scaledW = contentW * scale;
        float scaledH = contentH * scale;

        // Center offset in output space
        float offsetX = (outputWidth - scaledW) * 0.5f;
        float offsetY = (outputHeight - scaledH) * 0.5f;

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
                // Map output position to cropped source position (centered, scaled)
                float srcX = (x - offsetX) / scale + minX;
                float srcY = (y - offsetY) / scale + minY;

                // Skip pixels outside the source texture bounds
                if (srcX < 0 || srcX >= srcW || srcY < 0 || srcY >= srcH)
                    continue;

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
