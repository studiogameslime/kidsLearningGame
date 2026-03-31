using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Connect-the-Dots mini-game — redesigned for landscape with magical visuals.
///
/// GAMEPLAY IS UNCHANGED:
/// - Child presses on dot 1 and drags through dots in order
/// - Tapping individual dots also works
/// - Shape closes when child drags back to dot 1
///
/// VISUAL IMPROVEMENTS:
/// - Night sky with twinkling decorative stars
/// - Puzzle dots look like bright glowing stars
/// - Constellation-style warm white/yellow lines
/// - Auto-generated animal silhouette shapes from sprites (DotShapeData)
/// - Falls back to hardcoded simple shapes if no generated data found
/// - Completion: animal reveal + Hebrew name → celebration
/// </summary>
public class ConnectTheDotsController : BaseMiniGame
{
    [Header("UI References")]
    public RectTransform playArea;
    public RectTransform lineContainer;
    public TextMeshProUGUI shapeNameText;
    public Image revealImage;

    [Header("Night Sky")]
    public Image skyImage;
    public Image horizonGlow;
    public RectTransform starLayer;

    [Header("Prefabs")]
    public GameObject dotPrefab;

    [Header("Settings")]
    public float dotSize = 80f;
    public float lineWidth = 12f;
    public float hitRadius = 1.2f;
    public Color lineColor = new Color(0.3f, 0.7f, 1f, 0.9f);

    [Header("Guide Line")]
    public float guideLineAlpha = 0.10f;

    [Header("Difficulty (0=Easy, 1=Medium, 2=Hard)")]
    public int difficulty = 0;

    private Canvas canvas;
    private List<DotPoint> dots = new List<DotPoint>();
    private List<GameObject> drawnLines = new List<GameObject>();
    private List<GameObject> guideLines = new List<GameObject>();
    private List<GameObject> sparkParticles = new List<GameObject>();
    private List<Coroutine> activeCoroutines = new List<Coroutine>();
    private int currentDotIndex = 0;
    private int totalDots;
    private int lastShapeIndex = -1;
    private bool isDrawing;
    private bool roundComplete;
    private int roundNumber;
    private float currentDotSize;

    // Sprite-generated shapes loaded at runtime, filtered by difficulty
    private DotShapeData[] allGeneratedShapes;
    private List<DotShapeData> eligibleShapes = new List<DotShapeData>();
    private int lastGenShapeIndex = -1;

    // Live drawing line
    private GameObject liveLineGO;
    private RectTransform liveLineRT;
    private Image liveLineImg;

    // Glow line (wider, behind the main live line)
    private GameObject liveGlowGO;
    private RectTransform liveGlowRT;
    private Image liveGlowImg;

    // ══════════════════════════════════════════
    //  SHAPE DEFINITIONS
    //  Simple shapes with Hebrew names
    //  Points designed for landscape play area
    // ══════════════════════════════════════════

    private struct ShapeDef
    {
        public string name;
        public string animalId; // null for non-animal shapes
        public Vector2[] points;
        public Color color;

        public ShapeDef(string name, string animalId, Vector2[] points, Color color)
        {
            this.name = name;
            this.animalId = animalId;
            this.points = points;
            this.color = color;
        }
    }

