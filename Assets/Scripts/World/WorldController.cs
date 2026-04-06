using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the scrollable world scene. Auto-lays out unlocked animals on grass
/// and spawns color balloons in the sky. Uses layered backgrounds with day/night.
/// </summary>
public class WorldController : MonoBehaviour
{
    [Header("Data")]
    public GameDatabase gameDatabase;
    public Sprite circleSprite;

    [Header("UI References")]
    public RectTransform worldContent;   // wide horizontal container
    public RectTransform skyArea;        // sky portion of content
    public RectTransform grassArea;      // grass portion of content
    public Button homeButton;
    public Button gamesButton;
    public Button parentAreaButton;
    public Button albumButton;

    [Header("Star Display")]
    public TMPro.TextMeshProUGUI headerTitleTMP;

    [Header("Sun/Moon (fixed on screen)")]
    public RectTransform sunRT;
    public RectTransform moonRT;
    private float sunBaseX, moonBaseX;

    [Header("Screen Navigation")]
    public Button arrowLeftButton;
    public Button arrowRightButton;
    private int currentScreen = 1; // 0=left, 1=center, 2=right
    private const int TotalScreens = 3;


    [Header("Environment")]
    public WorldEnvironment environment;
    public WorldCloudSystem cloudSystem;

    [Header("Rewards")]
    public RewardRevealController rewardReveal;

    [Header("Game Shelf")]
    public Sprite gameShelfSprite; // "Games Collection" sprite from Art folder

    [Header("Settings")]
    public float animalSpacing = 320f;
    public float worldPadding = 250f;
    public float animalSize = 280f;
    public float balloonSize = 110f;
    public int balloonsPerColor = 1;
    public float shadowOffsetY = -8f;

    [Header("Exclusion Zones")]
    public float easelAnchorX = 0.06f;
    public float easelExclusionRadius = 160f;

    private List<WorldAnimal> spawnedAnimals = new List<WorldAnimal>();
    private List<WorldBalloon> spawnedBalloons = new List<WorldBalloon>();

    // Static exclusion zone for props (easel etc.) — used by WorldAnimal on drag release
    public static float ExclusionCenterX { get; private set; }
    public static float ExclusionHalfWidth { get; private set; }

    // Lookup built from all game sub-items: categoryKey (lowercase) → sprite
    private Dictionary<string, Sprite> _animalSprites;

    private void Start()
    {
        FirebaseAnalyticsManager.LogScreenView("world");
        FirebaseAnalyticsManager.UpdateUserProperties();

        // Home button removed from world header — profile switch is in parent dashboard settings
        if (homeButton != null) homeButton.gameObject.SetActive(false);
        if (gamesButton != null) gamesButton.onClick.AddListener(OnGamesPressed);
        if (parentAreaButton != null) parentAreaButton.onClick.AddListener(OnParentAreaPressed);
        if (albumButton != null) albumButton.onClick.AddListener(OnAlbumPressed);

        // Hide header games button — game shelf in the world replaces it
        if (gamesButton != null)
            gamesButton.gameObject.SetActive(false);

        BuildAnimalSpriteLookup();
        UpdateProfileAvatar();
        UpdateHeaderTitle();
        ApplyFeatureLocks();
        BuildWorld();

        // Wire screen navigation arrows
        if (arrowLeftButton != null) arrowLeftButton.onClick.AddListener(GoScreenLeft);
        if (arrowRightButton != null) arrowRightButton.onClick.AddListener(GoScreenRight);
        currentScreen = 1; // start on center screen
        SnapToScreen(currentScreen, false);
        UpdateArrowVisibility();

        // Store initial sun/moon X for counter-offset
        if (sunRT != null) sunBaseX = sunRT.anchoredPosition.x;
        if (moonRT != null) moonBaseX = moonRT.anchoredPosition.x;

        StartCoroutine(PlayWorldIntroThenGifts());
    }

    private GameObject _spotlightOverlay;
    private RawImage _spotlightImage;
    private RectTransform _overlayRT;
    private Texture2D _cutoutTexture;
    private Transform _alinOriginalParent;
    private int _alinOriginalSiblingIndex;

    private IEnumerator PlayWorldIntroThenGifts()
    {
        yield return StartCoroutine(PlayWorldIntro());

        // For returning visits (not first time), show gifts immediately
        var profile = ProfileManager.ActiveProfile;
        if (profile != null && profile.journey.hasPlayedWorldIntroSound)
        {
            if (rewardReveal != null)
                rewardReveal.CheckForPendingReward();
        }
    }

