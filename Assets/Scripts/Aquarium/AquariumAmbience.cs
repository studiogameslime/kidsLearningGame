using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ambient underwater effects: floating bubbles with soft glow,
/// visible light rays from above, and subtle sparkle moments.
/// </summary>
public class AquariumAmbience : MonoBehaviour
{
    public RectTransform areaRT;
    public Sprite circleSprite;
    public AquariumController controller;

    private float bubbleTimer;
    private float sparkleTimer;
    private float bigBubbleTimer;

    private void Start()
    {
        for (int i = 0; i < 5; i++)
            CreateLightRay(i);

        bigBubbleTimer = Random.Range(0.5f, 1.5f);
    }

    private void Update()
    {
        // Small ambient bubbles
        bubbleTimer += Time.deltaTime;
        if (bubbleTimer >= Random.Range(0.13f, 0.27f))
        {
            bubbleTimer = 0f;
            SpawnBubble();
        }

        // Large poppable bubbles
        bigBubbleTimer -= Time.deltaTime;
        if (bigBubbleTimer <= 0f)
        {
            bigBubbleTimer = Random.Range(1f, 2.5f);
            SpawnPoppableBubble();
        }

        // Occasional sparkle
        sparkleTimer += Time.deltaTime;
        if (sparkleTimer >= Random.Range(2f, 4f))
        {
            sparkleTimer = 0f;
            SpawnSparkle();
        }
    }

    // ── Bubbles ──

    private void SpawnBubble()
    {
        if (areaRT == null) return;

        var go = new GameObject("Bubble");
        go.transform.SetParent(areaRT, false);

        var rt = go.AddComponent<RectTransform>();
        float size = Random.Range(5f, 22f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        Rect bounds = areaRT.rect;
        float x = Random.Range(bounds.xMin + 50f, bounds.xMax - 50f);
        float startY = bounds.yMin + Random.Range(10f, bounds.height * 0.3f);
        rt.anchoredPosition = new Vector2(x, startY);

        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        // Vary bubble color: mostly light blue-white, some with cyan tint
        float hueShift = Random.Range(0f, 1f);
        if (hueShift < 0.6f)
            img.color = new Color(0.85f, 0.95f, 1f, 0.35f);     // white-blue
        else if (hueShift < 0.85f)
            img.color = new Color(0.7f, 0.95f, 0.98f, 0.3f);    // soft cyan
        else
            img.color = new Color(0.9f, 0.98f, 1f, 0.45f);       // bright white
        img.raycastTarget = false;

        // Glow behind bigger bubbles
        if (size > 14f && circleSprite != null)
        {
            var glowGO = new GameObject("BubbleGlow");
            glowGO.transform.SetParent(go.transform, false);
            glowGO.transform.SetAsFirstSibling();
            var glowRT = glowGO.AddComponent<RectTransform>();
            glowRT.anchorMin = new Vector2(-0.4f, -0.4f);
            glowRT.anchorMax = new Vector2(1.4f, 1.4f);
            glowRT.offsetMin = Vector2.zero;
            glowRT.offsetMax = Vector2.zero;
            var glowImg = glowGO.AddComponent<Image>();
            glowImg.sprite = circleSprite;
            glowImg.color = new Color(0.8f, 0.95f, 1f, 0.1f);
            glowImg.raycastTarget = false;
        }

        StartCoroutine(AnimateBubble(rt, img, bounds));
    }

    private IEnumerator AnimateBubble(RectTransform rt, Image img, Rect bounds)
    {
        float dur = Random.Range(3f, 6f);
        float elapsed = 0f;
        Vector2 start = rt.anchoredPosition;
        float totalRise = bounds.height * Random.Range(0.6f, 0.95f);
        float swayPhase = Random.Range(0f, Mathf.PI * 2f);
        float swayAmp = Random.Range(8f, 20f);
        float baseAlpha = img.color.a;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float y = start.y + totalRise * t;
            float x = start.x + Mathf.Sin(swayPhase + t * Mathf.PI * 3f) * swayAmp;
            rt.anchoredPosition = new Vector2(x, y);

            // Slight grow as it rises
            float scale = 1f + t * 0.15f;
            rt.localScale = Vector3.one * scale;

            // Fade out in last 25%
            float alpha = t > 0.75f ? baseAlpha * (1f - (t - 0.75f) / 0.25f) : baseAlpha;
            var c = img.color;
            img.color = new Color(c.r, c.g, c.b, alpha);
            yield return null;
        }

        Destroy(rt.gameObject);
    }

