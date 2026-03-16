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
public class MemoryGameController : MonoBehaviour
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
    private GameStatsCollector _stats;
    private int _difficultyLevel = 1;

    private void Start()
    {
        activeCategory = FindCategory();
        if (activeCategory == null)
        {
            Debug.LogError("MemoryGameController: No matching category found!");
            return;
        }

        SetupGame();
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
        GameDifficultyConfig.MemoryGridConfig(_difficultyLevel, out cols, out rows, out pairs);
    }

    // ── GAME SETUP ──

    private void SetupGame()
    {
        matchedPairs = 0;
        firstFlipped = null;
        secondFlipped = null;
        isProcessing = false;

        // Apply difficulty
        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : "memory";
        _difficultyLevel = GameDifficultyConfig.GetLevel(gameId);

        // Start analytics collector
        _stats = new GameStatsCollector(gameId);
        if (GameCompletionBridge.Instance != null)
            GameCompletionBridge.Instance.ActiveCollector = _stats;

        // Clear existing cards
        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);
        allCards.Clear();

        int cols, rows, pairs;
        GetGridConfig(out cols, out rows, out pairs);
        totalPairs = Mathf.Min(pairs, activeCategory.cardFaces.Count);
        Debug.Log($"[Difficulty] Game=memory Level={_difficultyLevel} Cards={totalPairs * 2} Pairs={totalPairs} Grid={cols}x{rows}");

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

        // Spawn cards
        for (int i = 0; i < entries.Count; i++)
        {
            var card = Instantiate(cardPrefab, cardContainer);
            card.Setup(entries[i].pairId, entries[i].face, activeCategory.cardBack, OnCardClicked);
            card.SetRandomRotation(cardRotationRange);
            allCards.Add(card);
        }
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

        // Scale spacing with column count for balanced breathing room
        float spacing = cols <= 4 ? 20f : (cols <= 6 ? 18f : 14f);
        float padH = 20f;
        float padV = 16f;

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
        if (isProcessing || card.IsFaceUp || card.IsMatched) return;

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
            _stats?.RecordCorrect();
            yield return new WaitForSeconds(0.2f);
            firstFlipped.IsMatched = true;
            secondFlipped.IsMatched = true;
            firstFlipped.PlayMatchAndHide();
            secondFlipped.PlayMatchAndHide();
            matchedPairs++;
            _stats?.SetCustom("pairsMatched", matchedPairs);

            if (matchedPairs >= totalPairs)
            {
                _stats?.SetCustom("totalPairs", totalPairs);
                yield return new WaitForSeconds(0.5f);
                OnGameComplete();
            }
        }
        else
        {
            _stats?.RecordMistake();
            yield return new WaitForSeconds(mismatchDelay);
            firstFlipped.FlipToBack();
            secondFlipped.FlipToBack();
            yield return new WaitForSeconds(0.3f);
        }

        firstFlipped = null;
        secondFlipped = null;
        isProcessing = false;
    }

    private void OnGameComplete()
    {
        ConfettiController.Instance.Play();
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

    public void OnExitPressed() => NavigationManager.GoToMainMenu();

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
