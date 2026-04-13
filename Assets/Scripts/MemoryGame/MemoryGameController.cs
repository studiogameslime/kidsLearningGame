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
    private Image _turnIndicatorLeft;
    private Image _turnIndicatorRight;

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

        // Player 1 score (LEFT, BLUE)
        var s1GO = new GameObject("P1Score");
        s1GO.transform.SetParent(canvas.transform, false);
        var s1RT = s1GO.AddComponent<RectTransform>();
        s1RT.anchorMin = new Vector2(0, 0.88f); s1RT.anchorMax = new Vector2(0.12f, 1);
        s1RT.offsetMin = new Vector2(10, 0); s1RT.offsetMax = Vector2.zero;
        _p1ScoreTMP = s1GO.AddComponent<TMPro.TextMeshProUGUI>();
        _p1ScoreTMP.text = "0"; _p1ScoreTMP.fontSize = 44; _p1ScoreTMP.fontStyle = TMPro.FontStyles.Bold;
        _p1ScoreTMP.color = TwoPlayerManager.Player1Color;
        _p1ScoreTMP.alignment = TMPro.TextAlignmentOptions.Center;

        // Player 1 name
        var n1GO = new GameObject("P1Name");
        n1GO.transform.SetParent(canvas.transform, false);
        var n1RT = n1GO.AddComponent<RectTransform>();
        n1RT.anchorMin = new Vector2(0, 0.82f); n1RT.anchorMax = new Vector2(0.12f, 0.88f);
        n1RT.offsetMin = new Vector2(10, 0); n1RT.offsetMax = Vector2.zero;
        var n1TMP = n1GO.AddComponent<TMPro.TextMeshProUGUI>();
        HebrewText.SetText(n1TMP, TwoPlayerManager.GetName(1));
        n1TMP.fontSize = 18; n1TMP.color = TwoPlayerManager.Player1Color;
        n1TMP.alignment = TMPro.TextAlignmentOptions.Center;

        // Player 2 score (RIGHT, RED)
        var s2GO = new GameObject("P2Score");
        s2GO.transform.SetParent(canvas.transform, false);
        var s2RT = s2GO.AddComponent<RectTransform>();
        s2RT.anchorMin = new Vector2(0.88f, 0.88f); s2RT.anchorMax = new Vector2(1, 1);
        s2RT.offsetMin = Vector2.zero; s2RT.offsetMax = new Vector2(-10, 0);
        _p2ScoreTMP = s2GO.AddComponent<TMPro.TextMeshProUGUI>();
        _p2ScoreTMP.text = "0"; _p2ScoreTMP.fontSize = 44; _p2ScoreTMP.fontStyle = TMPro.FontStyles.Bold;
        _p2ScoreTMP.color = TwoPlayerManager.Player2Color;
        _p2ScoreTMP.alignment = TMPro.TextAlignmentOptions.Center;

        // Player 2 name
        var n2GO = new GameObject("P2Name");
        n2GO.transform.SetParent(canvas.transform, false);
        var n2RT = n2GO.AddComponent<RectTransform>();
        n2RT.anchorMin = new Vector2(0.88f, 0.82f); n2RT.anchorMax = new Vector2(1, 0.88f);
        n2RT.offsetMin = Vector2.zero; n2RT.offsetMax = new Vector2(-10, 0);
        var n2TMP = n2GO.AddComponent<TMPro.TextMeshProUGUI>();
        HebrewText.SetText(n2TMP, TwoPlayerManager.GetName(2));
        n2TMP.fontSize = 18; n2TMP.color = TwoPlayerManager.Player2Color;
        n2TMP.alignment = TMPro.TextAlignmentOptions.Center;

        // Turn indicators (colored bars at sides)
        var liGO = new GameObject("TurnLeft");
        liGO.transform.SetParent(canvas.transform, false);
        var liRT = liGO.AddComponent<RectTransform>();
        liRT.anchorMin = new Vector2(0, 0); liRT.anchorMax = new Vector2(0.025f, 1);
        liRT.offsetMin = Vector2.zero; liRT.offsetMax = Vector2.zero;
        _turnIndicatorLeft = liGO.AddComponent<Image>();
        _turnIndicatorLeft.color = TwoPlayerManager.Player1Color;

        var riGO = new GameObject("TurnRight");
        riGO.transform.SetParent(canvas.transform, false);
        var riRT = riGO.AddComponent<RectTransform>();
        riRT.anchorMin = new Vector2(0.975f, 0); riRT.anchorMax = Vector2.one;
        riRT.offsetMin = Vector2.zero; riRT.offsetMax = Vector2.zero;
        _turnIndicatorRight = riGO.AddComponent<Image>();
        _turnIndicatorRight.color = TwoPlayerManager.Player2Color;

        Update2PlayerUI(); // set initial turn indicator state
    }
    // Note: TwoPlayerManager.End() is called by BaseMiniGame.ExitGame() — no OnDestroy needed

    private void Update2PlayerUI()
    {
        if (_p1ScoreTMP != null) _p1ScoreTMP.text = TwoPlayerManager.Score1.ToString();
        if (_p2ScoreTMP != null) _p2ScoreTMP.text = TwoPlayerManager.Score2.ToString();

        // Turn indicator: active side bright, inactive dimmed
        bool isP1Turn = TwoPlayerManager.CurrentTurn == 1;
        if (_turnIndicatorLeft != null)
            _turnIndicatorLeft.color = isP1Turn
                ? TwoPlayerManager.Player1Color
                : new Color(TwoPlayerManager.Player1Color.r, TwoPlayerManager.Player1Color.g, TwoPlayerManager.Player1Color.b, 0.15f);
        if (_turnIndicatorRight != null)
            _turnIndicatorRight.color = !isP1Turn
                ? TwoPlayerManager.Player2Color
                : new Color(TwoPlayerManager.Player2Color.r, TwoPlayerManager.Player2Color.g, TwoPlayerManager.Player2Color.b, 0.15f);
    }
}
