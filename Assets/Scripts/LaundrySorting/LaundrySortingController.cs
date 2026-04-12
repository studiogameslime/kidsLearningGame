using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Laundry Sorting game — drag clothes into washing machine, fruits into basket.
/// Win when ALL items are sorted correctly.
/// </summary>
public class LaundrySortingController : BaseMiniGame
{
    [Header("References")]
    public RectTransform playArea;
    public RectTransform itemsArea;
    public Image washingMachineImage;
    public RectTransform washingMachineRT;
    public Image basketImage;
    public RectTransform basketRT;

    [Header("Sprites")]
    public Sprite[] clothesSprites;
    public Sprite[] fruitSprites;
    public Sprite circleSprite;

    [Header("Settings")]
    public int clothesCount = 10;
    public int fruitsCount = 5;
    public float itemSize = 140f;

    // Runtime
    private List<GameObject> spawnedItems = new List<GameObject>();
    private int clothesRemaining;
    private int fruitsRemaining;
    private Canvas canvas;

    // Basket slots for arranged fruit placement
    private List<Vector2> basketSlots = new List<Vector2>();
    private int nextBasketSlot;

    // Machine slots for arranged clothes placement
    private List<Vector2> machineSlots = new List<Vector2>();
    private int nextMachineSlot;

    // ── BaseMiniGame Hooks ──

    protected override string GetFallbackGameId() => "laundrysorting";

    protected override void OnGameInit()
    {
        totalRounds = 1;
        isEndless = true;
        playConfettiOnRoundWin = true;
        playWinSound = true;
        delayBeforeNextRound = 2f;
        canvas = GetComponentInParent<Canvas>();
    }

    protected override void OnRoundSetup()
    {
        // Difficulty scaling: easy(1-3)=(6,3), medium(4-6)=(10,5), hard(7-10)=(14,7)
        int tier = Difficulty <= 3 ? 0 : Difficulty <= 6 ? 1 : 2;
        switch (tier)
        {
            case 0: clothesCount = 6;  fruitsCount = 3; break;
            case 1: clothesCount = 10; fruitsCount = 5; break;
            case 2: clothesCount = 14; fruitsCount = 7; break;
        }

        nextBasketSlot = 0;
        nextMachineSlot = 0;
        basketSlots.Clear();
        machineSlots.Clear();
        BuildBasketSlots();
        BuildMachineSlots();
        SpawnItems();
        PositionTutorialHand();
    }

    private void PositionTutorialHand()
    {
        if (TutorialHand == null) return;
        if (spawnedItems.Count == 0) return;

        // Find first spawned item and its correct target
        GameObject firstItem = null;
        RectTransform targetRT = null;

        foreach (var go in spawnedItems)
        {
            if (go == null) continue;
            var dragger = go.GetComponent<LaundryItemDragger>();
            if (dragger == null) continue;

            firstItem = go;
            targetRT = dragger.isClothes ? washingMachineRT : basketRT;
            break;
        }

        if (firstItem == null || targetRT == null) return;

        var itemRT = firstItem.GetComponent<RectTransform>();

        Vector2 fromLocal = TutorialHand.GetLocalCenter(itemRT);
        Vector2 toLocal = TutorialHand.GetLocalCenter(targetRT);

        TutorialHand.SetMovePath(fromLocal, toLocal, 1.2f);
    }

    protected override void OnRoundCleanup()
    {
        foreach (var go in spawnedItems)
            if (go != null) Destroy(go);
        spawnedItems.Clear();

        // Clean basket and machine contents
        CleanSortedChildren(basketRT);
        CleanSortedChildren(washingMachineRT);
    }