    private IEnumerator PlayWorldIntro()
    {
        // Only play world intro sound once per profile (first visit)
        var profile = ProfileManager.ActiveProfile;
        if (profile != null && profile.journey.hasPlayedWorldIntroSound)
            yield break;

        var alin = AlinGuide.Instance;

        // Block all input during intro
        EnsureSpotlightOverlay();
        ShowOverlayDark();
        LiftAlinAboveOverlay();

        // ── Step 1: "This is your world" ──
        var introClip = SoundLibrary.WorldIntro();
        if (introClip != null)
        {
            if (alin != null) alin.PlayTalking();
            BackgroundMusicManager.PlayOneShot(introClip);
            yield return new WaitForSeconds(introClip.length);
            if (alin != null) alin.StopTalking();
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }

        // ── Step 2: "Here all your discovered animals and colors" ──
        var followUp = SoundLibrary.WorldAnimalsAndColors();
        if (followUp != null)
        {
            if (alin != null) alin.PlayTalking();
            BackgroundMusicManager.PlayOneShot(followUp);
            yield return new WaitForSeconds(followUp.length);
            if (alin != null) alin.StopTalking();
        }

        yield return new WaitForSeconds(0.3f);

        // ── Step 3: Highlight ToyBox — "Here all the games" ──
        var toyBox = FindObjectOfType<WorldToyBox>();
        yield return StartCoroutine(LiftObjectStep(
            toyBox != null ? toyBox.gameObject : null,
            SoundLibrary.WorldAllGames()));

        // ── Step 5: Spawn first gift, then "Let's open your first gift!" ──
        if (rewardReveal != null)
        {
            // Spawn gift first (behind dark overlay)
            rewardReveal.CheckForPendingReward();
            yield return new WaitForSeconds(1.0f);

            // Now play the audio with spotlight on the gift
            var gift1 = FindObjectOfType<GiftBoxController>();
            if (gift1 != null)
            {
                ShowSpotlightOnTarget(gift1.GetComponent<RectTransform>(), 180f);

                // Play "let's open your first gift" while gift is visible
                var openClip = SoundLibrary.WorldOpenFirstGift();
                var alinRef = AlinGuide.Instance;
                if (openClip != null)
                {
                    if (alinRef != null) alinRef.PlayTalking();
                    BackgroundMusicManager.PlayOneShot(openClip);
                    yield return new WaitForSeconds(openClip.length + 0.3f);
                    if (alinRef != null) alinRef.StopTalking();
                }

                // Wait for gift to be opened
                while (FindObjectOfType<GiftBoxController>() != null)
                    yield return null;
            }
            // Stay dark while reveal animation plays
            ShowOverlayDark();
        }

        // Wait for reveal animation
        yield return new WaitForSeconds(2.5f);

        // ── Step 6: "You have another gift!" ──
        // Play audio while simultaneously watching for the second gift to appear.
        // Spotlight it the instant it spawns — no delay.
        {
            var alinRef2 = AlinGuide.Instance;
            var clip2 = SoundLibrary.WorldAnotherGift();
            EnsureSpotlightOverlay();
            ShowOverlayDark();

            if (clip2 != null)
            {
                if (alinRef2 != null) alinRef2.PlayTalking();
                BackgroundMusicManager.PlayOneShot(clip2);
            }

            // Wait for second gift to appear (check every frame), spotlight immediately
            GiftBoxController gift2 = null;
            float waitTime = 0f;
            float maxWait = (clip2 != null ? clip2.length + 2f : 5f);
            while (gift2 == null && waitTime < maxWait)
            {
                gift2 = FindObjectOfType<GiftBoxController>();
                if (gift2 == null) { yield return null; waitTime += Time.deltaTime; }
            }

            if (alinRef2 != null) alinRef2.StopTalking();

            if (gift2 != null)
            {
                ShowSpotlightOnTarget(gift2.GetComponent<RectTransform>(), 180f);
                while (FindObjectOfType<GiftBoxController>() != null)
                    yield return null;
            }
        }

        // Clean up
        RestoreAlin();
        if (_spotlightOverlay != null)
            Destroy(_spotlightOverlay);
        if (_cutoutTexture != null)
            Destroy(_cutoutTexture);

        // Mark as played and save
        if (profile != null)
        {
            profile.journey.hasPlayedWorldIntroSound = true;
            ProfileManager.Instance.Save();
        }
    }

    /// <summary>Dark overlay + Alin talks. No cutout. Overlay stays active after.</summary>
    private IEnumerator DarkOverlayStep(AudioClip clip)
    {
        yield return StartCoroutine(DarkOverlayStepKeepActive(clip));
    }

