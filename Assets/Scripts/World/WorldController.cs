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
    public Image profileAvatar;
    public TMPro.TextMeshProUGUI profileInitial;

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
        if (homeButton != null) homeButton.onClick.AddListener(OnHomePressed);
        if (gamesButton != null) gamesButton.onClick.AddListener(OnGamesPressed);
        if (parentAreaButton != null) parentAreaButton.onClick.AddListener(OnParentAreaPressed);

        // Hide header games button — game shelf in the world replaces it
        if (gamesButton != null)
            gamesButton.gameObject.SetActive(false);

        BuildAnimalSpriteLookup();
        UpdateProfileAvatar();
        BuildWorld();
        StartCoroutine(PlayWorldIntro());

        // Check for pending gift box reward
        if (rewardReveal != null)
            rewardReveal.CheckForPendingReward();
    }

    private IEnumerator PlayWorldIntro()
    {
        // Only play world intro sound once per profile (first visit)
        var profile = ProfileManager.ActiveProfile;
        if (profile != null && profile.journey.hasPlayedWorldIntroSound)
            yield break;

        var alin = AlinGuide.Instance;

        // "This is your world"
        var introClip = SoundLibrary.WorldIntro();
        if (introClip != null)
        {
            if (alin != null) alin.PlayTalking();
            BackgroundMusicManager.PlayOneShot(introClip);
            yield return new WaitForSeconds(introClip.length);
            if (alin != null) alin.StopTalking();
            yield return new WaitForSeconds(1f);
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }

        // "Here all your discovered animals and colors"
        var followUp = SoundLibrary.WorldAnimalsAndColors();
        if (followUp != null)
        {
            if (alin != null) alin.PlayTalking();
            BackgroundMusicManager.PlayOneShot(followUp);
            yield return new WaitForSeconds(followUp.length);
            if (alin != null) alin.StopTalking();
        }

        // Mark as played and save
        if (profile != null)
        {
            profile.journey.hasPlayedWorldIntroSound = true;
            ProfileManager.Instance.Save();
        }
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
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        if (profileAvatar != null)
            profileAvatar.color = profile.AvatarColor;
        if (profileInitial != null)
            profileInitial.text = profile.Initial;
    }

    private void BuildWorld()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        var jp = profile.journey;

        // Seed starters if world is visited before first journey
        if (jp.unlockedAnimalIds.Count == 0 && jp.unlockedGameIds.Count == 0)
        {
            string favAnimal = profile.favoriteAnimalId;
            if (string.IsNullOrEmpty(favAnimal)) favAnimal = "Cat";

            // Unlock starter games (needed for world menu to work)
            foreach (var id in DiscoveryCatalog.StarterGameIds)
                if (!jp.unlockedGameIds.Contains(id)) jp.unlockedGameIds.Add(id);

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

        // Size the world — fills viewport by default, expands only when needed
        int animalCount = jp.unlockedAnimalIds.Count;
        float viewportWidth = 1920f;
        var viewportParent = worldContent.parent as RectTransform;
        if (viewportParent != null && viewportParent.rect.width > 0)
            viewportWidth = viewportParent.rect.width;

        // Always fit viewport — no horizontal scrolling, animals squeeze to fit
        worldContent.anchorMin = Vector2.zero;
        worldContent.anchorMax = Vector2.one;
        worldContent.offsetMin = Vector2.zero;
        worldContent.offsetMax = Vector2.zero;
        float worldWidth = viewportWidth;

        // Update cloud system with world width
        if (cloudSystem != null)
            cloudSystem.worldWidth = worldWidth;

        // Set static exclusion zone for easel (used by WorldAnimal on drag)
        ExclusionCenterX = worldWidth * easelAnchorX;
        ExclusionHalfWidth = easelExclusionRadius;

        // Spawn animals evenly on grass
        SpawnAnimals(jp.unlockedAnimalIds, worldWidth);

        // Spawn balloons for unlocked colors
        SpawnBalloons(jp.unlockedColorIds, worldWidth);

        // Spawn game shelf on grass (right-center area)
        SpawnGameShelf(worldWidth);
    }

    private void SpawnAnimals(List<string> animalIds, float worldWidth)
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

            // Distribute animals evenly across the width with safe margins
            float usableWidth = worldWidth - worldPadding * 2;
            float x;
            if (animalIds.Count <= 1)
                x = worldWidth * 0.5f; // single animal centered
            else
                x = worldPadding + usableWidth * i / (animalIds.Count - 1);

            // Avoid easel exclusion zone — nudge animal to nearest safe edge
            float easelCenterX = worldWidth * easelAnchorX;
            float halfAnimal = animalSize * 0.5f;
            if (Mathf.Abs(x - easelCenterX) < easelExclusionRadius + halfAnimal)
            {
                float leftEdge = easelCenterX - easelExclusionRadius - halfAnimal;
                float rightEdge = easelCenterX + easelExclusionRadius + halfAnimal;
                // Nudge to whichever side is closer
                x = (x < easelCenterX) ? Mathf.Min(x, leftEdge) : Mathf.Max(x, rightEdge);
                // Clamp within world bounds
                x = Mathf.Clamp(x, worldPadding, worldWidth - worldPadding);
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

    private void SpawnBalloons(List<string> colorIds, float worldWidth)
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

                float x = Random.Range(worldPadding, worldWidth - worldPadding);
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
                balloon.skyWidth = worldWidth;
                balloon.skyHeight = skyHeight;
                balloon.padding = worldPadding;
                spawnedBalloons.Add(balloon);
            }
        }
    }

    private void SpawnGameShelf(float worldWidth)
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
        shadowRT.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRT.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRT.pivot = new Vector2(0.5f, 0.5f);
        shadowRT.sizeDelta = new Vector2(shelfSize * 0.75f, shelfSize * 0.18f);
        shadowRT.anchoredPosition = new Vector2(165.44f, 139f - 5f);
        var shadowImg = shadowGO.AddComponent<Image>();
        if (circleSprite != null) shadowImg.sprite = circleSprite;
        shadowImg.color = new Color(0f, 0f, 0f, 0.12f);
        shadowImg.raycastTarget = false;

        // Game shelf — anchored at center, matching Inspector values
        var go = new GameObject("GameShelf");
        go.transform.SetParent(grassArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(shelfSize, shelfSize);
        rt.anchoredPosition = new Vector2(165.44f, 139f);

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
            default:       return Color.white;
        }
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
}
