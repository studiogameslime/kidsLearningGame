using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the memory game: spawns cards in a dynamic grid, handles matching logic.
/// Landscape layout with difficulty-based grid configurations.
///
/// Difficulty: 0=Easy (4×2), 1=Medium (4×3), 2=Hard (4×4)
/// Cards maintain 3:4 portrait aspect ratio.
/// </summary>
public class MemoryGameController : BaseMiniGame
{
    [Header("Data")]
    public List<MemoryCategoryData> categories;

    [Header("UI References")]
    public RectTransform boardArea;       // the board panel containing cards
    public Transform cardContainer;       // parent for spawned cards (inside board)
    public MemoryCard cardPrefab;

    [Header("Settings")]
    [Tooltip("Small random rotation per card in degrees.")]
    public float cardRotationRange = 3f;
    public float mismatchDelay = 0.8f;

    // Card aspect ratio (width:height = 3:4)
    private const float CardAspect = 0.75f; // 3/4

    // Runtime state
    private MemoryCategoryData activeCategory;
    private List<MemoryCard> allCards = new List<MemoryCard>();
    private MemoryCard firstFlipped;
    private MemoryCard secondFlipped;
    private bool isProcessing;
    private int matchedPairs;
    private int totalPairs;

    // ── BaseMiniGame Overrides ──

    protected override string GetFallbackGameId() => "memory";

    protected override string GetContentId()
    {
        return activeCategory != null ? activeCategory.categoryKey : "animals";
    }

    // 2-Player UI
    private TMPro.TextMeshProUGUI _p1ScoreTMP;
    private TMPro.TextMeshProUGUI _p2ScoreTMP;
    private TMPro.TextMeshProUGUI _turnLabelTMP;
    private List<Image> _woodPlankImages;
    private List<Color> _originalPlankColors;

    protected override void OnGameInit()
    {
        totalRounds = 1;
        contentCategory = SessionContent.Animals;
        playConfettiOnSessionWin = true;

        activeCategory = FindCategory();
        if (activeCategory == null)
            Debug.LogError("MemoryGameController: No matching category found!");

        if (TwoPlayerManager.IsActive)
            Setup2PlayerUI();
    }

    protected override void OnRoundSetup()
    {
        if (activeCategory == null) return;

        matchedPairs = 0;
        firstFlipped = null;
        secondFlipped = null;
        isProcessing = false;

        // Clear existing cards
        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);
        allCards.Clear();

        int cols, rows, pairs;
        GetGridConfig(out cols, out rows, out pairs);
        totalPairs = Mathf.Min(pairs, activeCategory.cardFaces.Count);
        Debug.Log($"[Difficulty] Game=memory Level={Difficulty} Cards={totalPairs * 2} Pairs={totalPairs} Grid={cols}x{rows}");

        // Configure grid layout
        ConfigureGrid(cols, rows);

        // Pick random faces
        List<Sprite> available = new List<Sprite>(activeCategory.cardFaces);
        ShuffleList(available);
        List<Sprite> selected = available.GetRange(0, totalPairs);

        // Create pair entries
        List<CardEntry> entries = new List<CardEntry>();
        for (int i = 0; i < totalPairs; i++)
        {
            entries.Add(new CardEntry { pairId = i, face = selected[i] });
            entries.Add(new CardEntry { pairId = i, face = selected[i] });
        }
        ShuffleList(entries);

        // Spawn cards with spacers for short rows (e.g. 6-8-6 in 8-col grid)
        int totalCards = entries.Count;
        int totalSlots = cols * rows;
        int emptySlots = totalSlots - totalCards;

        // Build a set of spacer positions: for short rows, put 1 spacer at start and 1 at end
        var spacerSlots = new HashSet<int>();
        if (emptySlots > 0 && rows >= 3)
        {
            int perShortRow = emptySlots / 2; // e.g. 4 empty / 2 short rows = 2 per row
            int half = perShortRow / 2;       // e.g. 1 at start, 1 at end

            // Row 0 (first row)
            for (int s = 0; s < half; s++)
                spacerSlots.Add(s);                          // start of row 0
            for (int s = 0; s < perShortRow - half; s++)
                spacerSlots.Add(cols - 1 - s);               // end of row 0

            // Last row
            int lastRowStart = cols * (rows - 1);
            for (int s = 0; s < half; s++)
                spacerSlots.Add(lastRowStart + s);           // start of last row
            for (int s = 0; s < perShortRow - half; s++)
                spacerSlots.Add(lastRowStart + cols - 1 - s); // end of last row
        }

