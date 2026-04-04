using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Color Sort game — 3 colored baskets at the bottom, colored items scattered above.
/// Child drags each item to the matching colored basket.
/// Desert-themed background.
/// </summary>
public class ColorSortController : BaseMiniGame
{
    [Header("Layout")]
    public RectTransform basketArea;
    public RectTransform itemArea;

    [Header("UI")]
    public TextMeshProUGUI titleText;

    // Color sets per difficulty tier (easy = very distinct, hard = similar/confusing)
    private static readonly Color[][] ColorSets = new Color[][]
    {
        // Tier 0 (difficulty 1-2): very distinct — red, blue, green
        new[] { HexColor("#EF5350"), HexColor("#42A5F5"), HexColor("#66BB6A") },
        // Tier 1 (difficulty 3-4): distinct — red, blue, yellow
        new[] { HexColor("#EF5350"), HexColor("#42A5F5"), HexColor("#FFEE58") },
        // Tier 2 (difficulty 5-6): moderate — orange, green, purple
        new[] { HexColor("#FFA726"), HexColor("#66BB6A"), HexColor("#AB47BC") },
        // Tier 3 (difficulty 7-8): similar warm — red, orange, yellow
        new[] { HexColor("#EF5350"), HexColor("#FFA726"), HexColor("#FFEE58") },
        // Tier 4 (difficulty 9-10): very confusing — red, pink, purple
        new[] { HexColor("#EF5350"), HexColor("#EC407A"), HexColor("#AB47BC") },
    };

    // Loaded sprites
    private Sprite[] itemSprites;
    private Sprite basketSprite;

    // Round state
    private List<ColorSortBasket> activeBaskets = new List<ColorSortBasket>();
    private List<ColorSortItem> activeItems = new List<ColorSortItem>();
    private int placedCount;
    private int totalItems;
    private Color[] roundColors;

    // ── BaseMiniGame ──

    protected override void OnGameInit()
    {
        isEndless = true;
        totalRounds = 1;

        // Load sprites
        itemSprites = Resources.LoadAll<Sprite>("ColorSort/item");
        var basketSprites = Resources.LoadAll<Sprite>("ColorSort/ColoredBasket");
        if (basketSprites != null && basketSprites.Length > 0)
            basketSprite = basketSprites[0];
    }

    protected override void OnRoundSetup()
    {
        ClearRound();
        placedCount = 0;

        int difficulty = GameDifficultyConfig.GetLevel("colorsort");
        int colorSetIndex = Mathf.Clamp((difficulty - 1) / 2, 0, ColorSets.Length - 1);
        roundColors = ColorSets[colorSetIndex];

        // Easy: 9 items (3 per color), Medium: 12 (4 per color), Hard: 20+ (7 per color)
        int itemsPerColor;
        if (difficulty <= 3)      itemsPerColor = 3;  // 3×3 = 9
        else if (difficulty <= 6) itemsPerColor = 4;  // 3×4 = 12
        else if (difficulty <= 8) itemsPerColor = 5;  // 3×5 = 15
        else                      itemsPerColor = 7;  // 3×7 = 21
        totalItems = roundColors.Length * itemsPerColor;

        CreateBaskets();
        CreateItems(itemsPerColor);
    }

    protected override void OnRoundCleanup()
    {
        ClearRound();
    }

    protected override string GetFallbackGameId() => "colorsort";

    public void OnHomePressed() => ExitGame();

    private void ClearRound()
    {
        foreach (var b in activeBaskets)
            if (b != null) Destroy(b.gameObject);
        activeBaskets.Clear();

        foreach (var item in activeItems)
            if (item != null) Destroy(item.gameObject);
        activeItems.Clear();
    }

    // ── Baskets ──

