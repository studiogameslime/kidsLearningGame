using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pizza-making mini-game. Child spreads sauce, sprinkles cheese, and places toppings.
/// Steps flow automatically: Sauce → Cheese → Toppings → Complete.
/// All interactions restricted to pizza inner circle (crust excluded).
/// </summary>
public class PizzaMakerController : BaseMiniGame
{
    [Header("Pizza")]
    public RectTransform pizzaArea;       // container for the pizza
    public Image pizzaBase;               // the dough circle
    public Image pizzaCrust;              // outer ring (non-interactive)
    public RectTransform pizzaInner;      // inner circle where sauce/cheese/toppings go
    public Sprite circleSprite;

    [Header("UI")]
    public RectTransform toolBar;         // bottom bar for current tool/toppings
    public Image currentToolIcon;         // large icon showing current step
    public TextMeshProUGUI stepLabel;     // optional step hint

    [Header("Reference Preview")]
    public RectTransform previewArea;     // small preview in corner

    [Header("Topping Sprites")]
    public Sprite[] toppingSingles;       // single pieces (Pizza extra) — 6 sprites
    public Sprite[] toppingGroups;        // group packages (Pizza extras packages) — 6 sprites

    // Topping names matching sprite order
    private static readonly string[] ToppingNames =
        { "green_olive", "black_olive", "mushroom", "corn", "onion", "tomato" };

    // ── State ──
    private enum Step { Sauce, Cheese, Toppings, Done }
    private Step _currentStep;
    private float _sauceCoverage;
    private float _cheeseCoverage;
    private List<GameObject> _sauceStrokes = new List<GameObject>();
    private List<GameObject> _cheeseStrokes = new List<GameObject>();
    private List<GameObject> _placedToppings = new List<GameObject>();
    private int _selectedTopping = -1;
    private bool _isDragging;
    private float _pizzaRadius;
    private float _innerRadius;
    private Vector2 _pizzaCenter;

    // Level config
    private int[] _requiredToppingIndices;  // which toppings needed for this level
    private int[] _requiredToppingCounts;   // how many of each
    private Dictionary<int, int> _placedCounts = new Dictionary<int, int>();
    private List<Button> _toppingButtons = new List<Button>();

    // Coverage tracking
    private const float MinCoverageToAdvance = 0.30f;
    private Button _nextStepButton;
    private const int CoverageGridSize = 16; // 16x16 grid for coverage detection
    private bool[,] _sauceGrid;
    private bool[,] _cheeseGrid;

    // ── BaseMiniGame Hooks ──

    protected override void OnGameInit()
    {
        totalRounds = 5;
        isEndless = false;
        playWinSound = true;
        playConfettiOnRoundWin = false;
        playConfettiOnSessionWin = true;
        delayBeforeNextRound = 2.0f;
        delayAfterFinalRound = 2.5f;
        contentCategory = "cooking";
    }

    protected override string GetFallbackGameId() => "pizzamaker";

    protected override void OnRoundSetup()
    {
        ClearPizza();
        _currentStep = Step.Sauce;
        _sauceCoverage = 0f;
        _cheeseCoverage = 0f;
        _selectedTopping = -1;
        _isDragging = false;
        _sauceGrid = new bool[CoverageGridSize, CoverageGridSize];
        _cheeseGrid = new bool[CoverageGridSize, CoverageGridSize];
        _placedCounts.Clear();

        // Calculate pizza geometry
        UpdatePizzaGeometry();

        // Configure level based on difficulty
        ConfigureLevel();

        // Build reference preview
        BuildPreview();

        // Show sauce tool
        ShowSauceStep();

        // Position tutorial hand at center of pizza
        PositionTutorialHand();
    }

    private void PositionTutorialHand()
    {
        if (TutorialHand == null || pizzaInner == null) return;

        // Show spreading motion across the pizza
        Vector2 center = TutorialHand.GetLocalCenter(pizzaInner);

        float radius = pizzaInner.rect.width * 0.25f;
        Vector2 from = center + new Vector2(-radius, radius);
        Vector2 to = center + new Vector2(radius, -radius);
        TutorialHand.SetMovePath(from, to, 1.2f);
    }

