using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Handles touch/mouse drawing on a Texture2D displayed via a RawImage.
/// Supports brush color, size, eraser, undo, clear, and area fill mode.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class DrawingCanvas : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Canvas Settings")]
    public int textureWidth = 1024;
    public int textureHeight = 1024;

    [Header("Brush")]
    public Color brushColor = Color.red;
    public int brushSize = 20;
    public bool isEraser;

    [Header("Sticker")]
    public int stickerStampSize = 80;

    /// <summary>Invoked once on the very first pointer-down (draw or sticker stamp).</summary>
    public System.Action onFirstDraw;

    /// <summary>Invoked after a successful area fill with the number of pixels filled.</summary>
    public System.Action<int> onAreaFilled;

    // The drawable texture and its UI display
    private Texture2D drawTexture;
    private RawImage rawImage;
    private RectTransform rectTransform;

    // Undo system: stores texture snapshots at end of each stroke
    private List<Color[]> undoStack = new List<Color[]>();
    private const int MaxUndoSteps = 10;

    // Tracks previous draw position for smooth line interpolation
    private Vector2? lastDrawPos;
    private bool isDrawing;
    private int activePointerId = -1;

    // Deferred draw start — waits to detect pinch before committing paint
    private bool pendingDrawStart;
    private Vector2 pendingDrawPos;
    private enum PendingAction { Brush, Fill, Sticker }
    private PendingAction pendingAction;

    // Sticker stamp mode
    private Sprite activeSticker;
    public bool IsStickerMode => activeSticker != null;

    // Area fill mode
    private bool areaFillMode;
    private bool[] boundaryMask;

    // Pinch-to-zoom
    private bool isPinching;
    private float lastPinchDist;
    private Vector2 lastPinchCenter;
    private RectTransform zoomTarget;
    private float currentZoom = 1f;
    private Vector2 currentPan = Vector2.zero;
    private const float MinZoom = 1f;
    private const float MaxZoom = 4f;

    // Base color for the canvas (white) and the eraser
    private Color clearColor = Color.white;

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
        rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// Initialise the canvas. Call once after the component is ready.
    /// </summary>
    public void Init()
    {
        drawTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        drawTexture.filterMode = FilterMode.Bilinear;
        Clear();
        rawImage.texture = drawTexture;

        // Setup pinch-to-zoom: zoom the parent that holds drawing + outline
        zoomTarget = rectTransform.parent as RectTransform;
        if (zoomTarget != null)
        {
            // Add mask on the frame (grandparent) to clip zoomed content
            var clipParent = zoomTarget.parent;
            if (clipParent != null && clipParent.GetComponent<UnityEngine.UI.RectMask2D>() == null)
                clipParent.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        }
    }

    // ── Public API ──

    public void SetColor(Color color)
    {
        brushColor = color;
        isEraser = false;
    }

    public void SetBrushSize(int size)
    {
        brushSize = size;
    }

    public void SetEraser(bool eraser)
    {
        isEraser = eraser;
    }

    public void SetStickerMode(Sprite sticker)
    {
        activeSticker = sticker;
    }

    /// <summary>
    /// Enables or disables area fill mode. In fill mode, taps flood-fill regions
    /// instead of drawing brush strokes.
    /// </summary>
    public void SetAreaFillMode(bool enabled)
    {
        areaFillMode = enabled;
        if (!enabled)
            boundaryMask = null;
    }

    /// <summary>
    /// Sets the outline texture used to compute flood-fill boundaries.
    /// Call after SetAreaFillMode(true). The texture is resized to match the canvas dimensions.
    /// </summary>
    public void SetOutlineBoundary(Texture outlineTex)
    {
        if (outlineTex == null)
        {
            boundaryMask = null;
            return;
        }

        Color[] outlinePixels = GetResizedTexturePixels(outlineTex, textureWidth, textureHeight);
        boundaryMask = FloodFiller.BuildBoundaryMask(outlinePixels);
    }

    public void Clear()
    {
        var pixels = drawTexture.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clearColor;
        drawTexture.SetPixels(pixels);
        drawTexture.Apply();

        undoStack.Clear();
    }

    public void Undo()
    {
        if (undoStack.Count == 0) return;

        var previous = undoStack[undoStack.Count - 1];
        undoStack.RemoveAt(undoStack.Count - 1);

        drawTexture.SetPixels(previous);
        drawTexture.Apply();
    }

    public bool CanUndo => undoStack.Count > 0;

    /// <summary>Returns a PNG-encoded copy of the current drawing.</summary>
    public byte[] EncodeToPNG()
    {
        return drawTexture != null ? drawTexture.EncodeToPNG() : null;
    }

    /// <summary>Returns the current drawing pixels for compositing.</summary>
    public Color[] GetPixels()
    {
        return drawTexture != null ? drawTexture.GetPixels() : null;
    }

    /// <summary>Returns the drawing texture dimensions.</summary>
    public Vector2Int GetTextureSize()
    {
        return drawTexture != null ? new Vector2Int(drawTexture.width, drawTexture.height) : Vector2Int.zero;
    }

    // ── Pinch-to-Zoom ──

    private void Update()
    {
        int touchCount = Input.touchCount;

        if (touchCount >= 2)
        {
            var t0 = Input.GetTouch(0);
            var t1 = Input.GetTouch(1);

            if (!isPinching)
            {
                // Start pinch — cancel any drawing in progress
                isPinching = true;
                pendingDrawStart = false; // discard deferred draw
                if (isDrawing)
                {
                    isDrawing = false;
                    lastDrawPos = null;
                    // Restore undo snapshot to discard partial stroke
                    if (undoStack.Count > 0)
                    {
                        var prev = undoStack[undoStack.Count - 1];
                        undoStack.RemoveAt(undoStack.Count - 1);
                        drawTexture.SetPixels(prev);
                        drawTexture.Apply();
                    }
                }
                activePointerId = -1;
                lastPinchDist = Vector2.Distance(t0.position, t1.position);
                lastPinchCenter = (t0.position + t1.position) * 0.5f;
            }
            else
            {
                float dist = Vector2.Distance(t0.position, t1.position);
                Vector2 center = (t0.position + t1.position) * 0.5f;

                // Zoom
                if (lastPinchDist > 10f)
                {
                    float scale = dist / lastPinchDist;
                    currentZoom = Mathf.Clamp(currentZoom * scale, MinZoom, MaxZoom);
                }

                // Pan
                Vector2 panDelta = center - lastPinchCenter;
                currentPan += panDelta;

                ApplyZoomPan();

                lastPinchDist = dist;
                lastPinchCenter = center;
            }
        }
        else if (isPinching)
        {
            isPinching = false;

            // Snap back to 1x if nearly unzoomed
            if (currentZoom < 1.1f)
            {
                currentZoom = 1f;
                currentPan = Vector2.zero;
                ApplyZoomPan();
            }
        }
    }

    private void ApplyZoomPan()
    {
        if (zoomTarget == null) return;

        zoomTarget.localScale = new Vector3(currentZoom, currentZoom, 1f);

        // Clamp pan so content doesn't drift too far outside the frame
        Rect rect = zoomTarget.rect;
        float maxPanX = (currentZoom - 1f) * rect.width * 0.5f;
        float maxPanY = (currentZoom - 1f) * rect.height * 0.5f;
        currentPan.x = Mathf.Clamp(currentPan.x, -maxPanX, maxPanX);
        currentPan.y = Mathf.Clamp(currentPan.y, -maxPanY, maxPanY);

        zoomTarget.anchoredPosition = currentPan;
    }

    /// <summary>Resets zoom and pan to default (1x, centered).</summary>
    public void ResetZoom()
    {
        currentZoom = 1f;
        currentPan = Vector2.zero;
        ApplyZoomPan();
    }

    // ── Input Handling ──

    public void OnPointerDown(PointerEventData eventData)
    {
        // Block drawing during pinch or when second finger is already down
        if (isPinching || Input.touchCount >= 2) return;
        if (isDrawing) return;
        activePointerId = eventData.pointerId;

        // Fire first-draw callback once
        if (onFirstDraw != null)
        {
            onFirstDraw.Invoke();
            onFirstDraw = null;
        }

        Vector2 localPos;
        if (!ScreenToTexturePos(eventData.position, out localPos))
            return;

        // Don't draw/fill/stamp immediately — defer so pinch-to-zoom can cancel
        pendingDrawStart = true;
        pendingDrawPos = localPos;

        if (activeSticker != null)
            pendingAction = PendingAction.Sticker;
        else if (areaFillMode)
            pendingAction = PendingAction.Fill;
        else
        {
            pendingAction = PendingAction.Brush;
            isDrawing = true;
            lastDrawPos = null;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // No dragging in area fill mode or during pinch
        if (areaFillMode || isPinching) return;
        if (!isDrawing || activeSticker != null) return;
        if (eventData.pointerId != activePointerId) return;
        // Abort if second finger appeared (about to pinch)
        if (Input.touchCount >= 2) return;

        // Commit deferred draw start now that we know it's a single-finger drag
        if (pendingDrawStart)
        {
            pendingDrawStart = false;
            SaveUndoSnapshot();
            DrawCircle(pendingDrawPos);
            lastDrawPos = pendingDrawPos;
            drawTexture.Apply();
        }

        Vector2 localPos;
        if (ScreenToTexturePos(eventData.position, out localPos))
        {
            if (lastDrawPos.HasValue)
                DrawLine(lastDrawPos.Value, localPos);
            else
                DrawCircle(localPos);

            lastDrawPos = localPos;
            drawTexture.Apply();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;

        // If action was deferred and never committed (tap without drag), commit now
        if (pendingDrawStart && !isPinching && Input.touchCount < 2)
        {
            pendingDrawStart = false;
            SaveUndoSnapshot();
            switch (pendingAction)
            {
                case PendingAction.Fill:
                    PerformAreaFill(pendingDrawPos);
                    break;
                case PendingAction.Sticker:
                    StampSprite(activeSticker, pendingDrawPos, stickerStampSize);
                    break;
                case PendingAction.Brush:
                    DrawCircle(pendingDrawPos);
                    drawTexture.Apply();
                    break;
            }
        }
        pendingDrawStart = false;

        isDrawing = false;
        lastDrawPos = null;
        activePointerId = -1;
    }

    // ── Area Fill ──

    private void PerformAreaFill(Vector2 texturePos)
    {
        if (boundaryMask == null)
        {
            Debug.LogWarning("DrawingCanvas: Area fill attempted without boundary mask.");
            return;
        }

        int tapX = Mathf.RoundToInt(texturePos.x);
        int tapY = Mathf.RoundToInt(texturePos.y);

        List<int> filled = FloodFiller.Fill(boundaryMask, textureWidth, textureHeight, tapX, tapY);

        if (filled == null || filled.Count == 0)
            return;

        // Apply fill color
        Color[] pixels = drawTexture.GetPixels();
        for (int i = 0; i < filled.Count; i++)
            pixels[filled[i]] = brushColor;

        drawTexture.SetPixels(pixels);
        drawTexture.Apply();

        onAreaFilled?.Invoke(filled.Count);
    }

    // ── Drawing Methods ──

    private void DrawCircle(Vector2 center)
    {
        Color color = isEraser ? clearColor : brushColor;
        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        int r = brushSize / 2;

        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y <= r * r)
                {
                    int px = cx + x;
                    int py = cy + y;
                    if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                        drawTexture.SetPixel(px, py, color);
                }
            }
        }
    }

    private void DrawLine(Vector2 from, Vector2 to)
    {
        float dist = Vector2.Distance(from, to);
        // Step size: smaller than brush radius for smooth coverage
        float step = Mathf.Max(1f, brushSize * 0.3f);
        int steps = Mathf.CeilToInt(dist / step);

        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0 : (float)i / steps;
            Vector2 pos = Vector2.Lerp(from, to, t);
            DrawCircle(pos);
        }
    }

    /// <summary>Stamp a sprite onto the drawing texture at the given position.</summary>
    private void StampSprite(Sprite sprite, Vector2 center, int stampSize)
    {
        if (sprite == null || sprite.texture == null) return;

        // Blit full texture into a readable Texture2D via RenderTexture
        int tw = sprite.texture.width;
        int th = sprite.texture.height;
        var rt = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        Graphics.Blit(sprite.texture, rt);
        RenderTexture.active = rt;
        var readable = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
        readable.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        // Extract the sprite's sub-rect pixels
        Rect srcRect = sprite.textureRect;
        int srcX = (int)srcRect.x, srcY = (int)srcRect.y;
        int srcW = (int)srcRect.width, srcH = (int)srcRect.height;
        if (textureHeight <= 0) return;
        if (srcH <= 0 || srcW <= 0) return;

        // Compensate for display stretch: texture is square but RawImage may not be.
        // Calculate how many texture pixels correspond to equal screen distance in X vs Y.
        Rect displayRect = rectTransform.rect;
        float displayAspect = (displayRect.width > 0 && displayRect.height > 0)
            ? (displayRect.width / displayRect.height) : 1f;
        float texAspect = (float)textureWidth / textureHeight;
        // ratio > 1 means display is wider than texture: X pixels appear smaller on screen
        float stretchX = displayAspect / texAspect;

        // Preserve sprite aspect ratio AND compensate for display stretch
        float spriteAspect = (float)srcW / srcH;
        int drawW, drawH;
        // Start with stampSize as the height, compute width from sprite + stretch
        drawH = stampSize;
        drawW = Mathf.Max(1, Mathf.RoundToInt(stampSize * spriteAspect / stretchX));

        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        int halfW = drawW / 2;
        int halfH = drawH / 2;

        for (int y = 0; y < drawH; y++)
        {
            for (int x = 0; x < drawW; x++)
            {
                // Sample from sprite rect
                int sx = srcX + Mathf.Clamp((int)((float)x / drawW * srcW), 0, srcW - 1);
                int sy = srcY + Mathf.Clamp((int)((float)y / drawH * srcH), 0, srcH - 1);
                Color src = readable.GetPixel(sx, sy);

                if (src.a < 0.05f) continue;

                int dx = cx - halfW + x;
                int dy = cy - halfH + y;
                if (dx < 0 || dx >= textureWidth || dy < 0 || dy >= textureHeight) continue;

                // Alpha blend onto drawing
                Color dst = drawTexture.GetPixel(dx, dy);
                float a = src.a;
                drawTexture.SetPixel(dx, dy, new Color(
                    src.r * a + dst.r * (1f - a),
                    src.g * a + dst.g * (1f - a),
                    src.b * a + dst.b * (1f - a),
                    Mathf.Max(dst.a, a)));
            }
        }

        Destroy(readable);
        drawTexture.Apply();
    }

    // ── Coordinate Conversion ──

    private bool ScreenToTexturePos(Vector2 screenPos, out Vector2 texturePos)
    {
        texturePos = Vector2.zero;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, screenPos, null, out Vector2 localPoint))
            return false;

        // Convert from local rect space (-width/2..+width/2) to 0..1
        Rect rect = rectTransform.rect;
        if (rect.width <= 0 || rect.height <= 0) return false;
        float normalizedX = (localPoint.x - rect.x) / rect.width;
        float normalizedY = (localPoint.y - rect.y) / rect.height;

        if (normalizedX < 0 || normalizedX > 1 || normalizedY < 0 || normalizedY > 1)
            return false;

        texturePos = new Vector2(normalizedX * textureWidth, normalizedY * textureHeight);
        return true;
    }

    // ── Texture Helpers ──

    /// <summary>
    /// Reads pixels from any Texture (handles non-readable and size mismatch).
    /// Blits to a temporary RenderTexture at the target size.
    /// </summary>
    private Color[] GetResizedTexturePixels(Texture tex, int targetW, int targetH)
    {
        var rt = RenderTexture.GetTemporary(targetW, targetH, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        Graphics.Blit(tex, rt);
        RenderTexture.active = rt;

        var readable = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
        readable.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Color[] pixels = readable.GetPixels();
        Destroy(readable);
        return pixels;
    }

    // ── Undo ──

    private void SaveUndoSnapshot()
    {
        if (undoStack.Count >= MaxUndoSteps)
            undoStack.RemoveAt(0);

        undoStack.Add(drawTexture.GetPixels());
    }
}
