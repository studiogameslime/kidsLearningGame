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

    // ── Guide Rendering (Thick Road Style) ──

    // Road colors
    private static readonly Color RoadInactive = new Color(0.88f, 0.88f, 0.88f, 1f);
    private static readonly Color RoadActive   = new Color(0.72f, 0.72f, 0.72f, 1f);
    private static readonly Color RoadBorder   = new Color(0.60f, 0.60f, 0.60f, 1f);
    private int RoadRadius => brushSize / 2 + 10;

    private Texture2D guideTexture; // separate texture for guide roads (redrawn on stroke change)

    private void DrawAllStrokeGuides()
    {
        // Create a separate guide texture for the road
        guideTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        guideTexture.filterMode = FilterMode.Bilinear;
        ClearGuideTexture();

        // Draw all strokes as faint roads
        for (int s = 0; s < currentLetter.strokes.Count; s++)
            DrawStrokeRoad(s, RoadInactive);

        guideTexture.Apply();

        // Create a RawImage for the guide texture (behind the tracing layer)
        if (guideParent != null)
        {
            var guideImgGO = new GameObject("GuideRoadImage");
            guideImgGO.transform.SetParent(guideParent, false);
            var grt = guideImgGO.AddComponent<RectTransform>();
            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
            grt.offsetMin = Vector2.zero; grt.offsetMax = Vector2.zero;
            var rawImg = guideImgGO.AddComponent<RawImage>();
            rawImg.texture = guideTexture;
            rawImg.color = Color.white;
            rawImg.raycastTarget = false;
            guideObjects.Add(guideImgGO);
        }
    }

    private void DrawStrokeRoad(int strokeIdx, Color roadColor)
    {
        var points = currentLetter.strokes[strokeIdx].points;
        int r = RoadRadius;

        // Draw border (slightly larger, darker)
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 from = NormToTex(points[i]);
            Vector2 to = NormToTex(points[i + 1]);
            DrawRoadSegment(from, to, r + 3, RoadBorder);
        }

        // Draw road fill
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 from = NormToTex(points[i]);
            Vector2 to = NormToTex(points[i + 1]);
            DrawRoadSegment(from, to, r, roadColor);
        }
    }

    private void RedrawGuideForActiveStroke(int activeIdx)
    {
        if (guideTexture == null) return;

        ClearGuideTexture();

        // Draw inactive/completed strokes
        for (int s = 0; s < currentLetter.strokes.Count; s++)
        {
            if (s < activeIdx)
            {
                // Completed: show in trace color (faded)
                Color completedColor = new Color(traceColor.r * 0.5f + 0.5f,
                                                  traceColor.g * 0.5f + 0.5f,
                                                  traceColor.b * 0.5f + 0.5f, 0.3f);
                DrawStrokeRoad(s, completedColor);
            }
            else if (s == activeIdx)
            {
                // Active: highlighted road
                DrawStrokeRoad(s, RoadActive);
            }
            else
            {
                // Future: very faint
                DrawStrokeRoad(s, RoadInactive);
            }
        }

        guideTexture.Apply();
    }

    private void DrawRoadSegment(Vector2 from, Vector2 to, int radius, Color color)
    {
        float dist = Vector2.Distance(from, to);
        float step = Mathf.Max(1f, radius * 0.3f);
        int steps = Mathf.CeilToInt(dist / step);
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0 : (float)i / steps;
            Vector2 p = Vector2.Lerp(from, to, t);
            int cx = Mathf.RoundToInt(p.x);
            int cy = Mathf.RoundToInt(p.y);
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        int px = cx + x, py = cy + y;
                        if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                        {
                            Color existing = guideTexture.GetPixel(px, py);
                            if (existing.a < color.a)
                                guideTexture.SetPixel(px, py, color);
                        }
                    }
                }
            }
        }
    }

    private void ClearGuideTexture()
    {
        if (guideTexture == null) return;
        var pixels = guideTexture.GetPixels();
        Color clear = new Color(0, 0, 0, 0);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        guideTexture.SetPixels(pixels);
    }

    private void UpdateGuideHighlight(int activeStroke)
    {
        RedrawGuideForActiveStroke(activeStroke);
    }

    private void MarkStrokeComplete(int strokeIdx)
    {
        // Will be redrawn when next stroke activates
    }

    private void ShowStartMarker(int strokeIdx)
    {
        if (startMarker != null) Destroy(startMarker);
        if (guideParent == null || strokeIdx >= currentLetter.strokes.Count) return;

        var startPt = currentLetter.strokes[strokeIdx].points[0];

        // Glowing start circle
        startMarker = new GameObject("StartMarker");
        startMarker.transform.SetParent(guideParent, false);
        var rt = startMarker.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(startPt.x, startPt.y);
        rt.sizeDelta = new Vector2(55, 55);

        var img = startMarker.AddComponent<Image>();
        img.color = new Color(0.3f, 0.85f, 0.4f, 0.85f);
        img.raycastTarget = false;
        if (circleSprite != null) img.sprite = circleSprite;

        // Inner glow (pulsing feel via slightly larger outer ring)
        var glowGO = new GameObject("Glow");
        glowGO.transform.SetParent(startMarker.transform, false);
        var glowRT = glowGO.AddComponent<RectTransform>();
        glowRT.anchorMin = Vector2.zero; glowRT.anchorMax = Vector2.one;
        glowRT.offsetMin = new Vector2(-8, -8); glowRT.offsetMax = new Vector2(8, 8);
        var glowImg = glowGO.AddComponent<Image>();
        glowImg.color = new Color(0.3f, 0.85f, 0.4f, 0.3f);
        glowImg.raycastTarget = false;
        if (circleSprite != null) glowImg.sprite = circleSprite;

        // Direction arrow (small text arrow pointing toward second waypoint)
        if (strokeIdx < currentLetter.strokes.Count)
        {
            var points = currentLetter.strokes[strokeIdx].points;
            if (points.Count >= 2)
            {
                Vector2 dir = (points[1] - points[0]).normalized;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                var arrowGO = new GameObject("Arrow");
                arrowGO.transform.SetParent(startMarker.transform, false);
                var arrowRT = arrowGO.AddComponent<RectTransform>();
                arrowRT.anchoredPosition = dir * 40f;
                arrowRT.sizeDelta = new Vector2(30, 30);
                arrowRT.localRotation = Quaternion.Euler(0, 0, angle);
                var arrowTMP = arrowGO.AddComponent<TMPro.TextMeshProUGUI>();
                arrowTMP.text = "\u25B6"; // ▶
                arrowTMP.fontSize = 20;
                arrowTMP.color = new Color(0.2f, 0.7f, 0.3f, 0.8f);
                arrowTMP.alignment = TMPro.TextAlignmentOptions.Center;
                arrowTMP.raycastTarget = false;
            }
        }
    }

    private void ClearGuides()
    {
        foreach (var go in guideObjects) if (go != null) Object.Destroy(go);
        guideObjects.Clear();
        if (startMarker != null) { Destroy(startMarker); startMarker = null; }
        if (directionArrow != null) { Destroy(directionArrow); directionArrow = null; }
        if (guideTexture != null) { Destroy(guideTexture); guideTexture = null; }
    }

    private Vector2 NormToTex(Vector2 norm) =>
        new Vector2(norm.x * textureWidth, norm.y * textureHeight);

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
