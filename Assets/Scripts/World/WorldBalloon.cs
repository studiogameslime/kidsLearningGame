using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A floating bubble in the World sky, matching the BubblePop game look.
/// Sine-wave float + breathing pulse. Tap to pop with shard/sparkle particles.
/// Respawns after 1 second at a random sky position.
/// </summary>
public class WorldBalloon : MonoBehaviour
{
    public Color bubbleColor;
    public string colorId; // discovery color ID (e.g. "Red", "Blue") for sound playback
    public float skyWidth;
    public float skyHeight;
    public float padding;
    public Sprite circleSprite;

    private RectTransform rt;
    private Image img;
    private Vector2 basePosition;
    private float phaseX;
    private float phaseY;
    private float freqX;
    private float freqY;
    private float ampX;
    private float ampY;
    private float breathPhase;
    private float breathFreq;
    private bool isPopped;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        img = GetComponent<Image>();
    }

    private bool _baseSet;

    /// <summary>
    /// Pre-set base position to avoid a visual jerk on the first frame.
    /// Call right after AddComponent, before Start() runs.
    /// </summary>
    public void SetBasePosition(Vector2 pos)
    {
        basePosition = pos;
        _baseSet = true;
    }

    private void Start()
    {
        if (!_baseSet)
            basePosition = rt.anchoredPosition;
        phaseX = Random.Range(0f, Mathf.PI * 2f);
        phaseY = Random.Range(0f, Mathf.PI * 2f);
        freqX = Random.Range(0.3f, 0.8f);
        freqY = Random.Range(0.5f, 1.2f);
        ampX = Random.Range(15f, 40f);
        ampY = Random.Range(10f, 25f);
        breathPhase = Random.Range(0f, Mathf.PI * 2f);
        breathFreq = Random.Range(2.5f, 3.5f);
        // Start with current time phase so sine wave begins at 0 offset
        float now = Time.time;
        phaseX = -now * freqX;
        phaseY = -now * freqY;
    }

    private void Update()
    {
        if (isPopped) return;

        float t = Time.time;
        float offsetX = Mathf.Sin(t * freqX + phaseX) * ampX;
        float offsetY = Mathf.Sin(t * freqY + phaseY) * ampY;
        rt.anchoredPosition = basePosition + new Vector2(offsetX, offsetY);

        // Breathing pulse
        float breath = 1f + Mathf.Sin(t * breathFreq + breathPhase) * 0.05f;
        transform.localScale = Vector3.one * breath;
    }

    public void Pop()
    {
        if (isPopped) return;
        isPopped = true;
        FirebaseAnalyticsManager.LogBalloonPopped();

        // Track bubble pop in analytics
        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
        {
            profile.analytics.totalBubblesPopped++;
            ProfileManager.Instance?.Save();
        }

        // Play color name sound
        if (!string.IsNullOrEmpty(colorId))
            SoundLibrary.PlayColorName(colorId);

        SpawnPopParticles();
        StartCoroutine(PopAndRespawn());
    }

    private IEnumerator PopAndRespawn()
    {
        // Quick scale up then vanish
        float elapsed = 0f;
        float dur = 0.08f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.3f, elapsed / dur);
            yield return null;
        }

        // Hide visually (don't deactivate — that kills all coroutines)
        SetVisibility(false);
        transform.localScale = Vector3.zero;

        yield return new WaitForSeconds(1f);
        Respawn();
    }

    private void Respawn()
    {
        float x = Random.Range(padding, skyWidth - padding);
        float y = Random.Range(skyHeight * 0.2f, skyHeight * 0.8f);
        basePosition = new Vector2(x, y);
        rt.anchoredPosition = basePosition;

        transform.localScale = Vector3.zero;
        SetVisibility(true);
        isPopped = false;
        StartCoroutine(ScaleIn());
    }

    private void SetVisibility(bool visible)
    {
        // Toggle all Image components (bubble + rim + shine + shinedot)
        var images = GetComponentsInChildren<Image>(true);
        foreach (var i in images)
            i.enabled = visible;
    }

    private IEnumerator ScaleIn()
    {
        float elapsed = 0f;
        float dur = 0.3f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float s = t < 0.7f
                ? Mathf.Lerp(0f, 1.15f, t / 0.7f)
                : Mathf.Lerp(1.15f, 1f, (t - 0.7f) / 0.3f);
            transform.localScale = Vector3.one * s;
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    // ── Pop Particles (same as BubblePop) ──

    private void SpawnPopParticles()
    {
        RectTransform parentRT = rt.parent as RectTransform;
        Vector2 localPos = rt.anchoredPosition;

        Color solidColor = new Color(bubbleColor.r, bubbleColor.g, bubbleColor.b, 1f);
        Color lighterColor = Color.Lerp(solidColor, Color.white, 0.4f);
        Color darkerColor = Color.Lerp(solidColor, Color.black, 0.2f);

        int shardCount = Random.Range(10, 15);
        int sparkleCount = Random.Range(6, 10);

        // Shards
        for (int i = 0; i < shardCount; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(400f, 800f);
            float size = Random.Range(14f, 30f);
            float lifetime = Random.Range(0.5f, 0.85f);
            Color shardColor = Color.Lerp(solidColor, lighterColor, Random.Range(0f, 0.5f));
            if (Random.value < 0.3f) shardColor = darkerColor;

            var shard = CreateShard(parentRT, localPos, size, shardColor);
            StartCoroutine(AnimateShard(shard, localPos, angle, speed, lifetime, 1200f));
        }

        // Sparkles
        for (int i = 0; i < sparkleCount; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(250f, 550f);
            float size = Random.Range(6f, 12f);
            float lifetime = Random.Range(0.25f, 0.5f);
            Color sparkColor = Color.Lerp(Color.white, lighterColor, Random.Range(0f, 0.3f));

            var sparkle = CreateShard(parentRT, localPos, size, sparkColor);
            StartCoroutine(AnimateShard(sparkle, localPos, angle, speed, lifetime, 400f));
        }
    }

    private RectTransform CreateShard(RectTransform parent, Vector2 pos, float size, Color color)
    {
        var go = new GameObject("Shard");
        go.transform.SetParent(parent, false);
        var shardRT = go.AddComponent<RectTransform>();
        shardRT.anchorMin = Vector2.zero;
        shardRT.anchorMax = Vector2.zero;
        shardRT.pivot = new Vector2(0.5f, 0.5f);
        shardRT.anchoredPosition = pos;
        shardRT.sizeDelta = new Vector2(size, size);
        shardRT.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        var shardImg = go.AddComponent<Image>();
        shardImg.color = color;
        if (circleSprite != null) shardImg.sprite = circleSprite;
        shardImg.raycastTarget = false;

        return shardRT;
    }

    private IEnumerator AnimateShard(RectTransform shardRT, Vector2 startPos, float angle,
        float speed, float lifetime, float gravity)
    {
        Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        float spinSpeed = Random.Range(-720f, 720f);
        float t = 0f;
        Image shardImg = shardRT.GetComponent<Image>();
        Color startColor = shardImg.color;
        Vector3 startScale = shardRT.localScale;

        while (t < lifetime)
        {
            t += Time.deltaTime;
            float progress = t / lifetime;

            velocity.y -= gravity * Time.deltaTime;
            startPos += velocity * Time.deltaTime;
            shardRT.anchoredPosition = startPos;

            shardRT.Rotate(0, 0, spinSpeed * Time.deltaTime);

            float fade = 1f - progress * progress;
            shardRT.localScale = startScale * Mathf.Lerp(1f, 0.2f, progress);
            shardImg.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * fade);

            yield return null;
        }

        Destroy(shardRT.gameObject);
    }
}
