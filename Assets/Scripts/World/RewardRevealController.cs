using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the gift box reward flow in the World scene.
/// Checks for pending rewards, spawns a gift box on the grass,
/// and reveals the reward (animal or balloon) when the box is opened.
/// </summary>
public class RewardRevealController : MonoBehaviour
{
    [Header("References")]
    public RectTransform grassArea;
    public RectTransform skyArea;
    public Sprite giftSprite;
    public Sprite circleSprite;
    public GameDatabase gameDatabase;

    [Header("Settings")]
    public float giftSize = 200f;

    private GiftBoxController activeGift;
    private Dictionary<string, Sprite> _animalSprites;

    /// <summary>
    /// Called by WorldController after BuildWorld. Checks for pending reward
    /// and spawns a gift box if one exists.
    /// </summary>
    public void CheckForPendingReward()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        var pending = profile.journey.pendingWorldReward;
        if (pending == null) return;

        // Only show gift for animal/color rewards (games don't appear in world)
        if (pending.type != "animal" && pending.type != "color") return;

        BuildAnimalSpriteLookup();
        StartCoroutine(SpawnGiftDelayed(pending));
    }

    private IEnumerator SpawnGiftDelayed(DiscoveryEntry reward)
    {
        // Brief delay so the world settles first
        yield return new WaitForSeconds(0.8f);
        SpawnGiftBox(reward);
    }

    private void SpawnGiftBox(DiscoveryEntry reward)
    {
        if (grassArea == null) return;

        float grassHeight = grassArea.rect.height;
        if (grassHeight <= 0) grassHeight = 500f;
        float grassWidth = grassArea.rect.width;
        if (grassWidth <= 0) grassWidth = 1920f;

        // Place near center of grass
        float x = grassWidth * Random.Range(0.4f, 0.6f);
        float y = grassHeight * 0.25f;

        // Glow (behind gift)
        var glowGO = new GameObject("GiftGlow");
        glowGO.transform.SetParent(grassArea, false);
        var glowRT = glowGO.AddComponent<RectTransform>();
        glowRT.anchorMin = Vector2.zero;
        glowRT.anchorMax = Vector2.zero;
        glowRT.pivot = new Vector2(0.5f, 0.5f);
        glowRT.anchoredPosition = new Vector2(x, y + giftSize * 0.3f);
        glowRT.sizeDelta = new Vector2(giftSize * 1.8f, giftSize * 1.8f);
        var glowImg = glowGO.AddComponent<Image>();
        if (circleSprite != null) glowImg.sprite = circleSprite;
        glowImg.color = new Color(1f, 0.95f, 0.7f, 0.2f);
        glowImg.raycastTarget = false;

        // Gift box
        var go = new GameObject("GiftBox");
        go.transform.SetParent(grassArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(giftSize, giftSize);

        // Start at scale 0, pop in
        rt.localScale = Vector3.zero;

        var img = go.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = true;
        if (giftSprite != null)
            img.sprite = giftSprite;

        var gift = go.AddComponent<GiftBoxController>();
        gift.boxImage = img;
        gift.glowImage = glowImg;
        gift.circleSprite = circleSprite;
        gift.reward = reward;
        gift.onRewardRevealed = OnGiftOpened;

        activeGift = gift;

        // Pop-in animation
        StartCoroutine(PopInGift(rt, glowRT));
    }

    private IEnumerator PopInGift(RectTransform rt, RectTransform glowRT)
    {
        // Glow fades in
        if (glowRT != null)
            glowRT.localScale = Vector3.zero;

        float t = 0f;
        float dur = 0.4f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;

            // Overshoot bounce
            float s;
            if (p < 0.6f)
                s = Mathf.Lerp(0f, 1.2f, p / 0.6f);
            else
                s = Mathf.Lerp(1.2f, 1f, (p - 0.6f) / 0.4f);

            rt.localScale = Vector3.one * s;
            if (glowRT != null)
                glowRT.localScale = Vector3.one * s * 1.8f;

            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private void OnGiftOpened(GiftBoxController gift)
    {
        if (gift.reward == null) return;

        Vector2 giftPos = gift.GetComponent<RectTransform>().anchoredPosition;

        if (gift.reward.type == "animal")
            StartCoroutine(RevealAnimal(gift.reward.id, giftPos));
        else if (gift.reward.type == "color")
            StartCoroutine(RevealBalloon(gift.reward.id, giftPos));

        // Clear the pending reward
        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
        {
            profile.journey.pendingWorldReward = null;
            ProfileManager.Instance.Save();
        }
    }

    // ── Animal Reward ──────────────────────────────────────────

    private IEnumerator RevealAnimal(string animalId, Vector2 fromPos)
    {
        if (grassArea == null) yield break;

        float grassHeight = grassArea.rect.height;
        float grassWidth = grassArea.rect.width;

        // Create the animal at the gift position
        var go = new GameObject($"Revealed_{animalId}");
        go.transform.SetParent(grassArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(280f, 280f);
        rt.anchoredPosition = new Vector2(fromPos.x, fromPos.y + 40f);
        rt.localScale = Vector3.zero;

        var img = go.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = true;

        // Load sprite
        Sprite sprite = null;
        if (_animalSprites != null)
            _animalSprites.TryGetValue(animalId.ToLower(), out sprite);

        var animData = AnimalAnimData.Load(animalId);
        if (sprite == null && animData != null && animData.idleFrames != null && animData.idleFrames.Length > 0)
            sprite = animData.idleFrames[0];

        if (sprite != null)
            img.sprite = sprite;
        else
            img.color = new Color(0.8f, 0.6f, 0.4f);

        // Add animator
        if (animData != null && animData.idleFrames != null && animData.idleFrames.Length > 0)
        {
            var anim = go.AddComponent<UISpriteAnimator>();
            anim.targetImage = img;
            anim.idleFrames = animData.idleFrames;
            anim.floatingFrames = animData.floatingFrames;
            anim.successFrames = animData.successFrames;
            anim.framesPerSecond = animData.idleFps > 0 ? animData.idleFps : 30f;
        }

        // Pop out of gift — scale from 0 to 1 with overshoot
        float t = 0f;
        float popDur = 0.35f;
        while (t < popDur)
        {
            t += Time.deltaTime;
            float p = t / popDur;
            float s = p < 0.7f
                ? Mathf.Lerp(0f, 1.25f, p / 0.7f)
                : Mathf.Lerp(1.25f, 1f, (p - 0.7f) / 0.3f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;

        // Play success animation
        var spriteAnim = go.GetComponent<UISpriteAnimator>();
        if (spriteAnim != null)
            spriteAnim.PlaySuccess();

        // Play animal name voice
        SoundLibrary.PlayAnimalName(animalId);

        // Happy bounce
        yield return HappyBounce(rt);

        // Sparkle burst
        SpawnCelebrationParticles(rt.anchoredPosition + new Vector2(0, 140f), 15);

        yield return new WaitForSeconds(0.8f);

        // Move to permanent position on the grass
        float targetX = grassWidth * Random.Range(0.2f, 0.8f);
        float targetY = grassHeight * Random.Range(0.18f, 0.32f);
        yield return MoveToPosition(rt, new Vector2(targetX, targetY), 0.6f);

        // Add WorldAnimal component for future interaction
        var animal = go.AddComponent<WorldAnimal>();
        animal.animalId = animalId;
        animal.groundY = targetY;

        // Return to idle
        if (spriteAnim != null)
            spriteAnim.PlayIdle();
    }

    // ── Balloon Reward ─────────────────────────────────────────

    private IEnumerator RevealBalloon(string colorId, Vector2 fromPos)
    {
        if (skyArea == null || grassArea == null) yield break;

        Color solidColor = GetColorById(colorId);
        Color bubbleColor = new Color(solidColor.r, solidColor.g, solidColor.b, 0.7f);

        float balloonSize = 110f * Random.Range(0.9f, 1.1f);

        // Create balloon at gift position (in grassArea initially)
        var go = new GameObject($"RevealedBalloon_{colorId}");
        go.transform.SetParent(grassArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(fromPos.x, fromPos.y + giftSize * 0.5f);
        rt.sizeDelta = new Vector2(balloonSize, balloonSize);
        rt.localScale = Vector3.zero;

        var img = go.AddComponent<Image>();
        img.color = bubbleColor;
        img.raycastTarget = true;
        if (circleSprite != null) img.sprite = circleSprite;

        // Add shine
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

        // Pop out from gift
        float t = 0f;
        float popDur = 0.3f;
        while (t < popDur)
        {
            t += Time.deltaTime;
            float p = t / popDur;
            float s = p < 0.7f
                ? Mathf.Lerp(0f, 1.15f, p / 0.7f)
                : Mathf.Lerp(1.15f, 1f, (p - 0.7f) / 0.3f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;

        SpawnCelebrationParticles(rt.anchoredPosition + new Vector2(0, 50f), 10);

        yield return new WaitForSeconds(0.3f);

        // Float upward with gentle sway toward sky
        float floatDur = 2f;
        t = 0f;
        Vector2 startPos = rt.anchoredPosition;
        float grassHeight = grassArea.rect.height;
        float skyHeight = skyArea != null ? skyArea.rect.height : 600f;
        float targetY = grassHeight + skyHeight * 0.5f;

        while (t < floatDur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / floatDur);
            float swayX = Mathf.Sin(t * 2.5f) * 30f;
            float y = Mathf.Lerp(startPos.y, targetY, p);
            rt.anchoredPosition = new Vector2(startPos.x + swayX, y);
            yield return null;
        }

        // Reparent to skyArea as a proper WorldBalloon
        float skyWidth = skyArea.rect.width;
        if (skyWidth <= 0) skyWidth = 1920f;

        go.transform.SetParent(skyArea, false);
        rt.anchoredPosition = new Vector2(
            Random.Range(200f, skyWidth - 200f),
            skyHeight * Random.Range(0.2f, 0.6f));

        // Add ribbon
        var ribbonGO = new GameObject("Ribbon");
        ribbonGO.transform.SetParent(go.transform, false);
        var ribbonRT = ribbonGO.AddComponent<RectTransform>();
        ribbonRT.anchorMin = new Vector2(0.5f, 0f);
        ribbonRT.anchorMax = new Vector2(0.5f, 0f);
        ribbonRT.pivot = new Vector2(0.5f, 1f);
        ribbonRT.anchoredPosition = Vector2.zero;
        ribbonRT.sizeDelta = new Vector2(20f, balloonSize * 0.975f);
        var ribbonString = ribbonGO.AddComponent<BalloonString>();
        ribbonString.ribbonColor = new Color(
            bubbleColor.r * 0.65f, bubbleColor.g * 0.65f, bubbleColor.b * 0.65f, 0.55f);

        // Add WorldBalloon for floating behavior
        var balloon = go.AddComponent<WorldBalloon>();
        balloon.bubbleColor = bubbleColor;
        balloon.circleSprite = circleSprite;
        balloon.skyWidth = skyWidth;
        balloon.skyHeight = skyHeight;
        balloon.padding = 200f;
    }

    // ── Helpers ─────────────────────────────────────────────────

    private IEnumerator HappyBounce(RectTransform rt)
    {
        // Jump up
        Vector2 start = rt.anchoredPosition;
        float jumpH = 60f;
        float t = 0f;
        float dur = 0.2f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float y = start.y + Mathf.Sin(p * Mathf.PI) * jumpH;
            rt.anchoredPosition = new Vector2(start.x, y);
            float sx = 1f - Mathf.Sin(p * Mathf.PI) * 0.1f;
            float sy = 1f + Mathf.Sin(p * Mathf.PI) * 0.15f;
            rt.localScale = new Vector3(sx, sy, 1f);
            yield return null;
        }
        rt.anchoredPosition = start;
        rt.localScale = Vector3.one;

        // Small secondary bounce
        t = 0f;
        dur = 0.15f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float y = start.y + Mathf.Sin(p * Mathf.PI) * jumpH * 0.3f;
            rt.anchoredPosition = new Vector2(start.x, y);
            yield return null;
        }
        rt.anchoredPosition = start;
    }

    private IEnumerator MoveToPosition(RectTransform rt, Vector2 target, float dur)
    {
        Vector2 start = rt.anchoredPosition;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / dur);
            rt.anchoredPosition = Vector2.Lerp(start, target, p);
            // Gentle bob during movement
            float bob = Mathf.Sin(t * 8f) * 5f * (1f - t / dur);
            rt.anchoredPosition += new Vector2(0, bob);
            yield return null;
        }
        rt.anchoredPosition = target;
    }

    private void SpawnCelebrationParticles(Vector2 pos, int count)
    {
        if (grassArea == null) return;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("CelebPart");
            go.transform.SetParent(grassArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            float size = Random.Range(8f, 20f);
            rt.sizeDelta = new Vector2(size, size);

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            if (circleSprite != null) img.sprite = circleSprite;

            // Mix of gold, white, and profile-color sparkles
            Color c;
            float r = Random.value;
            if (r < 0.4f)
                c = new Color(1f, 0.85f, 0.3f); // gold
            else if (r < 0.7f)
                c = Color.white;
            else
            {
                var profile = ProfileManager.ActiveProfile;
                c = profile != null ? profile.AvatarColor : new Color(1f, 0.7f, 0.3f);
            }
            img.color = c;

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(200f, 500f);
            float lifetime = Random.Range(0.5f, 0.9f);
            StartCoroutine(AnimateParticle(rt, img, pos, angle, speed, lifetime));
        }
    }

    private IEnumerator AnimateParticle(RectTransform rt, Image img, Vector2 startPos,
        float angle, float speed, float lifetime)
    {
        Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        Color startColor = img.color;
        float t = 0f;

        while (t < lifetime)
        {
            t += Time.deltaTime;
            float p = t / lifetime;
            vel.y -= 600f * Time.deltaTime;
            startPos += vel * Time.deltaTime;
            rt.anchoredPosition = startPos;
            rt.localScale = Vector3.one * (1f - p * 0.5f);
            img.color = new Color(startColor.r, startColor.g, startColor.b, 1f - p);
            yield return null;
        }

        Destroy(rt.gameObject);
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
}
