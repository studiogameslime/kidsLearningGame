using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Color Catch game — colored items fall from the sky, player drags a basket
/// left/right to catch items matching the basket's color.
/// Wood-table themed background.
/// </summary>
public class ColorCatchController : BaseMiniGame
{
    [Header("Layout")]
    public RectTransform playArea;

    [Header("UI")]
    public TextMeshProUGUI titleText;
    public Image progressFill;
    public TextMeshProUGUI progressText;

    // ── Color definitions (same palette as ColorSort) ──
    private static readonly Color ColorRed    = HexColor("#EF5350");
    private static readonly Color ColorBlue   = HexColor("#42A5F5");
    private static readonly Color ColorYellow = HexColor("#FFEE58");
    private static readonly Color ColorGreen  = HexColor("#66BB6A");

    private static readonly Color[][] ColorSets = new Color[][]
    {
        new[] { ColorRed, ColorBlue },                           // 2 colors
        new[] { ColorRed, ColorBlue, ColorYellow },              // 3 colors
        new[] { ColorRed, ColorBlue, ColorYellow, ColorGreen },  // 4 colors
    };

    // ── Difficulty tiers ──
    private struct DifficultyTier
    {
        public int colorCount;
        public float fallSpeed;
        public float itemSize;
        public int targetCatches;
        public float spawnInterval;
        public float basketWidth;
    }

    private static DifficultyTier GetTier(int difficulty)
    {
        if (difficulty <= 3)
            return new DifficultyTier { colorCount = 2, fallSpeed = 150f, itemSize = 180f, targetCatches = 5, spawnInterval = 1.8f, basketWidth = 300f };
        if (difficulty <= 6)
            return new DifficultyTier { colorCount = 3, fallSpeed = 220f, itemSize = 150f, targetCatches = 8, spawnInterval = 1.3f, basketWidth = 250f };
        return new DifficultyTier { colorCount = 4, fallSpeed = 300f, itemSize = 120f, targetCatches = 12, spawnInterval = 0.9f, basketWidth = 200f };
    }

    // ── Loaded sprites ──
    private Sprite[] itemSprites;
    private Sprite basketSprite;

    // ── Round state ──
    private DifficultyTier currentTier;
    private Color[] roundColors;
    private Color targetColor;
    private int targetColorIndex;
    private int catchCount;
    private float spawnTimer;
    private bool roundActive;

    // ── Basket state ──
    private GameObject basketGO;
    private RectTransform basketRT;
    private Image basketImage;
    private List<RectTransform> caughtItemsInBasket = new List<RectTransform>();

    // ── Falling items ──
    private List<FallingItem> fallingItems = new List<FallingItem>();

    private class FallingItem
    {
        public GameObject go;
        public RectTransform rt;
        public Image img;
        public int colorIndex;
        public float fallSpeed;
        public float sinePhase;
        public float sineAmplitude;
        public float baseX;
        public float spawnTime;
        public bool caught;
    }

    // ── Play area bounds cache ──
    private float areaLeft, areaRight, areaTop, areaBottom;
    private float areaWidth, areaHeight;

    // ── BaseMiniGame ──

    protected override void OnGameInit()
    {
        isEndless = true;
        totalRounds = 1;

        itemSprites = Resources.LoadAll<Sprite>("ColorSort/item");
        var basketSprites = Resources.LoadAll<Sprite>("ColorSort/ColoredBasket");
        if (basketSprites != null && basketSprites.Length > 0)
            basketSprite = basketSprites[0];
    }

    protected override void OnRoundSetup()
    {
        ClearRound();
        catchCount = 0;
        spawnTimer = 0f;
        roundActive = false;

        int difficulty = GameDifficultyConfig.GetLevel("colorcatch");
        currentTier = GetTier(difficulty);

        // Pick color set
        int colorSetIndex = Mathf.Clamp(currentTier.colorCount - 2, 0, ColorSets.Length - 1);
        roundColors = ColorSets[colorSetIndex];

        // Pick target color for basket
        targetColorIndex = Random.Range(0, roundColors.Length);
        targetColor = roundColors[targetColorIndex];

        // Cache play area bounds
        CachePlayAreaBounds();

        // Update progress bar
        UpdateProgress();

        // Create basket with entrance animation
        CreateBasket();
    }

    protected override void OnRoundCleanup()
    {
        ClearRound();
    }

    protected override void OnGameplayUpdate()
    {
        if (!roundActive || IsInputLocked) return;

        HandleInput();
        UpdateSpawnTimer();
        UpdateFallingItems();
    }

    protected override string GetFallbackGameId() => "colorcatch";