    private static readonly ShapeDef[] Shapes = new ShapeDef[]
    {
        // ── SIMPLE SHAPES (Hebrew names) ──

        // כוכב — Star (10 dots)
        new ShapeDef("\u05DB\u05D5\u05DB\u05D1", null, new Vector2[] {
            new Vector2(0.50f, 0.88f),
            new Vector2(0.58f, 0.68f),
            new Vector2(0.80f, 0.65f),
            new Vector2(0.64f, 0.50f),
            new Vector2(0.70f, 0.25f),
            new Vector2(0.50f, 0.38f),
            new Vector2(0.30f, 0.25f),
            new Vector2(0.36f, 0.50f),
            new Vector2(0.20f, 0.65f),
            new Vector2(0.42f, 0.68f),
        }, new Color(1f, 0.84f, 0f)),

        // לב — Heart (10 dots)
        new ShapeDef("\u05DC\u05D1", null, new Vector2[] {
            new Vector2(0.50f, 0.28f),
            new Vector2(0.30f, 0.42f),
            new Vector2(0.20f, 0.58f),
            new Vector2(0.22f, 0.74f),
            new Vector2(0.35f, 0.84f),
            new Vector2(0.50f, 0.74f),
            new Vector2(0.65f, 0.84f),
            new Vector2(0.78f, 0.74f),
            new Vector2(0.80f, 0.58f),
            new Vector2(0.70f, 0.42f),
        }, new Color(0.96f, 0.30f, 0.45f)),

        // בית — House (8 dots)
        new ShapeDef("\u05D1\u05D9\u05EA", null, new Vector2[] {
            new Vector2(0.50f, 0.85f),
            new Vector2(0.75f, 0.58f),
            new Vector2(0.75f, 0.40f),
            new Vector2(0.75f, 0.22f),
            new Vector2(0.50f, 0.22f),
            new Vector2(0.25f, 0.22f),
            new Vector2(0.25f, 0.40f),
            new Vector2(0.25f, 0.58f),
        }, new Color(0.85f, 0.45f, 0.35f)),

        // עץ — Tree (8 dots — triangle crown + trunk)
        new ShapeDef("\u05E2\u05E5", null, new Vector2[] {
            new Vector2(0.50f, 0.88f),
            new Vector2(0.72f, 0.62f),
            new Vector2(0.62f, 0.62f),
            new Vector2(0.62f, 0.25f),
            new Vector2(0.38f, 0.25f),
            new Vector2(0.38f, 0.62f),
            new Vector2(0.28f, 0.62f),
        }, new Color(0.35f, 0.75f, 0.35f)),

        // בלון — Balloon (8 dots)
        new ShapeDef("\u05D1\u05DC\u05D5\u05DF", null, new Vector2[] {
            new Vector2(0.50f, 0.88f),
            new Vector2(0.68f, 0.78f),
            new Vector2(0.75f, 0.60f),
            new Vector2(0.68f, 0.42f),
            new Vector2(0.50f, 0.35f),
            new Vector2(0.32f, 0.42f),
            new Vector2(0.25f, 0.60f),
            new Vector2(0.32f, 0.78f),
        }, new Color(0.92f, 0.35f, 0.55f)),

        // פרח — Flower (6 dots)
        new ShapeDef("\u05E4\u05E8\u05D7", null, new Vector2[] {
            new Vector2(0.50f, 0.85f),
            new Vector2(0.72f, 0.70f),
            new Vector2(0.72f, 0.42f),
            new Vector2(0.50f, 0.25f),
            new Vector2(0.28f, 0.42f),
            new Vector2(0.28f, 0.70f),
        }, new Color(0.92f, 0.55f, 0.75f)),

        // משולש — Triangle (3 dots)
        new ShapeDef("\u05DE\u05E9\u05D5\u05DC\u05E9", null, new Vector2[] {
            new Vector2(0.50f, 0.85f),
            new Vector2(0.75f, 0.25f),
            new Vector2(0.25f, 0.25f),
        }, new Color(0.55f, 0.45f, 0.90f)),

        // ריבוע — Square (4 dots)
        new ShapeDef("\u05E8\u05D9\u05D1\u05D5\u05E2", null, new Vector2[] {
            new Vector2(0.30f, 0.78f),
            new Vector2(0.70f, 0.78f),
            new Vector2(0.70f, 0.28f),
            new Vector2(0.30f, 0.28f),
        }, new Color(0.3f, 0.75f, 0.95f)),
    };

    private ShapeDef currentShape;
    private Color currentLineColor;

    // Constellation line color — warm white/yellow
    private static readonly Color ConstellationColor = new Color(1f, 0.95f, 0.75f, 0.85f);
    private static readonly Color ConstellationGlow = new Color(1f, 0.92f, 0.6f, 0.2f);

    // Background star objects (persist across rounds)
    private List<GameObject> bgStars = new List<GameObject>();

    // ══════════════════════════════════════════
    //  BASE MINI GAME HOOKS
    // ══════════════════════════════════════════

    protected override string GetFallbackGameId() => "connectthedots";

    protected override void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        roundNumber = 0;

        // Apply difficulty: map 1-10 → 0-2 tier for shape filtering
        // Note: Difficulty is set by BaseMiniGame.Start(), so we read it in OnGameInit
        // But we need canvas before base.Start(), so set it here