    /// <summary>Dark overlay + Alin talks. Overlay stays active (no hide).</summary>
    private IEnumerator DarkOverlayStepKeepActive(AudioClip clip)
    {
        var alin = AlinGuide.Instance;
        EnsureSpotlightOverlay();
        ShowOverlayDark();

        if (clip != null)
        {
            if (alin != null) alin.PlayTalking();
            BackgroundMusicManager.PlayOneShot(clip);
            yield return new WaitForSeconds(clip.length + 0.5f);
            if (alin != null) alin.StopTalking();
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }

        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>
    /// Lift an object above the overlay (fully visible), play a clip, then restore it.
    /// Dark overlay stays active throughout.
    /// </summary>
    private IEnumerator LiftObjectStep(GameObject obj, AudioClip clip)
    {
        var alin = AlinGuide.Instance;
        ShowOverlayDark();

        Transform origParent = null;
        int origSibling = 0;

        Graphic objGraphic = null;
        if (obj != null && _spotlightOverlay != null)
        {
            origParent = obj.transform.parent;
            origSibling = obj.transform.GetSiblingIndex();
            // Place above overlay but below Alin (Alin is last sibling)
            obj.transform.SetParent(_spotlightOverlay.transform.parent, true);
            int alinIdx = (alin != null) ? alin.transform.GetSiblingIndex() : -1;
            if (alinIdx >= 0)
                obj.transform.SetSiblingIndex(alinIdx); // just before Alin
            else
                obj.transform.SetAsLastSibling();

            // Disable interaction — display only
            objGraphic = obj.GetComponent<Graphic>();
            if (objGraphic != null) objGraphic.raycastTarget = false;
        }

        if (clip != null)
        {
            if (alin != null) alin.PlayTalking();
            BackgroundMusicManager.PlayOneShot(clip);
            yield return new WaitForSeconds(clip.length + 0.5f);
            if (alin != null) alin.StopTalking();
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }

        // Restore object to original parent
        if (obj != null && origParent != null)
        {
            if (objGraphic != null) objGraphic.raycastTarget = true;
            obj.transform.SetParent(origParent, true);
            obj.transform.SetSiblingIndex(origSibling);
        }

        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>Show overlay with cutout centered on target.</summary>
    private void ShowSpotlightOnTarget(RectTransform target, float radius)
    {
        Vector3 wp = target.TransformPoint(target.rect.center);
        Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, wp);
        ShowOverlayWithCutout(sp, radius);
    }

    /// <summary>Reparent Alin above the overlay so she's never darkened.</summary>
    private void LiftAlinAboveOverlay()
    {
        var alin = AlinGuide.Instance;
        if (alin == null || _spotlightOverlay == null) return;

        _alinOriginalParent = alin.transform.parent;
        _alinOriginalSiblingIndex = alin.transform.GetSiblingIndex();

        // Move to same parent as overlay, rendered after (on top of) it
        alin.transform.SetParent(_spotlightOverlay.transform.parent, true);
        alin.transform.SetAsLastSibling();
    }

    /// <summary>Restore Alin to her original parent.</summary>
    private void RestoreAlin()
    {
        var alin = AlinGuide.Instance;
        if (alin == null || _alinOriginalParent == null) return;

        alin.transform.SetParent(_alinOriginalParent, true);
        alin.transform.SetSiblingIndex(_alinOriginalSiblingIndex);
        _alinOriginalParent = null;
    }

    private IEnumerator SpotlightStep(RectTransform target, AudioClip clip, float spotlightRadius)
    {
        var alin = AlinGuide.Instance;

        if (target != null)
        {
            // Convert target center to screen position
            Vector3 worldPos = target.TransformPoint(target.rect.center);
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, worldPos);
            ShowOverlayWithCutout(screenPos, spotlightRadius);
        }
        else
        {
            ShowOverlayDark();
        }

        if (clip != null)
        {
            if (alin != null) alin.PlayTalking();
            BackgroundMusicManager.PlayOneShot(clip);
            yield return new WaitForSeconds(clip.length + 0.5f);
            if (alin != null) alin.StopTalking();
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }

        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>Show overlay fully dark (no cutout). Blocks all input.</summary>
    private void ShowOverlayDark()
    {
        EnsureSpotlightOverlay();
        // Solid dark texture
        int w = 64, h = 64;
        EnsureCutoutTexture(w, h);
        var pixels = _cutoutTexture.GetPixels32();
        Color32 dark = new Color32(0, 0, 0, 180);
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = dark;
        _cutoutTexture.SetPixels32(pixels);
        _cutoutTexture.Apply();
        _spotlightImage.texture = _cutoutTexture;
        _spotlightOverlay.SetActive(true);
    }

    /// <summary>
    /// Show overlay with a circular transparent cutout at screenPos.
    /// Generates a texture with alpha=0.7 everywhere except a soft circle.
    /// </summary>
    private void ShowOverlayWithCutout(Vector2 screenPos, float radius)
    {
        EnsureSpotlightOverlay();

        int texW = Screen.width / 4; // quarter-res for performance
        int texH = Screen.height / 4;
        float scale = texW / (float)Screen.width;

        EnsureCutoutTexture(texW, texH);

        float cx = screenPos.x * scale;
        float cy = screenPos.y * scale;
        float r = radius * scale;
        float rSq = r * r;
        // Soft edge: fully transparent inside (r*0.7), fade to dark at edge
        float innerR = r * 0.7f;
        float innerRSq = innerR * innerR;

        var pixels = _cutoutTexture.GetPixels32();
        Color32 dark = new Color32(0, 0, 0, 180);
        Color32 clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < texH; y++)
        {
            for (int x = 0; x < texW; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float distSq = dx * dx + dy * dy;

                if (distSq <= innerRSq)
                {
                    pixels[y * texW + x] = clear;
                }
                else if (distSq <= rSq)
                {
                    // Smooth fade from transparent to dark
                    float dist = Mathf.Sqrt(distSq);
                    float t = (dist - innerR) / (r - innerR);
                    byte a = (byte)(180 * t);
                    pixels[y * texW + x] = new Color32(0, 0, 0, a);
                }
                else
                {
                    pixels[y * texW + x] = dark;
                }
            }
        }

        _cutoutTexture.SetPixels32(pixels);
        _cutoutTexture.Apply();
        _spotlightImage.texture = _cutoutTexture;
        _spotlightOverlay.SetActive(true);
    }

    private void EnsureCutoutTexture(int w, int h)
    {
        if (_cutoutTexture != null && _cutoutTexture.width == w && _cutoutTexture.height == h)
            return;
        if (_cutoutTexture != null)
            Destroy(_cutoutTexture);
        _cutoutTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        _cutoutTexture.filterMode = FilterMode.Bilinear;
        _cutoutTexture.wrapMode = TextureWrapMode.Clamp;
    }

    private void EnsureSpotlightOverlay()
    {
        if (_spotlightOverlay != null) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        _spotlightOverlay = new GameObject("SpotlightOverlay");
        _spotlightOverlay.transform.SetParent(canvas.transform, false);
        _spotlightOverlay.transform.SetAsLastSibling();
        _overlayRT = _spotlightOverlay.AddComponent<RectTransform>();
        _overlayRT.anchorMin = Vector2.zero;
        _overlayRT.anchorMax = Vector2.one;
        _overlayRT.offsetMin = Vector2.zero;
        _overlayRT.offsetMax = Vector2.zero;

        // RawImage stretches a generated texture across the full screen
        _spotlightImage = _spotlightOverlay.AddComponent<RawImage>();
        _spotlightImage.raycastTarget = true; // blocks input during tutorial
    }

    /// <summary>
    /// Maps the profile avatar color hex (from onboarding) to a discovery color ID.
    /// Exact mapping for the 12 onboarding palette colors.
    /// </summary>
    private static string MapProfileColorToId(string colorHex)
    {
        if (string.IsNullOrEmpty(colorHex)) return "Blue";

        string hex = colorHex.ToUpperInvariant();
        switch (hex)
        {
            case "#EF9A9A": return "Red";
            case "#F48FB1": return "Pink";
            case "#CE93D8": return "Purple";
            case "#B39DDB": return "Purple";
            case "#90CAF9": return "Blue";
            case "#80DEEA": return "Cyan";
            case "#80CBC4": return "Green";
            case "#A5D6A7": return "Green";
            case "#FFF59D": return "Yellow";
            case "#FFCC80": return "Orange";
            case "#FFAB91": return "Orange";
            case "#BCAAA4": return "Brown";
        }

        Debug.LogWarning($"[StarterFlow] Unknown avatar color hex: {colorHex}, defaulting to Blue");
        return "Blue";
    }

    private void BuildAnimalSpriteLookup()
    {
        _animalSprites = new Dictionary<string, Sprite>();
        if (gameDatabase == null) return;

        foreach (var game in gameDatabase.games)
        {
            if (game.subItems == null) continue;
            foreach (var sub in game.subItems)
            {
                if (sub.thumbnail == null && sub.contentAsset == null) continue;
                string key = sub.categoryKey;
                if (string.IsNullOrEmpty(key)) continue;

                string lowerKey = key.ToLower();
                if (!_animalSprites.ContainsKey(lowerKey))
                    _animalSprites[lowerKey] = sub.thumbnail != null ? sub.thumbnail : sub.contentAsset;
            }
        }
    }

