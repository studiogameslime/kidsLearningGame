using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Xylophone sandbox controller — kids tap colorful bars to make music.
/// Procedurally generates sine-wave AudioClips at runtime (no audio files needed).
/// Supports tap and slide/glissando across bars.
/// </summary>
public class XylophoneController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform playArea;
    public Button backButton;

    // Bar definitions: top (high) to bottom (low)
    private static readonly BarDef[] Bars = new BarDef[]
    {
        new BarDef("Do'", 1047f, "#EF5350"), // Red
        new BarDef("Si",   988f, "#FF7043"), // Orange
        new BarDef("La",   880f, "#FFEE58"), // Yellow
        new BarDef("Sol",  784f, "#66BB6A"), // Green
        new BarDef("Fa",   698f, "#26C6DA"), // Teal
        new BarDef("Mi",   659f, "#42A5F5"), // Blue
        new BarDef("Re",   587f, "#5C6BC0"), // Indigo
        new BarDef("Do",   523f, "#AB47BC"), // Purple
    };

    private struct BarDef
    {
        public string name;
        public float frequency;
        public Color color;
        public BarDef(string n, float f, string hex)
        {
            name = n;
            frequency = f;
            ColorUtility.TryParseHtmlString(hex, out color);
        }
    }

    // Runtime state
    private AudioSource audioSource;
    private AudioClip[] noteClips;
    private RectTransform[] barRects;
    private Image[] barImages;
    private Sprite roundedRectSprite;
    private Sprite circleSprite;
    private RectTransform sparkleContainer;

    // Slide tracking
    private bool isTouching;
    private int lastBarIndex = -1;
    private int activePointerId = -1;

    // Session tracking
    private float sessionStartTime;

    // Squash-stretch coroutines per bar (to cancel if re-triggered)
    private Coroutine[] barAnimCoroutines;

    private int sparkleCounter;

    private void Start()
    {
        // Disable finger trail in sandbox
        FingerTrail.SetEnabled(false);

        // Create AudioSource for playback
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // Load rounded rect sprite
        roundedRectSprite = Resources.Load<Sprite>("RoundedRect");
        if (roundedRectSprite == null)
        {
            // Fallback: try loading via path (editor might have it elsewhere)
            // Create a simple white sprite as fallback
            roundedRectSprite = CreateFallbackSprite();
        }

        // Create circle sprite for sparkles
        circleSprite = CreateCircleSprite(16);

        // Generate note AudioClips
        noteClips = new AudioClip[Bars.Length];
        for (int i = 0; i < Bars.Length; i++)
            noteClips[i] = GenerateNote(Bars[i].frequency);

        // Create bar GameObjects
        CreateBars();

        // Create sparkle container
        var sparkleGO = new GameObject("Sparkles");
        sparkleGO.transform.SetParent(playArea, false);
        sparkleContainer = sparkleGO.AddComponent<RectTransform>();
        sparkleContainer.anchorMin = Vector2.zero;
        sparkleContainer.anchorMax = Vector2.one;
        sparkleContainer.offsetMin = Vector2.zero;
        sparkleContainer.offsetMax = Vector2.zero;

        // Wire back button
        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);

        sessionStartTime = Time.realtimeSinceStartup;
        FirebaseAnalyticsManager.LogScreenView("xylophone");
    }

    private void Update()
    {
        HandleSlideInput();
    }

    private void OnDestroy()
    {
        if (noteClips != null)
            foreach (var clip in noteClips)
                if (clip != null) Destroy(clip);
    }

    // ── Sound Generation ──

    private AudioClip GenerateNote(float frequency, float duration = 0.5f, int sampleRate = 44100)
    {
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 4f); // exponential decay
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.8f;
        }
        var clip = AudioClip.Create($"Note_{frequency}", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // ── Bar Creation ──

    private void CreateBars()
    {
        int count = Bars.Length;
        barRects = new RectTransform[count];
        barImages = new Image[count];
        barAnimCoroutines = new Coroutine[count];

        // Bar layout: evenly distributed vertically within play area
        // Width ranges from 60% (top = highest) to 95% (bottom = lowest)
        float spacing = 10f; // pixels between bars

        for (int i = 0; i < count; i++)
        {
            float widthFraction = Mathf.Lerp(0.60f, 0.95f, (float)i / (count - 1));

            // Shadow behind the bar
            var shadowGO = new GameObject($"BarShadow_{i}");
            shadowGO.transform.SetParent(playArea, false);
            var shadowRT = shadowGO.AddComponent<RectTransform>();
            var shadowImg = shadowGO.AddComponent<Image>();
            if (roundedRectSprite != null)
            {
                shadowImg.sprite = roundedRectSprite;
                shadowImg.type = Image.Type.Sliced;
            }
            shadowImg.color = new Color(0, 0, 0, 0.2f);
            shadowImg.raycastTarget = false;

            // Calculate vertical position: distribute evenly
            // Anchor-based: each bar occupies an equal vertical slice
            float sliceHeight = 1f / count;
            float barTop = 1f - i * sliceHeight;
            float barBottom = 1f - (i + 1) * sliceHeight;

            // Add padding within each slice for spacing
            float padNorm = spacing / 1080f * 0.5f; // approximate normalized padding

            // Shadow position (slightly offset)
            float halfWidth = widthFraction / 2f;
            shadowRT.anchorMin = new Vector2(0.5f - halfWidth, barBottom + padNorm);
            shadowRT.anchorMax = new Vector2(0.5f + halfWidth, barTop - padNorm);
            shadowRT.offsetMin = new Vector2(3, -3);
            shadowRT.offsetMax = new Vector2(3, -3);

            // The actual bar
            var barGO = new GameObject($"Bar_{i}_{Bars[i].name}");
            barGO.transform.SetParent(playArea, false);
            var barRT = barGO.AddComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.5f - halfWidth, barBottom + padNorm);
            barRT.anchorMax = new Vector2(0.5f + halfWidth, barTop - padNorm);
            barRT.offsetMin = Vector2.zero;
            barRT.offsetMax = Vector2.zero;

            var barImg = barGO.AddComponent<Image>();
            if (roundedRectSprite != null)
            {
                barImg.sprite = roundedRectSprite;
                barImg.type = Image.Type.Sliced;
            }
            barImg.color = Bars[i].color;
            barImg.raycastTarget = true; // needed for raycasting in slide detection

            barRects[i] = barRT;
            barImages[i] = barImg;
        }
    }

    // ── Input Handling (tap + slide) ──

    private void HandleSlideInput()
    {
        bool touching = false;
        Vector2 screenPos = Vector2.zero;

        // Check touch input
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            touching = touch.phase == TouchPhase.Began ||
                       touch.phase == TouchPhase.Moved ||
                       touch.phase == TouchPhase.Stationary;
            screenPos = touch.position;

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isTouching = false;
                lastBarIndex = -1;
                return;
            }
        }
        // Fallback to mouse
        else if (Input.GetMouseButton(0))
        {
            touching = true;
            screenPos = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isTouching = false;
            lastBarIndex = -1;
            return;
        }

        if (!touching)
        {
            if (isTouching)
            {
                isTouching = false;
                lastBarIndex = -1;
            }
            return;
        }

        // Find which bar is under the finger
        int barIndex = GetBarAtScreenPos(screenPos);

        if (barIndex >= 0 && barIndex != lastBarIndex)
        {
            PlayBar(barIndex, screenPos);
            lastBarIndex = barIndex;
        }

        isTouching = true;
    }

    private int GetBarAtScreenPos(Vector2 screenPos)
    {
        for (int i = 0; i < barRects.Length; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(barRects[i], screenPos, null))
                return i;
        }
        return -1;
    }

    private void PlayBar(int index, Vector2 screenPos)
    {
        if (index < 0 || index >= Bars.Length) return;

        // Play note
        audioSource.PlayOneShot(noteClips[index], 1f);

        // Squash-stretch animation
        if (barAnimCoroutines[index] != null)
            StopCoroutine(barAnimCoroutines[index]);
        barAnimCoroutines[index] = StartCoroutine(SquashStretchAnim(barRects[index], index));

        // Sparkle particles
        SpawnSparkles(screenPos, Bars[index].color, 5);

        // Glow pulse
        StartCoroutine(GlowPulse(barImages[index], Bars[index].color));
    }

    // ── Animations ──

    private IEnumerator SquashStretchAnim(RectTransform rt, int index)
    {
        // Squash phase: wide + short
        float squashDur = 0.06f;
        float stretchDur = 0.08f;
        float returnDur = 0.12f;

        Vector3 original = Vector3.one;
        Vector3 squashed = new Vector3(1.12f, 0.8f, 1f);
        Vector3 stretched = new Vector3(0.95f, 1.08f, 1f);

        // Squash
        float t = 0;
        while (t < squashDur)
        {
            t += Time.deltaTime;
            rt.localScale = Vector3.Lerp(original, squashed, t / squashDur);
            yield return null;
        }

        // Stretch
        t = 0;
        while (t < stretchDur)
        {
            t += Time.deltaTime;
            rt.localScale = Vector3.Lerp(squashed, stretched, t / stretchDur);
            yield return null;
        }

        // Return
        t = 0;
        while (t < returnDur)
        {
            t += Time.deltaTime;
            float ease = 1f - Mathf.Pow(1f - t / returnDur, 2f);
            rt.localScale = Vector3.Lerp(stretched, original, ease);
            yield return null;
        }

        rt.localScale = original;
        barAnimCoroutines[index] = null;
    }

    private IEnumerator GlowPulse(Image img, Color baseColor)
    {
        Color bright = Color.Lerp(baseColor, Color.white, 0.45f);
        float dur = 0.2f;
        float t = 0;

        // Brighten
        while (t < dur * 0.4f)
        {
            t += Time.deltaTime;
            img.color = Color.Lerp(baseColor, bright, t / (dur * 0.4f));
            yield return null;
        }

        // Fade back
        t = 0;
        while (t < dur * 0.6f)
        {
            t += Time.deltaTime;
            img.color = Color.Lerp(bright, baseColor, t / (dur * 0.6f));
            yield return null;
        }

        img.color = baseColor;
    }

    // ── Sparkle Particles ──

    private void SpawnSparkles(Vector2 screenPos, Color barColor, int count)
    {
        if (sparkleContainer == null || circleSprite == null) return;

        for (int i = 0; i < count; i++)
        {
            sparkleCounter++;
            var go = new GameObject($"Sparkle_{sparkleCounter}");
            go.transform.SetParent(sparkleContainer, false);
            var rt = go.AddComponent<RectTransform>();

            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                sparkleContainer, screenPos, null, out localPos);

            rt.anchoredPosition = localPos + new Vector2(
                Random.Range(-40f, 40f), Random.Range(-20f, 20f));
            float size = Random.Range(10f, 24f);
            rt.sizeDelta = new Vector2(size, size);

            var img = go.AddComponent<Image>();
            img.sprite = circleSprite;
            // Star colors: mix of bar color and bright variants
            float hue, sat, val;
            Color.RGBToHSV(barColor, out hue, out sat, out val);
            img.color = Random.value > 0.5f
                ? Color.HSVToRGB(hue, sat * 0.5f, 1f)
                : Color.HSVToRGB(hue + Random.Range(-0.05f, 0.05f), sat, Mathf.Min(val + 0.2f, 1f));
            img.raycastTarget = false;

            StartCoroutine(AnimateSparkle(rt, img));
        }
    }

    private IEnumerator AnimateSparkle(RectTransform rt, Image img)
    {
        if (rt == null) yield break;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float speed = Random.Range(80f, 200f);
        Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        float lifetime = Random.Range(0.3f, 0.6f);
        float elapsed = 0f;
        Color startColor = img.color;

        // Pop in
        rt.localScale = Vector3.zero;
        float popDur = 0.06f;
        float popT = 0f;
        while (popT < popDur)
        {
            popT += Time.deltaTime;
            if (rt == null) yield break;
            rt.localScale = Vector3.one * Mathf.Lerp(0f, 1.3f, popT / popDur);
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;

        // Float and fade
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            if (rt == null) yield break;

            float progress = elapsed / lifetime;
            velocity.y += 100f * Time.deltaTime;
            rt.anchoredPosition += velocity * Time.deltaTime;

            float fade = 1f - progress * progress;
            img.color = new Color(startColor.r, startColor.g, startColor.b, fade);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.1f, progress);

            yield return null;
        }

        if (rt != null) Destroy(rt.gameObject);
    }

    // ── Helpers ──

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

    private Sprite CreateFallbackSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                tex.SetPixel(x, y, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
    }

    // ── Navigation ──

    private void OnBackPressed()
    {
        LogSessionDuration();
        FingerTrail.SetEnabled(true);
        NavigationManager.GoToWorld();
    }

    private void LogSessionDuration()
    {
        float duration = Time.realtimeSinceStartup - sessionStartTime;
        FirebaseAnalyticsManager.LogSandboxSession("xylophone", duration);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) LogSessionDuration();
    }
}
