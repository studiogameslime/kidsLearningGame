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

    protected override void OnGameInit()
    {
        totalRounds = 1;
        contentCategory = SessionContent.Animals;
        playConfettiOnSessionWin = true;

        activeCategory = FindCategory();
        if (activeCategory == null)
            Debug.LogError("MemoryGameController: No matching category found!");
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
            matchedPairs++;
            Stats?.SetCustom("pairsMatched", matchedPairs);
            Stats?.SetCustom("pairsTotal", totalPairs);

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
            yield return new WaitForSeconds(mismatchDelay);
            firstFlipped.FlipToBack();
            secondFlipped.FlipToBack();
            yield return new WaitForSeconds(0.3f);
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
}
