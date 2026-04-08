using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Size Sort game — tractor with 3 carts (large/medium/small).
/// Child drags 3 sizes of fruit into matching carts. 5 rounds.
/// Carts fill up visibly. Tractor drives off at end.
/// </summary>
public class SizeSortController : BaseMiniGame
{
    [Header("Layout")]
    public RectTransform gameplayArea;
    public RectTransform fruitSpawnArea;

    [Header("Tractor & Carts")]
    public RectTransform tractorRT;
    public SizeSortCart largeCart;
    public SizeSortCart mediumCart;
    public SizeSortCart smallCart;

    [Header("Background Layers (for parallax)")]
    public RectTransform hillsFarRT;
    public RectTransform hillsNearRT;
    public RectTransform grassBackRT;
    public RectTransform grassFrontRT;

    [Header("UI")]
    public TextMeshProUGUI titleText;

    // Loaded sprites
    private Sprite[] fruitSprites;
    private List<int> usedFruitIndices = new List<int>();
    private List<DraggableFruit> activeFruits = new List<DraggableFruit>();
    private int placedThisRound;
    private Sprite currentFruitSprite;
    private bool tractorEntered;

    // ── BaseMiniGame ──

    protected override void OnGameInit()
    {
        totalRounds = 3;
        isEndless = false;

        // Load fruit sprites
        fruitSprites = Resources.LoadAll<Sprite>("Tractor/Fruits");
    }

    protected override void OnRoundSetup()
    {
        placedThisRound = 0;
        ClearActiveFruits();

        if (!tractorEntered)
        {
            tractorEntered = true;
            StartCoroutine(TractorEntrance(() => SpawnFruits()));
        }
        else
        {
            SpawnFruits();
        }
    }

    protected override void OnRoundCleanup()
    {
        ClearActiveFruits();
    }

    protected override string GetFallbackGameId() => "sizesort";

    public void OnHomePressed() => ExitGame();

    // ── Fruit Spawning ──

    private void SpawnFruits()
    {
        if (fruitSprites == null || fruitSprites.Length == 0) return;

        // Pick a random fruit not used yet
        int fruitIndex;
        int attempts = 0;
        do
        {
            fruitIndex = Random.Range(0, fruitSprites.Length);
            attempts++;
        } while (usedFruitIndices.Contains(fruitIndex) && attempts < 50);

        usedFruitIndices.Add(fruitIndex);
        currentFruitSprite = fruitSprites[fruitIndex];

        // Get size scales from difficulty
        int difficulty = GameDifficultyConfig.GetLevel("sizesort");
        GameDifficultyConfig.SizeSortConfig(difficulty, out float smallScale, out float mediumScale);

        // Create 3 fruits: small, medium, large — shuffled positions
        var sizes = new List<int> { 0, 1, 2 }; // small, medium, large
        // Shuffle
        for (int i = sizes.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = sizes[i]; sizes[i] = sizes[j]; sizes[j] = tmp;
        }

        float[] scales = { smallScale, mediumScale, 1.0f };
        float baseSize = 260f;
        float spacing = 280f;
        float startX = -(sizes.Count - 1) * spacing / 2f;

        for (int i = 0; i < sizes.Count; i++)
        {
            int sizeCat = sizes[i];
            float scale = scales[sizeCat];

            var go = new GameObject($"Fruit_{sizeCat}");
            go.transform.SetParent(fruitSpawnArea, false);

            var rt = go.AddComponent<RectTransform>();
            float s = baseSize * scale;
            rt.sizeDelta = new Vector2(s, s);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(startX + i * spacing, 0);

            var img = go.AddComponent<Image>();
            img.sprite = currentFruitSprite;
            img.preserveAspect = true;
            img.raycastTarget = true;

            var fruit = go.AddComponent<DraggableFruit>();
            fruit.sizeCategory = sizeCat;
            fruit.controller = this;

            StartCoroutine(fruit.PopIn(i * 0.1f));
            activeFruits.Add(fruit);
        }
    }

    private void ClearActiveFruits()
    {
        foreach (var f in activeFruits)
            if (f != null) Destroy(f.gameObject);
        activeFruits.Clear();
    }

    // ── Drop Logic ──

    public void OnFruitDropped(DraggableFruit fruit)
    {
        // Check which cart the fruit was dropped on
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null,
            fruit.GetComponent<RectTransform>().position);