    protected override void OnGameplayUpdate()
    {
        if (_currentStep == Step.Done) return;

        // Lazy geometry update (rect may not be ready on first frame)
        if (_innerRadius <= 0 && pizzaInner != null)
            UpdatePizzaGeometry();

        // Unified input: touch or mouse
        Vector2 screenPos = Vector2.zero;
        bool pointerDown = false;
        bool pointerHeld = false;
        bool pointerUp = false;

        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            screenPos = touch.position;
            pointerDown = touch.phase == TouchPhase.Began;
            pointerHeld = touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
            pointerUp = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
        }
        else
        {
            screenPos = Input.mousePosition;
            pointerDown = Input.GetMouseButtonDown(0);
            pointerHeld = Input.GetMouseButton(0) && !pointerDown;
            pointerUp = Input.GetMouseButtonUp(0);
        }

        if (pointerDown)
        {
            DismissTutorial();

            if (_currentStep == Step.Toppings)
            {
                TryPlaceTopping(screenPos);
            }
            else
            {
                bool inside = IsInsidePizza(screenPos);
                if (inside)
                {
                    _isDragging = true;
                    ApplyBrush(screenPos);
                }
            }
        }
        else if (pointerHeld && _isDragging)
        {
            if (IsInsidePizza(screenPos))
                ApplyBrush(screenPos);
        }
        else if (pointerUp)
        {
            _isDragging = false;
        }
    }

    protected override void OnBeforeComplete()
    {
        Stats.SetCustom("sauceCoverage", _sauceCoverage);
        Stats.SetCustom("cheeseCoverage", _cheeseCoverage);
        Stats.SetCustom("toppingsPlaced", _placedToppings.Count);
    }

    protected override void OnRoundCleanup()
    {
        ClearPizza();
    }

    // ── Level Configuration ──

    private void ConfigureLevel()
    {
        // Difficulty determines topping variety and count
        if (Difficulty <= 2)
        {
            // Easy: 1 topping type, 3 pieces
            int t = Random.Range(0, 6);
            _requiredToppingIndices = new[] { t };
            _requiredToppingCounts = new[] { 3 };
        }
        else if (Difficulty <= 4)
        {
            // Medium: 2 topping types, 3 each
            var indices = PickRandom(6, 2);
            _requiredToppingIndices = indices;
            _requiredToppingCounts = new[] { 3, 3 };
        }
        else if (Difficulty <= 6)
        {
            // Medium-hard: 3 toppings, 3-4 each
            var indices = PickRandom(6, 3);
            _requiredToppingIndices = indices;
            _requiredToppingCounts = new[] { 3, 4, 3 };
        }
        else if (Difficulty <= 8)
        {
            // Hard: 4 toppings, 3-4 each
            var indices = PickRandom(6, 4);
            _requiredToppingIndices = indices;
            _requiredToppingCounts = new[] { 4, 3, 4, 3 };
        }
        else
        {
            // Very hard: 5 toppings
            var indices = PickRandom(6, 5);
            _requiredToppingIndices = indices;
            _requiredToppingCounts = new[] { 4, 4, 3, 4, 3 };
        }

        foreach (int idx in _requiredToppingIndices)
            _placedCounts[idx] = 0;
    }

    // ── Step Flow ──

    private void ShowSauceStep()
    {
        _currentStep = Step.Sauce;
        ClearToolBar();

        // Show sauce icon (red circle)
        var sauceGO = new GameObject("SauceIcon");
        sauceGO.transform.SetParent(toolBar, false);
        var sauceRT = sauceGO.AddComponent<RectTransform>();
        sauceRT.anchorMin = new Vector2(0.5f, 0.5f);
        sauceRT.anchorMax = new Vector2(0.5f, 0.5f);
        sauceRT.sizeDelta = new Vector2(120, 120);
        var sauceImg = sauceGO.AddComponent<Image>();
        if (circleSprite != null) sauceImg.sprite = circleSprite;
        sauceImg.color = new Color(0.85f, 0.15f, 0.08f); // tomato sauce red
        sauceImg.raycastTarget = false;

        if (stepLabel != null)
            HebrewText.SetText(stepLabel, "\u05DE\u05E8\u05D7\u05D5 \u05E8\u05D5\u05D8\u05D1!"); // !מרחו רוטב
    }

    private void ShowCheeseStep()
    {
        _currentStep = Step.Cheese;
        ClearToolBar();

        var cheeseGO = new GameObject("CheeseIcon");
        cheeseGO.transform.SetParent(toolBar, false);
        var cheeseRT = cheeseGO.AddComponent<RectTransform>();
        cheeseRT.anchorMin = new Vector2(0.5f, 0.5f);
        cheeseRT.anchorMax = new Vector2(0.5f, 0.5f);
        cheeseRT.sizeDelta = new Vector2(120, 120);
        var cheeseImg = cheeseGO.AddComponent<Image>();
        if (circleSprite != null) cheeseImg.sprite = circleSprite;
        cheeseImg.color = new Color(1f, 0.92f, 0.5f); // cheese yellow
        cheeseImg.raycastTarget = false;

        if (stepLabel != null)
            HebrewText.SetText(stepLabel, "\u05E4\u05D6\u05E8\u05D5 \u05D2\u05D1\u05D9\u05E0\u05D4!"); // !פזרו גבינה
    }

    private void ShowToppingsStep()
    {
        _currentStep = Step.Toppings;
        ClearToolBar();
        _toppingButtons.Clear();

        if (stepLabel != null)
            HebrewText.SetText(stepLabel, "\u05E9\u05D9\u05DE\u05D5 \u05EA\u05D5\u05E1\u05E4\u05D5\u05EA!"); // !שימו תוספות

        // Show ALL 6 toppings for free placement
        var gridGO = new GameObject("ToppingGrid");
        gridGO.transform.SetParent(toolBar, false);
        var gridRT = gridGO.AddComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0, 0);
        gridRT.anchorMax = new Vector2(0.85f, 1);
        gridRT.offsetMin = new Vector2(10, 5);
        gridRT.offsetMax = new Vector2(-10, -5);
        var gridLayout = gridGO.AddComponent<HorizontalLayoutGroup>();
        gridLayout.spacing = 10;
        gridLayout.childAlignment = TextAnchor.MiddleCenter;
        gridLayout.childForceExpandWidth = false;
        gridLayout.childForceExpandHeight = false;
        gridLayout.childControlWidth = true;
        gridLayout.childControlHeight = true;

        for (int i = 0; i < ToppingNames.Length; i++)
        {
            var btnGO = new GameObject($"Topping_{ToppingNames[i]}");
            btnGO.transform.SetParent(gridGO.transform, false);

            var le = btnGO.AddComponent<LayoutElement>();
            le.preferredWidth = 110;
            le.preferredHeight = 100;

            // Topping image (group sprite)
            var imgGO = new GameObject("Img");
            imgGO.transform.SetParent(btnGO.transform, false);
            var imgRT = imgGO.AddComponent<RectTransform>();
            imgRT.anchorMin = Vector2.zero;
            imgRT.anchorMax = Vector2.one;
            imgRT.offsetMin = new Vector2(8, 8);
            imgRT.offsetMax = new Vector2(-8, -8);
            var img = imgGO.AddComponent<Image>();
            if (toppingGroups != null && i < toppingGroups.Length)
                img.sprite = toppingGroups[i];
            img.preserveAspect = true;
            img.raycastTarget = false;

            // Button
            var bgImg = btnGO.AddComponent<Image>();
            bgImg.color = new Color(1, 1, 1, 0.01f);
            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = bgImg;
            int capturedIdx = i;
            btn.onClick.AddListener(() => SelectTopping(capturedIdx));
            _toppingButtons.Add(btn);
        }

        // Finish button (right side of toolbar)
        ShowNextStepButton(() =>
        {
            _currentStep = Step.Done;
            ClearToolBar();
            if (stepLabel != null) stepLabel.text = "";
            RecordCorrect("toppings_done");
            CompleteRound();
        });

        // Auto-select first topping
        SelectTopping(0);
    }

    private void SelectTopping(int toppingIndex)
    {
        _selectedTopping = toppingIndex;

        // Highlight selected button
        for (int i = 0; i < _toppingButtons.Count; i++)
        {
            var img = _toppingButtons[i].GetComponent<Image>();
            img.color = (i == toppingIndex)
                ? new Color(1f, 0.95f, 0.7f, 0.8f)
                : new Color(1, 1, 1, 0.01f);
        }
    }

    // ── Brush / Placement ──

    private void ApplyBrush(Vector2 screenPos)
    {
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            pizzaInner, screenPos, GetCanvasCamera(), out localPos);

        if (_currentStep == Step.Sauce)
        {
            SpawnSauceBlob(localPos);
            UpdateCoverage(_sauceGrid, localPos, ref _sauceCoverage);
            // Show next button once minimum coverage reached
            if (_sauceCoverage >= MinCoverageToAdvance && _nextStepButton == null)
                ShowNextStepButton(() => { RecordCorrect("sauce_done"); ShowCheeseStep(); });
        }
        else if (_currentStep == Step.Cheese)
        {
            SpawnCheeseBlob(localPos);
            UpdateCoverage(_cheeseGrid, localPos, ref _cheeseCoverage);
            if (_cheeseCoverage >= MinCoverageToAdvance && _nextStepButton == null)
                ShowNextStepButton(() => { RecordCorrect("cheese_done"); ShowToppingsStep(); });
        }
    }

    private void SpawnSauceBlob(Vector2 localPos)
    {
        var go = new GameObject("Sauce");
        go.transform.SetParent(pizzaInner, false);
        var rt = go.AddComponent<RectTransform>();
        float size = Random.Range(45f, 70f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = localPos;
        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        float r = Random.Range(0.78f, 0.88f);
        img.color = new Color(r, 0.12f, 0.05f, 0.75f);
        img.raycastTarget = false;

        _sauceStrokes.Add(go);
    }

    private void SpawnCheeseBlob(Vector2 localPos)
    {
        // Scattered cheese effect: multiple small dots
        int dots = Random.Range(4, 8);
        for (int i = 0; i < dots; i++)
        {
            var go = new GameObject("Cheese");
            go.transform.SetParent(pizzaInner, false);
            var rt = go.AddComponent<RectTransform>();
            float size = Random.Range(8f, 18f);
            rt.sizeDelta = new Vector2(size, size);
            Vector2 offset = new Vector2(Random.Range(-25f, 25f), Random.Range(-25f, 25f));
            rt.anchoredPosition = localPos + offset;

            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            float y = Random.Range(0.88f, 1f);
            img.color = new Color(y, y * 0.92f, y * 0.5f, 0.85f);
            img.raycastTarget = false;

            _cheeseStrokes.Add(go);
        }
    }

    private void TryPlaceTopping(Vector2 screenPos)
    {
        if (_selectedTopping < 0 || _selectedTopping >= ToppingNames.Length) return;
        if (!IsInsidePizza(screenPos)) return;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            pizzaInner, screenPos, GetCanvasCamera(), out localPos);

        // Free placement — no count limits
        var go = new GameObject($"Topping_{ToppingNames[_selectedTopping]}");
        go.transform.SetParent(pizzaInner, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(65, 65);
        rt.anchoredPosition = localPos;
        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-30f, 30f));
        float scale = Random.Range(0.85f, 1.15f);

        var img = go.AddComponent<Image>();
        if (toppingSingles != null && _selectedTopping < toppingSingles.Length)
            img.sprite = toppingSingles[_selectedTopping];
        img.preserveAspect = true;
        img.raycastTarget = false;

        _placedToppings.Add(go);
        RecordCorrect("topping_placed", ToppingNames[_selectedTopping]);

        // Pop-in animation
        StartCoroutine(PopIn(rt, scale));
    }

    private IEnumerator PopIn(RectTransform rt, float targetScale)
    {
        float dur = 0.2f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float s = Mathf.Lerp(0f, targetScale * 1.2f, p);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        // Settle
        t = 0f;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.1f);
            float s = Mathf.Lerp(targetScale * 1.2f, targetScale, p);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one * targetScale;
    }

    // ── Coverage Tracking ──

    private void UpdateCoverage(bool[,] grid, Vector2 localPos, ref float coverage)
    {
        // Map local position to grid cell
        float halfSize = _innerRadius;
        int gx = Mathf.Clamp(Mathf.FloorToInt((localPos.x + halfSize) / (halfSize * 2) * CoverageGridSize), 0, CoverageGridSize - 1);
        int gy = Mathf.Clamp(Mathf.FloorToInt((localPos.y + halfSize) / (halfSize * 2) * CoverageGridSize), 0, CoverageGridSize - 1);

        // Mark surrounding cells (brush size)
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            int nx = gx + dx, ny = gy + dy;
            if (nx >= 0 && nx < CoverageGridSize && ny >= 0 && ny < CoverageGridSize)
            {
                // Only count cells inside the circular pizza area
                float cx = (nx + 0.5f) / CoverageGridSize * 2f - 1f;
                float cy = (ny + 0.5f) / CoverageGridSize * 2f - 1f;
                if (cx * cx + cy * cy <= 1f)
                    grid[nx, ny] = true;
            }
        }

        // Recalculate coverage
        int filled = 0, total = 0;
        for (int x = 0; x < CoverageGridSize; x++)
        for (int y = 0; y < CoverageGridSize; y++)
        {
            float cx = (x + 0.5f) / CoverageGridSize * 2f - 1f;
            float cy = (y + 0.5f) / CoverageGridSize * 2f - 1f;
            if (cx * cx + cy * cy <= 1f)
            {
                total++;
                if (grid[x, y]) filled++;
            }
        }
        coverage = total > 0 ? (float)filled / total : 0f;
    }

    // ── Geometry ──

    private void UpdatePizzaGeometry()
    {
        if (pizzaInner == null) return;
        // Use rect (local space) for coverage grid — more reliable than world corners on first frame
        float w = pizzaInner.rect.width;
        float h = pizzaInner.rect.height;
        _innerRadius = Mathf.Min(w, h) * 0.5f;
        if (_innerRadius <= 0) _innerRadius = 200f; // fallback
    }

    private Canvas _canvas;

    private Camera GetCanvasCamera()
    {
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null) return null;
        return _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
    }

    private bool IsInsidePizza(Vector2 screenPos)
    {
        if (pizzaInner == null) return false;
        Vector2 localPos;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            pizzaInner, screenPos, GetCanvasCamera(), out localPos))
            return false;

        float w = pizzaInner.rect.width;
        float h = pizzaInner.rect.height;
        if (w <= 0 || h <= 0) return false;
        float halfSize = Mathf.Min(w, h) * 0.5f;
        return (localPos.x * localPos.x + localPos.y * localPos.y) <= halfSize * halfSize;
    }

    // ── Navigation ──

    public void OnHomePressed() => ExitGame();

    // ── Helpers ──

    private int GetNeededCount(int toppingIdx)
    {
        for (int i = 0; i < _requiredToppingIndices.Length; i++)
            if (_requiredToppingIndices[i] == toppingIdx)
                return _requiredToppingCounts[i];
        return 0;
    }

    private bool AllToppingsPlaced()
    {
        for (int i = 0; i < _requiredToppingIndices.Length; i++)
        {
            int idx = _requiredToppingIndices[i];
            int needed = _requiredToppingCounts[i];
            if (!_placedCounts.ContainsKey(idx) || _placedCounts[idx] < needed)
                return false;
        }
        return true;
    }

    private void UpdateToppingCounts()
    {
        for (int i = 0; i < _requiredToppingIndices.Length; i++)
        {
            if (i >= _toppingButtons.Count) break;
            int idx = _requiredToppingIndices[i];
            int needed = _requiredToppingCounts[i];
            int placed = _placedCounts.ContainsKey(idx) ? _placedCounts[idx] : 0;
            int remaining = Mathf.Max(0, needed - placed);

            var countTMP = _toppingButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (countTMP != null)
            {
                countTMP.text = remaining > 0 ? $"{remaining}" : "\u2714"; // ✔
                countTMP.color = remaining > 0 ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.2f, 0.75f, 0.2f);
            }

            // Grey out completed toppings
            if (remaining <= 0)
            {
                var btnImg = _toppingButtons[i].GetComponent<Image>();
                btnImg.color = new Color(0.7f, 0.7f, 0.7f, 0.3f);
                _toppingButtons[i].interactable = false;
            }
        }

        // Auto-select next needed topping
        if (_placedCounts.ContainsKey(_selectedTopping) &&
            _placedCounts[_selectedTopping] >= GetNeededCount(_selectedTopping))
        {
            for (int i = 0; i < _requiredToppingIndices.Length; i++)
            {
                int idx = _requiredToppingIndices[i];
                int needed = _requiredToppingCounts[i];
                if (!_placedCounts.ContainsKey(idx) || _placedCounts[idx] < needed)
                {
                    SelectTopping(idx);
                    break;
                }
            }
        }
    }

    private void BuildPreview()
    {
        if (previewArea == null) return;

        // Clear previous preview
        for (int i = previewArea.childCount - 1; i >= 0; i--)
            Destroy(previewArea.GetChild(i).gameObject);

        // Mini pizza base
        var baseGO = new GameObject("PreviewBase");
        baseGO.transform.SetParent(previewArea, false);
        var baseRT = baseGO.AddComponent<RectTransform>();
        baseRT.anchorMin = Vector2.zero;
        baseRT.anchorMax = Vector2.one;
        baseRT.offsetMin = new Vector2(8, 8);
        baseRT.offsetMax = new Vector2(-8, -8);
        var baseImg = baseGO.AddComponent<Image>();
        if (circleSprite != null) baseImg.sprite = circleSprite;
        baseImg.color = new Color(0.92f, 0.82f, 0.55f); // dough
        baseImg.raycastTarget = false;

        // Sauce layer
        var sauceGO = new GameObject("PreviewSauce");
        sauceGO.transform.SetParent(baseGO.transform, false);
        var sauceRT = sauceGO.AddComponent<RectTransform>();
        sauceRT.anchorMin = new Vector2(0.12f, 0.12f);
        sauceRT.anchorMax = new Vector2(0.88f, 0.88f);
        sauceRT.offsetMin = Vector2.zero;
        sauceRT.offsetMax = Vector2.zero;
        var sauceImg = sauceGO.AddComponent<Image>();
        if (circleSprite != null) sauceImg.sprite = circleSprite;
        sauceImg.color = new Color(0.82f, 0.15f, 0.08f, 0.8f);
        sauceImg.raycastTarget = false;

        // Cheese dots
        var cheeseGO = new GameObject("PreviewCheese");
        cheeseGO.transform.SetParent(baseGO.transform, false);
        var cheeseRT = cheeseGO.AddComponent<RectTransform>();
        cheeseRT.anchorMin = new Vector2(0.15f, 0.15f);
        cheeseRT.anchorMax = new Vector2(0.85f, 0.85f);
        cheeseRT.offsetMin = Vector2.zero;
        cheeseRT.offsetMax = Vector2.zero;
        var cheeseImg = cheeseGO.AddComponent<Image>();
        if (circleSprite != null) cheeseImg.sprite = circleSprite;
        cheeseImg.color = new Color(1f, 0.92f, 0.5f, 0.6f);
        cheeseImg.raycastTarget = false;

        // Preview toppings
        float previewSize = baseRT.rect.width > 0 ? baseRT.rect.width : 120f;
        for (int i = 0; i < _requiredToppingIndices.Length; i++)
        {
            int tIdx = _requiredToppingIndices[i];
            int count = Mathf.Min(_requiredToppingCounts[i], 3); // max 3 in preview
            for (int j = 0; j < count; j++)
            {
                float angle = (i * 137.5f + j * 60f) * Mathf.Deg2Rad; // golden angle spread
                float r = 0.25f + j * 0.12f;
                var tGO = new GameObject($"PT_{i}_{j}");
                tGO.transform.SetParent(baseGO.transform, false);
                var tRT = tGO.AddComponent<RectTransform>();
                tRT.anchorMin = new Vector2(0.5f + r * Mathf.Cos(angle) * 0.5f - 0.08f,
                                            0.5f + r * Mathf.Sin(angle) * 0.5f - 0.08f);
                tRT.anchorMax = new Vector2(tRT.anchorMin.x + 0.16f, tRT.anchorMin.y + 0.16f);
                tRT.offsetMin = Vector2.zero;
                tRT.offsetMax = Vector2.zero;
                var tImg = tGO.AddComponent<Image>();
                if (toppingSingles != null && tIdx < toppingSingles.Length)
                    tImg.sprite = toppingSingles[tIdx];
                tImg.preserveAspect = true;
                tImg.raycastTarget = false;
            }
        }
    }

    private void ClearPizza()
    {
        foreach (var go in _sauceStrokes) if (go != null) Destroy(go);
        _sauceStrokes.Clear();
        foreach (var go in _cheeseStrokes) if (go != null) Destroy(go);
        _cheeseStrokes.Clear();
        foreach (var go in _placedToppings) if (go != null) Destroy(go);
        _placedToppings.Clear();
    }

    private void ShowNextStepButton(UnityEngine.Events.UnityAction onClick)
    {
        if (_nextStepButton != null) return;

        // Big green checkmark button
        var btnGO = new GameObject("NextStepButton");
        btnGO.transform.SetParent(toolBar, false);
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(1, 0.5f);
        btnRT.anchorMax = new Vector2(1, 0.5f);
        btnRT.pivot = new Vector2(1, 0.5f);
        btnRT.anchoredPosition = new Vector2(-16, 0);
        btnRT.sizeDelta = new Vector2(90, 90);

        var btnImg = btnGO.AddComponent<Image>();
        if (circleSprite != null) btnImg.sprite = circleSprite;
        btnImg.color = new Color(0.2f, 0.78f, 0.35f); // green
        btnImg.raycastTarget = true;

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(() =>
        {
            _nextStepButton = null;
            onClick.Invoke();
        });

        // Checkmark text
        var checkGO = new GameObject("Check");
        checkGO.transform.SetParent(btnGO.transform, false);
        var checkRT = checkGO.AddComponent<RectTransform>();
        checkRT.anchorMin = Vector2.zero;
        checkRT.anchorMax = Vector2.one;
        checkRT.offsetMin = Vector2.zero;
        checkRT.offsetMax = Vector2.zero;
        var checkTMP = checkGO.AddComponent<TextMeshProUGUI>();
        checkTMP.text = "\u2714"; // ✔
        checkTMP.fontSize = 48;
        checkTMP.color = Color.white;
        checkTMP.alignment = TextAlignmentOptions.Center;
        checkTMP.raycastTarget = false;

        _nextStepButton = btn;

        // Pulse animation
        StartCoroutine(PulseButton(btnRT));
    }

    private IEnumerator PulseButton(RectTransform rt)
    {
        while (rt != null)
        {
            float t = Time.time * 2f;
            float s = 1f + Mathf.Sin(t) * 0.08f;
            rt.localScale = Vector3.one * s;
            yield return null;
        }
    }

    private void ClearToolBar()
    {
        _nextStepButton = null;
        if (toolBar == null) return;
        for (int i = toolBar.childCount - 1; i >= 0; i--)
        {
            var child = toolBar.GetChild(i);
            if (child.name != "StepLabel") // keep the label
                Destroy(child.gameObject);
        }
        _toppingButtons.Clear();
    }

    private int[] PickRandom(int max, int count)
    {
        var pool = new List<int>();
        for (int i = 0; i < max; i++) pool.Add(i);
        var result = new int[count];
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, pool.Count);
            result[i] = pool[idx];
            pool.RemoveAt(idx);
        }
        return result;
    }
}
