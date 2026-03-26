using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Handles touch-based letter tracing on a Texture2D.
/// Validates finger path against expected stroke waypoints and direction.
/// Draws a colored trail when tracing is correct.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class TracingCanvas : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Canvas")]
    public int textureWidth = 1024;
    public int textureHeight = 1024;

    [Header("Tracing")]
    public float pathTolerance = 0.08f;    // normalized distance tolerance
    public float startTolerance = 0.10f;   // how close to start point to begin
    public float advanceThreshold = 0.02f; // min distance to advance to next waypoint
    public float completionRatio = 0.80f;  // fraction of waypoints needed to complete stroke
    public int brushSize = 28;

    [Header("Guide Visuals")]
    public Sprite circleSprite;            // assigned by setup

    /// <summary>Called when a stroke is completed. Parameter: stroke index.</summary>
    public System.Action<int> onStrokeCompleted;
    /// <summary>Called when all strokes of the letter are completed.</summary>
    public System.Action onLetterCompleted;

    // Rendering
    private Texture2D drawTexture;
    private RawImage rawImage;
    private RectTransform rectTransform;
    private Color traceColor = Color.red;

    // Guide rendering (UI-based)
    private List<GameObject> guideObjects = new List<GameObject>();
    private GameObject startMarker;
    private GameObject directionArrow;

    // Letter state
    private HebrewLetterStrokeData.LetterData currentLetter;
    private int currentStrokeIndex;
    private int totalStrokes;
    private bool letterLoaded;

    // Tracing state
    private int progressIndex;   // how far along the current stroke waypoints
    private bool isTracing;
    private bool strokeStarted;  // finger is near start point
    private Vector2? lastTracePos;

    // Guide parent (for drawing dotted lines)
    private RectTransform guideParent;

    private Color clearColor = new Color(1, 1, 1, 0);

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
        rectTransform = GetComponent<RectTransform>();
    }

    public void Init()
    {
        drawTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        drawTexture.filterMode = FilterMode.Bilinear;
        ClearTexture();
        rawImage.texture = drawTexture;
    }

    // ── Public API ──

    public void SetColor(Color color) => traceColor = color;

    public void SetLetter(HebrewLetterStrokeData.LetterData data)
    {
        currentLetter = data;
        totalStrokes = data.strokes.Count;
        currentStrokeIndex = 0;
        letterLoaded = true;
        ClearTexture();
        ClearGuides();
        DrawAllStrokeGuides();
        ActivateStroke(0);
    }

    public void SetGuideParent(RectTransform parent)
    {
        guideParent = parent;
    }

    public void Clear()
    {
        ClearTexture();
        ClearGuides();
        letterLoaded = false;
        currentStrokeIndex = 0;
        progressIndex = 0;
        isTracing = false;
        strokeStarted = false;
        lastTracePos = null;
    }

    public int CurrentStrokeIndex => currentStrokeIndex;
    public int TotalStrokes => totalStrokes;

    // ── Input ──

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!letterLoaded || currentStrokeIndex >= totalStrokes) return;

        Vector2 texPos;
        if (!ScreenToTexturePos(eventData.position, out texPos)) return;

        Vector2 normPos = new Vector2(texPos.x / textureWidth, texPos.y / textureHeight);
        var stroke = currentLetter.strokes[currentStrokeIndex];

        // Check if finger is near the start point (or current progress point)
        Vector2 targetStart = stroke.points[progressIndex];
        float dist = Vector2.Distance(normPos, targetStart);

        if (dist <= startTolerance)
        {
            isTracing = true;
            strokeStarted = true;
            lastTracePos = texPos;
            DrawCircle(texPos, brushSize / 2);
            drawTexture.Apply();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isTracing || !letterLoaded) return;

        Vector2 texPos;
        if (!ScreenToTexturePos(eventData.position, out texPos)) return;

        Vector2 normPos = new Vector2(texPos.x / textureWidth, texPos.y / textureHeight);
        var stroke = currentLetter.strokes[currentStrokeIndex];
        var points = stroke.points;

        if (progressIndex >= points.Count - 1)
        {
            // Already at end — complete stroke
            CompleteCurrentStroke();
            return;
        }

        // Find nearest point on path ahead of current progress
        int nearestIdx = progressIndex;
        float nearestDist = float.MaxValue;

        // Look ahead (don't allow going backwards)
        int lookAhead = Mathf.Min(progressIndex + 5, points.Count - 1);
        for (int i = progressIndex; i <= lookAhead; i++)
        {
            float d = Vector2.Distance(normPos, points[i]);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearestIdx = i;
            }
        }

        // Also check distance to line segments ahead
        float segDist = DistanceToPathSegment(normPos, points, progressIndex, lookAhead);
        bool onPath = segDist <= pathTolerance || nearestDist <= pathTolerance;

        if (onPath && nearestIdx >= progressIndex)
        {
            // Check direction: movement should generally go forward along the path
            if (nearestIdx > progressIndex)
            {
                Vector2 expectedDir = (points[nearestIdx] - points[progressIndex]).normalized;
                Vector2 moveDir = (normPos - new Vector2(lastTracePos.Value.x / textureWidth,
                                                          lastTracePos.Value.y / textureHeight)).normalized;
                float dot = Vector2.Dot(moveDir, expectedDir);

                // Allow if generally moving forward (dot > -0.3 is very forgiving)
                if (dot > -0.3f)
                {
                    progressIndex = nearestIdx;

                    // Draw trail
                    if (lastTracePos.HasValue)
                        DrawLine(lastTracePos.Value, texPos, brushSize / 2);
                    else
                        DrawCircle(texPos, brushSize / 2);

                    lastTracePos = texPos;
                    drawTexture.Apply();

                    // Check completion
                    float progress = (float)progressIndex / (points.Count - 1);
                    if (progress >= completionRatio)
                    {
                        // Draw remaining path to end
                        Vector2 endTex = new Vector2(points[points.Count - 1].x * textureWidth,
                                                      points[points.Count - 1].y * textureHeight);
                        DrawLine(texPos, endTex, brushSize / 2);
                        drawTexture.Apply();
                        CompleteCurrentStroke();
                    }
                }
            }
            else
            {
                // Same waypoint — still draw if on path
                if (lastTracePos.HasValue)
                    DrawLine(lastTracePos.Value, texPos, brushSize / 2);
                lastTracePos = texPos;
                drawTexture.Apply();
            }
        }
        // Off-path: just ignore (no progress, no punishment)
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isTracing = false;
        // Don't reset progress — child can resume from last point
    }

    // ── Stroke Management ──

    private void CompleteCurrentStroke()
    {
        isTracing = false;
        strokeStarted = false;
        lastTracePos = null;

        int completedIdx = currentStrokeIndex;
        currentStrokeIndex++;
        progressIndex = 0;

        // Mark completed stroke guide as solid
        MarkStrokeComplete(completedIdx);

        onStrokeCompleted?.Invoke(completedIdx);

        if (currentStrokeIndex >= totalStrokes)
        {
            onLetterCompleted?.Invoke();
        }
        else
        {
            ActivateStroke(currentStrokeIndex);
        }
    }

    private void ActivateStroke(int idx)
    {
        progressIndex = 0;
        strokeStarted = false;
        isTracing = false;
        lastTracePos = null;

        // Update visual guides
        UpdateGuideHighlight(idx);
        ShowStartMarker(idx);
    }

    // ── Guide Rendering ──

    private void DrawAllStrokeGuides()
    {
        // Draw guide paths directly on the texture as dashed lines
        DrawGuideOnTexture();

        // Also create UI dot markers for highlighting
        if (guideParent == null) return;

        for (int s = 0; s < currentLetter.strokes.Count; s++)
        {
            var stroke = currentLetter.strokes[s];
            var points = stroke.points;

            for (int i = 0; i < points.Count; i++)
            {
                var dotGO = new GameObject($"Dot_{s}_{i}");
                dotGO.transform.SetParent(guideParent, false);
                var rt = dotGO.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(points[i].x, points[i].y);
                rt.sizeDelta = new Vector2(24, 24);

                var img = dotGO.AddComponent<Image>();
                img.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                img.raycastTarget = false;
                if (circleSprite != null) img.sprite = circleSprite;

                guideObjects.Add(dotGO);
            }
        }
    }

    /// <summary>
    /// Draws dashed guide lines for all strokes directly on the drawing texture.
    /// This ensures the letter shape is always visible on the canvas.
    /// </summary>
    private void DrawGuideOnTexture()
    {
        Color guideColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        int guideRadius = brushSize / 2 + 4;

        for (int s = 0; s < currentLetter.strokes.Count; s++)
        {
            var points = currentLetter.strokes[s].points;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 from = new Vector2(points[i].x * textureWidth, points[i].y * textureHeight);
                Vector2 to = new Vector2(points[i + 1].x * textureWidth, points[i + 1].y * textureHeight);

                // Draw dashed line: alternating draw/skip segments
                float dist = Vector2.Distance(from, to);
                float dashLen = guideRadius * 2.5f;
                float gapLen = guideRadius * 1.5f;
                float pos = 0;
                bool draw = true;

                while (pos < dist)
                {
                    float segEnd = Mathf.Min(pos + (draw ? dashLen : gapLen), dist);
                    if (draw)
                    {
                        Vector2 a = Vector2.Lerp(from, to, pos / dist);
                        Vector2 b = Vector2.Lerp(from, to, segEnd / dist);
                        DrawGuideLine(a, b, guideRadius, guideColor);
                    }
                    pos = segEnd;
                    draw = !draw;
                }
            }

            // Draw waypoint dots (solid circles at each waypoint)
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 center = new Vector2(points[i].x * textureWidth, points[i].y * textureHeight);
                DrawGuideCircle(center, guideRadius + 2, guideColor);
            }
        }

        drawTexture.Apply();
    }

    private void DrawGuideCircle(Vector2 center, int radius, Color color)
    {
        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    int px = cx + x, py = cy + y;
                    if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                        drawTexture.SetPixel(px, py, color);
                }
            }
        }
    }

    private void DrawGuideLine(Vector2 from, Vector2 to, int radius, Color color)
    {
        float dist = Vector2.Distance(from, to);
        float step = Mathf.Max(1f, radius * 0.4f);
        int steps = Mathf.CeilToInt(dist / step);
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0 : (float)i / steps;
            DrawGuideCircle(Vector2.Lerp(from, to, t), radius, color);
        }
    }

    private void UpdateGuideHighlight(int activeStroke)
    {
        int dotIdx = 0;
        for (int s = 0; s < currentLetter.strokes.Count; s++)
        {
            var points = currentLetter.strokes[s].points;
            bool isActive = s == activeStroke;
            bool isCompleted = s < activeStroke;

            for (int i = 0; i < points.Count; i++)
            {
                if (dotIdx < guideObjects.Count)
                {
                    var img = guideObjects[dotIdx].GetComponent<Image>();
                    if (img != null)
                    {
                        if (isCompleted)
                            img.color = new Color(traceColor.r, traceColor.g, traceColor.b, 0.6f);
                        else if (isActive)
                            img.color = new Color(0.3f, 0.3f, 0.3f, 0.7f);
                        else
                            img.color = new Color(0.6f, 0.6f, 0.6f, 0.3f);

                        // Active stroke dots are bigger
                        var rt = guideObjects[dotIdx].GetComponent<RectTransform>();
                        rt.sizeDelta = isActive ? new Vector2(20, 20) : new Vector2(14, 14);
                    }
                }
                dotIdx++;
            }
        }
    }

    private void MarkStrokeComplete(int strokeIdx)
    {
        int dotIdx = 0;
        for (int s = 0; s <= strokeIdx; s++)
        {
            var points = currentLetter.strokes[s].points;
            for (int i = 0; i < points.Count; i++)
            {
                if (s == strokeIdx && dotIdx < guideObjects.Count)
                {
                    var img = guideObjects[dotIdx].GetComponent<Image>();
                    if (img != null)
                        img.color = new Color(traceColor.r, traceColor.g, traceColor.b, 0.6f);
                }
                dotIdx++;
            }
        }
    }

    private void ShowStartMarker(int strokeIdx)
    {
        if (startMarker != null) Destroy(startMarker);
        if (guideParent == null || strokeIdx >= currentLetter.strokes.Count) return;

        var startPt = currentLetter.strokes[strokeIdx].points[0];

        startMarker = new GameObject("StartMarker");
        startMarker.transform.SetParent(guideParent, false);
        var rt = startMarker.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(startPt.x, startPt.y);
        rt.sizeDelta = new Vector2(40, 40);

        var img = startMarker.AddComponent<Image>();
        img.color = new Color(0.2f, 0.8f, 0.3f, 0.7f);
        img.raycastTarget = false;
        if (circleSprite != null) img.sprite = circleSprite;
    }

    private void ClearGuides()
    {
        foreach (var go in guideObjects) if (go != null) Object.Destroy(go);
        guideObjects.Clear();
        if (startMarker != null) { Destroy(startMarker); startMarker = null; }
        if (directionArrow != null) { Destroy(directionArrow); directionArrow = null; }
    }

    // ── Drawing ──

    private void DrawCircle(Vector2 center, int radius)
    {
        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    int px = cx + x, py = cy + y;
                    if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                        drawTexture.SetPixel(px, py, traceColor);
                }
            }
        }
    }

    private void DrawLine(Vector2 from, Vector2 to, int radius)
    {
        float dist = Vector2.Distance(from, to);
        float step = Mathf.Max(1f, radius * 0.4f);
        int steps = Mathf.CeilToInt(dist / step);
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0 : (float)i / steps;
            DrawCircle(Vector2.Lerp(from, to, t), radius);
        }
    }

    private void ClearTexture()
    {
        if (drawTexture == null) return;
        var pixels = drawTexture.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clearColor;
        drawTexture.SetPixels(pixels);
        drawTexture.Apply();
    }

    // ── Coordinate Conversion ──

    private bool ScreenToTexturePos(Vector2 screenPos, out Vector2 texturePos)
    {
        texturePos = Vector2.zero;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, screenPos, null, out Vector2 localPoint))
            return false;

        Rect rect = rectTransform.rect;
        float nx = (localPoint.x - rect.x) / rect.width;
        float ny = (localPoint.y - rect.y) / rect.height;

        if (nx < 0 || nx > 1 || ny < 0 || ny > 1) return false;

        texturePos = new Vector2(nx * textureWidth, ny * textureHeight);
        return true;
    }

    // ── Path Math ──

    /// <summary>
    /// Minimum distance from point to any line segment between indices start..end.
    /// </summary>
    private float DistanceToPathSegment(Vector2 point, List<Vector2> path, int start, int end)
    {
        float minDist = float.MaxValue;
        for (int i = start; i < end && i < path.Count - 1; i++)
        {
            float d = DistanceToSegment(point, path[i], path[i + 1]);
            if (d < minDist) minDist = d;
        }
        return minDist;
    }

    private float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        Vector2 closest = a + ab * t;
        return Vector2.Distance(p, closest);
    }
}