    public void OnHomePressed() => ExitGame();

    // ── Setup Helpers ──

    private void CachePlayAreaBounds()
    {
        if (playArea == null) return;
        Rect r = playArea.rect;
        areaLeft = r.xMin;
        areaRight = r.xMax;
        areaTop = r.yMax;
        areaBottom = r.yMin;
        areaWidth = r.width;
        areaHeight = r.height;
    }

    private void CreateBasket()
    {
        if (playArea == null) return;

        basketGO = new GameObject("Basket");
        basketGO.transform.SetParent(playArea, false);

        basketRT = basketGO.AddComponent<RectTransform>();
        basketRT.anchorMin = basketRT.anchorMax = new Vector2(0.5f, 0f);
        basketRT.pivot = new Vector2(0.5f, 0f);
        basketRT.sizeDelta = new Vector2(currentTier.basketWidth, currentTier.basketWidth * 0.7f);

        // Start below screen for entrance animation
        float basketY = 20f;
        basketRT.anchoredPosition = new Vector2(0f, basketY - 300f);

        basketImage = basketGO.AddComponent<Image>();
        if (basketSprite != null) basketImage.sprite = basketSprite;
        Color lightened = Color.Lerp(targetColor, Color.white, 0.3f);
        basketImage.color = lightened;
        basketImage.preserveAspect = true;
        basketImage.raycastTarget = false;

        // Slide-up entrance
        StartCoroutine(BasketEntrance(basketY));
    }

