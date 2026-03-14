using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns and recycles moving clouds in the World sky area.
/// Uses all 8 cloud sprites randomly with varied speed, scale, height, and direction.
/// Clouds move left→right or right→left across the sky.
/// Enforces minimum spacing to prevent clustering.
/// </summary>
public class WorldCloudSystem : MonoBehaviour
{
    [Header("Settings")]
    public RectTransform skyArea;
    public float worldWidth = 2000f;
    public int maxClouds = 10;
    public float minSpeed = 12f;
    public float maxSpeed = 35f;
    public float minScale = 0.6f;
    public float maxScale = 1.3f;
    public float spawnInterval = 2f;

    [Header("Spacing")]
    public float minHorizontalSpacing = 250f;
    public float minVerticalSpacing = 80f;

    private Sprite[] cloudSprites;
    private List<WorldCloud> activeClouds = new List<WorldCloud>();
    private float spawnTimer;

    private void Start()
    {
        // Load all 8 cloud sprites from Art/World
        cloudSprites = new Sprite[8];
        for (int i = 1; i <= 8; i++)
        {
            cloudSprites[i - 1] = Resources.Load<Sprite>($"WorldArt/cloud{i}");
        }

        // Spawn initial clouds spread across the sky using even distribution
        float skyHeight = skyArea != null ? skyArea.rect.height : 600f;
        if (skyHeight <= 0) skyHeight = 600f;

        int initial = Mathf.Min(maxClouds, 8);
        for (int i = 0; i < initial; i++)
        {
            // Distribute initial clouds evenly across the width
            float x = 100f + (worldWidth - 200f) * i / Mathf.Max(1, initial - 1);
            // Stagger heights to avoid vertical stacking
            float yMin = skyHeight * 0.15f;
            float yMax = skyHeight * 0.90f;
            float y = Mathf.Lerp(yMin, yMax, (i % 4) / 3f) + Random.Range(-30f, 30f);
            y = Mathf.Clamp(y, yMin, yMax);

            bool goRight = Random.value > 0.5f;
            SpawnCloudAt(x, y, goRight);
        }
    }

    private void Update()
    {
        // Remove off-screen clouds
        for (int i = activeClouds.Count - 1; i >= 0; i--)
        {
            if (activeClouds[i] == null || activeClouds[i].IsOffScreen())
            {
                if (activeClouds[i] != null) Destroy(activeClouds[i].gameObject);
                activeClouds.RemoveAt(i);
            }
        }

        // Spawn new clouds from either edge
        if (activeClouds.Count < maxClouds)
        {
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0f;
                TrySpawnEdgeCloud();
                spawnInterval = Random.Range(1.5f, 4f);
            }
        }
    }

    private void TrySpawnEdgeCloud()
    {
        float skyHeight = skyArea != null ? skyArea.rect.height : 600f;
        if (skyHeight <= 0) skyHeight = 600f;

        bool goRight = Random.value > 0.5f;
        float spawnX = goRight
            ? -Random.Range(50f, 250f)
            : worldWidth + Random.Range(50f, 250f);

        // Try several Y positions to find one with good spacing
        for (int attempt = 0; attempt < 6; attempt++)
        {
            float y = Random.Range(skyHeight * 0.15f, skyHeight * 0.90f);
            if (HasGoodSpacing(spawnX, y))
            {
                SpawnCloudAt(spawnX, y, goRight);
                return;
            }
        }

        // Fallback: spawn anyway at best available Y
        float bestY = FindLeastCrowdedY(spawnX, skyHeight);
        SpawnCloudAt(spawnX, bestY, goRight);
    }

    private bool HasGoodSpacing(float x, float y)
    {
        foreach (var cloud in activeClouds)
        {
            if (cloud == null) continue;
            var pos = cloud.GetPosition();
            float dx = Mathf.Abs(pos.x - x);
            float dy = Mathf.Abs(pos.y - y);
            if (dx < minHorizontalSpacing && dy < minVerticalSpacing)
                return false;
        }
        return true;
    }

    private float FindLeastCrowdedY(float x, float skyHeight)
    {
        float yMin = skyHeight * 0.15f;
        float yMax = skyHeight * 0.90f;
        float bestY = (yMin + yMax) * 0.5f;
        float bestMinDist = 0f;

        // Sample several candidates and pick the one farthest from any neighbor
        for (int i = 0; i < 8; i++)
        {
            float candidateY = Mathf.Lerp(yMin, yMax, i / 7f);
            float minDist = float.MaxValue;
            foreach (var cloud in activeClouds)
            {
                if (cloud == null) continue;
                var pos = cloud.GetPosition();
                float dist = Mathf.Abs(pos.y - candidateY);
                if (Mathf.Abs(pos.x - x) < minHorizontalSpacing)
                    dist *= 0.5f; // penalize clouds that are also horizontally close
                if (dist < minDist) minDist = dist;
            }
            if (minDist > bestMinDist)
            {
                bestMinDist = minDist;
                bestY = candidateY;
            }
        }
        return bestY;
    }

    private void SpawnCloudAt(float x, float y, bool movingRight)
    {
        if (skyArea == null || cloudSprites == null) return;

        // Pick random sprite
        Sprite sprite = null;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var candidate = cloudSprites[Random.Range(0, cloudSprites.Length)];
            if (candidate != null) { sprite = candidate; break; }
        }
        if (sprite == null) return;

        var go = new GameObject("Cloud");
        go.transform.SetParent(skyArea, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        float scale = Random.Range(minScale, maxScale);
        float w = sprite.rect.width * scale * 0.5f;
        float h = sprite.rect.height * scale * 0.5f;
        rt.sizeDelta = new Vector2(Mathf.Max(w, 120f), Mathf.Max(h, 60f));
        rt.anchoredPosition = new Vector2(x, y);
        rt.localScale = Vector3.one * scale;

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = true;
        img.color = Color.white;

        var cloud = go.AddComponent<WorldCloud>();
        float speed = Random.Range(minSpeed, maxSpeed);
        cloud.speed = movingRight ? speed : -speed;
        cloud.leftBound = -300f;
        cloud.rightBound = worldWidth + 300f;

        activeClouds.Add(cloud);
    }
}
