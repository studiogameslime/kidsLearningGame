using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Handles touch/mouse drawing on a Texture2D displayed via a RawImage.
/// Supports brush color, size, eraser, undo, and clear.
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

    // ── Input Handling ──

    public void OnPointerDown(PointerEventData eventData)
    {
        // Save snapshot for undo before starting a new stroke
        SaveUndoSnapshot();

        isDrawing = true;
        lastDrawPos = null;

        Vector2 localPos;
        if (ScreenToTexturePos(eventData.position, out localPos))
        {
            DrawCircle(localPos);
            lastDrawPos = localPos;
            drawTexture.Apply();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDrawing) return;

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
        isDrawing = false;
        lastDrawPos = null;
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

    // ── Coordinate Conversion ──

    private bool ScreenToTexturePos(Vector2 screenPos, out Vector2 texturePos)
    {
        texturePos = Vector2.zero;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, screenPos, null, out Vector2 localPoint))
            return false;

        // Convert from local rect space (-width/2..+width/2) to 0..1
        Rect rect = rectTransform.rect;
        float normalizedX = (localPoint.x - rect.x) / rect.width;
        float normalizedY = (localPoint.y - rect.y) / rect.height;

        if (normalizedX < 0 || normalizedX > 1 || normalizedY < 0 || normalizedY > 1)
            return false;

        texturePos = new Vector2(normalizedX * textureWidth, normalizedY * textureHeight);
        return true;
    }

    // ── Undo ──

    private void SaveUndoSnapshot()
    {
        if (undoStack.Count >= MaxUndoSteps)
            undoStack.RemoveAt(0);

        undoStack.Add(drawTexture.GetPixels());
    }
}
