using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Connect-the-Dots mini-game. Numbered dots appear on screen.
/// The child taps dot 1 to start, then drags to dot 2, 3, etc.
/// A live line follows the finger. Completed lines stay.
/// After the last dot, the animal sprite fades in.
/// </summary>
public class ConnectTheDotsController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform playArea;
    public RectTransform lineContainer;
    public RawImage animalRevealImage;
    public RectTransform dragArea;          // full-area transparent Image for touch input

    [Header("Prefabs")]
    public GameObject dotPrefab;

    [Header("Settings")]
    public float dotSize = 80f;
    public float lineWidth = 8f;
    public Color lineColor = new Color(0.3f, 0.7f, 1f, 0.9f);

    private Canvas canvas;
    private Camera eventCamera;
    private List<DotPoint> dots = new List<DotPoint>();
    private List<GameObject> lines = new List<GameObject>();
    private int currentDotIndex = 0;
    private int totalDots;
    private Sprite currentAnimalSprite;
    private int lastAnimalIndex = -1;
    private bool isDrawing;

    // Live drawing line (follows finger from last activated dot)
    private GameObject liveLineGO;
    private RectTransform liveLineRT;
    private Image liveLineImg;

    private static readonly Vector2[][] DotLayouts = new Vector2[][]
    {
        new Vector2[] {
            new Vector2(0.30f, 0.85f), new Vector2(0.50f, 0.95f), new Vector2(0.70f, 0.85f),
            new Vector2(0.75f, 0.65f), new Vector2(0.70f, 0.45f), new Vector2(0.60f, 0.30f),
            new Vector2(0.50f, 0.20f), new Vector2(0.40f, 0.30f), new Vector2(0.30f, 0.45f),
            new Vector2(0.25f, 0.65f)
        },
        new Vector2[] {
            new Vector2(0.50f, 0.95f), new Vector2(0.60f, 0.75f), new Vector2(0.80f, 0.70f),
            new Vector2(0.65f, 0.55f), new Vector2(0.75f, 0.30f), new Vector2(0.50f, 0.42f),
            new Vector2(0.25f, 0.30f), new Vector2(0.35f, 0.55f), new Vector2(0.20f, 0.70f),
            new Vector2(0.40f, 0.75f)
        },
        new Vector2[] {
            new Vector2(0.35f, 0.90f), new Vector2(0.50f, 0.95f), new Vector2(0.65f, 0.90f),
            new Vector2(0.80f, 0.70f), new Vector2(0.75f, 0.48f), new Vector2(0.60f, 0.25f),
            new Vector2(0.40f, 0.25f), new Vector2(0.25f, 0.48f), new Vector2(0.20f, 0.70f)
        },
        new Vector2[] {
            new Vector2(0.50f, 0.92f), new Vector2(0.68f, 0.85f), new Vector2(0.78f, 0.68f),
            new Vector2(0.80f, 0.50f), new Vector2(0.72f, 0.32f), new Vector2(0.55f, 0.22f),
            new Vector2(0.38f, 0.25f), new Vector2(0.22f, 0.38f), new Vector2(0.20f, 0.55f),
            new Vector2(0.25f, 0.72f), new Vector2(0.38f, 0.85f)
        },
        new Vector2[] {
            new Vector2(0.50f, 0.92f), new Vector2(0.65f, 0.78f), new Vector2(0.75f, 0.58f),
            new Vector2(0.70f, 0.35f), new Vector2(0.50f, 0.22f), new Vector2(0.30f, 0.35f),
            new Vector2(0.25f, 0.58f), new Vector2(0.35f, 0.78f)
        },
    };

    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();

        if (animalRevealImage != null)
        {
            animalRevealImage.color = new Color(1, 1, 1, 0);
            animalRevealImage.gameObject.SetActive(false);
        }

        // Create the live drawing line (hidden until drawing starts)
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

    private Sprite PickRandomAnimalSprite()
    {
        var game = GameContext.CurrentGame;
        if (game != null && game.subItems != null && game.subItems.Count > 0)
        {
            int index;
            if (game.subItems.Count == 1)
                index = 0;
            else
            {
                do { index = Random.Range(0, game.subItems.Count); }
                while (index == lastAnimalIndex);
            }
            lastAnimalIndex = index;
            var item = game.subItems[index];
            return item.contentAsset != null ? item.contentAsset : item.thumbnail;
        }
        return null;
    }

    private void LoadNewRound()
    {
        ClearRound();

        currentAnimalSprite = PickRandomAnimalSprite();
        if (currentAnimalSprite == null)
        {
            Debug.LogError("ConnectTheDots: No animal sprite found!");
            return;
        }

        int layoutIdx = Random.Range(0, DotLayouts.Length);
        Vector2[] layout = DotLayouts[layoutIdx];
        totalDots = layout.Length;
        currentDotIndex = 0;
        isDrawing = false;

        if (animalRevealImage != null)
        {
            animalRevealImage.gameObject.SetActive(false);
            animalRevealImage.color = new Color(1, 1, 1, 0);
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

    /// <summary>
    /// Called by DotPoint when tapped. Used to start drawing from dot 1.
    /// </summary>
    private void OnDotTapped(DotPoint dot)
    {
        if (dot.dotIndex != currentDotIndex) return;
        ActivateDot(dot);

        // Start drawing mode after tapping the first dot
        if (currentDotIndex < totalDots)
            isDrawing = true;
    }

    /// <summary>
    /// Called every frame from Update — checks if finger/mouse is near the next dot.
    /// </summary>
    private void Update()
    {
        if (!isDrawing || currentDotIndex >= totalDots) return;

        // Get current pointer position
        Vector2 screenPos;
        bool hasInput = false;

        if (Input.touchCount > 0)
        {
            screenPos = Input.GetTouch(0).position;
            hasInput = true;

            // Stop drawing if touch ended
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
            // No input — stop drawing
            if (isDrawing)
            {
                isDrawing = false;
                if (liveLineGO != null) liveLineGO.SetActive(false);
            }
            return;
        }

        if (!hasInput) return;

        // Convert screen pos to local play area position
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playArea, screenPos, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out localPoint);

        // Update live line from last activated dot to finger
        if (currentDotIndex > 0 && liveLineGO != null)
        {
            Vector2 fromPos = dots[currentDotIndex - 1].GetComponent<RectTransform>().anchoredPosition;
            UpdateLiveLine(fromPos, localPoint);
        }

        // Check if finger is over the next dot
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
        lineImg.color = lineColor;
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

        if (animalRevealImage != null && currentAnimalSprite != null)
        {
            animalRevealImage.texture = currentAnimalSprite.texture;
            animalRevealImage.gameObject.SetActive(true);

            float dur = 0.8f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                animalRevealImage.color = new Color(1, 1, 1, p);

                float dotAlpha = 1f - p;
                foreach (var dot in dots)
                {
                    if (dot != null)
                    {
                        var cg = dot.GetComponent<CanvasGroup>();
                        if (cg == null) cg = dot.gameObject.AddComponent<CanvasGroup>();
                        cg.alpha = dotAlpha;
                    }
                }
                foreach (var line in lines)
                {
                    if (line != null)
                    {
                        var cg = line.GetComponent<CanvasGroup>();
                        if (cg == null) cg = line.AddComponent<CanvasGroup>();
                        cg.alpha = dotAlpha;
                    }
                }
                yield return null;
            }
        }

        yield return new WaitForSeconds(1.5f);
        LoadNewRound();
    }

    public void OnHomePressed() => NavigationManager.GoToMainMenu();
    public void OnRestartPressed() => LoadNewRound();
}
