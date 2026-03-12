using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the scrollable world scene. Auto-lays out unlocked animals on grass
/// and spawns color balloons in the sky. No ScrollRect — uses WorldInputHandler.
/// </summary>
public class WorldController : MonoBehaviour
{
    [Header("Data")]
    public GameDatabase gameDatabase;
    public Sprite circleSprite;

    [Header("UI References")]
    public RectTransform worldContent;   // wide horizontal container
    public RectTransform skyArea;        // top 60% of content
    public RectTransform grassArea;      // bottom 40% of content
    public Button homeButton;
    public Image profileAvatar;
    public TMPro.TextMeshProUGUI profileInitial;

    [Header("Settings")]
    public float animalSpacing = 270f;
    public float worldPadding = 200f;
    public float animalSize = 200f;
    public float balloonSize = 100f;
    public int balloonsPerColor = 2;

    private List<WorldAnimal> spawnedAnimals = new List<WorldAnimal>();
    private List<WorldBalloon> spawnedBalloons = new List<WorldBalloon>();

    // Lookup built from all game sub-items: categoryKey (lowercase) → sprite
    private Dictionary<string, Sprite> _animalSprites;

    private void Start()
    {
        if (homeButton != null) homeButton.onClick.AddListener(OnHomePressed);

        BuildAnimalSpriteLookup();
        UpdateProfileAvatar();
        BuildWorld();
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

                // Store by lowercase key; prefer thumbnail for world display
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
        if (jp.unlockedAnimalIds.Count == 0)
        {
            foreach (var id in DiscoveryCatalog.StarterAnimals)
                if (!jp.unlockedAnimalIds.Contains(id)) jp.unlockedAnimalIds.Add(id);
            foreach (var id in DiscoveryCatalog.StarterColors)
                if (!jp.unlockedColorIds.Contains(id)) jp.unlockedColorIds.Add(id);
            foreach (var id in DiscoveryCatalog.StarterGameIds)
                if (!jp.unlockedGameIds.Contains(id)) jp.unlockedGameIds.Add(id);
            jp.gamesUntilNextDiscovery = 1;
            ProfileManager.Instance.Save();
        }

        // Size the world content
        int animalCount = jp.unlockedAnimalIds.Count;
        float worldWidth = Mathf.Max(1080f, animalCount * animalSpacing + worldPadding * 2);
        worldContent.sizeDelta = new Vector2(worldWidth, worldContent.sizeDelta.y);

        // Spawn animals evenly on grass
        SpawnAnimals(jp.unlockedAnimalIds, worldWidth);

        // Spawn balloons for unlocked colors
        SpawnBalloons(jp.unlockedColorIds, worldWidth);
    }

    private void SpawnAnimals(List<string> animalIds, float worldWidth)
    {
        if (grassArea == null) return;

        float grassHeight = grassArea.rect.height;
        if (grassHeight <= 0) grassHeight = 400f;

        for (int i = 0; i < animalIds.Count; i++)
        {
            string animalId = animalIds[i];

            var go = new GameObject($"Animal_{animalId}");
            go.transform.SetParent(grassArea, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(animalSize, animalSize);

            // Evenly spaced horizontally, random Y within grass
            float x = worldPadding + i * animalSpacing;
            float y = Random.Range(20f, grassHeight * 0.6f);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(x, y);

            var img = go.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = true;

            // Look up sprite from GameDatabase sub-items
            Sprite sprite = null;
            if (_animalSprites != null)
                _animalSprites.TryGetValue(animalId.ToLower(), out sprite);

            if (sprite != null)
                img.sprite = sprite;
            else
                img.color = new Color(0.8f, 0.6f, 0.4f); // brown placeholder

            // Load per-animal anim data on demand (only loads this animal's sprites)
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
            animal.groundY = y; // remember original spawn Y as ground level
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
            // Semi-transparent bubble color like BubblePop
            Color bubbleColor = new Color(solidColor.r, solidColor.g, solidColor.b, 0.7f);

            for (int j = 0; j < balloonsPerColor; j++)
            {
                float sizeVariation = balloonSize * Random.Range(0.85f, 1.15f);

                var go = new GameObject($"Bubble_{colorId}_{j}");
                go.transform.SetParent(skyArea, false);

                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(sizeVariation, sizeVariation);
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0.5f, 0.5f);

                float x = Random.Range(worldPadding, worldWidth - worldPadding);
                float y = Random.Range(skyHeight * 0.2f, skyHeight * 0.8f);
                rt.anchoredPosition = new Vector2(x, y);

                var img = go.AddComponent<Image>();
                img.color = bubbleColor;
                img.raycastTarget = true;
                if (circleSprite != null) img.sprite = circleSprite;

                // Rim (behind everything)
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

                // Shine highlight (top-left)
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

                var balloon = go.AddComponent<WorldBalloon>();
                balloon.bubbleColor = bubbleColor;
                balloon.circleSprite = circleSprite;
                balloon.skyWidth = worldWidth;
                balloon.skyHeight = skyHeight;
                balloon.padding = worldPadding;
                spawnedBalloons.Add(balloon);
            }
        }
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
        NavigationManager.GoToHome();
    }
}
