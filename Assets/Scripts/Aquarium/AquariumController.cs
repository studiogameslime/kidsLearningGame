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
    public float fishSize = 120f;
    public float decorationSize = 140f;
    public float feedProximityRadius = 300f;
    public int maxFeedResponders = 4;
    public int baseFeedsPerGift = 5;
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

    // Glass cleaning
    private RawImage _dirtyOverlay;
    private Texture2D _dirtyMask;
    private byte[] _dirtyPixels;
    private bool _dirtyMaskDirty;
    private const int DirtyTexW = 512;
    private const int DirtyTexH = 288;
    private int _cleanBrushRadius = 40;
    private Button _spongeButton;
    private float _totalDirtyPixels;
    private float _cleanedPixels;
    private float _cleanProgressAccum; // accumulates cleaned amount, awards point every threshold
    private GameObject _spongeCursor; // follows finger while cleaning
    private RectTransform _spongeCursorRT;
    private bool _dirtGrowing;
    private float _dirtGrowTimer;
    private float _dirtGrowInterval = 3f;
    private byte[] _dirtPattern;


    private void Start()
    {
        circleSprite = Resources.Load<Sprite>("Circle");
        if (ambience != null) ambience.controller = this;
        if (giftSprite == null)
        {
            var giftSprites = Resources.LoadAll<Sprite>("Gift");
            if (giftSprites != null && giftSprites.Length > 0)
                giftSprite = giftSprites[0];
        }

        LoadFoodSprites();
        FingerTrail.SetEnabled(false);
        FirebaseAnalyticsManager.LogScreenView("aquarium");

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
        CreateSpongeButton();
        CreateDirtyGlass();

        // Play intro voice guide
        StartCoroutine(PlayIntroGuide());
    }

    private System.Collections.IEnumerator PlayIntroGuide()
    {
        if (!_isFirstVisit) yield break; // Only play intro on first visit

        yield return new WaitForSeconds(0.5f);

        // "This is your aquarium"
        var introClip = SoundLibrary.AquariumIntro();
        if (introClip != null)
        {
            BackgroundMusicManager.PlayOneShot(introClip);
            yield return new WaitForSeconds(introClip.length + 0.3f);
        }

        // "Here you can feed all your fishes"
        var feedClip = SoundLibrary.AquariumFeedFishes();
        if (feedClip != null)
        {
            BackgroundMusicManager.PlayOneShot(feedClip);
            yield return new WaitForSeconds(feedClip.length + 0.3f);
        }

        // "Let's open your first gift"
        var openGiftClip = SoundLibrary.WorldOpenFirstGift();
        if (openGiftClip != null)
        {
            BackgroundMusicManager.PlayOneShot(openGiftClip);
            yield return new WaitForSeconds(openGiftClip.length + 0.2f);
        }

        SpawnFirstFishGift();
    }

    private void SpawnFirstFishGift()
    {
        giftActive = true;

        // Create gift in center of gameplay area (same pattern as SpawnGift)
        var go = new GameObject("FirstFishGift");
        go.transform.SetParent(gameplayArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(200, 200);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.zero;

        var img = go.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = true;
        if (giftSprite != null) img.sprite = giftSprite;

        var gift = go.AddComponent<GiftBoxController>();
        gift.boxImage = img;
        gift.circleSprite = circleSprite;
        gift.onRewardRevealed = (g) =>
        {
            // Unlock first fish
            var profile = ProfileManager.ActiveProfile;
            if (profile != null)
            {
                profile.aquarium.unlockedFishIds.Add("Fish_0");
                profile.aquarium.nextRewardIndex = Mathf.Max(profile.aquarium.nextRewardIndex, 1);
                ProfileManager.Instance.Save();
                FirebaseAnalyticsManager.LogAquariumGiftOpened("Fish_0");

                // Spawn the fish
                var fishSprites = LoadSpriteSheet("Aquarium/Fish");
                var sprite = FindSprite(fishSprites, "Fish_0");
                if (sprite != null)
                    SpawnFish("Fish_0", sprite);
            }
            giftActive = false;
            _isFirstVisit = false;
            UpdateEmptyHint();
        };

        activeGift = gift;
        StartCoroutine(PopInGift(rt));
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
                if (foodButton != null && IsPointerOverObject(foodButton.gameObject, Input.mousePosition))
                {
                    // Let the Button.onClick handle the toggle
                }
                else
                {
                    TryPlaceFood(Input.mousePosition);
                }
            }
            else if (!giftActive)
            {
                // Interactive taps (fish > decoration > water)
                HandleInteractiveTap(Input.mousePosition);
            }
        }

        // Draggable sponge — detect drag start on sponge, drag to clean, release to snap back
        if (_spongeRT != null && _dirtyOverlay != null)
        {
            if (Input.GetMouseButtonDown(0) && !_isDraggingSponge)
            {
                // Check if touching the sponge
                Vector2 spongeLocal;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _spongeRT, Input.mousePosition, null, out spongeLocal);
                if (_spongeRT.rect.Contains(spongeLocal))
                {
                    _isDraggingSponge = true;
                    isPlacingFood = false;
                    _spongeRT.SetAsLastSibling();
                    _spongeRT.localScale = Vector3.one * 1.1f;
                }
            }

            if (_isDraggingSponge && Input.GetMouseButton(0))
            {
                // Move sponge to exact finger position
                Vector3 worldPos;
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    _spongeRT.parent as RectTransform, Input.mousePosition, null, out worldPos);
                _spongeRT.position = worldPos;

                // Clean at sponge position (convert to dirty overlay coords)
                Vector2 dirtyLocal;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dirtyOverlay.rectTransform, Input.mousePosition, null, out dirtyLocal);
                Rect rect = _dirtyOverlay.rectTransform.rect;
                float nx = (dirtyLocal.x - rect.x) / rect.width;
                float ny = (dirtyLocal.y - rect.y) / rect.height;
                if (nx >= 0 && nx <= 1 && ny >= 0 && ny <= 1)
                    CleanAt(nx * DirtyTexW, ny * DirtyTexH);
            }

            if (_isDraggingSponge && Input.GetMouseButtonUp(0))
            {
                // Snap sponge back home
                _isDraggingSponge = false;
                _spongeRT.localScale = Vector3.one;
                StartCoroutine(SnapSpongeHome());
            }
        }

        // Track finger position for fish attraction
        if (Input.GetMouseButton(0) && !isPlacingFood && !giftActive && !_isDraggingSponge)
        {
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gameplayArea, Input.mousePosition, null, out localPos);
            AquariumFish.FingerActive = true;
            AquariumFish.FingerPos = localPos;
        }
        else
        {
            AquariumFish.FingerActive = false;
        }

        // Gradual dirt regrowth
        if (_dirtGrowing && _dirtPattern != null && !_isDraggingSponge)
        {
            _dirtGrowTimer += Time.deltaTime;
            if (_dirtGrowTimer >= _dirtGrowInterval)
            {
                _dirtGrowTimer = 0f;
                GrowDirtStep();
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

    // ── Interactive Taps (fish > decoration > water) ──

    private void HandleInteractiveTap(Vector2 screenPos)
    {
        // Check fish tap
        foreach (var fish in spawnedFish)
        {
            if (fish == null) continue;
            if (IsPointerOverObject(fish.gameObject, screenPos))
            {
                fish.OnTap();
                SpawnBubblesAt(fish.GetComponent<RectTransform>().anchoredPosition, 3);
                return;
            }
        }

        // Decoration taps are handled by IPointerClickHandler on the decoration itself

        // Water tap — spawn bubbles + ripple + nudge nearby fish
        if (gameplayArea == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gameplayArea, screenPos, null, out Vector2 localPos);
        if (!gameplayArea.rect.Contains(localPos)) return;

        SpawnBubblesAt(localPos, Random.Range(3, 6));
        StartCoroutine(SpawnRipple(localPos));

        // Nudge nearby fish toward tap
        foreach (var fish in spawnedFish)
        {
            if (fish != null)
                fish.Nudge(localPos, 200f);
        }
    }

    // ── Bubble & Ripple Effects ──

    public void SpawnBubblesAt(Vector2 localPos, int count)
    {
        if (gameplayArea == null) return;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("TapBubble");
            go.transform.SetParent(gameplayArea, false);
            var rt = go.AddComponent<RectTransform>();
            float size = Random.Range(6f, 14f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = localPos + Random.insideUnitCircle * 15f;

            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            img.color = new Color(0.85f, 0.95f, 1f, 0.5f);
            img.raycastTarget = false;

            StartCoroutine(AnimateTapBubble(rt, img));
        }
    }

    private IEnumerator AnimateTapBubble(RectTransform rt, Image img)
    {
        Vector2 start = rt.anchoredPosition;
        float dur = Random.Range(0.6f, 1.2f);
        float riseH = Random.Range(40f, 80f);
        float swayX = Random.Range(-15f, 15f);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float y = start.y + riseH * p;
            float x = start.x + swayX * Mathf.Sin(p * Mathf.PI);
            rt.anchoredPosition = new Vector2(x, y);
            float alpha = p > 0.6f ? 0.5f * (1f - (p - 0.6f) / 0.4f) : 0.5f;
            img.color = new Color(0.85f, 0.95f, 1f, alpha);
            yield return null;
        }

        Destroy(rt.gameObject);
    }

    private IEnumerator SpawnRipple(Vector2 localPos)
    {
        if (circleSprite == null) yield break;

        var go = new GameObject("Ripple");
        go.transform.SetParent(gameplayArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(10f, 10f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = localPos;

        var img = go.AddComponent<Image>();
        img.sprite = circleSprite;
        img.color = new Color(1f, 1f, 1f, 0.2f);
        img.raycastTarget = false;

        float dur = 0.5f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float size = Mathf.Lerp(10f, 100f, p);
            rt.sizeDelta = new Vector2(size, size);
            img.color = new Color(1f, 1f, 1f, 0.2f * (1f - p));
            yield return null;
        }

        Destroy(go);
    }

    // ── Content Loading ──

    private bool _isFirstVisit;

    private void LoadAquariumContent()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        var aquarium = profile.aquarium;

        // First visit: no fish — gift will appear after Alin's intro
        _isFirstVisit = aquarium.unlockedFishIds.Count == 0;
        if (_isFirstVisit) return; // don't spawn anything yet

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
        if (foodButton == null || foodButtonImage == null) return;

        foodModeIndicator = new GameObject("FoodModeGlow");
        foodModeIndicator.transform.SetParent(foodButton.transform, false);
        foodModeIndicator.transform.SetAsFirstSibling();

        var rt = foodModeIndicator.AddComponent<RectTransform>();
        // Slightly larger than the button to create a glow outline
        rt.anchorMin = new Vector2(-0.15f, -0.15f);
        rt.anchorMax = new Vector2(1.15f, 1.15f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = foodModeIndicator.AddComponent<Image>();
        // Use the same sprite as the food button so glow follows the shape, not a square
        img.sprite = foodButtonImage.sprite;
        img.preserveAspect = true;
        img.color = new Color(1f, 0.9f, 0.4f, 0.35f);
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
        FirebaseAnalyticsManager.LogAquariumFeed();
        if (giftActive) return;
        if (!HasMoreRewards()) return;

        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        AddProgress(10); // feeding = 10 points
    }

    /// <summary>Add points to aquarium progress bar. Feeding=10, Bubble=1, Cleaning=5.</summary>
    public void AddProgress(int points)
    {
        if (giftActive) return;
        if (!HasMoreRewards()) return;

        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        profile.aquarium.feedProgress += points;
        ProfileManager.Instance.Save();

        UpdateProgressBar(true);

        if (profile.aquarium.feedProgress >= GetFeedsForNextGift())
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
        float ratio = Mathf.Clamp01((float)progress / GetFeedsForNextGift());

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
        FirebaseAnalyticsManager.LogAquariumGiftOpened(rewardId);

        UnlockReward(rewardId);

        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
        {
            profile.aquarium.feedProgress = 0;
            profile.aquarium.nextRewardIndex++;

            // Award a star for each aquarium gift
            if (profile.journey != null)
            {
                profile.journey.totalStars++;
                profile.journey.totalGamesCompleted++;
            }

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

    /// <summary>
    /// Returns how many eats are needed for the next gift.
    /// Starts easy (baseFeedsPerGift) and increases gradually.
    /// </summary>
    private int GetFeedsForNextGift()
    {
        var profile = ProfileManager.ActiveProfile;
        int rewardIndex = profile?.aquarium?.nextRewardIndex ?? 0;
        // Points needed: first=20, then 50, 70, 90... (x10 of old system)
        if (rewardIndex <= 1) return 20;
        return Mathf.Min((baseFeedsPerGift + (rewardIndex - 1) * 2) * 10, 200);
    }

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
        FingerTrail.SetEnabled(true);
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

    // ── Glass Cleaning ──

    private RectTransform _spongeRT;
    private Vector2 _spongeHomePos;
    private bool _isDraggingSponge;

    private void CreateSpongeButton()
    {
        var safeArea = foodButton != null ? foodButton.transform.parent : null;
        if (safeArea == null) return;

        var spongeGO = new GameObject("Sponge");
        spongeGO.transform.SetParent(safeArea, false);
        _spongeRT = spongeGO.AddComponent<RectTransform>();
        _spongeRT.anchorMin = new Vector2(1, 1);
        _spongeRT.anchorMax = new Vector2(1, 1);
        _spongeRT.pivot = new Vector2(0.5f, 0.5f);
        _spongeRT.sizeDelta = new Vector2(180, 180);

        var foodRT = foodButton.GetComponent<RectTransform>();
        float foodBottom = foodRT.anchoredPosition.y - foodRT.sizeDelta.y;
        _spongeRT.anchoredPosition = new Vector2(foodRT.anchoredPosition.x - 90, foodBottom - 100);
        _spongeHomePos = _spongeRT.anchoredPosition;

        var spongeImg = spongeGO.AddComponent<Image>();
        var spongeSprite = Resources.Load<Sprite>("Aquarium/Sponge");
        if (spongeSprite != null)
        {
            spongeImg.sprite = spongeSprite;
            spongeImg.preserveAspect = true;
        }
        else
        {
            spongeImg.color = new Color(0.95f, 0.85f, 0.2f, 0.9f);
        }
        spongeImg.raycastTarget = true;
    }

    private IEnumerator SnapSpongeHome()
    {
        Vector2 start = _spongeRT.anchoredPosition;
        float dur = 0.25f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / dur);
            _spongeRT.anchoredPosition = Vector2.Lerp(start, _spongeHomePos, t);
            yield return null;
        }
        _spongeRT.anchoredPosition = _spongeHomePos;
    }

    private void CreateDirtyGlass()
    {
        // Create a RawImage overlay on top of gameplay area
        var dirtyGO = new GameObject("DirtyGlass");
        dirtyGO.transform.SetParent(gameplayArea, false);
        dirtyGO.transform.SetAsLastSibling();

        var dirtyRT = dirtyGO.AddComponent<RectTransform>();
        dirtyRT.anchorMin = Vector2.zero;
        dirtyRT.anchorMax = Vector2.one;
        dirtyRT.offsetMin = Vector2.zero;
        dirtyRT.offsetMax = Vector2.zero;

        _dirtyOverlay = dirtyGO.AddComponent<RawImage>();
        _dirtyOverlay.raycastTarget = false; // let touches pass through to fish/decorations

        // Create dirty mask texture
        _dirtyMask = new Texture2D(DirtyTexW, DirtyTexH, TextureFormat.RGBA32, false);
        _dirtyMask.filterMode = FilterMode.Bilinear;
        _dirtyMask.wrapMode = TextureWrapMode.Clamp;
        _dirtyPixels = new byte[DirtyTexW * DirtyTexH * 4];

        // Generate algae/dirt pattern using Perlin noise
        GenerateDirtyPattern();

        _dirtyOverlay.texture = _dirtyMask;
        _dirtyOverlay.color = Color.white;

        // Dirt is always visible — sponge button activates cleaning mode
    }

    private void GenerateDirtyPattern()
    {
        _totalDirtyPixels = 0;
        _cleanedPixels = 0;
        _dirtPattern = new byte[DirtyTexW * DirtyTexH * 4];

        // Use random offset so each generation looks different
        float offsetX = Random.Range(0f, 500f);
        float offsetY = Random.Range(0f, 500f);

        for (int y = 0; y < DirtyTexH; y++)
        {
            for (int x = 0; x < DirtyTexW; x++)
            {
                int idx = (y * DirtyTexW + x) * 4;

                float n1 = Mathf.PerlinNoise(x * 0.02f + offsetX, y * 0.02f + offsetY);
                float n2 = Mathf.PerlinNoise(x * 0.05f + offsetX + 100f, y * 0.05f + offsetY + 100f) * 0.5f;
                float n3 = Mathf.PerlinNoise(x * 0.12f + offsetX + 200f, y * 0.12f + offsetY + 200f) * 0.2f;
                float n = n1 + n2 + n3;

                if (n > 0.85f)
                {
                    float intensity = Mathf.Clamp01((n - 0.85f) / 0.4f);
                    _dirtPattern[idx]     = (byte)(40 + intensity * 30);
                    _dirtPattern[idx + 1] = (byte)(80 + intensity * 40);
                    _dirtPattern[idx + 2] = (byte)(30 + intensity * 20);
                    _dirtPattern[idx + 3] = (byte)(intensity * 120);
                    _totalDirtyPixels += intensity;
                }
                else
                {
                    _dirtPattern[idx] = 0;
                    _dirtPattern[idx + 1] = 0;
                    _dirtPattern[idx + 2] = 0;
                    _dirtPattern[idx + 3] = 0;
                }

                // Start clean — active pixels begin transparent
                _dirtyPixels[idx]     = 0;
                _dirtyPixels[idx + 1] = 0;
                _dirtyPixels[idx + 2] = 0;
                _dirtyPixels[idx + 3] = 0;
            }
        }

        _dirtyMask.LoadRawTextureData(_dirtyPixels);
        _dirtyMask.Apply();

        // Start growing dirt immediately
        _dirtGrowing = true;
        _dirtGrowTimer = 0f;
    }

    private void CleanAt(float texX, float texY)
    {
        int cx = Mathf.RoundToInt(texX);
        int cy = Mathf.RoundToInt(texY);
        int r = _cleanBrushRadius;
        bool anyChanged = false;

        for (int dy = -r; dy <= r; dy++)
        {
            int py = cy + dy;
            if (py < 0 || py >= DirtyTexH) continue;

            for (int dx = -r; dx <= r; dx++)
            {
                int px = cx + dx;
                if (px < 0 || px >= DirtyTexW) continue;

                if (dx * dx + dy * dy > r * r) continue;

                int idx = (py * DirtyTexW + px) * 4;
                if (_dirtyPixels[idx + 3] > 0)
                {
                    float amount = _dirtyPixels[idx + 3] / 255f;
                    _cleanedPixels += amount;
                    _cleanProgressAccum += amount;
                    _dirtyPixels[idx + 3] = 0;
                    anyChanged = true;
                }
            }
        }

        if (anyChanged)
        {
            _dirtyMask.LoadRawTextureData(_dirtyPixels);
            _dirtyMask.Apply();

            // Spawn sparkle at cleaned spot
            if (ambience != null)
                SpawnCleanSparkle(texX, texY);

            // Award 1 progress point for every 20% of dirt cleaned
            float pointThreshold = _totalDirtyPixels > 0 ? _totalDirtyPixels / 5f : 50f;
            while (_cleanProgressAccum >= pointThreshold && pointThreshold > 0)
            {
                _cleanProgressAccum -= pointThreshold;
                AddProgress(1);
            }

            // Check if mostly clean (80% threshold)
            if (_totalDirtyPixels > 0 && _cleanedPixels / _totalDirtyPixels > 0.80f)
            {
                OnGlassClean();
            }
        }
    }

    private void SpawnCleanSparkle(float texX, float texY)
    {
        // Convert texture coords to gameplay area local coords
        Rect rect = _dirtyOverlay.rectTransform.rect;
        float localX = rect.x + (texX / DirtyTexW) * rect.width;
        float localY = rect.y + (texY / DirtyTexH) * rect.height;

        SpawnBubblesAt(new Vector2(localX, localY), 1);
    }

    private void OnGlassClean()
    {
        SoundLibrary.PlayRandomFeedback();

        // Clear remaining dirt visually
        for (int i = 0; i < _dirtyPixels.Length; i += 4)
            _dirtyPixels[i + 3] = 0;
        _dirtyMask.LoadRawTextureData(_dirtyPixels);
        _dirtyMask.Apply();

        // Dirt grows back gradually
        _dirtGrowTimer = 0f;
        _dirtGrowing = true;
    }

    private void GrowDirtStep()
    {
        if (_dirtPattern == null || _dirtyPixels == null) return;

        // Pick a random cluster of pixels and restore their dirt from the pattern
        int centerX = Random.Range(20, DirtyTexW - 20);
        int centerY = Random.Range(20, DirtyTexH - 20);
        int radius = Random.Range(15, 35);
        bool anyChanged = false;

        for (int dy = -radius; dy <= radius; dy++)
        {
            int py = centerY + dy;
            if (py < 0 || py >= DirtyTexH) continue;
            for (int dx = -radius; dx <= radius; dx++)
            {
                int px = centerX + dx;
                if (px < 0 || px >= DirtyTexW) continue;
                if (dx * dx + dy * dy > radius * radius) continue;

                int idx = (py * DirtyTexW + px) * 4;
                byte targetAlpha = _dirtPattern[idx + 3];
                if (targetAlpha > 0 && _dirtyPixels[idx + 3] < targetAlpha)
                {
                    // Grow towards target gradually
                    int newAlpha = Mathf.Min(targetAlpha, _dirtyPixels[idx + 3] + 8);
                    _dirtyPixels[idx]     = _dirtPattern[idx];
                    _dirtyPixels[idx + 1] = _dirtPattern[idx + 1];
                    _dirtyPixels[idx + 2] = _dirtPattern[idx + 2];
                    _dirtyPixels[idx + 3] = (byte)newAlpha;
                    anyChanged = true;
                }
            }
        }

        if (anyChanged)
        {
            _dirtyMask.LoadRawTextureData(_dirtyPixels);
            _dirtyMask.Apply();

            // Reset counters for cleaning detection
            _cleanedPixels = 0;
            _totalDirtyPixels = 0;
            for (int i = 0; i < _dirtyPixels.Length; i += 4)
                if (_dirtyPixels[i + 3] > 0)
                    _totalDirtyPixels += _dirtyPixels[i + 3] / 255f;
        }

        // Check if fully regrown — stop growing
        bool fullyGrown = true;
        for (int i = 0; i < _dirtyPixels.Length; i += 4)
        {
            if (_dirtPattern[i + 3] > 0 && _dirtyPixels[i + 3] < _dirtPattern[i + 3])
            {
                fullyGrown = false;
                break;
            }
        }
        if (fullyGrown) _dirtGrowing = false;
    }
}