        base.Start();
    }

    protected override void OnGameInit()
    {
        totalRounds = 1;
        isEndless = true;
        playWinSound = true;
        playConfettiOnRoundWin = true;
        delayBeforeNextRound = 0.5f;

        difficulty = GameDifficultyConfig.ConnectDotsTier(Difficulty);
        Debug.Log($"[Difficulty] Game=connectthedots Level={Difficulty} Tier={difficulty}");

        // Load all generated shapes, then filter by difficulty+complexity
        allGeneratedShapes = Resources.LoadAll<DotShapeData>("DotShapes");
        BuildEligibleShapes();

        if (shapeNameText != null)
        {
            shapeNameText.color = new Color(1, 1, 1, 0);
            shapeNameText.gameObject.SetActive(false);
        }

        if (revealImage != null)
        {
            revealImage.color = new Color(1, 1, 1, 0);
            revealImage.gameObject.SetActive(false);
        }

        // Create live drawing line
        liveLineGO = new GameObject("LiveLine");
        liveLineGO.transform.SetParent(lineContainer, false);
        liveLineRT = liveLineGO.AddComponent<RectTransform>();
        liveLineImg = liveLineGO.AddComponent<Image>();
        liveLineImg.raycastTarget = false;
        liveLineRT.pivot = new Vector2(0.5f, 0.5f);
        liveLineGO.SetActive(false);

        // Create glow line (wider, behind live line)
        liveGlowGO = new GameObject("LiveGlow");
        liveGlowGO.transform.SetParent(lineContainer, false);
        liveGlowGO.transform.SetAsFirstSibling();
        liveGlowRT = liveGlowGO.AddComponent<RectTransform>();
        liveGlowImg = liveGlowGO.AddComponent<Image>();
        liveGlowImg.raycastTarget = false;
        liveGlowRT.pivot = new Vector2(0.5f, 0.5f);
        liveGlowGO.SetActive(false);

        PlaceBackgroundStars();
    }

    protected override void OnRoundSetup()
    {
        // Prefer generated shapes if available; fall back to hardcoded
        Vector2[] layout;
        if (eligibleShapes.Count > 0)
        {
            var genShape = PickGeneratedShape();
            layout = genShape.GetPoints(difficulty);
            if (layout == null || layout.Length < 3)
                layout = genShape.easyPoints;

            currentShape = new ShapeDef(
                genShape.hebrewName,
                genShape.animalId,
                layout,
                genShape.lineColor);
        }
        else
        {
            currentShape = PickRandomShape();
            layout = currentShape.points;
        }

        currentLineColor = currentShape.color;
        totalDots = layout.Length;
        currentDotIndex = 0;
        isDrawing = false;
        roundComplete = false;

        if (shapeNameText != null)
        {
            shapeNameText.gameObject.SetActive(false);
            shapeNameText.color = new Color(1, 1, 1, 0);
        }

        if (revealImage != null)
        {
            revealImage.gameObject.SetActive(false);
            revealImage.color = new Color(1, 1, 1, 0);
        }

        if (liveLineGO != null) liveLineGO.SetActive(false);
        if (liveGlowGO != null) liveGlowGO.SetActive(false);

        float areaW = playArea.rect.width;
        float areaH = playArea.rect.height;

        // Use the smaller dimension for both axes to preserve aspect ratio
        // (prevents circles becoming ellipses in landscape)
        float uniformSize = Mathf.Min(areaW, areaH);

        // Scale dot size based on point count — fewer points = bigger dots
        currentDotSize = CalculateDotSize(totalDots);

        // Create dots
        for (int i = 0; i < totalDots; i++)
        {
            var dotGO = Instantiate(dotPrefab, playArea);
            var rt = dotGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(currentDotSize, currentDotSize);

            float x = (layout[i].x - 0.5f) * uniformSize;
            float y = (layout[i].y - 0.5f) * uniformSize;
            rt.anchoredPosition = new Vector2(x, y);

            var dot = dotGO.GetComponent<DotPoint>();
            dot.normalizedPosition = layout[i];
            dot.Init(i, i + 1, null);
            dots.Add(dot);
        }

        CreateGuideLines();
        dots[0].SetAsNext();

        // Position tutorial hand: show connecting dot 1 to dot 2
        if (TutorialHand != null && dots.Count >= 2)
        {
            var dot1RT = dots[0].GetComponent<RectTransform>();
            var dot2RT = dots[1].GetComponent<RectTransform>();

            Vector2 fromPos = TutorialHand.GetLocalCenter(dot1RT);
            Vector2 toPos = TutorialHand.GetLocalCenter(dot2RT);

            TutorialHand.SetMovePath(fromPos, toPos);
        }

        roundNumber++;
    }

    protected override void OnRoundCleanup()
    {
        foreach (var c in activeCoroutines) if (c != null) StopCoroutine(c);
        activeCoroutines.Clear();

        foreach (var dot in dots)
            if (dot != null) Destroy(dot.gameObject);
        dots.Clear();

        foreach (var line in drawnLines)
            if (line != null) Destroy(line);
        drawnLines.Clear();

        foreach (var line in guideLines)
            if (line != null) Destroy(line);
        guideLines.Clear();

        foreach (var sp in sparkParticles)
            if (sp != null) Destroy(sp);
        sparkParticles.Clear();

        currentDotIndex = 0;
        isDrawing = false;
        roundComplete = false;
    }

    protected override void OnBeforeComplete()
    {
        Stats?.SetCustom("totalDots", totalDots);
    }

    protected override IEnumerator OnAfterComplete()
    {
        // Play animal name audio if applicable
        if (!string.IsNullOrEmpty(currentShape.animalId))
            SoundLibrary.PlayAnimalName(currentShape.animalId);

        yield return new WaitForSeconds(0.4f);

        // Fade out guide lines
        float gf = 0f;
        while (gf < 0.3f)
        {
            gf += Time.deltaTime;
            float a = Mathf.Lerp(guideLineAlpha, 0f, gf / 0.3f);
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

        // Reveal animal sprite if this is an animal shape
        bool hasReveal = false;
        if (!string.IsNullOrEmpty(currentShape.animalId) && revealImage != null)
        {
            var animData = AnimalAnimData.Load(currentShape.animalId);
            Sprite revealSprite = null;

            if (animData != null && animData.idleFrames != null && animData.idleFrames.Length > 0)
                revealSprite = animData.idleFrames[0];
            if (revealSprite == null)
                revealSprite = Resources.Load<Sprite>($"AnimalSprites/{currentShape.animalId}");

            if (revealSprite != null)
            {
                hasReveal = true;
                revealImage.sprite = revealSprite;
                revealImage.preserveAspect = true;
                revealImage.gameObject.SetActive(true);

                float dur = 0.5f;
                float t = 0f;
                while (t < dur)
                {
                    t += Time.deltaTime;
                    float p = Mathf.Clamp01(t / dur);
                    revealImage.color = new Color(1, 1, 1, p);
                    float scale = 0.5f + 0.5f * p + 0.15f * Mathf.Sin(p * Mathf.PI);
                    revealImage.transform.localScale = Vector3.one * scale;
                    yield return null;
                }
                revealImage.transform.localScale = Vector3.one;

                // Play success animation
                var anim = revealImage.GetComponent<UISpriteAnimator>();
                if (anim == null) anim = revealImage.gameObject.AddComponent<UISpriteAnimator>();
                if (animData != null)
                {
                    anim.targetImage = revealImage;
                    anim.idleFrames = animData.idleFrames;
                    anim.successFrames = animData.successFrames;
                    anim.framesPerSecond = animData.successFps > 0 ? animData.successFps : 30f;
                    if (animData.successFrames != null && animData.successFrames.Length > 0)
                        anim.PlaySuccess();
                    else
                        anim.PlayIdle();
                }
            }
        }

        // Show shape name text (Hebrew)
        if (shapeNameText != null)
        {
            HebrewText.SetText(shapeNameText, currentShape.name);
            shapeNameText.color = new Color(
                currentShape.color.r, currentShape.color.g, currentShape.color.b, 0f);
            shapeNameText.gameObject.SetActive(true);

            float dur = 0.6f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                shapeNameText.color = new Color(
                    currentShape.color.r, currentShape.color.g, currentShape.color.b, p);
                float scale = 1f + 0.25f * Mathf.Sin(p * Mathf.PI);
                shapeNameText.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            shapeNameText.transform.localScale = Vector3.one;
        }

        yield return new WaitForSeconds(2.0f);

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
            if (hasReveal && revealImage != null)
                revealImage.color = new Color(1, 1, 1, alpha);
            yield return null;
        }

        // Clean up reveal
        if (revealImage != null)
        {
            var anim = revealImage.GetComponent<UISpriteAnimator>();
            if (anim != null) Destroy(anim);
            revealImage.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(0.5f);
    }

    // ══════════════════════════════════════════
    //  SHAPE SELECTION
    // ══════════════════════════════════════════

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

    /// <summary>
    /// Build the list of shapes eligible for the current difficulty.
    /// Easy = Low complexity only. Medium = Low+Medium. Hard = all.
    /// </summary>
    private void BuildEligibleShapes()
    {
        eligibleShapes.Clear();
        lastGenShapeIndex = -1;

        if (allGeneratedShapes == null || allGeneratedShapes.Length == 0)
            return;

        foreach (var shape in allGeneratedShapes)
        {
            if (shape != null && shape.IsAllowedForDifficulty(difficulty))
            {
                var pts = shape.GetPoints(difficulty);
                if (pts != null && pts.Length >= 3)
                    eligibleShapes.Add(shape);
            }
        }

        if (eligibleShapes.Count > 0)
            Debug.Log($"ConnectTheDots: {eligibleShapes.Count} shapes eligible for difficulty {difficulty} " +
                      $"(of {allGeneratedShapes.Length} total)");
    }

    /// <summary>Pick an eligible shape, avoiding the last one played.</summary>
    private DotShapeData PickGeneratedShape()
    {
        int count = eligibleShapes.Count;
        int index;
        if (count == 1)
            index = 0;
        else
        {
            do { index = Random.Range(0, count); }
            while (index == lastGenShapeIndex);
        }
        lastGenShapeIndex = index;
        return eligibleShapes[index];
    }

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
            lineImg.color = new Color(1f, 0.95f, 0.8f, guideLineAlpha);
            lineImg.raycastTarget = false;

            PositionLine(lineRT, fromRT.anchoredPosition, toRT.anchoredPosition, lineWidth * 0.5f);

            guideLines.Add(lineGO);
        }
    }

    // ══════════════════════════════════════════
    //  BACKGROUND STARS (decorative twinkling)
    // ══════════════════════════════════════════

    private void PlaceBackgroundStars()
    {
        if (starLayer == null) return;

        // Get a circle sprite from the dot prefab for star visuals
        Sprite circleSprite = null;
        if (dotPrefab != null)
        {
            var dp = dotPrefab.GetComponent<DotPoint>();
            if (dp != null && dp.dotImage != null)
                circleSprite = dp.dotImage.sprite;
        }

        int count = Random.Range(80, 120);
        for (int i = 0; i < count; i++)
        {
            float cx = Random.Range(0.02f, 0.98f);
            float cy = Random.Range(0.08f, 0.98f);
            float sz = Random.Range(3f, 10f);

            // A few slightly larger stars
            if (Random.value < 0.1f) sz = Random.Range(10f, 16f);

            var go = new GameObject($"BgStar_{i}");
            go.transform.SetParent(starLayer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(cx, cy);
            rt.anchorMax = new Vector2(cx, cy);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(sz, sz);

            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            img.raycastTarget = false;

            // Warm white/yellow tint
            float warmth = Random.Range(0f, 1f);
            float baseAlpha = Random.Range(0.15f, 0.55f);
            if (sz > 10f) baseAlpha = Random.Range(0.4f, 0.7f);
            img.color = new Color(
                1f,
                0.92f + warmth * 0.08f,
                0.7f + warmth * 0.3f,
                baseAlpha);

            bgStars.Add(go);

            // Each star twinkles at its own pace
            activeCoroutines.Add(StartCoroutine(TwinkleStar(img, baseAlpha, i)));
        }
    }

    private IEnumerator TwinkleStar(Image img, float baseAlpha, int index)
    {
        if (img == null) yield break;
        float speed = Random.Range(0.3f, 0.8f);
        float range = baseAlpha * Random.Range(0.3f, 0.6f);
        float phase = index * 0.7f + Random.Range(0f, Mathf.PI * 2f);

        while (img != null)
        {
            float a = baseAlpha + Mathf.Sin(Time.time * speed * Mathf.PI * 2f + phase) * range;
            Color c = img.color;
            img.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
            yield return null;
        }
    }

    // ══════════════════════════════════════════
    //  UPDATE — input handling (UNCHANGED MECHANICS)
    // ══════════════════════════════════════════

    protected override void OnGameplayUpdate()
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
            if (isDrawing)
            {
                isDrawing = false;
                HideLiveLines();
            }
            return;
        }

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playArea, screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out localPoint);

        if (pointerUp)
        {
            // Before stopping, check if finger landed on the closing dot (dot 0)
            if (isDrawing && currentDotIndex >= totalDots)
            {
                float closingRadius = currentDotSize * hitRadius * 2f;
                var firstRT = dots[0].GetComponent<RectTransform>();
                float d = Vector2.Distance(localPoint, firstRT.anchoredPosition);
                if (d < closingRadius)
                {
                    DrawLine(dots[totalDots - 1], dots[0]);
                    dots[0].Activate();
                    roundComplete = true;
                    HideLiveLines();
                    isDrawing = false;
                    CompleteRound();
                    return;
                }
            }
            isDrawing = false;
            HideLiveLines();
            return;
        }

        if (!pointerDown) return;

        // Scale hit radius tighter when dots are dense to avoid skipping
        float scaledHitRadius = hitRadius * Mathf.Lerp(1f, 0.6f, Mathf.InverseLerp(8f, 30f, totalDots));
        float detectRadius = currentDotSize * scaledHitRadius;

        // Not yet drawing — must press on the current dot to start
        if (!isDrawing)
        {
            int targetIdx = (currentDotIndex < totalDots) ? currentDotIndex : 0;
            var startDot = dots[targetIdx];
            var startRT = startDot.GetComponent<RectTransform>();
            float dist = Vector2.Distance(localPoint, startRT.anchoredPosition);
            float radius = (currentDotIndex >= totalDots)
                ? currentDotSize * hitRadius * 2f  // generous for closing
                : detectRadius;

            if (dist < radius)
            {
                // If all dots done and touching dot 0 — close the shape
                if (currentDotIndex >= totalDots)
                {
                    DrawLine(dots[totalDots - 1], dots[0]);
                    dots[0].Activate();
                    roundComplete = true;
                    HideLiveLines();
                    CompleteRound();
                    return;
                }

                if (currentDotIndex == 0)
                {
                    ActivateDot(startDot);
                    DismissTutorial();
                }
                isDrawing = true;
            }
            // All dots done — allow dragging from anywhere to show live line to dot 0
            else if (currentDotIndex >= totalDots)
            {
                isDrawing = true;
            }
            return;
        }

        // Currently drawing — update live line from last dot to finger
        int lastActivated = Mathf.Min(currentDotIndex - 1, totalDots - 1);
        if (lastActivated >= 0)
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
        // All dots done — check if finger reached dot 0 to close (generous radius)
        else
        {
            var firstDot = dots[0];
            var firstRT = firstDot.GetComponent<RectTransform>();
            float dist = Vector2.Distance(localPoint, firstRT.anchoredPosition);
            float closingDetectRadius = currentDotSize * hitRadius * 2f;

            if (dist < closingDetectRadius)
            {
                DrawLine(dots[totalDots - 1], dots[0]);
                dots[0].Activate();
                isDrawing = false;
                roundComplete = true;
                HideLiveLines();
                CompleteRound();
            }
        }
    }

    private void HideLiveLines()
    {
        if (liveLineGO != null) liveLineGO.SetActive(false);
        if (liveGlowGO != null) liveGlowGO.SetActive(false);
    }

    private void UpdateLiveLine(Vector2 from, Vector2 to)
    {
        // Main line — warm constellation glow
        liveLineGO.SetActive(true);
        liveLineImg.color = new Color(ConstellationColor.r, ConstellationColor.g, ConstellationColor.b, 0.7f);
        PositionLine(liveLineRT, from, to, lineWidth);

        // Glow behind — soft warm halo
        liveGlowGO.SetActive(true);
        liveGlowImg.color = ConstellationGlow;
        PositionLine(liveGlowRT, from, to, lineWidth * 3.5f);
    }

    private void ActivateDot(DotPoint dot)
    {
        if (dot.dotIndex != currentDotIndex) return;

        dot.Activate();

        // Spark particles at dot position
        SpawnSparks(dot.GetComponent<RectTransform>().anchoredPosition, currentLineColor);

        // Draw permanent line from previous dot (with glow)
        if (currentDotIndex > 0)
            DrawLine(dots[currentDotIndex - 1], dot);

        currentDotIndex++;
        PlayCorrectEffect(dot.GetComponent<RectTransform>());
        RecordCorrect(isLast: currentDotIndex >= totalDots);

        if (currentDotIndex < totalDots)
        {
            dots[currentDotIndex].SetAsNext();
        }
        else
        {
            dots[0].SetAsNext();
        }
    }

    private void DrawLine(DotPoint from, DotPoint to)
    {
        Vector2 fromPos = from.GetComponent<RectTransform>().anchoredPosition;
        Vector2 toPos = to.GetComponent<RectTransform>().anchoredPosition;

        // Glow layer — warm golden halo
        var glowGO = new GameObject("LineGlow");
        glowGO.transform.SetParent(lineContainer, false);
        glowGO.transform.SetAsFirstSibling();
        var glowRT = glowGO.AddComponent<RectTransform>();
        var glowImg = glowGO.AddComponent<Image>();
        glowImg.color = ConstellationGlow;
        glowImg.raycastTarget = false;
        PositionLine(glowRT, fromPos, toPos, lineWidth * 3.5f);
        drawnLines.Add(glowGO);

        // Main line — constellation white/yellow
        var lineGO = new GameObject("DrawnLine");
        lineGO.transform.SetParent(lineContainer, false);
        var lineRT = lineGO.AddComponent<RectTransform>();
        var lineImg = lineGO.AddComponent<Image>();
        lineImg.color = ConstellationColor;
        lineImg.raycastTarget = false;
        PositionLine(lineRT, fromPos, toPos, lineWidth);
        drawnLines.Add(lineGO);
    }

    private void PositionLine(RectTransform rt, Vector2 from, Vector2 to, float width)
    {
        Vector2 dir = to - from;
        float distance = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rt.sizeDelta = new Vector2(distance, width);
        rt.anchoredPosition = from + dir * 0.5f;
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    // ══════════════════════════════════════════
    //  SPARK PARTICLES
    // ══════════════════════════════════════════

    private void SpawnSparks(Vector2 pos, Color color)
    {
        int count = Random.Range(5, 9);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Spark");
            go.transform.SetParent(playArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            float sz = Random.Range(5f, 12f);
            rt.sizeDelta = new Vector2(sz, sz);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;

            var circleSprite = dots.Count > 0 && dots[0].dotImage != null ? dots[0].dotImage.sprite : null;
            if (circleSprite != null) img.sprite = circleSprite;

            // Golden/warm white sparks like tiny stars
            float warmth = Random.Range(0.7f, 1f);
            img.color = new Color(1f, 0.9f * warmth, 0.6f * warmth, 1f);

            sparkParticles.Add(go);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(80f, 200f);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            activeCoroutines.Add(StartCoroutine(AnimateSpark(rt, img, vel)));
        }
    }

    private IEnumerator AnimateSpark(RectTransform rt, Image img, Vector2 velocity)
    {
        float life = Random.Range(0.25f, 0.45f);
        float t = 0f;
        Vector2 startPos = rt.anchoredPosition;

        while (t < life)
        {
            t += Time.deltaTime;
            float p = t / life;
            if (rt == null) yield break;

            rt.anchoredPosition = startPos + velocity * p;
            rt.localScale = Vector3.one * (1f - p * 0.6f);

            Color c = img.color;
            img.color = new Color(c.r, c.g, c.b, 1f - p);
            yield return null;
        }

        if (rt != null) Destroy(rt.gameObject);
    }

    // ══════════════════════════════════════════
    //  NAVIGATION
    // ══════════════════════════════════════════

    public void OnHomePressed() => ExitGame();
    public void OnRestartPressed()
    {
        OnRoundCleanup();
        OnRoundSetup();
    }

    /// <summary>
    /// Scale visual dot size based on point count.
    /// Few points → large dots (dotSize). Many points → smaller dots.
    /// Touch detection still uses the full dotSize via hitRadius.
    /// </summary>
    private float CalculateDotSize(int pointCount)
    {
        // 8 points or fewer → full size (80)
        // 30 points → 55% of base size (44)
        float t = Mathf.InverseLerp(8f, 30f, pointCount);
        return Mathf.Lerp(dotSize, dotSize * 0.55f, t);
    }

    private static Color HC(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