    private void CleanSortedChildren(RectTransform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (child.name.StartsWith("Sorted_"))
                Destroy(child.gameObject);
        }
    }

    // ── Basket Slots ──

    private void BuildBasketSlots()
    {
        if (basketRT == null) return;
        float w = basketRT.rect.width;
        float h = basketRT.rect.height;
        if (w <= 0) w = 560f;
        if (h <= 0) h = 560f;

        // Slots inside the basket bowl (upper-center portion)
        float slotSize = itemSize * 0.55f;
        float bowlW = w * 0.55f;
        float bowlH = h * 0.3f;
        int cols = Mathf.Max(2, Mathf.FloorToInt(bowlW / slotSize));
        int rows = Mathf.Max(1, Mathf.FloorToInt(bowlH / slotSize));

        float offsetY = -h * 0.02f; // slightly above center
        float startX = -(cols - 1) * slotSize * 0.5f;
        float startY = offsetY - (rows - 1) * slotSize * 0.5f;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                basketSlots.Add(new Vector2(startX + c * slotSize, startY + r * slotSize));
    }

    private void BuildMachineSlots()
    {
        if (washingMachineRT == null) return;
        float w = washingMachineRT.rect.width;
        float h = washingMachineRT.rect.height;
        if (w <= 0) w = 700f;
        if (h <= 0) h = 700f;

        // Slots inside the drum area (lower portion of machine)
        float slotSize = itemSize * 0.5f;
        float drumW = w * 0.35f;
        float drumH = h * 0.30f;
        int cols = Mathf.Max(2, Mathf.FloorToInt(drumW / slotSize));
        int rows = Mathf.Max(2, Mathf.FloorToInt(drumH / slotSize));

        float offsetY = -h * 0.09f; // inside drum
        float startX = -(cols - 1) * slotSize * 0.5f;
        float startY = offsetY - (rows - 1) * slotSize * 0.5f;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                machineSlots.Add(new Vector2(startX + c * slotSize, startY + r * slotSize));
    }

    // ── Item Spawning ──

    private void SpawnItems()
    {
        if (itemsArea == null) return;

        float areaW = itemsArea.rect.width;
        float areaH = itemsArea.rect.height;
        if (areaW <= 0) areaW = 800f;
        if (areaH <= 0) areaH = 800f;

        var clothesList = new List<Sprite>(clothesSprites);
        Shuffle(clothesList);
        int actualClothes = Mathf.Min(clothesCount, clothesList.Count);

        var fruitsList = new List<Sprite>(fruitSprites);
        Shuffle(fruitsList);
        int actualFruits = Mathf.Min(fruitsCount, fruitsList.Count);

        clothesRemaining = actualClothes;
        fruitsRemaining = actualFruits;

        var items = new List<(Sprite sprite, bool isClothes)>();
        for (int i = 0; i < actualClothes; i++)
            items.Add((clothesList[i], true));
        for (int i = 0; i < actualFruits; i++)
            items.Add((fruitsList[i], false));
        Shuffle(items);

        var positions = GeneratePositions(items.Count, areaW, areaH);

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var go = CreateDraggableItem(item.sprite, item.isClothes, positions[i]);
            spawnedItems.Add(go);
            StartCoroutine(PopIn(go.GetComponent<RectTransform>(), i * 0.05f));
        }
    }

    private List<Vector2> GeneratePositions(int count, float areaW, float areaH)
    {
        // Fixed layout: row 1 = 7 (full width), row 2 = 4 (center-wide), row 3 = 3 (center)
        int[] rowCounts = { 7, 5, 3 };
        float[] rowWidths = { areaW * 0.95f, areaW * 0.65f, areaW * 0.45f };
        float totalH = areaH * 0.85f;
        float rowSpacing = totalH / rowCounts.Length;
        float startY = areaH * 0.45f;

        var positions = new List<Vector2>();

        for (int r = 0; r < rowCounts.Length; r++)
        {
            int cols = rowCounts[r];
            float rowW = rowWidths[r];
            float cellW = rowW / cols;
            float y = startY - r * rowSpacing;

            for (int c = 0; c < cols; c++)
            {
                float x = -rowW / 2f + (c + 0.5f) * cellW + Random.Range(-cellW * 0.08f, cellW * 0.08f);
                positions.Add(new Vector2(x, y + Random.Range(-10f, 10f)));
            }
        }

        // Shuffle
        for (int i = positions.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = positions[i]; positions[i] = positions[j]; positions[j] = tmp;
        }

        if (positions.Count > count)
            positions.RemoveRange(count, positions.Count - count);

        return positions;
    }

    private GameObject CreateDraggableItem(Sprite sprite, bool isClothes, Vector2 position)
    {
        var go = new GameObject(isClothes ? "Clothes" : "Fruit");
        go.transform.SetParent(itemsArea, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(itemSize, itemSize);
        rt.anchoredPosition = position;

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = true;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.15f);
        shadow.effectDistance = new Vector2(3, -3);

        var dragger = go.AddComponent<LaundryItemDragger>();
        dragger.isClothes = isClothes;
        dragger.controller = this;
        dragger.canvas = canvas;

        return go;
    }

    // ── Drop Handling ──

    public void OnItemDropped(LaundryItemDragger item)
    {
        DismissTutorial();

        if (IsInputLocked) return;

        var itemRT = item.GetComponent<RectTransform>();
        bool overMachine = IsOverTarget(itemRT, washingMachineRT);
        bool overBasket = IsOverTarget(itemRT, basketRT);

        if (overMachine)
        {
            if (item.isClothes)
            {
                PlayCorrectEffect(itemRT);
                RecordCorrect("clothes", item.GetComponent<Image>().sprite.name);
                StartCoroutine(AcceptIntoTarget(item.gameObject, washingMachineRT, true));
            }
            else
            {
                PlayWrongEffect(itemRT);
                RecordMistake("fruit_to_machine", item.GetComponent<Image>().sprite.name);
                StartCoroutine(RejectItem(item));
            }
        }
        else if (overBasket)
        {
            if (!item.isClothes)
            {
                PlayCorrectEffect(itemRT);
                RecordCorrect("fruit", item.GetComponent<Image>().sprite.name);
                StartCoroutine(AcceptIntoTarget(item.gameObject, basketRT, false));
            }
            else
            {
                PlayWrongEffect(itemRT);
                RecordMistake("clothes_to_basket", item.GetComponent<Image>().sprite.name);
                StartCoroutine(RejectItem(item));
            }
        }
        else
        {
            // Dropped on neither target — shake and return to original position
            StartCoroutine(RejectItem(item));
        }
    }

    private bool IsOverTarget(RectTransform itemRT, RectTransform targetRT)
    {
        if (targetRT == null) return false;

        Vector3[] corners = new Vector3[4];
        targetRT.GetWorldCorners(corners);
        Vector3 pos = itemRT.position;

        float expand = itemSize * 0.2f;
        return pos.x >= corners[0].x - expand && pos.x <= corners[2].x + expand
            && pos.y >= corners[0].y - expand && pos.y <= corners[2].y + expand;
    }

    // ── Washing Machine Animation ──

    private IEnumerator AcceptIntoTarget(GameObject item, RectTransform targetRT, bool isMachine)
    {
        var rt = item.GetComponent<RectTransform>();
        var img = item.GetComponent<Image>();
        var dragger = item.GetComponent<LaundryItemDragger>();
        if (dragger != null) dragger.enabled = false;

        // Get slot from the appropriate list
        var slots = isMachine ? machineSlots : basketSlots;
        int slotIdx = isMachine ? nextMachineSlot : nextBasketSlot;
        Vector2 slotLocal = Vector2.zero;
        if (slotIdx < slots.Count)
            slotLocal = slots[slotIdx];
        if (isMachine) nextMachineSlot++;
        else nextBasketSlot++;

        float targetSize = isMachine ? itemSize * 0.4f : itemSize * 0.55f;

        // Reparent to target FIRST so we can animate in local space
        rt.SetParent(targetRT, true);
        item.name = "Sorted_" + item.name;
        if (img != null) img.raycastTarget = false;
        var shadow = item.GetComponent<Shadow>();
        if (shadow != null) Destroy(shadow);

        // Animate from current local position to slot
        Vector2 startLocal = rt.anchoredPosition;
        Vector2 startSize = rt.sizeDelta;
        float dur = 0.35f;
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float ease = Mathf.SmoothStep(0, 1, p);
            rt.anchoredPosition = Vector2.Lerp(startLocal, slotLocal, ease);
            float sz = Mathf.Lerp(startSize.x, targetSize, ease);
            rt.sizeDelta = new Vector2(sz, sz);
            yield return null;
        }

        rt.anchoredPosition = slotLocal;
        rt.sizeDelta = new Vector2(targetSize, targetSize);

        StartCoroutine(BounceTarget(targetRT));
        if (isMachine) SpawnBubbles(targetRT, 3);

        if (isMachine) clothesRemaining--;
        else fruitsRemaining--;
        CheckWin();
    }


    // ── Reject ──

    private IEnumerator RejectItem(LaundryItemDragger item)
    {
        if (item == null) yield break;
        var rt = item.GetComponent<RectTransform>();
        if (rt == null) yield break;

        Vector2 current = rt.anchoredPosition;
        Vector2 returnTo = item.startPosition;

        // Shake
        float dur = 0.35f;
        float t = 0f;
        while (t < dur)
        {
            if (rt == null) yield break;
            t += Time.deltaTime;
            float p = t / dur;
            float shake = Mathf.Sin(p * Mathf.PI * 6f) * 15f * (1f - p);
            rt.anchoredPosition = current + new Vector2(shake, 0);
            yield return null;
        }

        // Return to original position
        if (rt != null)
            rt.anchoredPosition = returnTo;
    }

    // ── Win Check ──

    private void CheckWin()
    {
        if (clothesRemaining <= 0 && fruitsRemaining <= 0)
            StartCoroutine(DelayedWin());
    }

    private IEnumerator DelayedWin()
    {
        yield return new WaitForSeconds(0.5f);
        CompleteRound();
    }

    // ── Shared Animations ──

    private IEnumerator BounceTarget(RectTransform target)
    {
        if (target == null) yield break;
        float dur = 0.2f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float s = 1f + 0.06f * Mathf.Sin(p * Mathf.PI);
            target.localScale = Vector3.one * s;
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    private IEnumerator PopIn(RectTransform rt, float delay)
    {
        rt.localScale = Vector3.zero;
        yield return new WaitForSeconds(delay);
        float dur = 0.3f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float s = 1f + 0.15f * Mathf.Sin(p * Mathf.PI);
            rt.localScale = Vector3.one * Mathf.Min(s, 1f + 0.15f * (1f - p));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private void SpawnBubbles(RectTransform near, int count)
    {
        Vector2 pos = near.anchoredPosition + new Vector2(0, near.rect.height * 0.3f);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Bubble");
            go.transform.SetParent(playArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos + new Vector2(Random.Range(-40f, 40f), Random.Range(-20f, 20f));
            float sz = Random.Range(12f, 28f);
            rt.sizeDelta = new Vector2(sz, sz);

            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            img.color = new Color(0.7f, 0.85f, 1f, 0.6f);
            img.raycastTarget = false;

            StartCoroutine(AnimateBubble(rt, img));
        }
    }

    private IEnumerator AnimateBubble(RectTransform rt, Image img)
    {
        Vector2 start = rt.anchoredPosition;
        float dur = Random.Range(0.6f, 1f);
        float t = 0f;
        float speed = Random.Range(60f, 120f);
        float sway = Random.Range(-30f, 30f);

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            rt.anchoredPosition = start + new Vector2(Mathf.Sin(p * Mathf.PI * 2f) * sway, speed * p);
            img.color = new Color(img.color.r, img.color.g, img.color.b, 0.6f * (1f - p));
            rt.localScale = Vector3.one * (1f - p * 0.3f);
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    // ── Navigation ──

    public void OnHomePressed() => ExitGame();

    // ── Helpers ──

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }
}
