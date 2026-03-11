using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the memory game: spawns cards, handles matching logic, and tracks score.
/// Attach to the Canvas root of the MemoryGame scene.
/// </summary>
public class MemoryGameController : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("All available memory categories. The controller picks the one matching GameContext.")]
    public List<MemoryCategoryData> categories;

    [Header("UI References")]
    public Transform cardContainer;      // parent for spawned cards
    public MemoryCard cardPrefab;        // the card prefab
    public TextMeshProUGUI matchCountText;  // shows matched pairs count
    public TextMeshProUGUI moveCountText;   // shows number of moves

    [Header("Grid Settings")]
    public int columns = 4;
    [Tooltip("Small random rotation per card in degrees (0 = no rotation).")]
    public float cardRotationRange = 4f;

    [Header("Timing")]
    [Tooltip("Seconds to wait before flipping unmatched cards back.")]
    public float mismatchDelay = 0.8f;

    // Runtime state
    private MemoryCategoryData activeCategory;
    private List<MemoryCard> allCards = new List<MemoryCard>();
    private MemoryCard firstFlipped;
    private MemoryCard secondFlipped;
    private bool isProcessing;  // true while waiting for mismatch delay
    private int matchedPairs;
    private int totalPairs;
    private int moveCount;

    private void Start()
    {
        // Determine which category to use from the navigation context
        activeCategory = FindCategory();

        if (activeCategory == null)
        {
            Debug.LogError("MemoryGameController: No matching category found!");
            return;
        }

        totalPairs = activeCategory.pairCount;
        SetupGame();
    }

    private MemoryCategoryData FindCategory()
    {
        // Try to match the categoryKey from GameContext
        string key = GameContext.CurrentSelection != null
            ? GameContext.CurrentSelection.categoryKey
            : "animals"; // default fallback

        foreach (var cat in categories)
        {
            if (cat.categoryKey == key)
                return cat;
        }

        // Fallback: return first category if no match
        return categories.Count > 0 ? categories[0] : null;
    }

    private void SetupGame()
    {
        // Reset state
        matchedPairs = 0;
        moveCount = 0;
        firstFlipped = null;
        secondFlipped = null;
        isProcessing = false;
        UpdateUI();

        // Clear existing cards
        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);
        allCards.Clear();

        // Pick random card faces
        List<Sprite> availableFaces = new List<Sprite>(activeCategory.cardFaces);
        int pairsToUse = Mathf.Min(totalPairs, availableFaces.Count);

        // Shuffle available faces and pick the first N
        ShuffleList(availableFaces);
        List<Sprite> selectedFaces = availableFaces.GetRange(0, pairsToUse);

        // Create pair entries (each face appears twice)
        List<CardEntry> entries = new List<CardEntry>();
        for (int i = 0; i < pairsToUse; i++)
        {
            entries.Add(new CardEntry { pairId = i, face = selectedFaces[i] });
            entries.Add(new CardEntry { pairId = i, face = selectedFaces[i] });
        }

        // Shuffle the entries for random placement
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

    private void OnCardClicked(MemoryCard card)
    {
        // Ignore taps while processing a mismatch or if card is already face-up
        if (isProcessing || card.IsFaceUp || card.IsMatched) return;

        if (firstFlipped == null)
        {
            // First card of the pair
            firstFlipped = card;
            card.FlipToFront();
        }
        else if (secondFlipped == null && card != firstFlipped)
        {
            // Second card of the pair
            secondFlipped = card;
            card.FlipToFront(() =>
            {
                // Check for match after flip animation completes
                StartCoroutine(CheckMatch());
            });
            moveCount++;
            UpdateUI();
        }
    }

    private IEnumerator CheckMatch()
    {
        isProcessing = true;

        if (firstFlipped.PairId == secondFlipped.PairId)
        {
            // Match found!
            yield return new WaitForSeconds(0.2f);

            firstFlipped.IsMatched = true;
            secondFlipped.IsMatched = true;
            firstFlipped.PlayMatchAndHide();
            secondFlipped.PlayMatchAndHide();

            matchedPairs++;
            UpdateUI();

            if (matchedPairs >= totalPairs)
            {
                yield return new WaitForSeconds(0.5f);
                OnGameComplete();
            }
        }
        else
        {
            // No match — wait, then flip both back
            yield return new WaitForSeconds(mismatchDelay);

            firstFlipped.FlipToBack();
            secondFlipped.FlipToBack();

            // Small delay for flip animation to finish
            yield return new WaitForSeconds(0.3f);
        }

        firstFlipped = null;
        secondFlipped = null;
        isProcessing = false;
    }

    private void OnGameComplete()
    {
        Debug.Log($"Game complete! Moves: {moveCount}");
        // TODO: show a celebration screen / animation
    }

    private void UpdateUI()
    {
        if (matchCountText != null)
            matchCountText.text = $"{matchedPairs}/{totalPairs}";
        if (moveCountText != null)
            moveCountText.text = $"{moveCount}";
    }

    /// <summary>Called by the Home/Exit button.</summary>
    public void OnExitPressed()
    {
        NavigationManager.GoToMainMenu();
    }

    /// <summary>Called by the Restart button.</summary>
    public void OnRestartPressed()
    {
        SetupGame();
    }

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
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
