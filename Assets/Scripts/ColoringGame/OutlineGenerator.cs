using UnityEngine;

/// <summary>
/// Generates clean coloring-book outlines from colored cartoon sprites.
/// Uses Sobel on individual RGB channels (catches color boundaries like blue bird on brown tree)
/// with median pre-filtering and density-based noise suppression.
/// </summary>
public static class OutlineGenerator
{
    public static Texture2D Generate(Sprite sourceSprite, int outputWidth, int outputHeight,
        float threshold = 0.12f, int lineThickness = 2)
    {
        if (sourceSprite == null) return null;

        Texture2D source = GetReadableTexture(sourceSprite);
        int srcW = source.width;
        int srcH = source.height;
        Color[] srcPixels = source.GetPixels();

        // Find bounding box
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

        if (maxX < minX || maxY < minY)
        {
            minX = 0; maxX = srcW - 1;
            minY = 0; maxY = srcH - 1;
        }

        int contentW = maxX - minX + 1;
        int contentH = maxY - minY + 1;
        float margin = 0.04f;
        int marginPx = Mathf.RoundToInt(Mathf.Min(outputWidth, outputHeight) * margin);
        int availW = outputWidth - marginPx * 2;
        int availH = outputHeight - marginPx * 2;

        float scale = Mathf.Min((float)availW / contentW, (float)availH / contentH);
        float offsetX = (outputWidth - contentW * scale) * 0.5f;
        float offsetY = (outputHeight - contentH * scale) * 0.5f;

        int w = outputWidth;
        int h = outputHeight;

        // ── Step 1: Resample ──
        float[] rMap = new float[w * h];
        float[] gMap = new float[w * h];
        float[] bMap = new float[w * h];
        float[] alphaMap = new float[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float sx = (x - offsetX) / scale + minX;
                float sy = (y - offsetY) / scale + minY;
                int idx = y * w + x;

                if (sx < -0.5f || sx >= srcW + 0.5f || sy < -0.5f || sy >= srcH + 0.5f)
                {
                    rMap[idx] = 0; gMap[idx] = 0; bMap[idx] = 0; alphaMap[idx] = 0;
                    continue;
                }

                Color c = SampleBilinear(srcPixels, srcW, srcH, sx, sy);
                rMap[idx] = c.r;
                gMap[idx] = c.g;
                bMap[idx] = c.b;
                alphaMap[idx] = c.a;
            }
        }

        // ── Step 2: Light median filter (3x3, one pass) — removes noise without killing small features ──
        float[] rMed = MedianFilter3x3(rMap, w, h);
        float[] gMed = MedianFilter3x3(gMap, w, h);
        float[] bMed = MedianFilter3x3(bMap, w, h);

        // ── Step 3: Sobel on each color channel (catches color-vs-color boundaries) ──
        float[] edgeStrength = new float[w * h];

        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                int idx = y * w + x;
                if (alphaMap[idx] < 0.3f) continue;

                float eR = SobelMag(rMed, w, x, y);
                float eG = SobelMag(gMed, w, x, y);
                float eB = SobelMag(bMed, w, x, y);

