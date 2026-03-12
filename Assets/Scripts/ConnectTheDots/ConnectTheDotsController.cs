using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Connect-the-Dots mini-game with pure drag-to-draw mechanic.
/// The child presses on dot 1 and drags continuously through each dot
/// in order, drawing the shape in one fluid motion.
/// </summary>
public class ConnectTheDotsController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform playArea;
    public RectTransform lineContainer;
    public TextMeshProUGUI shapeNameText;

    [Header("Prefabs")]
    public GameObject dotPrefab;

    [Header("Settings")]
    public float dotSize = 80f;
    public float lineWidth = 10f;
    public float hitRadius = 1.2f;  // multiplier on dotSize for drag detection
    public Color lineColor = new Color(0.3f, 0.7f, 1f, 0.9f);

    [Header("Guide Line")]
    public float guideLineAlpha = 0.12f;

    private Canvas canvas;
    private List<DotPoint> dots = new List<DotPoint>();
    private List<GameObject> drawnLines = new List<GameObject>();
    private List<GameObject> guideLines = new List<GameObject>();
    private int currentDotIndex = 0;
    private int totalDots;
    private int lastShapeIndex = -1;
    private bool isDrawing;
    private bool roundComplete;

    // Live drawing line (follows finger from last activated dot)
    private GameObject liveLineGO;
    private RectTransform liveLineRT;
    private Image liveLineImg;

    // ---------- Shape definitions ----------

    private struct ShapeDef
    {
        public string name;
        public Vector2[] points;
        public Color color;

        public ShapeDef(string name, Vector2[] points, Color color)
        {
            this.name = name;
            this.points = points;
            this.color = color;
        }
    }

    private static readonly ShapeDef[] Shapes = new ShapeDef[]
    {
        // Star (5-pointed)
        new ShapeDef("Star", new Vector2[] {
            new Vector2(0.50f, 0.92f),
            new Vector2(0.59f, 0.72f),
            new Vector2(0.82f, 0.68f),
            new Vector2(0.65f, 0.52f),
            new Vector2(0.72f, 0.28f),
            new Vector2(0.50f, 0.40f),
            new Vector2(0.28f, 0.28f),
            new Vector2(0.35f, 0.52f),
            new Vector2(0.18f, 0.68f),
            new Vector2(0.41f, 0.72f),
        }, new Color(1f, 0.84f, 0f)),

        // Circle (12 points)
        new ShapeDef("Circle", new Vector2[] {
            new Vector2(0.50f, 0.90f),
            new Vector2(0.67f, 0.87f),
            new Vector2(0.80f, 0.75f),
            new Vector2(0.85f, 0.60f),
            new Vector2(0.80f, 0.45f),
            new Vector2(0.67f, 0.33f),
            new Vector2(0.50f, 0.30f),
            new Vector2(0.33f, 0.33f),
            new Vector2(0.20f, 0.45f),
            new Vector2(0.15f, 0.60f),
            new Vector2(0.20f, 0.75f),
            new Vector2(0.33f, 0.87f),
        }, new Color(0.3f, 0.69f, 0.93f)),

        // Square
        new ShapeDef("Square", new Vector2[] {
            new Vector2(0.22f, 0.82f),
            new Vector2(0.51f, 0.82f),
            new Vector2(0.78f, 0.82f),
            new Vector2(0.78f, 0.53f),
            new Vector2(0.78f, 0.25f),
            new Vector2(0.51f, 0.25f),
            new Vector2(0.22f, 0.25f),
            new Vector2(0.22f, 0.53f),
        }, new Color(0.3f, 0.85f, 0.4f)),

        // Heart
        new ShapeDef("Heart", new Vector2[] {
            new Vector2(0.50f, 0.30f),
            new Vector2(0.28f, 0.45f),
            new Vector2(0.17f, 0.62f),
            new Vector2(0.20f, 0.78f),
            new Vector2(0.33f, 0.88f),
            new Vector2(0.50f, 0.78f),
            new Vector2(0.67f, 0.88f),
            new Vector2(0.80f, 0.78f),
            new Vector2(0.83f, 0.62f),
            new Vector2(0.72f, 0.45f),
        }, new Color(0.96f, 0.26f, 0.42f)),

        // Triangle
        new ShapeDef("Triangle", new Vector2[] {
            new Vector2(0.50f, 0.88f),
            new Vector2(0.65f, 0.65f),
            new Vector2(0.80f, 0.30f),
            new Vector2(0.50f, 0.30f),
            new Vector2(0.20f, 0.30f),
            new Vector2(0.35f, 0.65f),
        }, new Color(1f, 0.6f, 0.2f)),

        // Diamond
        new ShapeDef("Diamond", new Vector2[] {
            new Vector2(0.50f, 0.90f),
            new Vector2(0.68f, 0.72f),
            new Vector2(0.80f, 0.55f),
            new Vector2(0.68f, 0.38f),
            new Vector2(0.50f, 0.22f),
            new Vector2(0.32f, 0.38f),
            new Vector2(0.20f, 0.55f),
            new Vector2(0.32f, 0.72f),
        }, new Color(0.68f, 0.51f, 0.93f)),

        // Moon (crescent)
        new ShapeDef("Moon", new Vector2[] {
            new Vector2(0.55f, 0.90f),
            new Vector2(0.38f, 0.85f),
            new Vector2(0.25f, 0.75f),
            new Vector2(0.18f, 0.60f),
            new Vector2(0.25f, 0.45f),
            new Vector2(0.38f, 0.35f),
            new Vector2(0.55f, 0.30f),
            new Vector2(0.55f, 0.42f),
            new Vector2(0.45f, 0.55f),
            new Vector2(0.45f, 0.65f),
            new Vector2(0.55f, 0.78f),
        }, new Color(0.95f, 0.85f, 0.3f)),

        // House
        new ShapeDef("House", new Vector2[] {
            new Vector2(0.50f, 0.88f),
            new Vector2(0.78f, 0.62f),
            new Vector2(0.78f, 0.38f),
            new Vector2(0.78f, 0.22f),
            new Vector2(0.50f, 0.22f),
            new Vector2(0.22f, 0.22f),
            new Vector2(0.22f, 0.38f),
            new Vector2(0.22f, 0.62f),
        }, new Color(0.82f, 0.41f, 0.32f)),

        // Arrow (pointing up)
        new ShapeDef("Arrow", new Vector2[] {
            new Vector2(0.50f, 0.90f),
            new Vector2(0.75f, 0.62f),
            new Vector2(0.62f, 0.62f),
            new Vector2(0.62f, 0.25f),
            new Vector2(0.38f, 0.25f),
            new Vector2(0.38f, 0.62f),
            new Vector2(0.25f, 0.62f),
        }, new Color(0.2f, 0.6f, 0.86f)),

        // Hexagon
        new ShapeDef("Hexagon", new Vector2[] {
            new Vector2(0.50f, 0.88f),
            new Vector2(0.76f, 0.73f),
            new Vector2(0.76f, 0.43f),
            new Vector2(0.50f, 0.28f),
            new Vector2(0.24f, 0.43f),
            new Vector2(0.24f, 0.73f),
        }, new Color(0.56f, 0.83f, 0.47f)),
    };

    private ShapeDef currentShape;
    private Color currentLineColor;

    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();

        if (shapeNameText != null)
        {
            shapeNameText.color = new Color(1, 1, 1, 0);
            shapeNameText.gameObject.SetActive(false);
        }

        // Create the live drawing line (hidden until drawing starts)
        liveLineGO = new GameObject("LiveLine");
        liveLineGO.transform.SetParent(lineContainer, false);
        liveLineRT = liveLineGO.AddComponent<RectTransform>();
        liveLineImg = liveLineGO.AddComponent<Image>();
        liveLineImg.raycastTarget = false;
        liveLineRT.pivot = new Vector2(0.5f, 0.5f);
        liveLineGO.SetActive(false);

        LoadNewRound();
    }

    private ShapeDef PickRandomShape()
    {
        int index;
        if (Shapes.Length == 1)
            index = 0;
        else
        {
            do { index = Random.Range(0, Shapes.Length); }
            while (index == lastShapeIndex);
        }
        lastShapeIndex = index;
        return Shapes[index];
    }

    private void LoadNewRound()
    {
        ClearRound();

        currentShape = PickRandomShape();
        currentLineColor = currentShape.color;

        Vector2[] layout = currentShape.points;
        totalDots = layout.Length;
        currentDotIndex = 0;
        isDrawing = false;
        roundComplete = false;

        if (shapeNameText != null)
        {
            shapeNameText.gameObject.SetActive(false);
            shapeNameText.color = new Color(1, 1, 1, 0);
        }

        if (liveLineGO != null)
            liveLineGO.SetActive(false);

        float areaW = playArea.rect.width;
        float areaH = playArea.rect.height;

        // Create dots
        for (int i = 0; i < totalDots; i++)
        {
            var dotGO = Instantiate(dotPrefab, playArea);
            var rt = dotGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(dotSize, dotSize);

            float x = (layout[i].x - 0.5f) * areaW;
            float y = (layout[i].y - 0.5f) * areaH;
            rt.anchoredPosition = new Vector2(x, y);

            var dot = dotGO.GetComponent<DotPoint>();
            dot.normalizedPosition = layout[i];
            dot.Init(i, i + 1, null); // no tap callback — pure drag
            dots.Add(dot);
        }

        // Draw faint guide lines showing the path to trace
        CreateGuideLines();

        // Highlight first dot
        dots[0].SetAsNext();
    }

    /// <summary>
    /// Creates faint dashed guide lines between consecutive dots
    /// so the child can see the path they need to draw.
    /// </summary>
    private void CreateGuideLines()
    {
        for (int i = 0; i < totalDots; i++)
        {
            int next = (i + 1) % totalDots;
            var fromRT = dots[i].GetComponent<RectTransform>();
            var toRT = dots[next].GetComponent<RectTransform>();

            var lineGO = new GameObject("GuideLine");
            lineGO.transform.SetParent(lineContainer, false);
            lineGO.transform.SetAsFirstSibling();

            var lineRT = lineGO.AddComponent<RectTransform>();
            var lineImg = lineGO.AddComponent<Image>();
            lineImg.color = new Color(currentLineColor.r, currentLineColor.g, currentLineColor.b, guideLineAlpha);
            lineImg.raycastTarget = false;

            Vector2 fromPos = fromRT.anchoredPosition;
            Vector2 toPos = toRT.anchoredPosition;
            Vector2 dir = toPos - fromPos;
            float distance = dir.magnitude;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            lineRT.sizeDelta = new Vector2(distance, lineWidth * 0.5f);
            lineRT.anchoredPosition = fromPos + dir * 0.5f;
            lineRT.localRotation = Quaternion.Euler(0, 0, angle);
            lineRT.pivot = new Vector2(0.5f, 0.5f);

            guideLines.Add(lineGO);
        }
    }

    private void ClearRound()
    {
        foreach (var dot in dots)
            if (dot != null) Destroy(dot.gameObject);
        dots.Clear();

        foreach (var line in drawnLines)
            if (line != null) Destroy(line);
        drawnLines.Clear();

        foreach (var line in guideLines)
            if (line != null) Destroy(line);
        guideLines.Clear();

        currentDotIndex = 0;
        isDrawing = false;
        roundComplete = false;
    }

    private void Update()
    {
        if (roundComplete) return;

        Vector2 screenPos;
        bool pointerDown = false;
        bool pointerUp = false;

        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            screenPos = touch.position;
            pointerDown = true;
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                pointerUp = true;
        }
        else if (Input.GetMouseButton(0))
        {
            screenPos = Input.mousePosition;
            pointerDown = true;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            screenPos = Input.mousePosition;
            pointerUp = true;
        }
        else
        {
            // No input — if we were drawing, stop
            if (isDrawing)
            {
                isDrawing = false;
                if (liveLineGO != null) liveLineGO.SetActive(false);
            }
            return;
        }

        // Convert screen position to play area local coords
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playArea, screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out localPoint);

        if (pointerUp)
        {
            isDrawing = false;
            if (liveLineGO != null) liveLineGO.SetActive(false);
            return;
        }

        if (!pointerDown) return;

        float detectRadius = dotSize * hitRadius;

        // Not yet drawing — must press and drag on the current dot to start/resume
        if (!isDrawing)
        {
            // Determine which dot the finger must be on
            int targetIdx = (currentDotIndex < totalDots) ? currentDotIndex : 0;
            var startDot = dots[targetIdx];
            var startRT = startDot.GetComponent<RectTransform>();
            float dist = Vector2.Distance(localPoint, startRT.anchoredPosition);

            if (dist < detectRadius)
            {
                if (currentDotIndex == 0)
                    ActivateDot(startDot);
                isDrawing = true;
            }
            return;
        }

        // Currently drawing — update live line from last activated dot to finger
        int lastActivated = Mathf.Min(currentDotIndex - 1, totalDots - 1);
        if (lastActivated >= 0 && liveLineGO != null)
        {
            Vector2 fromPos = dots[lastActivated].GetComponent<RectTransform>().anchoredPosition;
            UpdateLiveLine(fromPos, localPoint);
        }

        // Check if finger reached the next dot
        if (currentDotIndex < totalDots)
        {
            var nextDot = dots[currentDotIndex];
            var dotRT = nextDot.GetComponent<RectTransform>();
            float dist = Vector2.Distance(localPoint, dotRT.anchoredPosition);

            if (dist < detectRadius)
            {
                ActivateDot(nextDot);
            }
        }
        // All dots done — check if finger dragged back to dot 0 to close the shape
        else
        {
            var firstDot = dots[0];
            var firstRT = firstDot.GetComponent<RectTransform>();
            float dist = Vector2.Distance(localPoint, firstRT.anchoredPosition);

            if (dist < detectRadius)
            {
                DrawLine(dots[totalDots - 1], dots[0]);
                dots[0].Activate();
                isDrawing = false;
                roundComplete = true;
                if (liveLineGO != null) liveLineGO.SetActive(false);
                StartCoroutine(OnAllDotsConnected());
            }
        }
    }

    private void UpdateLiveLine(Vector2 from, Vector2 to)
    {
        liveLineGO.SetActive(true);
        liveLineImg.color = new Color(currentLineColor.r, currentLineColor.g, currentLineColor.b, 0.5f);
        Vector2 dir = to - from;
        float distance = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        liveLineRT.sizeDelta = new Vector2(distance, lineWidth);
        liveLineRT.anchoredPosition = from + dir * 0.5f;
        liveLineRT.localRotation = Quaternion.Euler(0, 0, angle);
    }

    private void ActivateDot(DotPoint dot)
    {
        if (dot.dotIndex != currentDotIndex) return;

        dot.Activate();

        // Draw permanent line from previous dot
        if (currentDotIndex > 0)
            DrawLine(dots[currentDotIndex - 1], dot);

        currentDotIndex++;

        if (currentDotIndex < totalDots)
        {
            dots[currentDotIndex].SetAsNext();
        }
        else
        {
            // All dots hit — highlight dot 0 so the child drags back to close
            dots[0].SetAsNext();
        }
    }

    private void DrawLine(DotPoint from, DotPoint to)
    {
        var lineGO = new GameObject("DrawnLine");
        lineGO.transform.SetParent(lineContainer, false);

        var lineRT = lineGO.AddComponent<RectTransform>();
        var lineImg = lineGO.AddComponent<Image>();
        lineImg.color = currentLineColor;
        lineImg.raycastTarget = false;

        Vector2 fromPos = from.GetComponent<RectTransform>().anchoredPosition;
        Vector2 toPos = to.GetComponent<RectTransform>().anchoredPosition;
        Vector2 dir = toPos - fromPos;
        float distance = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        lineRT.sizeDelta = new Vector2(distance, lineWidth);
        lineRT.anchoredPosition = fromPos + dir * 0.5f;
        lineRT.localRotation = Quaternion.Euler(0, 0, angle);
        lineRT.pivot = new Vector2(0.5f, 0.5f);

        drawnLines.Add(lineGO);

        // Instant appearance — feels like drawing
        lineImg.color = currentLineColor;
    }

    private IEnumerator OnAllDotsConnected()
    {
        yield return new WaitForSeconds(0.4f);

        // Fade out guide lines
        float guideFade = 0.3f;
        float gf = 0f;
        while (gf < guideFade)
        {
            gf += Time.deltaTime;
            float a = Mathf.Lerp(guideLineAlpha, 0f, gf / guideFade);
            foreach (var gl in guideLines)
            {
                if (gl != null)
                {
                    var img = gl.GetComponent<Image>();
                    if (img != null)
                    {
                        Color c = img.color;
                        img.color = new Color(c.r, c.g, c.b, a);
                    }
                }
            }
            yield return null;
        }

        // Show shape name
        if (shapeNameText != null)
        {
            shapeNameText.text = currentShape.name;
            shapeNameText.color = new Color(currentShape.color.r, currentShape.color.g, currentShape.color.b, 0f);
            shapeNameText.gameObject.SetActive(true);

            float dur = 0.6f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                shapeNameText.color = new Color(currentShape.color.r, currentShape.color.g, currentShape.color.b, p);
                float scale = 1f + 0.3f * Mathf.Sin(p * Mathf.PI);
                shapeNameText.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            shapeNameText.transform.localScale = Vector3.one;
        }

        yield return new WaitForSeconds(1.8f);

        // Fade out everything
        float fadeDur = 0.5f;
        float ft = 0f;
        while (ft < fadeDur)
        {
            ft += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(ft / fadeDur);
            foreach (var dot in dots)
            {
                if (dot != null)
                {
                    var cg = dot.GetComponent<CanvasGroup>();
                    if (cg == null) cg = dot.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = alpha;
                }
            }
            foreach (var line in drawnLines)
            {
                if (line != null)
                {
                    var cg = line.GetComponent<CanvasGroup>();
                    if (cg == null) cg = line.AddComponent<CanvasGroup>();
                    cg.alpha = alpha;
                }
            }
            if (shapeNameText != null)
            {
                Color c = shapeNameText.color;
                shapeNameText.color = new Color(c.r, c.g, c.b, alpha);
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);
        LoadNewRound();
    }

    public void OnHomePressed() => NavigationManager.GoToMainMenu();
    public void OnRestartPressed() => LoadNewRound();
}
