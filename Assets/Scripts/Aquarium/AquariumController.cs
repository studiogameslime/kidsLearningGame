using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Main controller for the Aquarium scene.
/// Progression loop: Tap food button → tap aquarium to place food → fish eat → progress bar fills → gift appears → reward.
/// Uses the same GiftBoxController as the World screen for consistent gift presentation.
/// </summary>
public class AquariumController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform gameplayArea;
    public RectTransform sandLayer;
    public Button backButton;
    public Button foodButton;
    public Image foodButtonImage;
    public Image backgroundImage;
    public TextMeshProUGUI emptyHintText;

    [Header("Progress Bar")]
    public RectTransform progressBarBg;
    public RectTransform progressBarFill;
    public Image progressBarFillImage;

    [Header("Gift Assets")]
    public Sprite giftSprite;

    [Header("FX")]
    public AquariumAmbience ambience;

    [Header("Settings")]
    public float fishSize = 80f;
    public float decorationSize = 70f;
    public float feedProximityRadius = 300f;
    public int maxFeedResponders = 4;
    public int feedsPerGift = 5;
    public int maxActiveFoodPieces = 20;

    private List<AquariumFish> spawnedFish = new List<AquariumFish>();
    private List<AquariumDecoration> spawnedDecorations = new List<AquariumDecoration>();
    private List<AquariumFood> activeFood = new List<AquariumFood>();
    private Sprite circleSprite;

    // Food sprites (loaded from Aquarium/Food sheet)
    private Sprite foodBoxSprite;     // Food_0 — button icon
    private Sprite spoonSprite;       // Food_1 — spoon animation
    private Sprite[] foodPieceSprites; // Food_2..Food_11 — food pieces

    // Food placement mode
    private bool isPlacingFood;
    private GameObject foodModeIndicator; // glow behind food button when active

    // Gift state
    private bool giftActive;
    private GiftBoxController activeGift;

    // Periodic food scanning
    private float foodScanTimer;
    private const float FoodScanInterval = 0.5f;

    private void Start()
    {
        circleSprite = Resources.Load<Sprite>("Circle");
        if (giftSprite == null)
        {
            var giftSprites = Resources.LoadAll<Sprite>("Gift");
            if (giftSprites != null && giftSprites.Length > 0)
                giftSprite = giftSprites[0];
        }

        LoadFoodSprites();

        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);
        if (foodButton != null)
            foodButton.onClick.AddListener(OnFoodButtonPressed);

        // Set food box sprite on the button
        if (foodButtonImage != null && foodBoxSprite != null)
            foodButtonImage.sprite = foodBoxSprite;

        LoadAquariumContent();
        UpdateProgressBar(false);
        UpdateEmptyHint();
    }

    private void LoadFoodSprites()
    {
        var all = Resources.LoadAll<Sprite>("Aquarium/Food");
        if (all == null || all.Length == 0) return;

        var pieces = new List<Sprite>();
        foreach (var s in all)
        {
            if (s.name == "Food_0") foodBoxSprite = s;
            else if (s.name == "Food_1") spoonSprite = s;
            else if (s.name.StartsWith("Food_")) pieces.Add(s);
        }
        foodPieceSprites = pieces.ToArray();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Gift tap takes priority
            if (giftActive && activeGift != null && activeGift.CurrentState == GiftBoxController.State.Idle)
            {
                if (IsPointerOverObject(activeGift.gameObject, Input.mousePosition))
                {
                    activeGift.OnTap();
                    return;
                }
            }

            // Food placement mode
            if (isPlacingFood)
            {
                // Check if the tap is on the food button (toggle off)
                if (foodButton != null && IsPointerOverObject(foodButton.gameObject, Input.mousePosition))
                {
                    // Let the Button.onClick handle the toggle
                }
                else
                {
                    TryPlaceFood(Input.mousePosition);
                }
            }
        }

        // Periodic food scanning — fish detect food while swimming near it
        foodScanTimer += Time.deltaTime;
        if (foodScanTimer >= FoodScanInterval)
        {
            foodScanTimer = 0f;
            activeFood.RemoveAll(f => f == null);
            bool hasValidFood = false;
            foreach (var f in activeFood)
                if (f.IsValid) { hasValidFood = true; break; }
            if (hasValidFood)
                AssignFishToFood();
        }

        // Pulse food mode indicator
        if (isPlacingFood && foodModeIndicator != null)
        {
            var indicatorImg = foodModeIndicator.GetComponent<Image>();
            if (indicatorImg != null)
            {
                float pulse = 0.25f + Mathf.Sin(Time.time * 3f) * 0.1f;
                indicatorImg.color = new Color(1f, 0.9f, 0.4f, pulse);
            }
        }
    }

    private bool IsPointerOverObject(GameObject obj, Vector2 screenPos)
    {
        var rt = obj.GetComponent<RectTransform>();
        if (rt == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null);
    }

    // ── Content Loading ──

    private void LoadAquariumContent()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        var aquarium = profile.aquarium;

        var fishSprites = LoadSpriteSheet("Aquarium/Fish");
        foreach (var fishId in aquarium.unlockedFishIds)
        {
            Sprite sprite = FindSprite(fishSprites, fishId);
            if (sprite != null)
                SpawnFish(fishId, sprite);
        }

        var itemSprites = LoadSpriteSheet("Aquarium/AquariumItem");
        foreach (var decoId in aquarium.unlockedDecorationIds)
        {
            Sprite sprite = FindSprite(itemSprites, decoId);
            if (sprite != null)
                SpawnDecoration(decoId, sprite, aquarium);
        }
    }

    private Sprite[] LoadSpriteSheet(string path)
    {
        return Resources.LoadAll<Sprite>(path);
    }

    private Sprite FindSprite(Sprite[] sprites, string id)
    {
        if (sprites == null) return null;
        foreach (var s in sprites)
        {
            if (s.name.Equals(id, System.StringComparison.OrdinalIgnoreCase))
                return s;
        }
        return null;
    }

    private void SpawnFish(string fishId, Sprite sprite)
    {
        if (gameplayArea == null) return;

        var go = new GameObject($"Fish_{fishId}");
        go.transform.SetParent(gameplayArea, false);

        var rt = go.AddComponent<RectTransform>();
        float aspect = sprite.rect.width / sprite.rect.height;
        float h = fishSize;
        float w = h * aspect;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Rect bounds = gameplayArea.rect;
        float x = Random.Range(bounds.xMin + w, bounds.xMax - w);
        float y = Random.Range(bounds.yMin + h, bounds.yMax - h);
        rt.anchoredPosition = new Vector2(x, y);

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        var fish = go.AddComponent<AquariumFish>();
        fish.fishId = fishId;
        fish.Initialize(gameplayArea);
        fish.onFinishedEating = AssignFishToFood;

        spawnedFish.Add(fish);
    }

    private void SpawnDecoration(string decoId, Sprite sprite, AquariumCollection aquarium)
    {
        if (gameplayArea == null) return;

        var go = new GameObject($"Deco_{decoId}");
        go.transform.SetParent(gameplayArea, false);

        var rt = go.AddComponent<RectTransform>();
        float aspect = sprite.rect.width / sprite.rect.height;
        float h = decorationSize;
        float w = h * aspect;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 pos = Vector2.zero;
        bool foundPlacement = false;
        foreach (var p in aquarium.decorationPlacements)
        {
            if (p.itemId == decoId)
            {
                pos = new Vector2(p.x, p.y);
                foundPlacement = true;
                break;
            }
        }

        if (!foundPlacement)
        {
            Rect bounds = gameplayArea.rect;
            pos = new Vector2(
                Random.Range(bounds.xMin + w, bounds.xMax - w),
                bounds.yMin + h * 0.5f + Random.Range(0f, 60f));
        }

        rt.anchoredPosition = pos;

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = true;

        var deco = go.AddComponent<AquariumDecoration>();
        deco.itemId = decoId;
        deco.dragBounds = gameplayArea;
        deco.controller = this;
        deco.sandMaxY = 0.28f; // bottom ~28% of gameplay area = sand zone

        spawnedDecorations.Add(deco);
    }

    // ── Food Placement Mode ──

    private void OnFoodButtonPressed()
    {
        if (giftActive) return;

        isPlacingFood = !isPlacingFood;

        if (foodButton != null)
        {
            var btnRT = foodButton.GetComponent<RectTransform>();
            btnRT.localScale = isPlacingFood ? Vector3.one * 1.1f : Vector3.one;
        }

        // Show/hide glow indicator behind button
        if (isPlacingFood)
            ShowFoodModeIndicator();
        else
            HideFoodModeIndicator();
    }

    private void ShowFoodModeIndicator()
    {
        if (foodModeIndicator != null) return;
        if (foodButton == null) return;

        foodModeIndicator = new GameObject("FoodModeGlow");
        foodModeIndicator.transform.SetParent(foodButton.transform, false);
        foodModeIndicator.transform.SetAsFirstSibling(); // behind button content

        var rt = foodModeIndicator.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(-0.25f, -0.25f);
        rt.anchorMax = new Vector2(1.25f, 1.25f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = foodModeIndicator.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = new Color(1f, 0.9f, 0.4f, 0.3f);
        img.raycastTarget = false;
    }

    private void HideFoodModeIndicator()
    {
        if (foodModeIndicator != null)
        {
            Destroy(foodModeIndicator);
            foodModeIndicator = null;
        }
    }

    private void TryPlaceFood(Vector2 screenPos)
    {
        if (gameplayArea == null) return;

        // Convert screen position to local position in gameplay area
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gameplayArea, screenPos, null, out Vector2 localPos);

        // Check if inside gameplay area bounds
        Rect bounds = gameplayArea.rect;
        if (!bounds.Contains(localPos)) return;

        // Cap active food pieces to prevent spam
        activeFood.RemoveAll(f => f == null);
        if (activeFood.Count >= maxActiveFoodPieces) return;

        PlaceFoodAt(localPos);
        AssignFishToFood();
    }

    private void PlaceFoodAt(Vector2 localPos)
    {
        StartCoroutine(SpoonAndScatter(localPos));
    }

    private IEnumerator SpoonAndScatter(Vector2 tapPos)
    {
        // ── Step 1: Spoon animation ──
        if (spoonSprite != null)
        {
            var spoonGO = new GameObject("Spoon");
            spoonGO.transform.SetParent(gameplayArea, false);

            var spoonRT = spoonGO.AddComponent<RectTransform>();
            spoonRT.sizeDelta = new Vector2(70f, 70f);
            spoonRT.anchorMin = spoonRT.anchorMax = new Vector2(0.5f, 0.5f);
            spoonRT.pivot = new Vector2(0.5f, 0.5f);
            spoonRT.anchoredPosition = tapPos + new Vector2(0, 40f);
            spoonRT.localScale = Vector3.zero;

            var spoonImg = spoonGO.AddComponent<Image>();
            spoonImg.sprite = spoonSprite;
            spoonImg.preserveAspect = true;
            spoonImg.raycastTarget = false;

            // Pop in
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                float p = t / 0.15f;
                spoonRT.localScale = Vector3.one * Mathf.Lerp(0f, 1.1f, p);
                yield return null;
            }
            spoonRT.localScale = Vector3.one;

            // Tilt pour animation
            t = 0f;
            float pourDur = 0.3f;
            while (t < pourDur)
            {
                t += Time.deltaTime;
                float p = t / pourDur;
                float angle = Mathf.Sin(p * Mathf.PI) * -35f; // tilt to pour
                spoonRT.localRotation = Quaternion.Euler(0, 0, angle);
                spoonRT.anchoredPosition = tapPos + new Vector2(0, 40f - 10f * p);
                yield return null;
            }

            // Fade out spoon
            t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                float p = t / 0.15f;
                spoonRT.localScale = Vector3.one * (1f - p);
                spoonImg.color = new Color(1f, 1f, 1f, 1f - p);
                yield return null;
            }

            Destroy(spoonGO);
        }

        // ── Step 2: Scatter food pieces ──
        int count = Random.Range(3, 7);
        for (int i = 0; i < count; i++)
        {
            SpawnFoodPiece(tapPos, i);
        }
    }

    private void SpawnFoodPiece(Vector2 center, int index)
    {
        Sprite sprite = null;
        if (foodPieceSprites != null && foodPieceSprites.Length > 0)
            sprite = foodPieceSprites[Random.Range(0, foodPieceSprites.Length)];

        var go = new GameObject("FoodPiece");
        go.transform.SetParent(gameplayArea, false);

        var rt = go.AddComponent<RectTransform>();
        float baseSize = Random.Range(14f, 22f);
        rt.sizeDelta = new Vector2(baseSize, baseSize);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        // Scatter offset
        Vector2 offset = Random.insideUnitCircle * 30f;
        rt.anchoredPosition = center + offset;
        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-25f, 25f));

        var img = go.AddComponent<Image>();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.preserveAspect = true;
        }
        else
        {
            if (circleSprite != null) img.sprite = circleSprite;
            img.color = new Color(0.95f, 0.75f, 0.3f, 0.9f);
        }
        img.raycastTarget = false;

        // Calculate floor Y in gameplay area local space
        Rect bounds = gameplayArea.rect;
        float floorY = bounds.yMin + bounds.height * 0.1f;

        var food = go.AddComponent<AquariumFood>();
        food.controller = this;
        food.floorY = floorY;
        // Randomize per-piece sinking behavior
        food.sinkSpeed = Random.Range(18f, 40f);
        food.driftAmp = Random.Range(6f, 18f);
        food.wobbleSpeed = Random.Range(2f, 5f);
        food.startDelay = index * Random.Range(0.04f, 0.1f); // stagger spawn

        activeFood.Add(food);
    }

    /// <summary>
    /// Assigns free fish to uneaten food. Called when new food is placed,
    /// and also when a fish finishes eating (so it can pick up remaining food).
    /// </summary>
    private void AssignFishToFood()
    {
        activeFood.RemoveAll(f => f == null);

        foreach (var food in activeFood)
        {
            if (!food.IsValid) continue;
            var foodPos = food.GetComponent<RectTransform>().anchoredPosition;

            // Count how many fish are already chasing this food
            int alreadyChasing = 0;
            foreach (var f in spawnedFish)
                if (f.IsChasing && f.TargetFood == food) alreadyChasing++;

            int slotsLeft = maxFeedResponders - alreadyChasing;
            if (slotsLeft <= 0) continue;

            // Sort free fish by distance
            spawnedFish.Sort((a, b) =>
            {
                float dA = Vector2.Distance(a.GetComponent<RectTransform>().anchoredPosition, foodPos);
                float dB = Vector2.Distance(b.GetComponent<RectTransform>().anchoredPosition, foodPos);
                return dA.CompareTo(dB);
            });

            foreach (var fish in spawnedFish)
            {
                if (slotsLeft <= 0) break;
                if (fish.TryReactToFood(food, feedProximityRadius))
                    slotsLeft--;
            }
        }
    }

    // ── Food Eaten Callback (called by AquariumFood) ──

    public void OnFoodEaten()
    {
        if (giftActive) return;
        if (!HasMoreRewards()) return;

        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        profile.aquarium.feedProgress++;
        ProfileManager.Instance.Save();

        UpdateProgressBar(true);

        if (profile.aquarium.feedProgress >= feedsPerGift)
        {
            // Exit food placement mode
            isPlacingFood = false;
            if (foodButton != null)
                foodButton.GetComponent<RectTransform>().localScale = Vector3.one;
            HideFoodModeIndicator();

            StartCoroutine(SpawnGiftDelayed());
        }
    }

    // ── Progress Bar ──

    private void UpdateProgressBar(bool animate)
    {
        if (progressBarFill == null || progressBarBg == null) return;

        var profile = ProfileManager.ActiveProfile;
        int progress = profile?.aquarium?.feedProgress ?? 0;
        float ratio = Mathf.Clamp01((float)progress / feedsPerGift);

        if (animate)
            StartCoroutine(AnimateProgressBar(ratio));
        else
            progressBarFill.anchorMax = new Vector2(ratio, 1f);

        if (progressBarFillImage != null)
        {
            Color blue = new Color(0.3f, 0.7f, 1f);
            Color gold = new Color(1f, 0.85f, 0.3f);
            progressBarFillImage.color = Color.Lerp(blue, gold, ratio);
        }
    }

    private IEnumerator AnimateProgressBar(float targetRatio)
    {
        float startRatio = progressBarFill.anchorMax.x;
        float dur = 0.4f;
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            float r = Mathf.Lerp(startRatio, targetRatio, t);
            progressBarFill.anchorMax = new Vector2(r, 1f);

            if (progressBarFillImage != null)
            {
                Color blue = new Color(0.3f, 0.7f, 1f);
                Color gold = new Color(1f, 0.85f, 0.3f);
                progressBarFillImage.color = Color.Lerp(blue, gold, r);
            }

            yield return null;
        }

        progressBarFill.anchorMax = new Vector2(targetRatio, 1f);

        // Pulse when full
        if (targetRatio >= 1f && progressBarBg != null)
        {
            elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                float pulse = 1f + 0.08f * Mathf.Sin(elapsed / 0.3f * Mathf.PI);
                progressBarBg.localScale = Vector3.one * pulse;
                yield return null;
            }
            progressBarBg.localScale = Vector3.one;
        }
    }

    // ── Gift (uses existing GiftBoxController) ──

    private IEnumerator SpawnGiftDelayed()
    {
        yield return new WaitForSeconds(0.8f);
        SpawnGift();
    }

    private void SpawnGift()
    {
        if (gameplayArea == null || giftActive) return;
        giftActive = true;

        if (foodButton != null) foodButton.interactable = false;

        Rect bounds = gameplayArea.rect;
        float giftX = Random.Range(bounds.center.x - 80f, bounds.center.x + 80f);
        float giftY = bounds.center.y + Random.Range(-20f, 20f);
        float giftSize = 160f;

        // Gift box
        var go = new GameObject("AquaGiftBox");
        go.transform.SetParent(gameplayArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(giftX, giftY);
        rt.sizeDelta = new Vector2(giftSize, giftSize);
        rt.localScale = Vector3.zero;

        var img = go.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = true;
        if (giftSprite != null)
            img.sprite = giftSprite;

        var gift = go.AddComponent<GiftBoxController>();
        gift.boxImage = img;
        gift.circleSprite = circleSprite;
        gift.onRewardRevealed = OnAquaGiftOpened;

        activeGift = gift;

        // Pop-in animation
        StartCoroutine(PopInGift(rt));
    }

    private IEnumerator PopInGift(RectTransform rt)
    {
        float t = 0f;
        float dur = 0.4f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float s = p < 0.6f
                ? Mathf.Lerp(0f, 1.2f, p / 0.6f)
                : Mathf.Lerp(1.2f, 1f, (p - 0.6f) / 0.4f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    /// <summary>Called by GiftBoxController.onRewardRevealed when the gift is opened.</summary>
    private void OnAquaGiftOpened(GiftBoxController gift)
    {
        string rewardId = GetNextRewardId();
        if (rewardId == null)
        {
            FinishGiftSequence();
            return;
        }

        UnlockReward(rewardId);

        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
        {
            profile.aquarium.feedProgress = 0;
            profile.aquarium.nextRewardIndex++;
            ProfileManager.Instance.Save();
        }

        UpdateProgressBar(false);

        StartCoroutine(RevealNewItem(rewardId, gift.GetComponent<RectTransform>().anchoredPosition));
    }

    private IEnumerator RevealNewItem(string itemId, Vector2 fromPos)
    {
        string sheetName = itemId.StartsWith("Fish_") ? "Aquarium/Fish" : "Aquarium/AquariumItem";
        var sprites = LoadSpriteSheet(sheetName);
        Sprite sprite = FindSprite(sprites, itemId);

        // Create reveal item
        var go = new GameObject($"Reveal_{itemId}");
        go.transform.SetParent(gameplayArea, false);

        var rt = go.AddComponent<RectTransform>();
        float size = itemId.StartsWith("Fish_") ? fishSize * 1.5f : decorationSize * 1.5f;
        if (sprite != null)
        {
            float aspect = sprite.rect.width / sprite.rect.height;
            rt.sizeDelta = new Vector2(size * aspect, size);
        }
        else
        {
            rt.sizeDelta = new Vector2(size, size);
        }
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = fromPos;
        rt.localScale = Vector3.zero;

        var img = go.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        if (sprite != null) img.sprite = sprite;

        // Pop in with bounce
        float t = 0f;
        float dur = 0.45f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float s = p < 0.6f
                ? Mathf.Lerp(0f, 1.3f, p / 0.6f)
                : Mathf.Lerp(1.3f, 1f, (p - 0.6f) / 0.4f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;

        // Confetti
        if (ConfettiController.Instance != null)
            ConfettiController.Instance.PlayBig();

        // Label
        var labelGO = new GameObject("RewardLabel");
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.5f, 0f);
        labelRT.anchorMax = new Vector2(0.5f, 0f);
        labelRT.pivot = new Vector2(0.5f, 1f);
        labelRT.sizeDelta = new Vector2(300f, 50f);
        labelRT.anchoredPosition = new Vector2(0, -10f);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        string label = itemId.StartsWith("Fish_")
            ? "\u05D3\u05D2 \u05D7\u05D3\u05E9!"
            : "\u05E7\u05D9\u05E9\u05D5\u05D8 \u05D7\u05D3\u05E9!";
        HebrewText.SetText(labelTMP, label);
        labelTMP.fontSize = 28;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.color = Color.white;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.raycastTarget = false;

        yield return new WaitForSeconds(2.5f);

        // Fade out
        t = 0f;
        dur = 0.4f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            img.color = new Color(1f, 1f, 1f, 1f - p);
            labelTMP.color = new Color(1f, 1f, 1f, 1f - p);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.8f, p);
            yield return null;
        }

        Destroy(go);

        // Spawn the actual item in the aquarium
        if (sprite != null)
        {
            if (itemId.StartsWith("Fish_"))
                SpawnFish(itemId, sprite);
            else
            {
                var profile = ProfileManager.ActiveProfile;
                if (profile != null)
                    SpawnDecoration(itemId, sprite, profile.aquarium);
            }
        }

        FinishGiftSequence();
    }

    private void FinishGiftSequence()
    {
        giftActive = false;
        activeGift = null;
        if (foodButton != null) foodButton.interactable = true;
        UpdateEmptyHint();
    }

    // ── Reward Logic ──

    private bool HasMoreRewards()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return false;
        return profile.aquarium.nextRewardIndex < DiscoveryCatalog.AquariumRewardOrder.Length;
    }

    private string GetNextRewardId()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return null;
        int idx = profile.aquarium.nextRewardIndex;
        var order = DiscoveryCatalog.AquariumRewardOrder;
        if (idx >= order.Length) return null;
        return order[idx];
    }

    private void UnlockReward(string itemId)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        if (itemId.StartsWith("Fish_"))
        {
            if (!profile.aquarium.unlockedFishIds.Contains(itemId))
                profile.aquarium.unlockedFishIds.Add(itemId);
        }
        else
        {
            if (!profile.aquarium.unlockedDecorationIds.Contains(itemId))
                profile.aquarium.unlockedDecorationIds.Add(itemId);
        }
    }

    // ── Decoration Persistence ──

    public void OnDecorationMoved(AquariumDecoration deco)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        var aquarium = profile.aquarium;
        var rt = deco.GetComponent<RectTransform>();
        Vector2 pos = rt.anchoredPosition;

        AquariumItemPlacement existing = null;
        foreach (var p in aquarium.decorationPlacements)
        {
            if (p.itemId == deco.itemId)
            {
                existing = p;
                break;
            }
        }

        if (existing != null)
        {
            existing.x = pos.x;
            existing.y = pos.y;
        }
        else
        {
            aquarium.decorationPlacements.Add(new AquariumItemPlacement
            {
                itemId = deco.itemId,
                x = pos.x,
                y = pos.y
            });
        }

        ProfileManager.Instance.Save();
    }

    // ── Navigation ──

    private void OnBackPressed()
    {
        NavigationManager.GoToHome();
    }

    // ── Empty State ──

    private void UpdateEmptyHint()
    {
        if (emptyHintText == null) return;

        var profile = ProfileManager.ActiveProfile;
        bool isEmpty = profile == null ||
            (profile.aquarium.unlockedFishIds.Count == 0 && profile.aquarium.unlockedDecorationIds.Count == 0);

        emptyHintText.gameObject.SetActive(isEmpty);
        if (isEmpty)
            HebrewText.SetText(emptyHintText, "\u05D4\u05D0\u05DB\u05D9\u05DC\u05D5 \u05D0\u05EA \u05D4\u05D3\u05D2\u05D9\u05DD \u05DB\u05D3\u05D9 \u05DC\u05E7\u05D1\u05DC \u05DE\u05EA\u05E0\u05D5\u05EA!");
    }
}