    private void UpdateProfileAvatar()
    {
        // Profile avatar removed from header — parent dashboard icon replaces it
    }

    private void BuildWorld()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        var jp = profile.journey;

        // Seed starters if world is visited before first journey
        if (jp.unlockedAnimalIds.Count == 0 && jp.pendingWorldRewards.Count == 0)
        {
            string favAnimal = profile.favoriteAnimalId;
            if (string.IsNullOrEmpty(favAnimal)) favAnimal = "Cat";

            // Determine balloon color from onboarding selection
            string profileColorId = MapProfileColorToId(profile.avatarColorHex);

            // DO NOT add animal/balloon to unlocked lists yet!
            // They will be unlocked when the child opens the gift boxes.
            // Queue first-time gifts only if not already queued
            if (jp.pendingWorldRewards.Count == 0)
            {
                jp.pendingWorldRewards.Add(new DiscoveryEntry { type = "animal", id = favAnimal });
                jp.pendingWorldRewards.Add(new DiscoveryEntry { type = "color", id = profileColorId });
                Debug.Log($"[StarterFlow] New profile detected. Queued gifts: animal={favAnimal}, balloon={profileColorId} (from color hex {profile.avatarColorHex})");
            }

            jp.gamesUntilNextDiscovery = 1;
            ProfileManager.Instance.Save();
        }

        // 3 screens side by side: left | center | right
        float viewportWidth = 1920f;
        var viewportParent = worldContent.parent as RectTransform;
        if (viewportParent != null && viewportParent.rect.width > 0)
            viewportWidth = viewportParent.rect.width;

        float worldWidth = viewportWidth * TotalScreens;
        worldContent.anchorMin = new Vector2(0, 0);
        worldContent.anchorMax = new Vector2(0, 1);
        worldContent.pivot = new Vector2(0, 0.5f);
        worldContent.sizeDelta = new Vector2(worldWidth, 0);
        worldContent.anchoredPosition = new Vector2(-viewportWidth, 0); // center screen

        if (cloudSystem != null)
            cloudSystem.worldWidth = worldWidth;

        // Screen layout:
        // Left (screen 0):  Gallery + Sticker Tree   → X: 0 to viewportWidth
        // Center (screen 1): ToyBox + GameShelf + Alin → X: viewportWidth to viewportWidth*2
        // Right (screen 2):  Animals + Balloons       → X: viewportWidth*2 to viewportWidth*3

        float centerOffset = viewportWidth;     // center screen X start
        float rightOffset = viewportWidth * 2f; // right screen X start

        // Easel exclusion zone (left screen)
        ExclusionCenterX = viewportWidth * easelAnchorX;
        ExclusionHalfWidth = easelExclusionRadius;

        // Animals on RIGHT screen
        SpawnAnimals(jp.unlockedAnimalIds, viewportWidth, rightOffset);

        // Balloons on RIGHT screen (above animals)
        SpawnBalloons(jp.unlockedColorIds, viewportWidth, rightOffset);

        // Game shelf removed — ToyBox opens game collection directly

        // Aquarium on CENTER screen
        SpawnAquarium(viewportWidth, centerOffset);

        // Sand Drawing sandbox on LEFT screen
        SpawnSandbox(viewportWidth, 0f);

        // Bubble Lab on LEFT screen
        // SpawnBubbleLab(viewportWidth, 0f); // hidden for now