    private IEnumerator BasketEntrance(float targetY)
    {
        float elapsed = 0f;
        float duration = 0.5f;
        float startY = targetY - 300f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Elastic ease-out
            float ease = 1f - Mathf.Pow(1f - t, 3f) * Mathf.Cos(t * Mathf.PI * 1.5f);
            ease = Mathf.Clamp01(ease);
            basketRT.anchoredPosition = new Vector2(basketRT.anchoredPosition.x, Mathf.Lerp(startY, targetY, ease));
            yield return null;
        }
        basketRT.anchoredPosition = new Vector2(basketRT.anchoredPosition.x, targetY);
        roundActive = true;
    }

    // ── Input ──

    private void HandleInput()
    {
        if (basketRT == null) return;

        Vector2 inputPos;
        bool hasInput = false;

#if UNITY_EDITOR
        if (Input.GetMouseButton(0))
        {
            inputPos = Input.mousePosition;
            hasInput = true;
        }
        else
        {
            inputPos = Vector2.zero;
        }
#else
        if (Input.touchCount > 0)
        {
            inputPos = Input.GetTouch(0).position;
            hasInput = true;
        }
        else
        {
            inputPos = Vector2.zero;
        }
#endif

        if (!hasInput) return;

        DismissTutorial();

        // Convert screen position to local position in play area
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playArea, inputPos, null, out Vector2 localPoint))
        {
            // Clamp X to play area bounds
            float halfBasket = currentTier.basketWidth * 0.5f;
            float clampedX = Mathf.Clamp(localPoint.x, areaLeft + halfBasket, areaRight - halfBasket);

            // Smooth move towards target
            Vector2 pos = basketRT.anchoredPosition;
            pos.x = Mathf.Lerp(pos.x, clampedX, Time.deltaTime * 18f);
            basketRT.anchoredPosition = pos;
        }
    }

    // ── Spawning ──

    private void UpdateSpawnTimer()
    {
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= currentTier.spawnInterval)
        {
            spawnTimer = 0f;
            SpawnItem();
        }
    }

    private void SpawnItem()
    {
        if (playArea == null || itemSprites == null || itemSprites.Length == 0) return;

        // Decide color: 40% target, 60% split among others
        int colorIdx;
        if (Random.value < 0.4f || roundColors.Length == 1)
        {
            colorIdx = targetColorIndex;
        }
        else
        {
            // Pick a non-target color
            int offset = Random.Range(1, roundColors.Length);
            colorIdx = (targetColorIndex + offset) % roundColors.Length;
        }

        Color itemColor = roundColors[colorIdx];
        Sprite sprite = itemSprites[Random.Range(0, itemSprites.Length)];

        var go = new GameObject($"FallingItem_{fallingItems.Count}");
        go.transform.SetParent(playArea, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        float size = currentTier.itemSize;
        rt.sizeDelta = new Vector2(size, size);

        // Random X within play area
        float halfSize = size * 0.5f;
        float x = Random.Range(areaLeft + halfSize, areaRight - halfSize);
        float y = areaTop + halfSize + 20f; // Start just above visible area
        rt.anchoredPosition = new Vector2(x, y);

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = itemColor;
        img.preserveAspect = true;
        img.raycastTarget = false;

        var item = new FallingItem
        {
            go = go,
            rt = rt,
            img = img,
            colorIndex = colorIdx,
            fallSpeed = currentTier.fallSpeed,
            sinePhase = Random.Range(0f, Mathf.PI * 2f),
            sineAmplitude = Random.Range(15f, 40f),
            baseX = x,
            spawnTime = Time.time,
            caught = false
        };
        fallingItems.Add(item);

        // Pop-in animation
        StartCoroutine(PopInAnimation(rt));
    }

    private IEnumerator PopInAnimation(RectTransform rt)
    {
        if (rt == null) yield break;
        float elapsed = 0f;
        float dur = 0.25f;
        while (elapsed < dur && rt != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float scale;
            if (t < 0.7f)
                scale = Mathf.Lerp(0f, 1.1f, t / 0.7f);
            else
                scale = Mathf.Lerp(1.1f, 1f, (t - 0.7f) / 0.3f);
            rt.localScale = Vector3.one * scale;
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    // ── Falling Items Update ──

    private void UpdateFallingItems()
    {
        for (int i = fallingItems.Count - 1; i >= 0; i--)
        {
            var item = fallingItems[i];
            if (item.caught || item.go == null)
            {
                if (item.go == null) fallingItems.RemoveAt(i);
                continue;
            }

            // Move down
            Vector2 pos = item.rt.anchoredPosition;
            pos.y -= item.fallSpeed * Time.deltaTime;

            // Sine wobble
            float elapsed = Time.time - item.spawnTime;
            pos.x = item.baseX + Mathf.Sin(elapsed * 2.5f + item.sinePhase) * item.sineAmplitude;
            item.rt.anchoredPosition = pos;

            // Check if below screen
            if (pos.y < areaBottom - currentTier.itemSize)
            {
                Destroy(item.go);
                fallingItems.RemoveAt(i);
                continue;
            }

            // Check overlap with basket — different zones for correct vs wrong color
            if (basketRT != null)
            {
                int hitZone = GetBasketHitZone(item.rt);
                // hitZone: 0=no hit, 1=top opening, 2=sides/body
                if (hitZone > 0)
                {
                    bool isCorrectColor = item.colorIndex == targetColorIndex;
                    // Correct color: catch from top or sides
                    // Wrong color: only mistake if enters from top opening
                    if (isCorrectColor || hitZone == 1)
                    {
                        item.caught = true;
                        OnItemCaught(item);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns 0=no hit, 1=top opening of basket, 2=sides/body of basket.
    /// Top opening = upper 35% of the basket rect.
    /// </summary>
    private int GetBasketHitZone(RectTransform itemRT)
    {
        Rect itemRect = GetScreenRect(itemRT);
        Rect basketRect = GetScreenRect(basketRT);

        // No overlap at all
        if (!itemRect.Overlaps(basketRect)) return 0;

        // Item center Y position
        float itemCenterY = itemRect.y + itemRect.height * 0.5f;
        // Top opening = upper 35% of basket
        float topThreshold = basketRect.y + basketRect.height * 0.65f;

        return (itemCenterY >= topThreshold) ? 1 : 2;
    }

    private Rect GetScreenRect(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        float xMin = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
        float xMax = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
        float yMin = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
        float yMax = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
        return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    // ── Catch Logic ──

    private void OnItemCaught(FallingItem item)
    {
        if (item.colorIndex == targetColorIndex)
        {
            // Correct catch!
            catchCount++;
            bool isLast = catchCount >= currentTier.targetCatches;

            RecordCorrect("color", item.colorIndex.ToString(), isLast);
            PlayCorrectEffect(basketRT);
            ShowFloatingScore(basketRT);

            // Bounce item into basket
            StartCoroutine(ItemLandInBasket(item));

            // Happy basket bounce (squash/stretch)
            StartCoroutine(BasketHappyBounce());

            // Spawn colored star particles
            SpawnCatchParticles(basketRT, targetColor);

            UpdateProgress();

            if (isLast)
            {
                roundActive = false;
                StartCoroutine(RoundCompleteSequence());
            }
        }
        else
        {
            // Wrong catch!
            RecordMistake("color", item.colorIndex.ToString());
            PlayWrongEffect(basketRT);

            // Bounce item OUT of basket with spin and fade
            StartCoroutine(ItemBounceOut(item));

            // Basket shake
            StartCoroutine(BasketShake());
        }
    }

    // ── Correct Catch: Item lands IN basket ──

    private IEnumerator ItemLandInBasket(FallingItem item)
    {
        if (item.go == null) yield break;

        RectTransform rt = item.rt;
        // Reparent item to basket so it moves with it
        rt.SetParent(basketRT, true);

        // Convert to basket-local coords
        Vector2 startLocal = rt.anchoredPosition;
        // Target: center-bottom of basket, stacked
        int stackIndex = caughtItemsInBasket.Count;
        float stackOffsetY = stackIndex * 15f;
        float targetY = 20f + stackOffsetY;
        float targetX = Random.Range(-30f, 30f);
        Vector2 targetLocal = new Vector2(targetX, targetY);

        // Shrink item to fit in basket
        float targetScale = 0.55f;

        // Bounce animation: 3 decreasing bounces
        float duration = 0.4f;
        float elapsed = 0f;
        float bounceHeight = 40f;

        while (elapsed < duration && rt != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Ease position
            float x = Mathf.Lerp(startLocal.x, targetLocal.x, t);
            float baseY = Mathf.Lerp(startLocal.y, targetLocal.y, t);

            // Bouncing: sin wave with decay
            float bounce = Mathf.Sin(t * Mathf.PI * 3f) * bounceHeight * (1f - t);
            float y = baseY + Mathf.Abs(bounce);

            rt.anchoredPosition = new Vector2(x, y);
            float scale = Mathf.Lerp(1f, targetScale, t);
            rt.localScale = Vector3.one * scale;

            yield return null;
        }

        if (rt != null)
        {
            rt.anchoredPosition = targetLocal;
            rt.localScale = Vector3.one * targetScale;
            caughtItemsInBasket.Add(rt);
        }
    }

    // ── Wrong Catch: Item bounces OUT with spin ──

    private IEnumerator ItemBounceOut(FallingItem item)
    {
        if (item.go == null) yield break;

        RectTransform rt = item.rt;
        Image img = item.img;

        // Flash red
        img.color = Color.red;

        // Launch upward and to a random side
        float launchDirX = Random.value > 0.5f ? 1f : -1f;
        Vector2 startPos = rt.anchoredPosition;
        float launchSpeedX = 300f * launchDirX;
        float launchSpeedY = 400f;
        float spinSpeed = 720f * launchDirX;
        float gravity = 1200f;

        float elapsed = 0f;
        float fadeStart = 0.3f;
        float duration = 0.7f;
        float velY = launchSpeedY;

        while (elapsed < duration && rt != null)
        {
            elapsed += Time.deltaTime;
            float dt = Time.deltaTime;

            velY -= gravity * dt;
            Vector2 pos = rt.anchoredPosition;
            pos.x += launchSpeedX * dt;
            pos.y += velY * dt;
            rt.anchoredPosition = pos;

            // Spin
            rt.Rotate(0, 0, spinSpeed * dt);

            // Fade
            if (elapsed > fadeStart)
            {
                float fadeT = (elapsed - fadeStart) / (duration - fadeStart);
                Color c = img.color;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                img.color = c;
            }

            yield return null;
        }

        if (item.go != null)
        {
            Destroy(item.go);
        }
    }

    // ── Basket Animations ──

    private IEnumerator BasketHappyBounce()
    {
        if (basketRT == null) yield break;

        float elapsed = 0f;
        float duration = 0.3f;
        Vector3 original = Vector3.one;

        while (elapsed < duration && basketRT != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Squash/stretch: wide+short → tall+thin → settle
            float stretchY = 1f + Mathf.Sin(t * Mathf.PI * 2f) * 0.15f * (1f - t);
            float stretchX = 1f - Mathf.Sin(t * Mathf.PI * 2f) * 0.1f * (1f - t);
            basketRT.localScale = new Vector3(stretchX, stretchY, 1f);

            yield return null;
        }

        if (basketRT != null)
            basketRT.localScale = Vector3.one;
    }

    private IEnumerator BasketShake()
    {
        if (basketRT == null) yield break;

        Vector2 originalPos = basketRT.anchoredPosition;
        float elapsed = 0f;
        float duration = 0.25f;
        float amplitude = 12f;
        int oscillations = 3;

        while (elapsed < duration && basketRT != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float decay = 1f - t;
            float offset = Mathf.Sin(t * oscillations * Mathf.PI * 2f) * amplitude * decay;
            basketRT.anchoredPosition = originalPos + new Vector2(offset, 0f);
            yield return null;
        }

        if (basketRT != null)
            basketRT.anchoredPosition = originalPos;
    }

    // ── Particles ──

    private void SpawnCatchParticles(RectTransform target, Color color)
    {
        if (target == null) return;

        RectTransform parentRT = target.parent as RectTransform;
        if (parentRT == null) return;

        Vector2 localPos;
        // Convert basket world position to parent local position
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRT,
            RectTransformUtility.WorldToScreenPoint(null, target.position),
            null,
            out localPos);

        Color solidColor = new Color(color.r, color.g, color.b, 1f);
        Color lighterColor = Color.Lerp(solidColor, Color.white, 0.4f);
        Color starYellow = HexColor("#FFD54F");

        int starCount = Random.Range(8, 13);

        for (int i = 0; i < starCount; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(300f, 650f);
            float size = Random.Range(16f, 32f);
            float lifetime = Random.Range(0.4f, 0.7f);
            Color particleColor = Random.value < 0.5f
                ? Color.Lerp(solidColor, lighterColor, Random.Range(0f, 0.5f))
                : starYellow;

            var particleGO = new GameObject("CatchParticle");
            particleGO.transform.SetParent(parentRT, false);
            var prt = particleGO.AddComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(size, size);
            prt.anchoredPosition = localPos;

            // Star shape: use a rotated square (diamond)
            var pimg = particleGO.AddComponent<Image>();
            pimg.color = particleColor;
            pimg.raycastTarget = false;
            prt.localRotation = Quaternion.Euler(0, 0, 45f);

            StartCoroutine(AnimateParticle(prt, pimg, localPos, angle, speed, lifetime));
        }
    }

    private IEnumerator AnimateParticle(RectTransform rt, Image img, Vector2 startPos,
        float angle, float speed, float lifetime)
    {
        float elapsed = 0f;
        float dirX = Mathf.Cos(angle);
        float dirY = Mathf.Sin(angle);
        float gravity = 600f;
        float velY = dirY * speed;

        while (elapsed < lifetime && rt != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            float x = startPos.x + dirX * speed * elapsed;
            velY -= gravity * Time.deltaTime;
            float y = startPos.y + velY * elapsed;

            rt.anchoredPosition = new Vector2(x, y);

            // Fade and shrink
            float alpha = 1f - t * t;
            float scale = 1f - t * 0.5f;
            img.color = new Color(img.color.r, img.color.g, img.color.b, alpha);
            rt.localScale = Vector3.one * scale;

            yield return null;
        }

        if (rt != null) Destroy(rt.gameObject);
    }

    // ── Progress ──

    private void UpdateProgress()
    {
        float ratio = currentTier.targetCatches > 0
            ? (float)catchCount / currentTier.targetCatches
            : 0f;

        if (progressFill != null)
            StartCoroutine(AnimateProgressFill(ratio));

        if (progressText != null)
            HebrewText.SetText(progressText, $"{catchCount}/{currentTier.targetCatches}");
    }

    private IEnumerator AnimateProgressFill(float targetFill)
    {
        if (progressFill == null) yield break;
        float start = progressFill.fillAmount;
        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            progressFill.fillAmount = Mathf.Lerp(start, targetFill, t);
            yield return null;
        }
        progressFill.fillAmount = targetFill;
    }

    // ── Round Complete ──

    private IEnumerator RoundCompleteSequence()
    {
        // Pop all remaining falling items
        for (int i = fallingItems.Count - 1; i >= 0; i--)
        {
            var item = fallingItems[i];
            if (item.go != null && !item.caught)
            {
                StartCoroutine(PopOutItem(item.rt));
            }
        }
        fallingItems.Clear();

        // Big basket bounce
        if (basketRT != null)
        {
            float elapsed = 0f;
            float dur = 0.4f;
            while (elapsed < dur && basketRT != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.25f;
                basketRT.localScale = Vector3.one * s;
                yield return null;
            }
            if (basketRT != null) basketRT.localScale = Vector3.one;
        }

        yield return new WaitForSeconds(0.3f);
        CompleteRound();
    }

    private IEnumerator PopOutItem(RectTransform rt)
    {
        if (rt == null) yield break;
        float elapsed = 0f;
        float dur = 0.2f;
        while (elapsed < dur && rt != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            rt.localScale = Vector3.one * (1f - t);
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    // ── Cleanup ──

    private void ClearRound()
    {
        roundActive = false;

        foreach (var item in fallingItems)
            if (item.go != null) Destroy(item.go);
        fallingItems.Clear();

        caughtItemsInBasket.Clear();

        if (basketGO != null)
            Destroy(basketGO);
        basketGO = null;
        basketRT = null;
        basketImage = null;
    }

    // ── Helpers ──

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
