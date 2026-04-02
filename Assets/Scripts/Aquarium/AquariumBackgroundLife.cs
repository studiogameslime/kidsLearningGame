using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns non-interactive background elements: small fish, plants, rocks/shells.
/// All elements are desaturated, semi-transparent, and never respond to input.
/// Creates depth and life without confusing the player.
/// </summary>
public class AquariumBackgroundLife : MonoBehaviour
{
    public RectTransform areaRT;
    public Sprite circleSprite;

    [Header("Settings")]
    public int bgFishCount = 4;
    public int bgPlantCount = 5;
    public int bgRockCount = 4;

    private List<RectTransform> bgFishList = new List<RectTransform>();
    private List<float> bgFishDirections = new List<float>(); // 1 = right, -1 = left

    private void Start()
    {
        if (areaRT == null) return;

        // Load sprites
        var fishSprites = Resources.LoadAll<Sprite>("Aquarium/Fish");
        var itemSprites = Resources.LoadAll<Sprite>("Aquarium/AquariumItem");

        SpawnBackgroundFish(fishSprites);
        SpawnBackgroundPlants(itemSprites);
        SpawnBackgroundRocks(itemSprites);
    }

    private void Update()
    {
        // Animate background fish — slow continuous swim
        Rect bounds = areaRT.rect;
        for (int i = 0; i < bgFishList.Count; i++)
        {
            var fishRT = bgFishList[i];
            if (fishRT == null) continue;
            float dir = bgFishDirections[i];

            var pos = fishRT.anchoredPosition;
            float speed = fishRT.sizeDelta.x * 0.3f;
            pos.x += speed * dir * Time.deltaTime;

            // Wrap around
            if (dir > 0 && pos.x > bounds.xMax + 50f)
                pos.x = bounds.xMin - 50f;
            else if (dir < 0 && pos.x < bounds.xMin - 50f)
                pos.x = bounds.xMax + 50f;

            // Subtle vertical bob
            pos.y += Mathf.Sin(Time.time * 0.8f + fishRT.GetHashCode() * 0.1f) * 0.3f;

            fishRT.anchoredPosition = pos;
        }
    }

    // ── Background Fish ──

    private void SpawnBackgroundFish(Sprite[] fishSprites)
    {
        if (fishSprites == null || fishSprites.Length == 0) return;
        Rect bounds = areaRT.rect;

        for (int i = 0; i < bgFishCount; i++)
        {
            var sprite = fishSprites[Random.Range(0, fishSprites.Length)];

            var go = new GameObject($"BgFish_{i}");
            go.transform.SetParent(areaRT, false);

            var rt = go.AddComponent<RectTransform>();
            float size = Random.Range(30f, 50f); // smaller than player fish (80)
            float aspect = sprite.rect.width / sprite.rect.height;
            rt.sizeDelta = new Vector2(size * aspect, size);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            float x = Random.Range(bounds.xMin + 50f, bounds.xMax - 50f);
            float y = Random.Range(bounds.yMin + bounds.height * 0.3f, bounds.yMax - bounds.height * 0.15f);
            rt.anchoredPosition = new Vector2(x, y);

            // Random direction: 1 = swim right, -1 = swim left
            float dir = Random.value > 0.5f ? 1f : -1f;
            // Fish sprite faces right by default, flip when swimming left
            rt.localScale = new Vector3(dir, 1, 1);

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = new Color(0.7f, 0.75f, 0.8f, 0.35f);

            bgFishList.Add(rt);
            bgFishDirections.Add(dir);
        }
    }

    // ── Background Plants ──

    private void SpawnBackgroundPlants(Sprite[] itemSprites)
    {
        if (itemSprites == null || itemSprites.Length == 0) return;
        Rect bounds = areaRT.rect;

        // Pick plant-like items (use a spread of item sprites)
        for (int i = 0; i < bgPlantCount; i++)
        {
            var sprite = itemSprites[Random.Range(0, itemSprites.Length)];

            var go = new GameObject($"BgPlant_{i}");
            go.transform.SetParent(areaRT, false);

            var rt = go.AddComponent<RectTransform>();
            float size = Random.Range(40f, 65f);
            float aspect = sprite.rect.width / sprite.rect.height;
            rt.sizeDelta = new Vector2(size * aspect, size);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f); // anchor to bottom

            // Place along the bottom (sand area)
            float x = Random.Range(bounds.xMin + 30f, bounds.xMax - 30f);
            float y = bounds.yMin + bounds.height * Random.Range(0.05f, 0.2f);
            rt.anchoredPosition = new Vector2(x, y);

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = new Color(0.65f, 0.7f, 0.7f, 0.4f);

            // Start idle sway
            StartCoroutine(PlantSway(rt, i));
        }
    }

    private IEnumerator PlantSway(RectTransform rt, int index)
    {
        float phase = index * 1.3f;
        float swayAmp = Random.Range(1.5f, 3f);
        float swaySpeed = Random.Range(0.6f, 1.2f);

        while (rt != null)
        {
            float angle = Mathf.Sin(Time.time * swaySpeed + phase) * swayAmp;
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            yield return null;
        }
    }

    // ── Background Rocks / Shells ──

    private void SpawnBackgroundRocks(Sprite[] itemSprites)
    {
        if (circleSprite == null) return;
        Rect bounds = areaRT.rect;

        for (int i = 0; i < bgRockCount; i++)
        {
            var go = new GameObject($"BgRock_{i}");
            go.transform.SetParent(areaRT, false);
            // Place rocks before plants in hierarchy (behind them)
            go.transform.SetSiblingIndex(0);

            var rt = go.AddComponent<RectTransform>();
            float w = Random.Range(20f, 45f);
            float h = Random.Range(14f, 28f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // Embed partially in sand
            float x = Random.Range(bounds.xMin + 40f, bounds.xMax - 40f);
            float y = bounds.yMin + bounds.height * Random.Range(0.08f, 0.16f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-10f, 10f));

            var img = go.AddComponent<Image>();
            img.sprite = circleSprite;
            img.raycastTarget = false;
            // Muted earthy tone, semi-transparent
            float grey = Random.Range(0.5f, 0.65f);
            img.color = new Color(grey, grey * 0.95f, grey * 0.85f, 0.3f);
        }
    }
}
