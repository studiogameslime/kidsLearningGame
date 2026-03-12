using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Connect-the-Dots mini-game. Numbered dots form geometric shapes.
/// The child taps dot 1 to start, then drags to connect sequentially.
/// After the last dot, the shape name fades in.
/// </summary>
public class ConnectTheDotsController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform playArea;
    public RectTransform lineContainer;
    public TextMeshProUGUI shapeNameText;
    public RectTransform dragArea;

    [Header("Prefabs")]
    public GameObject dotPrefab;

    [Header("Settings")]
    public float dotSize = 80f;
    public float lineWidth = 8f;
    public Color lineColor = new Color(0.3f, 0.7f, 1f, 0.9f);

    private Canvas canvas;
    private List<DotPoint> dots = new List<DotPoint>();
    private List<GameObject> lines = new List<GameObject>();
    private int currentDotIndex = 0;
    private int totalDots;
    private int lastShapeIndex = -1;
    private bool isDrawing;

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
        }, new Color(1f, 0.84f, 0f)), // gold

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
        }, new Color(0.3f, 0.69f, 0.93f)), // sky blue

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
        }, new Color(0.3f, 0.85f, 0.4f)), // green

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
        }, new Color(0.96f, 0.26f, 0.42f)), // red/pink

        // Triangle
        new ShapeDef("Triangle", new Vector2[] {
            new Vector2(0.50f, 0.88f),
            new Vector2(0.65f, 0.65f),
            new Vector2(0.80f, 0.30f),
            new Vector2(0.50f, 0.30f),
            new Vector2(0.20f, 0.30f),
            new Vector2(0.35f, 0.65f),
        }, new Color(1f, 0.6f, 0.2f)), // orange

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
        }, new Color(0.68f, 0.51f, 0.93f)), // purple

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
        }, new Color(0.95f, 0.85f, 0.3f)), // yellow

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
        }, new Color(0.82f, 0.41f, 0.32f)), // brown-red

        // Arrow (pointing up)
        new ShapeDef("Arrow", new Vector2[] {
            new Vector2(0.50f, 0.90f),
            new Vector2(0.75f, 0.62f),
            new Vector2(0.62f, 0.62f),
            new Vector2(0.62f, 0.25f),
            new Vector2(0.38f, 0.25f),
            new Vector2(0.38f, 0.62f),
            new Vector2(0.25f, 0.62f),
        }, new Color(0.2f, 0.6f, 0.86f)), // blue

        // Hexagon
        new ShapeDef("Hexagon", new Vector2[] {
            new Vector2(0.50f, 0.88f),
            new Vector2(0.76f, 0.73f),
            new Vector2(0.76f, 0.43f),
            new Vector2(0.50f, 0.28f),
            new Vector2(0.24f, 0.43f),
            new Vector2(0.24f, 0.73f),
        }, new Color(0.56f, 0.83f, 0.47f)), // light green
    };

    // Per-round state
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

        liveLineGO = new GameObject("LiveLine");
        liveLineGO.transform.SetParent(lineContainer, false);
        liveLineRT = liveLineGO.AddComponent<RectTransform>();
        liveLineImg = liveLineGO.AddComponent<Image>();
        liveLineImg.color = new Color(lineColor.r, lineColor.g, lineColor.b, 0.5f);
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

        if (shapeNameText != null)
        {
            shapeNameText.gameObject.SetActive(false);
            shapeNameText.color = new Color(1, 1, 1, 0);
        }

        if (liveLineGO != null)
            liveLineGO.SetActive(false);

        float areaW = playArea.rect.width;
        float areaH = playArea.rect.height;

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
            dot.Init(i, i + 1, OnDotTapped);

            dots.Add(dot);
        }

        dots[0].SetAsNext();
    }

    private void ClearRound()
    {
        foreach (var dot in dots)
            if (dot != null) Destroy(dot.gameObject);
        dots.Clear();

        foreach (var line in lines)
            if (line != null) Destroy(line);
        lines.Clear();

        currentDotIndex = 0;
        isDrawing = false;
    }

    private void OnDotTapped(DotPoint dot)
    {
        if (dot.dotIndex != currentDotIndex) return;
        ActivateDot(dot);

        if (currentDotIndex < totalDots)
            isDrawing = true;
    }

    private void Update()
    {
        if (!isDrawing || currentDotIndex >= totalDots) return;

        Vector2 screenPos;
        bool hasInput = false;

        if (Input.touchCount > 0)
        {
            screenPos = Input.GetTouch(0).position;
            hasInput = true;

            if (Input.GetTouch(0).phase == TouchPhase.Ended || Input.GetTouch(0).phase == TouchPhase.Canceled)
            {
                isDrawing = false;
                if (liveLineGO != null) liveLineGO.SetActive(false);
                return;
            }
        }
        else if (Input.GetMouseButton(0))
        {
            screenPos = Input.mousePosition;
            hasInput = true;
        }
        else
        {
            if (isDrawing)
            {
                isDrawing = false;
                if (liveLineGO != null) liveLineGO.SetActive(false);
            }
            return;
        }

        if (!hasInput) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playArea, screenPos, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out localPoint);

        if (currentDotIndex > 0 && liveLineGO != null)
        {
            Vector2 fromPos = dots[currentDotIndex - 1].GetComponent<RectTransform>().anchoredPosition;
            UpdateLiveLine(fromPos, localPoint);
        }

        var nextDot = dots[currentDotIndex];
        if (nextDot == null) return;

        var dotRT = nextDot.GetComponent<RectTransform>();
        float dist = Vector2.Distance(localPoint, dotRT.anchoredPosition);
        if (dist < dotSize * 1.0f)
        {
            ActivateDot(nextDot);
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

        if (currentDotIndex > 0)
            DrawLine(dots[currentDotIndex - 1], dot);

        currentDotIndex++;

        if (currentDotIndex >= totalDots)
        {
            isDrawing = false;
            if (liveLineGO != null) liveLineGO.SetActive(false);
            DrawLine(dot, dots[0]);
            StartCoroutine(OnAllDotsConnected());
        }
        else
        {
            dots[currentDotIndex].SetAsNext();
        }
    }

    private void DrawLine(DotPoint from, DotPoint to)
    {
        var lineGO = new GameObject("Line");
        lineGO.transform.SetParent(lineContainer, false);
        lineGO.transform.SetAsFirstSibling();

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

        lines.Add(lineGO);
        StartCoroutine(AnimateLineIn(lineImg));
    }

    private IEnumerator AnimateLineIn(Image lineImg)
    {
        Color c = lineImg.color;
        lineImg.color = new Color(c.r, c.g, c.b, 0);
        float t = 0f;
        float dur = 0.15f;
        while (t < dur)
        {
            t += Time.deltaTime;
            lineImg.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(t / dur) * c.a);
            yield return null;
        }
        lineImg.color = c;
    }

    private IEnumerator OnAllDotsConnected()
    {
        yield return new WaitForSeconds(0.3f);

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
                // Scale bounce
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
            foreach (var line in lines)
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