        // Color Studio — hidden for now
        // SpawnColorStudio(viewportWidth, centerOffset);
    }

    private void SpawnAnimals(List<string> animalIds, float screenWidth, float xOffset = 0f)
    {
        if (grassArea == null) return;

        float grassHeight = grassArea.rect.height;
        if (grassHeight <= 0) grassHeight = 500f;

        // Place animals on the main lower grass field (below the tree ridge)
        float placementMinY = grassHeight * 0.18f;
        float placementMaxY = grassHeight * 0.32f;

        for (int i = 0; i < animalIds.Count; i++)
        {
            string animalId = animalIds[i];

            // Shadow first (renders behind animal)
            var shadowGO = new GameObject($"Shadow_{animalId}");
            shadowGO.transform.SetParent(grassArea, false);
            var shadowRT = shadowGO.AddComponent<RectTransform>();
            float shadowW = animalSize * 0.7f;
            float shadowH = animalSize * 0.18f;
            shadowRT.sizeDelta = new Vector2(shadowW, shadowH);
            shadowRT.anchorMin = Vector2.zero;
            shadowRT.anchorMax = Vector2.zero;
            shadowRT.pivot = new Vector2(0.5f, 0.5f);

            var shadowImg = shadowGO.AddComponent<Image>();
            if (circleSprite != null) shadowImg.sprite = circleSprite;
            shadowImg.color = new Color(0f, 0f, 0f, 0.15f);
            shadowImg.raycastTarget = false;

            // Animal
            var go = new GameObject($"Animal_{animalId}");
            go.transform.SetParent(grassArea, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(animalSize, animalSize);

            // Distribute animals evenly across screen width with offset
            float usableWidth = screenWidth - worldPadding * 2;
            float x;
            if (animalIds.Count <= 1)
                x = xOffset + screenWidth * 0.5f;
            else
                x = xOffset + worldPadding + usableWidth * i / (animalIds.Count - 1);

            // Avoid easel exclusion zone — nudge animal to nearest safe edge
            float easelCenterX = xOffset + screenWidth * easelAnchorX;
            float halfAnimal = animalSize * 0.5f;
            if (Mathf.Abs(x - easelCenterX) < easelExclusionRadius + halfAnimal)
            {
                float leftEdge = easelCenterX - easelExclusionRadius - halfAnimal;
                float rightEdge = easelCenterX + easelExclusionRadius + halfAnimal;
                // Nudge to whichever side is closer
                x = (x < easelCenterX) ? Mathf.Min(x, leftEdge) : Mathf.Max(x, rightEdge);
                // Clamp within world bounds
                x = Mathf.Clamp(x, xOffset + worldPadding, xOffset + screenWidth - worldPadding);
            }

            float y = Mathf.Lerp(placementMinY, placementMaxY, Random.Range(0f, 1f));
            // Alternate slight Y offset for visual interest
            if (i % 2 == 1) y += 15f;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(x, y);

            // Position shadow under animal
            shadowRT.anchoredPosition = new Vector2(x, y + shadowOffsetY);

            var img = go.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = true;

            Sprite sprite = null;
            if (_animalSprites != null)
                _animalSprites.TryGetValue(animalId.ToLower(), out sprite);

            if (sprite != null)
                img.sprite = sprite;
            else
                img.color = new Color(0.8f, 0.6f, 0.4f);

            // Load per-animal anim data
            var animData = AnimalAnimData.Load(animalId);
            if (animData != null && animData.idleFrames != null && animData.idleFrames.Length > 0)
            {
                var anim = go.AddComponent<UISpriteAnimator>();
                anim.targetImage = img;
                anim.idleFrames = animData.idleFrames;
                anim.floatingFrames = animData.floatingFrames;
                anim.successFrames = animData.successFrames;
                anim.framesPerSecond = animData.idleFps > 0 ? animData.idleFps : 30f;
            }

            var animal = go.AddComponent<WorldAnimal>();
            animal.animalId = animalId;
            animal.groundY = y;
            animal.shadowTransform = shadowRT;
            spawnedAnimals.Add(animal);
        }
    }

    private void SpawnBalloons(List<string> colorIds, float screenWidth, float xOffset = 0f)
    {
        if (skyArea == null) return;

        float skyHeight = skyArea.rect.height;
        if (skyHeight <= 0) skyHeight = 600f;

        foreach (var colorId in colorIds)
        {
            Color solidColor = GetColorById(colorId);
            Color bubbleColor = new Color(solidColor.r, solidColor.g, solidColor.b, 0.7f);

            for (int j = 0; j < balloonsPerColor; j++)
            {
                float sizeVariation = balloonSize * Random.Range(0.85f, 1.15f);

                var go = new GameObject($"Bubble_{colorId}_{j}");
                go.transform.SetParent(skyArea, false);

                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(sizeVariation, sizeVariation);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);

                float x = xOffset + Random.Range(worldPadding, screenWidth - worldPadding);
                float y = Random.Range(skyHeight * 0.15f, skyHeight * 0.65f);
                rt.anchoredPosition = new Vector2(x, y);

                var img = go.AddComponent<Image>();
                img.color = bubbleColor;
                img.raycastTarget = true;
                if (circleSprite != null) img.sprite = circleSprite;

                // Rim
                var rimGO = new GameObject("Rim");
                rimGO.transform.SetParent(go.transform, false);
                rimGO.transform.SetAsFirstSibling();
                var rimRT = rimGO.AddComponent<RectTransform>();
                rimRT.anchorMin = new Vector2(-0.03f, -0.03f);
                rimRT.anchorMax = new Vector2(1.03f, 1.03f);
                rimRT.offsetMin = Vector2.zero;
                rimRT.offsetMax = Vector2.zero;
                var rimImg = rimGO.AddComponent<Image>();
                if (circleSprite != null) rimImg.sprite = circleSprite;
                rimImg.color = new Color(bubbleColor.r * 0.7f, bubbleColor.g * 0.7f, bubbleColor.b * 0.7f, 0.3f);
                rimImg.raycastTarget = false;

                // Shine
                var shineGO = new GameObject("Shine");
                shineGO.transform.SetParent(go.transform, false);
                var shineRT = shineGO.AddComponent<RectTransform>();
                shineRT.anchorMin = new Vector2(0.15f, 0.55f);
                shineRT.anchorMax = new Vector2(0.45f, 0.85f);
                shineRT.offsetMin = Vector2.zero;
                shineRT.offsetMax = Vector2.zero;
                var shineImg = shineGO.AddComponent<Image>();
                if (circleSprite != null) shineImg.sprite = circleSprite;
                shineImg.color = new Color(1f, 1f, 1f, 0.4f);
                shineImg.raycastTarget = false;

                // Shine dot
                var shineDotGO = new GameObject("ShineDot");
                shineDotGO.transform.SetParent(go.transform, false);
                var shineDotRT = shineDotGO.AddComponent<RectTransform>();
                shineDotRT.anchorMin = new Vector2(0.25f, 0.45f);
                shineDotRT.anchorMax = new Vector2(0.35f, 0.55f);
                shineDotRT.offsetMin = Vector2.zero;
                shineDotRT.offsetMax = Vector2.zero;
                var shineDotImg = shineDotGO.AddComponent<Image>();
                if (circleSprite != null) shineDotImg.sprite = circleSprite;
                shineDotImg.color = new Color(1f, 1f, 1f, 0.6f);
                shineDotImg.raycastTarget = false;

                // Curly ribbon string below balloon
                var ribbonGO = new GameObject("Ribbon");
                ribbonGO.transform.SetParent(go.transform, false);
                var ribbonRT = ribbonGO.AddComponent<RectTransform>();
                ribbonRT.anchorMin = new Vector2(0.5f, 0f);
                ribbonRT.anchorMax = new Vector2(0.5f, 0f);
                ribbonRT.pivot = new Vector2(0.5f, 1f);
                ribbonRT.anchoredPosition = Vector2.zero;
                ribbonRT.sizeDelta = new Vector2(20f, sizeVariation * 0.975f);
                var ribbonString = ribbonGO.AddComponent<BalloonString>();
                Color ribbonColor = new Color(bubbleColor.r * 0.65f, bubbleColor.g * 0.65f, bubbleColor.b * 0.65f, 0.55f);
                ribbonString.ribbonColor = ribbonColor;

                var balloon = go.AddComponent<WorldBalloon>();
                balloon.bubbleColor = bubbleColor;
                balloon.colorId = colorId;
                balloon.circleSprite = circleSprite;
                balloon.skyWidth = screenWidth;
                balloon.skyHeight = skyHeight;
                balloon.padding = worldPadding;
                spawnedBalloons.Add(balloon);
            }
        }
    }

    private void SpawnAquarium(float screenWidth, float xOffset = 0f)
    {
        if (grassArea == null) return;

        float grassHeight = grassArea.rect.height;
        if (grassHeight <= 0) grassHeight = 500f;

        // Load aquarium icon sprite
        var sprites = Resources.LoadAll<Sprite>("Aquarium/AquariumIcon");
        Sprite aquariumSprite = null;
        if (sprites != null && sprites.Length > 0)
            aquariumSprite = sprites[0];
        if (aquariumSprite == null)
            aquariumSprite = Resources.Load<Sprite>("Aquarium/AquariumIcon");

        if (aquariumSprite == null)
        {
            Debug.LogWarning("[WorldController] AquariumIcon sprite not found in Resources/Aquarium/");
            return;
        }

        float aquariumSize = 180f;

        // Shadow behind the aquarium
        var shadowGO = new GameObject("AquariumShadow");
        shadowGO.transform.SetParent(grassArea, false);
        var shadowRT = shadowGO.AddComponent<RectTransform>();
        shadowRT.anchorMin = Vector2.zero;
        shadowRT.anchorMax = Vector2.zero;
        shadowRT.pivot = new Vector2(0.5f, 0.5f);
        shadowRT.sizeDelta = new Vector2(aquariumSize * 0.7f, aquariumSize * 0.18f);
        shadowRT.anchoredPosition = new Vector2(xOffset + screenWidth * 0.35f, 134f);
        var shadowImg = shadowGO.AddComponent<Image>();
        if (circleSprite != null) shadowImg.sprite = circleSprite;
        shadowImg.color = new Color(0f, 0f, 0f, 0.12f);
        shadowImg.raycastTarget = false;

        // Aquarium icon
        var go = new GameObject("Aquarium");
        go.transform.SetParent(grassArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(aquariumSize, aquariumSize);
        rt.anchoredPosition = new Vector2(xOffset + screenWidth * 0.35f, 139f);

        var img = go.AddComponent<Image>();
        img.sprite = aquariumSprite;
        img.preserveAspect = true;
        img.raycastTarget = true;

        var aquarium = go.AddComponent<WorldAquarium>();
        aquarium.circleSprite = circleSprite;
    }

    private void SpawnSandbox(float screenWidth, float xOffset = 0f)
    {
        if (grassArea == null) return;

        float grassHeight = grassArea.rect.height;
        if (grassHeight <= 0) grassHeight = 500f;

        float sandboxSize = 240f;

        // Shadow behind the sandbox icon
        var shadowGO = new GameObject("SandboxShadow");
        shadowGO.transform.SetParent(grassArea, false);
        var shadowRT = shadowGO.AddComponent<RectTransform>();
        shadowRT.anchorMin = Vector2.zero;
        shadowRT.anchorMax = Vector2.zero;
        shadowRT.pivot = new Vector2(0.5f, 0.5f);
        shadowRT.sizeDelta = new Vector2(sandboxSize * 0.7f, sandboxSize * 0.18f);
        shadowRT.anchoredPosition = new Vector2(xOffset + screenWidth * 0.7f, 134f);
        var shadowImg = shadowGO.AddComponent<Image>();
        if (circleSprite != null) shadowImg.sprite = circleSprite;
        shadowImg.color = new Color(0f, 0f, 0f, 0.12f);
        shadowImg.raycastTarget = false;

        // Sandbox icon — temporary circle placeholder with sand color
        var go = new GameObject("Sandbox");
        go.transform.SetParent(grassArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(sandboxSize, sandboxSize);
        rt.anchoredPosition = new Vector2(xOffset + screenWidth * 0.7f, 139f);

        var img = go.AddComponent<Image>();
        var sandboxSprite = Resources.Load<Sprite>("Sandbox/SandboxIcon");
        if (sandboxSprite == null)
        {
            // Fallback: try loading from Art folder path via LoadAll
            var allSprites = Resources.LoadAll<Sprite>("Sandbox");
            if (allSprites != null && allSprites.Length > 0) sandboxSprite = allSprites[0];
        }
        img.sprite = sandboxSprite != null ? sandboxSprite : circleSprite;
        img.color = Color.white;
        img.preserveAspect = true;
        img.raycastTarget = true;

        var sandbox = go.AddComponent<WorldSandbox>();
        sandbox.circleSprite = circleSprite;
    }

    private void SpawnBubbleLab(float screenWidth, float xOffset = 0f)
    {
        if (grassArea == null) return;

        float grassHeight = grassArea.rect.height;
        if (grassHeight <= 0) grassHeight = 500f;

        float labSize = 160f;

        // Shadow behind the lab icon
        var shadowGO = new GameObject("BubbleLabShadow");
        shadowGO.transform.SetParent(grassArea, false);
        var shadowRT = shadowGO.AddComponent<RectTransform>();
        shadowRT.anchorMin = Vector2.zero;
        shadowRT.anchorMax = Vector2.zero;
        shadowRT.pivot = new Vector2(0.5f, 0.5f);
        shadowRT.sizeDelta = new Vector2(labSize * 0.7f, labSize * 0.18f);
        shadowRT.anchoredPosition = new Vector2(xOffset + screenWidth * 0.35f, 134f);
        var shadowImg = shadowGO.AddComponent<Image>();
        if (circleSprite != null) shadowImg.sprite = circleSprite;
        shadowImg.color = new Color(0f, 0f, 0f, 0.12f);
        shadowImg.raycastTarget = false;

        // Bubble Lab icon — purple circle (lab theme)
        var go = new GameObject("BubbleLab");
        go.transform.SetParent(grassArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(labSize, labSize);
        rt.anchoredPosition = new Vector2(xOffset + screenWidth * 0.35f, 139f);

        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = new Color(0.45f, 0.30f, 0.70f); // purple lab color
        img.preserveAspect = true;
        img.raycastTarget = true;

        var bubbleLab = go.AddComponent<WorldBubbleLab>();
        bubbleLab.circleSprite = circleSprite;
    }

    private void SpawnColorStudio(float screenWidth, float xOffset = 0f)
    {
        if (grassArea == null) return;

        float studioSize = 170f;

        // Shadow
        var shadowGO = new GameObject("ColorStudioShadow");
        shadowGO.transform.SetParent(grassArea, false);
        var shadowRT = shadowGO.AddComponent<RectTransform>();
        shadowRT.anchorMin = Vector2.zero;
        shadowRT.anchorMax = Vector2.zero;
        shadowRT.pivot = new Vector2(0.5f, 0.5f);
        shadowRT.sizeDelta = new Vector2(studioSize * 0.7f, studioSize * 0.18f);
        shadowRT.anchoredPosition = new Vector2(xOffset + screenWidth * 0.65f, 134f);
        var shadowImg = shadowGO.AddComponent<Image>();
        if (circleSprite != null) shadowImg.sprite = circleSprite;
        shadowImg.color = new Color(0f, 0f, 0f, 0.12f);
        shadowImg.raycastTarget = false;

        // Color Studio icon — use a colored circle as placeholder
        var go = new GameObject("ColorStudio");
        go.transform.SetParent(grassArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(studioSize, studioSize);
        rt.anchoredPosition = new Vector2(xOffset + screenWidth * 0.65f, 139f);

        var img = go.AddComponent<Image>();
        // Try loading a dedicated icon, fall back to circle
        var iconSprites = Resources.LoadAll<Sprite>("ColorStudio/ColorStudioIcon");
        if (iconSprites != null && iconSprites.Length > 0)
            img.sprite = iconSprites[0];
        else if (circleSprite != null)
            img.sprite = circleSprite;
        img.preserveAspect = true;
        img.raycastTarget = true;
        img.color = new Color(0.95f, 0.75f, 0.3f); // warm yellow placeholder

        var studio = go.AddComponent<WorldColorStudio>();
        studio.circleSprite = circleSprite;
    }

    private void SpawnGameShelf(float screenWidth, float xOffset = 0f)
    {
        // Load from Resources if not set in Inspector
        if (gameShelfSprite == null)
            gameShelfSprite = Resources.Load<Sprite>("Games Collection");

        if (grassArea == null || gameShelfSprite == null) return;

        float grassHeight = grassArea.rect.height;
        if (grassHeight <= 0) grassHeight = 500f;

        float shelfSize = 200f;

        // Shadow behind the shelf
        var shadowGO = new GameObject("ShelfShadow");
        shadowGO.transform.SetParent(grassArea, false);
        var shadowRT = shadowGO.AddComponent<RectTransform>();
        shadowRT.anchorMin = Vector2.zero;
        shadowRT.anchorMax = Vector2.zero;
        shadowRT.pivot = new Vector2(0.5f, 0.5f);
        shadowRT.sizeDelta = new Vector2(shelfSize * 0.75f, shelfSize * 0.18f);
        shadowRT.anchoredPosition = new Vector2(xOffset + screenWidth * 0.6f, 134f);
        var shadowImg = shadowGO.AddComponent<Image>();
        if (circleSprite != null) shadowImg.sprite = circleSprite;
        shadowImg.color = new Color(0f, 0f, 0f, 0.12f);
        shadowImg.raycastTarget = false;

        // Game shelf — absolute positioning on grass
        var go = new GameObject("GameShelf");
        go.transform.SetParent(grassArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(shelfSize, shelfSize);
        rt.anchoredPosition = new Vector2(xOffset + screenWidth * 0.6f, 139f);

        var img = go.AddComponent<Image>();
        img.sprite = gameShelfSprite;
        img.preserveAspect = true;
        img.raycastTarget = true;

        go.AddComponent<WorldGameShelf>();
    }

    private Color GetColorById(string colorId)
    {
        switch (colorId)
        {
            case "Red":    return new Color(0.94f, 0.27f, 0.27f);
            case "Blue":   return new Color(0.23f, 0.51f, 0.96f);
            case "Yellow": return new Color(0.98f, 0.80f, 0.08f);
            case "Green":  return new Color(0.13f, 0.77f, 0.37f);
            case "Orange": return new Color(0.98f, 0.45f, 0.09f);
            case "Purple": return new Color(0.55f, 0.36f, 0.96f);
            case "Pink":   return new Color(0.93f, 0.29f, 0.60f);
            case "Cyan":   return new Color(0.02f, 0.71f, 0.83f);
            case "Brown":  return new Color(0.47f, 0.33f, 0.28f);
            case "Black":  return new Color(0.12f, 0.12f, 0.12f);
            case "White":  return new Color(0.95f, 0.95f, 0.95f);
            case "Grey":   return new Color(0.6f, 0.6f, 0.6f);
            default:       return Color.white;
        }
    }

    // ── Keep sun/moon visually fixed while worldContent scrolls ──
    // Sun/moon are in worldContent (correct z-order) with anchor (0,1).
    // To keep them visually fixed: X = desiredScreenX - worldContent.x
    private void LateUpdate()
    {
        if (worldContent == null) return;
        float contentX = worldContent.anchoredPosition.x; // negative when scrolled right

        if (sunRT != null && sunRT.gameObject.activeInHierarchy)
            sunRT.anchoredPosition = new Vector2(sunBaseX - contentX, sunRT.anchoredPosition.y);
        if (moonRT != null && moonRT.gameObject.activeInHierarchy)
            moonRT.anchoredPosition = new Vector2(moonBaseX - contentX, moonRT.anchoredPosition.y);
    }

    // ── Screen Navigation (arrows) ──

    public void GoScreenLeft()
    {
        if (currentScreen > 0)
        {
            currentScreen--;
            SnapToScreen(currentScreen, true);
            UpdateArrowVisibility();
        }
    }

    public void GoScreenRight()
    {
        if (currentScreen < TotalScreens - 1)
        {
            currentScreen++;
            SnapToScreen(currentScreen, true);
            UpdateArrowVisibility();
        }
    }

    private void SnapToScreen(int screenIndex, bool animate)
    {
        if (worldContent == null) return;

        float viewportWidth = 1920f;
        var viewportParent = worldContent.parent as RectTransform;
        if (viewportParent != null && viewportParent.rect.width > 0)
            viewportWidth = viewportParent.rect.width;

        float targetX = -screenIndex * viewportWidth;

        if (animate)
            StartCoroutine(AnimateToScreen(targetX));
        else
            worldContent.anchoredPosition = new Vector2(targetX, worldContent.anchoredPosition.y);
    }

    private System.Collections.IEnumerator AnimateToScreen(float targetX)
    {
        float startX = worldContent.anchoredPosition.x;
        float duration = 0.35f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);
            float x = Mathf.Lerp(startX, targetX, t);
            worldContent.anchoredPosition = new Vector2(x, worldContent.anchoredPosition.y);
            yield return null;
        }
        worldContent.anchoredPosition = new Vector2(targetX, worldContent.anchoredPosition.y);
    }

    private void UpdateArrowVisibility()
    {
        if (arrowLeftButton != null) arrowLeftButton.gameObject.SetActive(currentScreen > 0);
        if (arrowRightButton != null) arrowRightButton.gameObject.SetActive(currentScreen < TotalScreens - 1);
    }

    // ── Star Header & Feature Locks ──

    private void UpdateHeaderTitle()
    {
        if (headerTitleTMP == null) return;
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        string name = profile.displayName ?? "";
        int stars = profile.journey?.totalStars ?? 0;
        HebrewText.SetText(headerTitleTMP, $"\u05D4\u05E2\u05D5\u05DC\u05DD \u05E9\u05DC {name} \u2B50 {stars}");
    }

    private void ApplyFeatureLocks()
    {
        // Game Shelf
        var shelf = FindObjectOfType<WorldGameShelf>();
        if (shelf != null)
            ApplyLock(shelf.gameObject, FeatureUnlockManager.Feature.GameCollection);

        // Easel / Gallery
        var easel = FindObjectOfType<WorldEasel>();
        if (easel != null)
            ApplyLock(easel.gameObject, FeatureUnlockManager.Feature.Gallery);

        // Sticker Tree
        var tree = FindObjectOfType<StickerTreeController>();
        if (tree != null)
            ApplyLock(tree.gameObject, FeatureUnlockManager.Feature.StickerTree);
    }

    private void ApplyLock(GameObject featureGO, FeatureUnlockManager.Feature feature)
    {
        if (FeatureUnlockManager.IsUnlocked(feature)) return;

        // Disable interaction
        var buttons = featureGO.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        foreach (var btn in buttons) btn.interactable = false;

        // Dim the feature
        var images = featureGO.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        foreach (var img in images) img.color = new UnityEngine.Color(img.color.r, img.color.g, img.color.b, 0.45f);

        // Add lock overlay with progress
        var lockGO = new GameObject("LockOverlay");
        lockGO.transform.SetParent(featureGO.transform, false);
        var lockRT = lockGO.AddComponent<RectTransform>();
        lockRT.anchorMin = new Vector2(0.5f, 0.5f);
        lockRT.anchorMax = new Vector2(0.5f, 0.5f);
        lockRT.sizeDelta = new Vector2(160, 80);
        lockRT.anchoredPosition = new Vector2(0, -20);

        // Background
        var lockBg = lockGO.AddComponent<UnityEngine.UI.Image>();
        var roundedRect = Resources.Load<Sprite>("UI/RoundedRect");
        if (roundedRect != null) { lockBg.sprite = roundedRect; lockBg.type = UnityEngine.UI.Image.Type.Sliced; }
        lockBg.color = new UnityEngine.Color(0, 0, 0, 0.6f);
        lockBg.raycastTarget = false;

        // Lock icon + progress text
        var textGO = new GameObject("LockText");
        textGO.transform.SetParent(lockGO.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(8, 4); textRT.offsetMax = new Vector2(-8, -4);
        var lockTMP = textGO.AddComponent<TMPro.TextMeshProUGUI>();
        int remaining = FeatureUnlockManager.GetRemainingStars(feature);
        HebrewText.SetText(lockTMP, $"\uD83D\uDD12 \u05E2\u05D5\u05D3 {remaining} \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD"); // 🔒 עוד X משחקים
        lockTMP.fontSize = 16;
        lockTMP.fontStyle = TMPro.FontStyles.Bold;
        lockTMP.color = UnityEngine.Color.white;
        lockTMP.alignment = TMPro.TextAlignmentOptions.Center;
        lockTMP.raycastTarget = false;

        // Progress bar
        var barBgGO = new GameObject("ProgressBarBg");
        barBgGO.transform.SetParent(lockGO.transform, false);
        var barBgRT = barBgGO.AddComponent<RectTransform>();
        barBgRT.anchorMin = new Vector2(0.1f, 0); barBgRT.anchorMax = new Vector2(0.9f, 0);
        barBgRT.pivot = new Vector2(0.5f, 1f);
        barBgRT.anchoredPosition = new Vector2(0, -2);
        barBgRT.sizeDelta = new Vector2(0, 8);
        var barBgImg = barBgGO.AddComponent<UnityEngine.UI.Image>();
        barBgImg.color = new UnityEngine.Color(1, 1, 1, 0.2f);
        barBgImg.raycastTarget = false;

        var barFillGO = new GameObject("ProgressBarFill");
        barFillGO.transform.SetParent(barBgGO.transform, false);
        var barFillRT = barFillGO.AddComponent<RectTransform>();
        barFillRT.anchorMin = Vector2.zero;
        barFillRT.anchorMax = new Vector2(FeatureUnlockManager.GetProgress(feature), 1);
        barFillRT.offsetMin = Vector2.zero; barFillRT.offsetMax = Vector2.zero;
        var barFillImg = barFillGO.AddComponent<UnityEngine.UI.Image>();
        barFillImg.color = new UnityEngine.Color(1f, 0.85f, 0.2f, 0.9f); // gold
        barFillImg.raycastTarget = false;
    }

    public void OnHomePressed()
    {
        NavigationManager.GoToProfileSelection();
    }

    public void OnGamesPressed()
    {
        NavigationManager.GoToGamesCollection();
    }

    public void OnParentAreaPressed()
    {
        NavigationManager.GoToParentDashboard();
    }

    public void OnAlbumPressed()
    {
        var album = GetComponentInParent<Canvas>().GetComponent<CollectibleAlbumController>();
        if (album != null) album.Open();
    }
}
