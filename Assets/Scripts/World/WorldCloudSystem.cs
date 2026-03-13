using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns and recycles moving clouds in the World sky area.
/// Uses all 8 cloud sprites randomly with varied speed, scale, and position.
/// </summary>
public class WorldCloudSystem : MonoBehaviour
{
    [Header("Settings")]
    public RectTransform skyArea;
    public float worldWidth = 2000f;
    public int maxClouds = 5;
    public float minSpeed = 15f;
    public float maxSpeed = 40f;
    public float minScale = 0.6f;
    public float maxScale = 1.2f;
    public float spawnInterval = 3f;

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

        // Spawn initial clouds spread across the world
        int initial = Mathf.Min(maxClouds, 4);
        for (int i = 0; i < initial; i++)
        {
            float x = Random.Range(100f, worldWidth - 100f);
            SpawnCloud(x);
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

        // Spawn new clouds from the right
        if (activeClouds.Count < maxClouds)
        {
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0f;
                // Spawn just off the right edge
                float spawnX = worldWidth + Random.Range(50f, 200f);
                SpawnCloud(spawnX);
                // Randomize next interval
                spawnInterval = Random.Range(2f, 5f);
            }
        }
    }

    private void SpawnCloud(float x)
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

        float skyHeight = skyArea.rect.height;
        if (skyHeight <= 0) skyHeight = 600f;

        var go = new GameObject("Cloud");
        go.transform.SetParent(skyArea, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        float scale = Random.Range(minScale, maxScale);
        float w = sprite.rect.width * scale * 0.5f; // scaled pixel size (approx)
        float h = sprite.rect.height * scale * 0.5f;
        rt.sizeDelta = new Vector2(Mathf.Max(w, 120f), Mathf.Max(h, 60f));

        float y = Random.Range(skyHeight * 0.3f, skyHeight * 0.85f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.localScale = Vector3.one * scale;

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = true;
        img.color = Color.white; // tinted by environment

        var cloud = go.AddComponent<WorldCloud>();
        cloud.speed = Random.Range(minSpeed, maxSpeed);
        cloud.leftBound = -300f;

        activeClouds.Add(cloud);
    }
}