                // Use the max channel edge — this catches blue-on-brown, etc.
                edgeStrength[idx] = Mathf.Max(eR, Mathf.Max(eG, eB));
            }
        }

        // ── Step 4: Alpha edges (silhouette — always kept) ──
        float[] alphaEdgeStr = new float[w * h];
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                alphaEdgeStr[y * w + x] = SobelMag(alphaMap, w, x, y);
            }
        }

        // ── Step 5: Threshold ──
        float colorThreshold = 0.35f;  // catches color boundaries like blue bird on brown tree
        float alphaThreshold = 0.15f;   // all shape boundaries

        bool[] edgeMask = new bool[w * h];
        bool[] isAlphaEdge = new bool[w * h];

        for (int i = 0; i < w * h; i++)
        {
            if (alphaEdgeStr[i] > alphaThreshold)
            {
                edgeMask[i] = true;
                isAlphaEdge[i] = true;
            }
            else if (edgeStrength[i] > colorThreshold)
            {
                edgeMask[i] = true;
            }
        }

        // ── Step 6: Suppress dense texture regions (nest, bark detail) ──
        // Check edge density in a local window — if too dense, it's texture noise
        bool[] cleaned = new bool[w * h];
        int dRadius = 12;
        int sampleStep = 2;
        int windowSamples = 0;
        for (int dy = -dRadius; dy <= dRadius; dy += sampleStep)
            for (int dx = -dRadius; dx <= dRadius; dx += sampleStep)
                windowSamples++;

        int maxAllowed = windowSamples * 55 / 100; // 55% — only suppress really packed texture areas

        for (int y = dRadius; y < h - dRadius; y++)
        {
            for (int x = dRadius; x < w - dRadius; x++)
            {
                int idx = y * w + x;
                if (!edgeMask[idx]) continue;

                // Alpha edges bypass density check
                if (isAlphaEdge[idx])
                {
                    cleaned[idx] = true;
                    continue;
                }

                // Count edge density
                int count = 0;
                for (int dy = -dRadius; dy <= dRadius; dy += sampleStep)
                {
                    for (int dx = -dRadius; dx <= dRadius; dx += sampleStep)
                    {
                        if (edgeMask[(y + dy) * w + (x + dx)])
                            count++;
                    }
                }

                cleaned[idx] = count < maxAllowed;
            }
        }

        // Keep edges near borders that the density check missed
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (y < dRadius || y >= h - dRadius || x < dRadius || x >= w - dRadius)
                {
                    if (edgeMask[y * w + x])
                        cleaned[y * w + x] = true;
                }
            }
        }

        // ── Step 7: Remove small connected components ──
        cleaned = RemoveSmallComponents(cleaned, w, h, 60);

        // ── Step 8: Dilate for line thickness ──
        bool[] thickEdge = lineThickness > 1 ? DilateCir(cleaned, w, h, lineThickness) : cleaned;

        // ── Step 9: Render with anti-aliased borders ──
        var outPixels = new Color[w * h];
        Color transparent = new Color(0, 0, 0, 0);
        Color outlineColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        for (int i = 0; i < w * h; i++)
        {
            if (thickEdge[i])
            {
                int x = i % w;
                int y = i / w;

                bool isBorder = false;
                if (x > 0 && !thickEdge[i - 1]) isBorder = true;
                else if (x < w - 1 && !thickEdge[i + 1]) isBorder = true;
                else if (y > 0 && !thickEdge[i - w]) isBorder = true;
                else if (y < h - 1 && !thickEdge[i + w]) isBorder = true;

                outPixels[i] = isBorder
                    ? new Color(outlineColor.r, outlineColor.g, outlineColor.b, 0.5f)
                    : outlineColor;
            }
            else
            {
                outPixels[i] = transparent;
            }
        }

        var outline = new Texture2D(w, h, TextureFormat.RGBA32, false);
        outline.filterMode = FilterMode.Bilinear;
        outline.SetPixels(outPixels);
        outline.Apply();

        if (source != sourceSprite.texture)
            Object.Destroy(source);

        return outline;
    }

    // ── Median Filter 3x3 ──

    private static float[] MedianFilter3x3(float[] channel, int w, int h)
    {
        float[] result = new float[w * h];
        float[] window = new float[9];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = Mathf.Clamp(x + dx, 0, w - 1);
                        int ny = Mathf.Clamp(y + dy, 0, h - 1);
                        window[idx++] = channel[ny * w + nx];
                    }
                }

                // Insertion sort on 9 elements
                for (int i = 1; i < 9; i++)
                {
                    float key = window[i];
                    int j = i - 1;
                    while (j >= 0 && window[j] > key)
                    {
                        window[j + 1] = window[j];
                        j--;
                    }
                    window[j + 1] = key;
                }

                result[y * w + x] = window[4];
            }
        }

        return result;
    }

    // ── Sobel ──

    private static float SobelMag(float[] ch, int w, int x, int y)
    {
        float tl = ch[(y + 1) * w + (x - 1)];
        float tc = ch[(y + 1) * w + x];
        float tr = ch[(y + 1) * w + (x + 1)];
        float ml = ch[y * w + (x - 1)];
        float mr = ch[y * w + (x + 1)];
        float bl = ch[(y - 1) * w + (x - 1)];
        float bc = ch[(y - 1) * w + x];
        float br = ch[(y - 1) * w + (x + 1)];

        float gx = -tl - 2f * ml - bl + tr + 2f * mr + br;
        float gy = -tl - 2f * tc - tr + bl + 2f * bc + br;
        return Mathf.Sqrt(gx * gx + gy * gy);
    }

    // ── Dilate (circular) ──

    private static bool[] DilateCir(bool[] mask, int w, int h, int radius)
    {
        bool[] result = new bool[w * h];
        int rSq = radius * radius;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (result[y * w + x]) continue;
                bool found = false;
                int yMin = Mathf.Max(0, y - radius);
                int yMax = Mathf.Min(h - 1, y + radius);
                int xMin = Mathf.Max(0, x - radius);
                int xMax = Mathf.Min(w - 1, x + radius);

                for (int ny = yMin; ny <= yMax && !found; ny++)
                    for (int nx = xMin; nx <= xMax && !found; nx++)
                    {
                        int ddx = x - nx, ddy = y - ny;
                        if (ddx * ddx + ddy * ddy <= rSq && mask[ny * w + nx])
                        { result[y * w + x] = true; found = true; }
                    }
            }
        }
        return result;
    }

    // ── Connected Component Cleanup ──

    private static bool[] RemoveSmallComponents(bool[] mask, int w, int h, int minSize)
    {
        bool[] result = new bool[w * h];
        System.Array.Copy(mask, result, mask.Length);

        bool[] visited = new bool[w * h];
        var stack = new System.Collections.Generic.Stack<int>();
        var component = new System.Collections.Generic.List<int>();

        for (int i = 0; i < w * h; i++)
        {
            if (!result[i] || visited[i]) continue;

            component.Clear();
            stack.Push(i);
            visited[i] = true;

            while (stack.Count > 0)
            {
                int idx = stack.Pop();
                component.Add(idx);
                int cx = idx % w, cy = idx / w;

                if (cx > 0     && result[idx - 1] && !visited[idx - 1]) { visited[idx - 1] = true; stack.Push(idx - 1); }
                if (cx < w - 1 && result[idx + 1] && !visited[idx + 1]) { visited[idx + 1] = true; stack.Push(idx + 1); }
                if (cy > 0     && result[idx - w] && !visited[idx - w]) { visited[idx - w] = true; stack.Push(idx - w); }
                if (cy < h - 1 && result[idx + w] && !visited[idx + w]) { visited[idx + w] = true; stack.Push(idx + w); }
            }

            if (component.Count < minSize)
                foreach (int idx in component)
                    result[idx] = false;
        }
        return result;
    }

    // ── Sampling ──

    private static Color SampleBilinear(Color[] pixels, int w, int h, float x, float y)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        Color c00 = GetPx(pixels, w, h, x0, y0);
        Color c10 = GetPx(pixels, w, h, x0 + 1, y0);
        Color c01 = GetPx(pixels, w, h, x0, y0 + 1);
        Color c11 = GetPx(pixels, w, h, x0 + 1, y0 + 1);
        return Color.Lerp(Color.Lerp(c00, c10, fx), Color.Lerp(c01, c11, fx), fy);
    }

    private static Color GetPx(Color[] p, int w, int h, int x, int y)
    {
        if (x < 0 || x >= w || y < 0 || y >= h) return new Color(0, 0, 0, 0);
        return p[y * w + x];
    }

    private static Texture2D GetReadableTexture(Sprite sprite)
    {
        var tex = sprite.texture;
        try { tex.GetPixels(); return tex; } catch { }

        RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(tex, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        readable.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return readable;
    }
}