    // ── Light Rays ──

    private void CreateLightRay(int index)
    {
        if (areaRT == null) return;

        var go = new GameObject($"LightRay_{index}");
        go.transform.SetParent(areaRT, false);

        var rt = go.AddComponent<RectTransform>();
        float width = Random.Range(80f, 180f);
        float height = areaRT.rect.height > 0 ? areaRT.rect.height * 1.3f : 1400f;
        rt.sizeDelta = new Vector2(width, height);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 1f);

        // Spread rays across the screen
        float xSpread = 600f;
        float xPos = -xSpread + (xSpread * 2f / 4f) * index + Random.Range(-60f, 60f);
        rt.anchoredPosition = new Vector2(xPos, height * 0.4f);
        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-20f, -5f));

        var img = go.AddComponent<Image>();
        // Warm white-yellow tint — like sunlight filtering through water
        img.color = new Color(1f, 0.98f, 0.88f, 0.07f);
        img.raycastTarget = false;

        StartCoroutine(AnimateLightRay(img, rt, index));
    }

    private IEnumerator AnimateLightRay(Image img, RectTransform rt, int index)
    {
        float phase = index * 1.7f;
        float baseAlpha = img.color.a;
        Vector2 basePos = rt.anchoredPosition;

        while (img != null)
        {
            float t = Time.time * 0.25f + phase;
            // Pulse alpha between 0.04 and 0.10
            float alpha = baseAlpha + 0.04f * Mathf.Sin(t);
            img.color = new Color(1f, 0.98f, 0.88f, alpha);

            // Very subtle horizontal drift
            float drift = Mathf.Sin(t * 0.7f) * 8f;
            rt.anchoredPosition = basePos + new Vector2(drift, 0);

            yield return null;
        }
    }

    // ── Sparkles ──

    private void SpawnSparkle()
    {
        if (areaRT == null || circleSprite == null) return;

        Rect bounds = areaRT.rect;
        float x = Random.Range(bounds.xMin + 100f, bounds.xMax - 100f);
        float y = Random.Range(bounds.yMin + 50f, bounds.yMax - 50f);

        var go = new GameObject("Sparkle");
        go.transform.SetParent(areaRT, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(6f, 6f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);

        var img = go.AddComponent<Image>();
        img.sprite = circleSprite;
        // Soft cyan or warm yellow sparkle
        img.color = Random.value > 0.5f
            ? new Color(0.7f, 1f, 1f, 0f)
            : new Color(1f, 0.95f, 0.7f, 0f);
        img.raycastTarget = false;

        StartCoroutine(AnimateSparkle(rt, img));
    }

    private IEnumerator AnimateSparkle(RectTransform rt, Image img)
    {
        Color c = img.color;
        float dur = Random.Range(0.8f, 1.5f);
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            // Fade in then out
            float alpha = Mathf.Sin(t * Mathf.PI) * 0.4f;
            img.color = new Color(c.r, c.g, c.b, alpha);
            // Subtle scale pulse
            float scale = 0.8f + Mathf.Sin(t * Mathf.PI) * 0.5f;
            rt.localScale = Vector3.one * scale;
            yield return null;
        }

        Destroy(rt.gameObject);
    }

    // ── Large Poppable Bubbles ──

    private void SpawnPoppableBubble()
    {
        if (areaRT == null || circleSprite == null) return;

        Rect bounds = areaRT.rect;
        float x = Random.Range(bounds.xMin + 80f, bounds.xMax - 80f);
        float startY = bounds.yMin + 30f;

        var go = new GameObject("PoppableBubble");
        go.transform.SetParent(areaRT, false);

        var rt = go.AddComponent<RectTransform>();
        float size = Random.Range(40f, 80f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, startY);

        // Main bubble
        var img = go.AddComponent<Image>();
        img.sprite = circleSprite;
        img.color = new Color(0.85f, 0.95f, 1f, 0.25f);
        img.raycastTarget = true; // tappable!

        // Shine highlight
        var shineGO = new GameObject("Shine");
        shineGO.transform.SetParent(go.transform, false);
        var shineRT = shineGO.AddComponent<RectTransform>();
        shineRT.anchorMin = new Vector2(0.2f, 0.55f);
        shineRT.anchorMax = new Vector2(0.5f, 0.85f);
        shineRT.offsetMin = Vector2.zero;
        shineRT.offsetMax = Vector2.zero;
        var shineImg = shineGO.AddComponent<Image>();
        shineImg.sprite = circleSprite;
        shineImg.color = new Color(1f, 1f, 1f, 0.3f);
        shineImg.raycastTarget = false;

        // Soft glow behind
        var glowGO = new GameObject("Glow");
        glowGO.transform.SetParent(go.transform, false);
        glowGO.transform.SetAsFirstSibling();
        var glowRT = glowGO.AddComponent<RectTransform>();
        glowRT.anchorMin = new Vector2(-0.2f, -0.2f);
        glowRT.anchorMax = new Vector2(1.2f, 1.2f);
        glowRT.offsetMin = Vector2.zero;
        glowRT.offsetMax = Vector2.zero;
        var glowImg = glowGO.AddComponent<Image>();
        glowImg.sprite = circleSprite;
        glowImg.color = new Color(0.8f, 0.93f, 1f, 0.1f);
        glowImg.raycastTarget = false;

        // Add pop behavior
        var popper = go.AddComponent<PoppableBubble>();
        popper.ambience = this;
        popper.controller = controller;

        StartCoroutine(RiseBubble(rt, img, bounds));
    }

    private IEnumerator RiseBubble(RectTransform rt, Image img, Rect bounds)
    {
        float speed = Random.Range(25f, 50f);
        float driftPhase = Random.Range(0f, Mathf.PI * 2f);
        float driftAmp = Random.Range(15f, 30f);
        float startX = rt.anchoredPosition.x;
        float elapsed = 0f;

        while (rt != null && rt.anchoredPosition.y < bounds.yMax + 30f)
        {
            elapsed += Time.deltaTime;
            Vector2 pos = rt.anchoredPosition;
            pos.y += speed * Time.deltaTime;
            pos.x = startX + Mathf.Sin(driftPhase + elapsed * 0.8f) * driftAmp;

            // Subtle scale breathing
            float pulse = 1f + Mathf.Sin(elapsed * 2f) * 0.04f;
            rt.localScale = Vector3.one * pulse;

            rt.anchoredPosition = pos;
            yield return null;
        }

        if (rt != null) Destroy(rt.gameObject);
    }

    /// <summary>Called by PoppableBubble when tapped. Spawns burst particles.</summary>
    public void PopBubbleAt(Vector2 pos, float size)
    {
        if (circleSprite == null) return;

        int count = Mathf.RoundToInt(size * 0.3f) + 3;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("PopParticle");
            go.transform.SetParent(areaRT, false);
            var rt = go.AddComponent<RectTransform>();
            float s = Random.Range(4f, 10f);
            rt.sizeDelta = new Vector2(s, s);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos + Random.insideUnitCircle * size * 0.3f;

            var img = go.AddComponent<Image>();
            img.sprite = circleSprite;
            img.color = new Color(0.85f, 0.95f, 1f, 0.6f);
            img.raycastTarget = false;

            StartCoroutine(AnimatePopParticle(rt, img, pos));
        }
    }

    private IEnumerator AnimatePopParticle(RectTransform rt, Image img, Vector2 origin)
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float speed = Random.Range(80f, 200f);
        Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        float lifetime = Random.Range(0.3f, 0.5f);
        float t = 0f;
        Color c = img.color;

        while (t < lifetime)
        {
            t += Time.deltaTime;
            float p = t / lifetime;
            vel.y += 50f * Time.deltaTime; // slight upward bias
            origin += vel * Time.deltaTime;
            vel *= 0.95f; // drag
            rt.anchoredPosition = origin;
            rt.localScale = Vector3.one * (1f - p * 0.7f);
            img.color = new Color(c.r, c.g, c.b, c.a * (1f - p));
            yield return null;
        }

        Destroy(rt.gameObject);
    }
}