        SizeSortCart targetCart = null;
        SizeSortCart[] carts = { largeCart, mediumCart, smallCart };
        foreach (var cart in carts)
        {
            if (cart == null) continue;
            Rect zone = cart.GetDropZoneScreen();
            if (zone.Contains(screenPos))
            {
                targetCart = cart;
                break;
            }
        }

        if (targetCart != null && targetCart.sizeCategory == fruit.sizeCategory)
        {
            // Correct!
            bool isLastThisRound = (placedThisRound == 2);
            placedThisRound++;

            // Add fruit to cart visually
            float[] scales = { 0.5f, 0.75f, 1.0f };
            int diff = GameDifficultyConfig.GetLevel("sizesort");
            GameDifficultyConfig.SizeSortConfig(diff, out float ss, out float ms);
            scales[0] = ss; scales[1] = ms;

            targetCart.AddFruit(currentFruitSprite, scales[fruit.sizeCategory]);
            fruit.MarkPlaced();

            RecordCorrect("size", fruit.sizeCategory.ToString(), isLastThisRound);
            PlayCorrectEffect(targetCart.GetComponent<RectTransform>());

            if (placedThisRound >= 3)
            {
                // Round complete
                if (CurrentRound >= totalRounds - 1)
                {
                    // Last round — tractor exit
                    StartCoroutine(TractorExit());
                }
                else
                {
                    // World scroll then next round
                    StartCoroutine(WorldScrollThenNext());
                }
            }
        }
        else
        {
            // Wrong cart or dropped in empty space
            RecordMistake("size", fruit.sizeCategory.ToString());
            fruit.ReturnToHome();
        }
    }

    // ── Tractor Animations ──

    private IEnumerator TractorEntrance(System.Action onComplete)
    {
        if (gameplayArea == null) { onComplete?.Invoke(); yield break; }

        // Calculate endX so the whole convoy is centered on screen
        float convoyWidth = tractorRT.sizeDelta.x;
        float cartGap = -60f;
        if (largeCart != null) convoyWidth += largeCart.GetComponent<RectTransform>().sizeDelta.x + cartGap;
        if (mediumCart != null) convoyWidth += mediumCart.GetComponent<RectTransform>().sizeDelta.x + cartGap;
        if (smallCart != null) convoyWidth += smallCart.GetComponent<RectTransform>().sizeDelta.x + cartGap;
        float endX = -convoyWidth * 0.5f + tractorRT.sizeDelta.x * 0.5f;

        float startX = 1800f;
        tractorRT.anchoredPosition = new Vector2(startX, tractorRT.anchoredPosition.y);

        // Carts follow (they're children of gameplay area, positioned relative to tractor)
        float dur = 2f;
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / dur);
            float x = Mathf.Lerp(startX, endX, p);
            tractorRT.anchoredPosition = new Vector2(x, tractorRT.anchoredPosition.y);
            PositionCarts(x);
            yield return null;
        }

        tractorRT.anchoredPosition = new Vector2(endX, tractorRT.anchoredPosition.y);
        PositionCarts(endX);

        onComplete?.Invoke();
    }

    private IEnumerator WorldScrollThenNext()
    {
        yield return new WaitForSeconds(0.4f);

        // Calculate total convoy width so everything exits fully
        float totalWidth = tractorRT.sizeDelta.x;
        if (largeCart != null) totalWidth += largeCart.GetComponent<RectTransform>().sizeDelta.x;
        if (mediumCart != null) totalWidth += mediumCart.GetComponent<RectTransform>().sizeDelta.x;
        if (smallCart != null) totalWidth += smallCart.GetComponent<RectTransform>().sizeDelta.x;

        // Drive off to the left — far enough that the rightmost cart is fully off-screen
        float startX = tractorRT.anchoredPosition.x;
        float exitX = -(960f + totalWidth);
        float dur = 1.5f;
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float eased = p * p;
            float x = Mathf.Lerp(startX, exitX, eased);
            tractorRT.anchoredPosition = new Vector2(x, tractorRT.anchoredPosition.y);
            PositionCarts(x);
            yield return null;
        }

        // Brief pause off-screen
        yield return new WaitForSeconds(0.2f);

        // Advance round (spawns new fruits)
        CompleteRound();

        // Re-enter from the right — same centered position
        float enterX = 1800f;
        float cw2 = tractorRT.sizeDelta.x;
        float cg = -60f;
        if (largeCart != null) cw2 += largeCart.GetComponent<RectTransform>().sizeDelta.x + cg;
        if (mediumCart != null) cw2 += mediumCart.GetComponent<RectTransform>().sizeDelta.x + cg;
        if (smallCart != null) cw2 += smallCart.GetComponent<RectTransform>().sizeDelta.x + cg;
        float endX = -cw2 * 0.5f + tractorRT.sizeDelta.x * 0.5f;
        dur = 1.5f;
        t = 0f;
        tractorRT.anchoredPosition = new Vector2(enterX, tractorRT.anchoredPosition.y);
        PositionCarts(enterX);

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / dur);
            float x = Mathf.Lerp(enterX, endX, p);
            tractorRT.anchoredPosition = new Vector2(x, tractorRT.anchoredPosition.y);
            PositionCarts(x);
            yield return null;
        }

        tractorRT.anchoredPosition = new Vector2(endX, tractorRT.anchoredPosition.y);
        PositionCarts(endX);
    }

    private IEnumerator TractorExit()
    {
        yield return new WaitForSeconds(0.5f);

        // Confetti celebration
        if (ConfettiController.Instance != null)
            ConfettiController.Instance.PlayBig();

        yield return new WaitForSeconds(1.5f);

        // Drive off to the left — full convoy must exit
        float tw = tractorRT.sizeDelta.x;
        if (largeCart != null) tw += largeCart.GetComponent<RectTransform>().sizeDelta.x;
        if (mediumCart != null) tw += mediumCart.GetComponent<RectTransform>().sizeDelta.x;
        if (smallCart != null) tw += smallCart.GetComponent<RectTransform>().sizeDelta.x;
        float startX = tractorRT.anchoredPosition.x;
        float endX = -(960f + tw);
        float dur = 2.5f;
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float eased = p * p; // ease in (accelerate)
            float x = Mathf.Lerp(startX, endX, eased);
            tractorRT.anchoredPosition = new Vector2(x, tractorRT.anchoredPosition.y);
            PositionCarts(x);
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        // Reset carts (clear all fruits inside them)
        ResetCarts();
        usedFruitIndices.Clear();
        tractorEntered = false;

        // Start fresh — CompleteRound triggers OnRoundSetup which will re-enter
        CompleteRound();
    }

    private void ResetCarts()
    {
        SizeSortCart[] carts = { largeCart, mediumCart, smallCart };
        foreach (var cart in carts)
        {
            if (cart == null) continue;
            // Destroy all fruit children (they were added as first siblings)
            var children = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < cart.transform.childCount; i++)
            {
                var child = cart.transform.GetChild(i).gameObject;
                if (child.name.StartsWith("CartFruit_"))
                    children.Add(child);
            }
            foreach (var c in children)
                Destroy(c);
            cart.filledCount = 0;
        }
    }

    // ── Cart Positioning ──

    private void PositionCarts(float tractorX)
    {
        // Carts follow behind tractor (to the RIGHT of it)
        // Negative gap so they overlap and look physically connected
        float gap = -60f;
        float tractorW = tractorRT.sizeDelta.x;

        if (largeCart != null)
        {
            float lcW = largeCart.GetComponent<RectTransform>().sizeDelta.x;
            float lcX = tractorX + tractorW * 0.5f + lcW * 0.5f + gap;
            largeCart.GetComponent<RectTransform>().anchoredPosition =
                new Vector2(lcX, largeCart.GetComponent<RectTransform>().anchoredPosition.y);

            if (mediumCart != null)
            {
                float mcW = mediumCart.GetComponent<RectTransform>().sizeDelta.x;
                float mcX = lcX + lcW * 0.5f + mcW * 0.5f + gap;
                mediumCart.GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(mcX, mediumCart.GetComponent<RectTransform>().anchoredPosition.y);

                if (smallCart != null)
                {
                    float scW = smallCart.GetComponent<RectTransform>().sizeDelta.x;
                    float scX = mcX + mcW * 0.5f + scW * 0.5f + gap;
                    smallCart.GetComponent<RectTransform>().anchoredPosition =
                        new Vector2(scX, smallCart.GetComponent<RectTransform>().anchoredPosition.y);
                }
            }
        }
    }
}