        int cardIndex = 0;
        for (int slot = 0; slot < totalSlots; slot++)
        {
            if (spacerSlots.Contains(slot))
            {
                var spacer = new GameObject("Spacer");
                spacer.transform.SetParent(cardContainer);
                spacer.AddComponent<RectTransform>();
                spacer.AddComponent<LayoutElement>();
            }
            else if (cardIndex < entries.Count)
            {
                var card = Instantiate(cardPrefab, cardContainer);
                card.Setup(entries[cardIndex].pairId, entries[cardIndex].face, activeCategory.cardBack, OnCardClicked);
                card.SetRandomRotation(cardRotationRange);
                allCards.Add(card);
                cardIndex++;
            }
        }

        // Position tutorial hand on the first card
        StartCoroutine(PositionTutorialHandOnFirstCard());
    }

    private IEnumerator PositionTutorialHandOnFirstCard()
    {
        // Wait a frame for the grid layout to calculate positions
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (TutorialHand != null && allCards.Count > 0)
        {
            var firstCardRT = allCards[0].GetComponent<RectTransform>();
            Vector2 localPos = TutorialHand.GetLocalCenter(firstCardRT);
            TutorialHand.SetPosition(localPos);
        }
    }

    protected override void OnRoundCleanup()
    {
        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);
        allCards.Clear();
    }

    private MemoryCategoryData FindCategory()
    {
        string key = GameContext.CurrentSelection != null
            ? GameContext.CurrentSelection.categoryKey
            : "animals";

        foreach (var cat in categories)
            if (cat.categoryKey == key)
                return cat;

        return categories.Count > 0 ? categories[0] : null;
    }

    // ── GRID CONFIGURATION ──

    private void GetGridConfig(out int cols, out int rows, out int pairs)
    {
        GameDifficultyConfig.MemoryGridConfig(Difficulty, out cols, out rows, out pairs);
    }

    /// <summary>
    /// Dynamically calculate card size to fit the board with 3:4 aspect ratio.
    /// Cards fill available width first, then check height constraint.
    /// </summary>
    private void ConfigureGrid(int cols, int rows)
    {
        var grid = cardContainer.GetComponent<GridLayoutGroup>();
        if (grid == null || boardArea == null) return;

        // Force layout rebuild to get accurate rect
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(boardArea);

        float boardW = boardArea.rect.width;
        float boardH = boardArea.rect.height;

        // Tight spacing to maximize card area
        float spacing = cols <= 4 ? 12f : (cols <= 6 ? 10f : 8f);
        float padH = 8f;
        float padV = 8f;

        float availW = boardW - padH * 2f - (cols - 1) * spacing;
        float availH = boardH - padV * 2f - (rows - 1) * spacing;

        // Calculate cell size maintaining 3:4 ratio — width first
        float cellW = availW / cols;
        float cellH = cellW / CardAspect;

        // Check if height fits; if not, constrain by height
        if (cellH * rows > availH)
        {
            cellH = availH / rows;
            cellW = cellH * CardAspect;
        }

        grid.cellSize = new Vector2(cellW, cellH);
        grid.spacing = new Vector2(spacing, spacing);
        grid.padding = new RectOffset(
            Mathf.RoundToInt(padH), Mathf.RoundToInt(padH),
            Mathf.RoundToInt(padV), Mathf.RoundToInt(padV));
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;
    }

    // ── CARD INTERACTION ──

    private void OnCardClicked(MemoryCard card)
    {
        if (IsInputLocked || isProcessing || card.IsFaceUp || card.IsMatched) return;

        DismissTutorial();

        if (firstFlipped == null)
        {
            firstFlipped = card;
            card.FlipToFront();
            PlayCardAnimalName(card);
        }
        else if (secondFlipped == null && card != firstFlipped)
        {
            secondFlipped = card;
            PlayCardAnimalName(card);
            card.FlipToFront(() => StartCoroutine(CheckMatch()));
        }
    }

    private IEnumerator CheckMatch()
    {
        isProcessing = true;

        if (firstFlipped.PairId == secondFlipped.PairId)
        {
            Stats?.RecordCorrect("match");
            yield return new WaitForSeconds(0.2f);
            firstFlipped.IsMatched = true;
            secondFlipped.IsMatched = true;
            firstFlipped.PlayMatchAndHide();
            secondFlipped.PlayMatchAndHide();
            yield return new WaitForSeconds(0.3f);
            PlayCorrectEffect(firstFlipped.GetComponent<RectTransform>());
            PlayCorrectEffect(secondFlipped.GetComponent<RectTransform>());
            matchedPairs++;
            Stats?.SetCustom("pairsMatched", matchedPairs);
            Stats?.SetCustom("pairsTotal", totalPairs);

            // 2-Player: current player scores + keeps their turn
            if (TwoPlayerManager.IsActive)
            {
                if (TwoPlayerManager.CurrentTurn == 1) TwoPlayerManager.Score1++;
                else TwoPlayerManager.Score2++;
                Update2PlayerUI();
                // Player keeps turn on match (no SwitchTurn)
            }

            if (matchedPairs >= totalPairs)
            {
                yield return new WaitForSeconds(0.5f);
                CompleteRound();
            }
        }
        else
        {
            Stats?.RecordMistake("mismatch");
            Stats?.IncrementCustom("mismatchCount");
            PlayWrongEffect(firstFlipped.GetComponent<RectTransform>());
            PlayWrongEffect(secondFlipped.GetComponent<RectTransform>());
            yield return new WaitForSeconds(mismatchDelay);
            firstFlipped.FlipToBack();
            secondFlipped.FlipToBack();
            yield return new WaitForSeconds(0.3f);

            // 2-Player: switch turn on mismatch
            if (TwoPlayerManager.IsActive)
            {
                TwoPlayerManager.SwitchTurn();
                Update2PlayerUI();
            }
        }

        firstFlipped = null;
        secondFlipped = null;
        isProcessing = false;
    }

    private void PlayCardAnimalName(MemoryCard card)
    {
        if (card == null || string.IsNullOrEmpty(card.FaceSpriteName)) return;
        string name = card.FaceSpriteName;
        // Sprite names are like "CatMemorySprite" — strip suffix to get "Cat"
        if (name.EndsWith("MemorySprite"))
            name = name.Substring(0, name.Length - "MemorySprite".Length);
        if (name.Contains(" "))
            name = name.Substring(0, name.IndexOf(' '));
        SoundLibrary.PlayAnimalName(name);
    }

    public void OnExitPressed() => ExitGame();

    // ── Utility ──

    private struct CardEntry
    {
        public int pairId;
        public Sprite face;
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i]; list[i] = list[j]; list[j] = temp;
        }
    }

    // ── 2-Player UI ──

    private void Setup2PlayerUI()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // ── Scores in header: P1 (blue, left) — score — P2 (red, right) ──
        // P1 score + name (left of header)
        var s1GO = new GameObject("P1Score");
        s1GO.transform.SetParent(canvas.transform, false);
        var s1RT = s1GO.AddComponent<RectTransform>();
        s1RT.anchorMin = new Vector2(0.12f, 0.9f); s1RT.anchorMax = new Vector2(0.3f, 1);
        s1RT.offsetMin = Vector2.zero; s1RT.offsetMax = Vector2.zero;
        _p1ScoreTMP = s1GO.AddComponent<TMPro.TextMeshProUGUI>();
        _p1ScoreTMP.fontSize = 36; _p1ScoreTMP.fontStyle = TMPro.FontStyles.Bold;
        _p1ScoreTMP.color = TwoPlayerManager.Player1Color;
        _p1ScoreTMP.alignment = TMPro.TextAlignmentOptions.Center;

        // P2 score + name (right of header)
        var s2GO = new GameObject("P2Score");
        s2GO.transform.SetParent(canvas.transform, false);
        var s2RT = s2GO.AddComponent<RectTransform>();
        s2RT.anchorMin = new Vector2(0.7f, 0.9f); s2RT.anchorMax = new Vector2(0.88f, 1);
        s2RT.offsetMin = Vector2.zero; s2RT.offsetMax = Vector2.zero;
        _p2ScoreTMP = s2GO.AddComponent<TMPro.TextMeshProUGUI>();
        _p2ScoreTMP.fontSize = 36; _p2ScoreTMP.fontStyle = TMPro.FontStyles.Bold;
        _p2ScoreTMP.color = TwoPlayerManager.Player2Color;
        _p2ScoreTMP.alignment = TMPro.TextAlignmentOptions.Center;

        // ── "התור של X" — replace the header title text ──
        // Find existing header title and append turn info
        var headerTitle = canvas.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        // Store reference — we'll update the title text directly
        // Find the title that says "משחק זיכרון"
        foreach (var tmp in canvas.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
        {
            if (tmp.text.Contains("\u05D6\u05D9\u05DB\u05E8\u05D5\u05DF") || tmp.text.Contains("זיכרון"))
            {
                _turnLabelTMP = tmp;
                break;
            }
        }

        // ── Collect wood plank images to tint with player color ──
        _woodPlankImages = new List<Image>();
        if (boardArea != null)
        {
            // Find WoodSurface → its children are the planks
            var woodSurface = boardArea.parent != null
                ? boardArea.parent.Find("WoodSurface")
                : null;
            // boardArea might be GridContent, parent is BoardPanel
            if (woodSurface == null && boardArea.parent != null)
            {
                foreach (Transform child in boardArea.parent)
                {
                    if (child.name == "WoodSurface")
                    { woodSurface = child; break; }
                }
            }
            if (woodSurface != null)
            {
                foreach (Transform plank in woodSurface)
                {
                    var img = plank.GetComponent<Image>();
                    if (img != null) _woodPlankImages.Add(img);
                }
                // Also get the surface itself if it has an image
                var surfaceImg = woodSurface.GetComponent<Image>();
                if (surfaceImg != null) _woodPlankImages.Add(surfaceImg);
            }

            // Save original colors for blending
            _originalPlankColors = new List<Color>();
            foreach (var img in _woodPlankImages)
                _originalPlankColors.Add(img.color);
        }

        Update2PlayerUI();
    }

    private void Update2PlayerUI()
    {
        bool isP1Turn = TwoPlayerManager.CurrentTurn == 1;
        string currentName = TwoPlayerManager.GetName(isP1Turn ? 1 : 2);
        Color currentColor = TwoPlayerManager.GetColor(isP1Turn ? 1 : 2);

        // Scores: "Name: X"
        if (_p1ScoreTMP != null)
            HebrewText.SetText(_p1ScoreTMP, $"{TwoPlayerManager.GetName(1)}: {TwoPlayerManager.Score1}");
        if (_p2ScoreTMP != null)
            HebrewText.SetText(_p2ScoreTMP, $"{TwoPlayerManager.GetName(2)}: {TwoPlayerManager.Score2}");

        // Header title: "משחק זיכרון — התור של X"
        if (_turnLabelTMP != null)
            HebrewText.SetText(_turnLabelTMP,
                $"\u05DE\u05E9\u05D7\u05E7 \u05D6\u05D9\u05DB\u05E8\u05D5\u05DF - \u05D4\u05EA\u05D5\u05E8 \u05E9\u05DC {currentName}");
            // משחק זיכרון - התור של X

        // Tint wood planks with player color
        if (_woodPlankImages != null && _originalPlankColors != null)
        {
            for (int i = 0; i < _woodPlankImages.Count && i < _originalPlankColors.Count; i++)
            {
                if (_woodPlankImages[i] != null)
                    _woodPlankImages[i].color = Color.Lerp(_originalPlankColors[i], currentColor, 0.35f);
            }
        }
    }
}