    private void CreateBaskets()
    {
        if (basketArea == null) return;

        int count = roundColors.Length;
        // Vertical column layout — evenly spaced top to bottom
        Rect bounds = basketArea.rect;
        float basketW = 400f;
        float basketH = 280f;
        float totalH = count * basketH;
        float vertSpacing = (bounds.height - totalH) / (count + 1);

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"Basket_{i}");
            go.transform.SetParent(basketArea, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(basketW, basketH);
            float y = -vertSpacing - i * (basketH + vertSpacing);
            rt.anchoredPosition = new Vector2(0, y);

            var img = go.AddComponent<Image>();
            if (basketSprite != null) img.sprite = basketSprite;
            // Lighten the basket color so items are visible inside
            Color lightened = Color.Lerp(roundColors[i], Color.white, 0.3f);
            img.color = lightened;
            img.preserveAspect = true;
            img.raycastTarget = false;

            var basket = go.AddComponent<ColorSortBasket>();
            basket.basketColor = roundColors[i];
            basket.colorIndex = i;

            StartCoroutine(basket.BounceIn(i * 0.1f));
            activeBaskets.Add(basket);
        }
    }

    // ── Items ──

    private void CreateItems(int itemsPerColor)
    {
        if (itemArea == null || itemSprites == null || itemSprites.Length == 0) return;

        Rect bounds = itemArea.rect;
        float margin = 110f;
        float minDist = 140f;

        var positions = new List<Vector2>();
        int idx = 0;

        for (int c = 0; c < roundColors.Length; c++)
        {
            for (int j = 0; j < itemsPerColor; j++)
            {
                // Pick random item type
                Sprite sprite = itemSprites[Random.Range(0, itemSprites.Length)];

                var go = new GameObject($"Item_{idx}");
                go.transform.SetParent(itemArea, false);

                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(200, 200);

                // Find scattered position
                Vector2 pos = FindScatterPosition(bounds, margin, minDist, positions);
                positions.Add(pos);
                rt.anchoredPosition = pos;
                rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-15f, 15f));

                var img = go.AddComponent<Image>();
                img.sprite = sprite;
                img.color = roundColors[c];
                img.preserveAspect = true;
                img.raycastTarget = true;

                var item = go.AddComponent<ColorSortItem>();
                item.colorIndex = c;
                item.itemColor = roundColors[c];
                item.itemSprite = sprite;
                item.controller = this;

                StartCoroutine(DelayedSetHome(item, pos, idx * 0.05f));
                StartCoroutine(item.PopIn(idx * 0.05f));
                activeItems.Add(item);
                idx++;
            }
        }
    }

    private IEnumerator DelayedSetHome(ColorSortItem item, Vector2 pos, float delay)
    {
        yield return new WaitForSeconds(delay + 0.3f);
        item.SetHome();
    }

    private Vector2 FindScatterPosition(Rect bounds, float margin, float minDist, List<Vector2> existing)
    {
        float xMin = bounds.xMin + margin;
        float xMax = bounds.xMax - margin;
        float yMin = bounds.yMin + margin;
        float yMax = bounds.yMax - margin;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            float x = Random.Range(xMin, xMax);
            float y = Random.Range(yMin, yMax);
            var pos = new Vector2(x, y);

            bool tooClose = false;
            foreach (var other in existing)
            {
                if (Vector2.Distance(pos, other) < minDist)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose) return pos;
        }

        // Fallback: just use a random position
        return new Vector2(Random.Range(xMin, xMax), Random.Range(yMin, yMax));
    }

    // ── Drop Logic ──

    public void OnItemDropped(ColorSortItem item)
    {
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null,
            item.GetComponent<RectTransform>().position);

        ColorSortBasket targetBasket = null;
        foreach (var basket in activeBaskets)
        {
            if (basket == null) continue;
            if (basket.GetDropZoneScreen().Contains(screenPos))
            {
                targetBasket = basket;
                break;
            }
        }

        if (targetBasket != null && targetBasket.colorIndex == item.colorIndex)
        {
            // Correct!
            placedCount++;
            bool isLast = placedCount >= totalItems;

            targetBasket.AddItem(item.itemSprite, item.itemColor);
            item.MarkPlaced();

            RecordCorrect("color", item.colorIndex.ToString(), isLast);
            PlayCorrectEffect(targetBasket.GetComponent<RectTransform>());

            if (isLast)
            {
                StartCoroutine(RoundComplete());
            }
        }
        else
        {
            RecordMistake("color", item.colorIndex.ToString());
            item.ReturnToHome();
        }
    }

    private IEnumerator RoundComplete()
    {
        yield return new WaitForSeconds(0.5f);
        CompleteRound();
    }

    // ── Helpers ──

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
