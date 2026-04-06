using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Bubble Lab sandbox controller — kids pick colors and create bubbles that float,
/// wobble, bounce, merge, and pop with satisfying particles.
/// All visuals procedural — no external art required.
/// Pure sandbox: NO scoring, NO rounds, NO BaseMiniGame.
/// </summary>
public class BubbleLabController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("UI References")]
    public RectTransform playArea;
    public Button backButton;

    [Header("Settings")]
    public int maxBubbles = 30;
    public float minBubbleRadius = 40f;
    public float maxBubbleRadius = 140f;
    public float longPressDuration = 0.4f;
    public float mergeOverlapRatio = 0.5f;

    // Color palette
    private static readonly Color[] Palette = {
        new Color(0.95f, 0.25f, 0.25f),   // Red
        new Color(0.25f, 0.55f, 0.95f),   // Blue
        new Color(0.95f, 0.85f, 0.15f),   // Yellow
        new Color(0.25f, 0.85f, 0.40f),   // Green
        new Color(0.70f, 0.30f, 0.85f),   // Purple
        new Color(0.95f, 0.55f, 0.15f),   // Orange
    };

    private int selectedColorIndex = 0;
    private List<Bubble> activeBubbles = new List<Bubble>();
    private Sprite circleSprite;
    private RectTransform canvasRT;

    // Input state
    private bool isPressing;
    private float pressStartTime;
    private Vector2 pressStartPos;
    private int pressPointerId = -1;
    private GameObject previewGO;
    private RectTransform previewRT;
    private Image previewImage;
    private bool draggedBeyondThreshold;

    // Color selector references
    private Image[] selectorImages;
    private GameObject[] selectorGlows;

    // Background particles
    private RectTransform bgParticleContainer;

    // Pop particle container (renders above bubbles)
    private RectTransform particleContainer;

    // Bubble container
    private RectTransform bubbleContainer;

    private class Bubble
    {
        public GameObject go;
        public RectTransform rt;
        public Image mainImage;
        public Image shineImage;
        public Image rimImage;
        public Vector2 velocity;
        public float radius;
        public float wobblePhase;
        public float wobbleFreq;
        public float wobbleAmp;
        public Color color;
        public bool popping;
        public float age;
        public float lifetime; // bubbles pop after this many seconds
    }

    private void Start()
    {
        canvasRT = GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        circleSprite = CreateCircleSprite(64);

        // Create bubble container
        var bubbleContGO = new GameObject("BubbleContainer");
        bubbleContGO.transform.SetParent(playArea, false);
        bubbleContainer = bubbleContGO.AddComponent<RectTransform>();
        StretchFull(bubbleContainer);

        // Create particle container (above bubbles)
        var particleContGO = new GameObject("ParticleContainer");
        particleContGO.transform.SetParent(playArea, false);
        particleContainer = particleContGO.AddComponent<RectTransform>();
        StretchFull(particleContainer);

        // Create background particle container (behind bubbles)
        var bgContGO = new GameObject("BgParticles");
        bgContGO.transform.SetParent(playArea, false);
        bgParticleContainer = bgContGO.AddComponent<RectTransform>();
        StretchFull(bgParticleContainer);
        bgContGO.transform.SetAsFirstSibling();

        CreateColorSelector();
        StartCoroutine(SpawnBackgroundParticles());

        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);

        // Disable finger trail in this scene
        FingerTrail.SetEnabled(false);

        _sessionStart = Time.realtimeSinceStartup;
        FirebaseAnalyticsManager.LogScreenView("bubble_lab");
    }

    private float _sessionStart;

    private void Update()
    {
        float dt = Time.deltaTime;
        Rect area = playArea.rect;

        // Update all bubbles
        for (int i = activeBubbles.Count - 1; i >= 0; i--)
        {
            var b = activeBubbles[i];
            if (b.popping) continue;

            b.age += dt;

            // Float upward
            b.velocity.y += 20f * dt; // gentle upward acceleration
            b.velocity.y = Mathf.Clamp(b.velocity.y, 30f, 80f);

            // Sine wobble on X
            b.wobblePhase += b.wobbleFreq * dt;
            float wobbleX = Mathf.Sin(b.wobblePhase) * b.wobbleAmp;

            // Apply velocity
            Vector2 pos = b.rt.anchoredPosition;
            pos.x += wobbleX * dt;
            pos.y += b.velocity.y * dt;

            // Edge bounce (left/right)
            float leftBound = area.xMin + b.radius;
            float rightBound = area.xMax - b.radius;
            if (pos.x < leftBound) { pos.x = leftBound; b.wobbleAmp *= -1f; }
            if (pos.x > rightBound) { pos.x = rightBound; b.wobbleAmp *= -1f; }

            b.rt.anchoredPosition = pos;

            // Pop if floated off top
            float topBound = area.yMax + b.radius;
            if (pos.y > topBound)
            {
                PopBubbleSilent(b);
                activeBubbles.RemoveAt(i);
                continue;
            }

            // Pop if exceeded lifetime
            if (b.age > b.lifetime)
            {
                PopBubble(b);
                activeBubbles.RemoveAt(i);
                continue;
            }

            // Fade slightly near end of life
            if (b.age > b.lifetime - 2f)
            {
                float fade = (b.lifetime - b.age) / 2f;
                var c = b.mainImage.color;
                b.mainImage.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.3f, 0.7f, fade));
            }
        }

        // Mutual repulsion between nearby bubbles
        for (int i = 0; i < activeBubbles.Count; i++)
        {
            var a = activeBubbles[i];
            if (a.popping) continue;

            for (int j = i + 1; j < activeBubbles.Count; j++)
            {
                var bub = activeBubbles[j];
                if (bub.popping) continue;

                Vector2 diff = a.rt.anchoredPosition - bub.rt.anchoredPosition;
                float dist = diff.magnitude;
                float minDist = a.radius + bub.radius;

                if (dist < minDist && dist > 0.01f)
                {
                    Vector2 pushDir = diff.normalized;
                    float overlap = minDist - dist;
                    float pushForce = overlap * 0.5f;

                    a.rt.anchoredPosition += pushDir * pushForce * dt * 8f;
                    bub.rt.anchoredPosition -= pushDir * pushForce * dt * 8f;
                }
            }
        }

        // Check merges
        CheckMerge();

        // Update press preview
        if (isPressing && previewGO != null)
        {
            float holdTime = Time.time - pressStartTime;
            if (holdTime > 0.15f)
            {
                previewGO.SetActive(true);
                float t = Mathf.Clamp01((holdTime - 0.15f) / longPressDuration);
                float previewRadius = Mathf.Lerp(minBubbleRadius, maxBubbleRadius, t);
                previewRT.sizeDelta = new Vector2(previewRadius * 2f, previewRadius * 2f);

                // Pulse effect
                float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.05f;
                previewRT.localScale = Vector3.one * pulse;
            }
        }
    }

    // ── Input Handling ──

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isPressing) return;

        // Check if tapping an existing bubble to pop it
        Bubble hitBubble = GetBubbleAtScreenPos(eventData.position);
        if (hitBubble != null)
        {
            PopBubble(hitBubble);
            activeBubbles.Remove(hitBubble);
            return;
        }

        isPressing = true;
        pressStartTime = Time.time;
        pressPointerId = eventData.pointerId;
        draggedBeyondThreshold = false;

        // Convert to play area local position
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playArea, eventData.position, null, out localPos);
        pressStartPos = localPos;

        // Create preview bubble
        CreatePreview(localPos);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isPressing || eventData.pointerId != pressPointerId) return;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playArea, eventData.position, null, out localPos);

        float dist = Vector2.Distance(localPos, pressStartPos);
        if (dist > 30f) draggedBeyondThreshold = true;

        // Move preview to follow finger
        if (previewRT != null)
            previewRT.anchoredPosition = localPos;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPressing || eventData.pointerId != pressPointerId) return;
        isPressing = false;
        pressPointerId = -1;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playArea, eventData.position, null, out localPos);

        float holdTime = Time.time - pressStartTime;
        float t = Mathf.Clamp01((holdTime - 0.15f) / longPressDuration);
        float radius = Mathf.Lerp(minBubbleRadius, maxBubbleRadius, Mathf.Max(0f, t));

        // Destroy preview
        if (previewGO != null)
        {
            Destroy(previewGO);
            previewGO = null;
            previewRT = null;
            previewImage = null;
        }

        // Spawn bubbles — short tap creates a burst of small ones, long press creates one big one
        Color color = Palette[selectedColorIndex];
        if (holdTime < 0.25f)
        {
            // Quick tap: burst of 3-5 small bubbles
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                float r = Random.Range(minBubbleRadius * 0.6f, minBubbleRadius * 1.2f);
                Vector2 offset = new Vector2(Random.Range(-60f, 60f), Random.Range(-40f, 40f));
                CreateBubble(localPos + offset, color, r);
            }
        }
        else
        {
            // Long press: one big bubble
            CreateBubble(localPos, color, radius);
        }
    }

    // ── Bubble Creation ──

    private void CreateBubble(Vector2 position, Color color, float radius)
    {
        // Enforce max bubble limit
        while (activeBubbles.Count >= maxBubbles)
        {
            var oldest = activeBubbles[0];
            PopBubbleSilent(oldest);
            activeBubbles.RemoveAt(0);
        }

        var bubble = new Bubble();
        bubble.color = color;
        bubble.radius = radius;
        bubble.wobblePhase = Random.Range(0f, Mathf.PI * 2f);
        bubble.wobbleFreq = Random.Range(1.5f, 3.5f);
        bubble.wobbleAmp = Random.Range(40f, 100f);
        bubble.velocity = new Vector2(Random.Range(-40f, 40f), Random.Range(40f, 90f));
        bubble.lifetime = Random.Range(8f, 15f);
        bubble.age = 0f;

        // Main bubble GameObject
        var go = new GameObject("Bubble");
        go.transform.SetParent(bubbleContainer, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(radius * 2f, radius * 2f);
        rt.localScale = Vector3.zero; // start invisible for pop-in

        var img = go.AddComponent<Image>();
        img.sprite = circleSprite;
        Color bubbleColor = new Color(color.r, color.g, color.b, 0.4f); // more transparent/glassy
        img.color = bubbleColor;
        img.raycastTarget = false;

        bubble.go = go;
        bubble.rt = rt;
        bubble.mainImage = img;

        // Rim (lighter ring) — slightly larger circle behind, or same size with lighter color
        var rimGO = new GameObject("Rim");
        rimGO.transform.SetParent(go.transform, false);
        var rimRT = rimGO.AddComponent<RectTransform>();
        rimRT.anchorMin = Vector2.zero;
        rimRT.anchorMax = Vector2.one;
        rimRT.offsetMin = new Vector2(-3f, -3f);
        rimRT.offsetMax = new Vector2(3f, 3f);
        var rimImg = rimGO.AddComponent<Image>();
        rimImg.sprite = circleSprite;
        Color rimColor = Color.Lerp(color, Color.white, 0.5f);
        rimImg.color = new Color(rimColor.r, rimColor.g, rimColor.b, 0.35f);
        rimImg.raycastTarget = false;
        rimGO.transform.SetAsFirstSibling(); // behind main
        bubble.rimImage = rimImg;

        // Shine highlight — small white ellipse at top-left
        var shineGO = new GameObject("Shine");
        shineGO.transform.SetParent(go.transform, false);
        var shineRT = shineGO.AddComponent<RectTransform>();
        shineRT.anchorMin = new Vector2(0.2f, 0.55f);
        shineRT.anchorMax = new Vector2(0.55f, 0.85f);
        shineRT.offsetMin = Vector2.zero;
        shineRT.offsetMax = Vector2.zero;
        var shineImg = shineGO.AddComponent<Image>();
        shineImg.sprite = circleSprite;
        shineImg.color = new Color(1f, 1f, 1f, 0.6f); // brighter shine
        shineImg.raycastTarget = false;
        bubble.shineImage = shineImg;

        // Second smaller shine dot
        var shineDotGO = new GameObject("ShineDot");
        shineDotGO.transform.SetParent(go.transform, false);
        var shineDotRT = shineDotGO.AddComponent<RectTransform>();
        shineDotRT.anchorMin = new Vector2(0.28f, 0.45f);
        shineDotRT.anchorMax = new Vector2(0.38f, 0.55f);
        shineDotRT.offsetMin = Vector2.zero;
        shineDotRT.offsetMax = Vector2.zero;
        var shineDotImg = shineDotGO.AddComponent<Image>();
        shineDotImg.sprite = circleSprite;
        shineDotImg.color = new Color(1f, 1f, 1f, 0.3f);
        shineDotImg.raycastTarget = false;

        activeBubbles.Add(bubble);

        // Pop-in animation
        StartCoroutine(BubblePopIn(rt));
    }

    private IEnumerator BubblePopIn(RectTransform rt)
    {
        float dur = 0.25f;
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            if (rt == null) yield break;
            float t = elapsed / dur;
            // Overshoot bounce
            float scale;
            if (t < 0.6f)
                scale = Mathf.Lerp(0f, 1.2f, t / 0.6f);
            else
                scale = Mathf.Lerp(1.2f, 1f, (t - 0.6f) / 0.4f);
            rt.localScale = Vector3.one * scale;
            yield return null;
        }

        if (rt != null) rt.localScale = Vector3.one;
    }

    // ── Bubble Popping ──

    private void PopBubble(Bubble bubble)
    {
        if (bubble.popping) return;
        bubble.popping = true;
        SpawnPopParticles(bubble.rt.anchoredPosition, bubble.color, bubble.radius);
        Destroy(bubble.go);
    }

    private void PopBubbleSilent(Bubble bubble)
    {
        if (bubble.popping) return;
        bubble.popping = true;
        // Small sparkle when floating off top
        SpawnSmallSparkle(bubble.rt.anchoredPosition, bubble.color);
        Destroy(bubble.go);
    }

    private void SpawnPopParticles(Vector2 localPos, Color color, float radius)
    {
        Color solidColor = new Color(color.r, color.g, color.b, 1f);
        Color lighterColor = Color.Lerp(solidColor, Color.white, 0.4f);
        Color darkerColor = Color.Lerp(solidColor, Color.black, 0.2f);

        float sizeScale = Mathf.Clamp(radius / 40f, 0.5f, 2.5f);
        int shardCount = Mathf.RoundToInt(Random.Range(8, 14) * sizeScale);
        int sparkleCount = Mathf.RoundToInt(Random.Range(5, 9) * sizeScale);

        // Shards
        for (int i = 0; i < shardCount; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(300f, 700f) * sizeScale;
            float size = Random.Range(10f, 24f) * sizeScale;
            float lifetime = Random.Range(0.4f, 0.75f);
            Color shardColor = Color.Lerp(solidColor, lighterColor, Random.Range(0f, 0.5f));
            if (Random.value < 0.3f) shardColor = darkerColor;

            var shard = CreateShard(localPos, size, shardColor);
            StartCoroutine(AnimateShard(shard, localPos, angle, speed, lifetime, 1000f));
        }

        // Sparkles
        for (int i = 0; i < sparkleCount; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(200f, 500f) * sizeScale;
            float size = Random.Range(5f, 10f) * sizeScale;
            float lifetime = Random.Range(0.2f, 0.45f);
            Color sparkColor = Color.Lerp(Color.white, lighterColor, Random.Range(0f, 0.3f));

            var sparkle = CreateShard(localPos, size, sparkColor);
            StartCoroutine(AnimateShard(sparkle, localPos, angle, speed, lifetime, 400f));
        }
    }

    private void SpawnSmallSparkle(Vector2 localPos, Color color)
    {
        for (int i = 0; i < 4; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(80f, 200f);
            float size = Random.Range(4f, 8f);
            Color sparkColor = Color.Lerp(color, Color.white, 0.5f);

            var sparkle = CreateShard(localPos, size, sparkColor);
            StartCoroutine(AnimateShard(sparkle, localPos, angle, speed, 0.3f, 300f));
        }
    }

    private RectTransform CreateShard(Vector2 pos, float size, Color color)
    {
        var go = new GameObject("Shard");
        go.transform.SetParent(particleContainer, false);
        var shardRT = go.AddComponent<RectTransform>();
        shardRT.anchorMin = new Vector2(0.5f, 0f);
        shardRT.anchorMax = new Vector2(0.5f, 0f);
        shardRT.pivot = new Vector2(0.5f, 0.5f);
        shardRT.anchoredPosition = pos;
        shardRT.sizeDelta = new Vector2(size, size);
        shardRT.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        var shardImg = go.AddComponent<Image>();
        shardImg.sprite = circleSprite;
        shardImg.color = color;
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
            if (shardRT == null) yield break;
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

        if (shardRT != null) Destroy(shardRT.gameObject);
    }

    // ── Merge ──

    private void CheckMerge()
    {
        for (int i = 0; i < activeBubbles.Count; i++)
        {
            var a = activeBubbles[i];
            if (a.popping) continue;

            for (int j = i + 1; j < activeBubbles.Count; j++)
            {
                var b = activeBubbles[j];
                if (b.popping) continue;

                // Only merge different colors
                if (ColorsSimilar(a.color, b.color)) continue;

                Vector2 diff = a.rt.anchoredPosition - b.rt.anchoredPosition;
                float dist = diff.magnitude;
                float minDist = (a.radius + b.radius) * mergeOverlapRatio;

                if (dist < minDist)
                {
                    MergeBubbles(a, b);
                    return; // only one merge per frame
                }
            }
        }
    }

    private bool ColorsSimilar(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.1f &&
               Mathf.Abs(a.g - b.g) < 0.1f &&
               Mathf.Abs(a.b - b.b) < 0.1f;
    }

    private void MergeBubbles(Bubble a, Bubble b)
    {
        Vector2 midpoint = (a.rt.anchoredPosition + b.rt.anchoredPosition) * 0.5f;
        Color mergedColor = Color.Lerp(a.color, b.color, 0.5f);
        float mergedRadius = Mathf.Sqrt(a.radius * a.radius + b.radius * b.radius);
        mergedRadius = Mathf.Min(mergedRadius, maxBubbleRadius);

        // Pop both originals with particles
        SpawnPopParticles(a.rt.anchoredPosition, a.color, a.radius * 0.5f);
        SpawnPopParticles(b.rt.anchoredPosition, b.color, b.radius * 0.5f);

        a.popping = true;
        b.popping = true;
        Destroy(a.go);
        Destroy(b.go);
        activeBubbles.Remove(a);
        activeBubbles.Remove(b);

        // Create merged bubble
        CreateBubble(midpoint, mergedColor, mergedRadius);
    }

    // ── Hit Testing ──

    private Bubble GetBubbleAtScreenPos(Vector2 screenPos)
    {
        Vector2 localPos;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playArea, screenPos, null, out localPos))
            return null;

        // Check from newest (top) to oldest
        for (int i = activeBubbles.Count - 1; i >= 0; i--)
        {
            var b = activeBubbles[i];
            if (b.popping) continue;

            float dist = Vector2.Distance(localPos, b.rt.anchoredPosition);
            if (dist <= b.radius)
                return b;
        }

        return null;
    }

    // ── Preview ──

    private void CreatePreview(Vector2 localPos)
    {
        previewGO = new GameObject("Preview");
        previewGO.transform.SetParent(bubbleContainer, false);
        previewRT = previewGO.AddComponent<RectTransform>();
        previewRT.anchorMin = new Vector2(0.5f, 0f);
        previewRT.anchorMax = new Vector2(0.5f, 0f);
        previewRT.pivot = new Vector2(0.5f, 0.5f);
        previewRT.anchoredPosition = localPos;
        previewRT.sizeDelta = new Vector2(minBubbleRadius * 2f, minBubbleRadius * 2f);

        previewImage = previewGO.AddComponent<Image>();
        previewImage.sprite = circleSprite;
        Color c = Palette[selectedColorIndex];
        previewImage.color = new Color(c.r, c.g, c.b, 0.3f);
        previewImage.raycastTarget = false;

        previewGO.SetActive(false); // show only after short delay
    }

    // ── Color Selector ──

    private void CreateColorSelector()
    {
        float selectorHeight = 130f;
        float circleSize = 85f;
        float spacing = 24f;
        float totalWidth = Palette.Length * circleSize + (Palette.Length - 1) * spacing;

        // Selector container at bottom of play area
        var selectorGO = new GameObject("ColorSelector");
        selectorGO.transform.SetParent(playArea, false);
        var selectorRT = selectorGO.AddComponent<RectTransform>();
        selectorRT.anchorMin = new Vector2(0.5f, 0f);
        selectorRT.anchorMax = new Vector2(0.5f, 0f);
        selectorRT.pivot = new Vector2(0.5f, 0f);
        selectorRT.sizeDelta = new Vector2(totalWidth + 40f, selectorHeight);
        selectorRT.anchoredPosition = new Vector2(0, 10f);

        // Semi-transparent background for selector
        var selectorBg = selectorGO.AddComponent<Image>();
        selectorBg.color = new Color(0f, 0f, 0f, 0.2f);
        selectorBg.raycastTarget = false;

        selectorImages = new Image[Palette.Length];
        selectorGlows = new GameObject[Palette.Length];

        float startX = -totalWidth / 2f + circleSize / 2f;

        for (int i = 0; i < Palette.Length; i++)
        {
            float xPos = startX + i * (circleSize + spacing);
            int colorIndex = i; // capture for closure

            // Glow ring (behind the circle)
            var glowGO = new GameObject($"Glow_{i}");
            glowGO.transform.SetParent(selectorGO.transform, false);
            var glowRT = glowGO.AddComponent<RectTransform>();
            glowRT.anchorMin = new Vector2(0.5f, 0.5f);
            glowRT.anchorMax = new Vector2(0.5f, 0.5f);
            glowRT.pivot = new Vector2(0.5f, 0.5f);
            glowRT.sizeDelta = new Vector2(circleSize + 16f, circleSize + 16f);
            glowRT.anchoredPosition = new Vector2(xPos, 0f);
            var glowImg = glowGO.AddComponent<Image>();
            glowImg.sprite = circleSprite;
            glowImg.color = new Color(1f, 1f, 1f, 0.8f);
            glowImg.raycastTarget = false;
            selectorGlows[i] = glowGO;

            // Color circle button
            var btnGO = new GameObject($"Color_{i}");
            btnGO.transform.SetParent(selectorGO.transform, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.sizeDelta = new Vector2(circleSize, circleSize);
            btnRT.anchoredPosition = new Vector2(xPos, 0f);

            var btnImg = btnGO.AddComponent<Image>();
            btnImg.sprite = circleSprite;
            btnImg.color = Palette[i];
            btnImg.raycastTarget = true;
            selectorImages[i] = btnImg;

            // Small shine on the color button
            var shineGO = new GameObject("Shine");
            shineGO.transform.SetParent(btnGO.transform, false);
            var shineRT = shineGO.AddComponent<RectTransform>();
            shineRT.anchorMin = new Vector2(0.2f, 0.55f);
            shineRT.anchorMax = new Vector2(0.5f, 0.8f);
            shineRT.offsetMin = Vector2.zero;
            shineRT.offsetMax = Vector2.zero;
            var shineImg = shineGO.AddComponent<Image>();
            shineImg.sprite = circleSprite;
            shineImg.color = new Color(1f, 1f, 1f, 0.35f);
            shineImg.raycastTarget = false;

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            // Remove default color tint transition
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f);
            colors.selectedColor = Color.white;
            btn.colors = colors;

            btn.onClick.AddListener(() => SelectColor(colorIndex));
        }

        UpdateColorSelection();
    }

    private void SelectColor(int index)
    {
        selectedColorIndex = index;
        UpdateColorSelection();
    }

    private void UpdateColorSelection()
    {
        for (int i = 0; i < selectorGlows.Length; i++)
        {
            bool selected = (i == selectedColorIndex);
            selectorGlows[i].SetActive(selected);

            // Scale selected circle bigger
            var btnRT = selectorImages[i].GetComponent<RectTransform>();
            btnRT.localScale = selected ? Vector3.one * 1.15f : Vector3.one;
        }
    }

    // ── Background Particles ──

    private IEnumerator SpawnBackgroundParticles()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(0.3f, 0.8f));

            if (bgParticleContainer == null) yield break;

            var go = new GameObject("BgParticle");
            go.transform.SetParent(bgParticleContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            Rect area = playArea.rect;
            float x = Random.Range(area.xMin, area.xMax);
            rt.anchoredPosition = new Vector2(x, area.yMin - 10f);
            float size = Random.Range(3f, 8f);
            rt.sizeDelta = new Vector2(size, size);

            var img = go.AddComponent<Image>();
            img.sprite = circleSprite;
            float hue = Random.Range(0f, 1f);
            Color c = Color.HSVToRGB(hue, 0.2f, 1f);
            img.color = new Color(c.r, c.g, c.b, 0.15f);
            img.raycastTarget = false;

            StartCoroutine(AnimateBgParticle(rt, img, area));
        }
    }

    private IEnumerator AnimateBgParticle(RectTransform rt, Image img, Rect area)
    {
        float speed = Random.Range(15f, 40f);
        float xDrift = Random.Range(-20f, 20f);
        float lifetime = area.height / speed + 2f;
        float elapsed = 0f;
        Vector2 pos = rt.anchoredPosition;
        float swayPhase = Random.Range(0f, Mathf.PI * 2f);

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            if (rt == null) yield break;

            pos.y += speed * Time.deltaTime;
            pos.x += Mathf.Sin(swayPhase + elapsed * 0.5f) * xDrift * Time.deltaTime;
            rt.anchoredPosition = pos;

            yield return null;
        }

        if (rt != null) Destroy(rt.gameObject);
    }

    // ── Utility ──

    private void OnBackPressed()
    {
        FirebaseAnalyticsManager.LogSandboxSession("bubble_lab", Time.realtimeSinceStartup - _sessionStart);
        FingerTrail.SetEnabled(true);
        NavigationManager.GoToWorld();
    }

    private Sprite CreateCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = center - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float a = Mathf.Clamp01((radius - dist) / 1.5f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        // Clean up
        foreach (var b in activeBubbles)
        {
            if (b.go != null) Destroy(b.go);
        }
        activeBubbles.Clear();
    }
}
