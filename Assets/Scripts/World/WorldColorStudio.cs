using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interactive Color Studio icon in the World scene.
/// Rainbow pulse and color-wheel idle animations.
/// Tapping opens the Color Studio scene.
/// </summary>
public class WorldColorStudio : MonoBehaviour
{
    public Sprite circleSprite;

    private RectTransform rt;
    private Image mainImage;
    private bool isAnimatingTap;
    private bool isIdleAnimating;
    private float idleTimer;
    private float nextIdleTime;
    private int lastIdleAction = -1;

    // Rainbow colors for pulse
    private static readonly Color[] RainbowColors = {
        new Color(0.94f, 0.27f, 0.27f), // red
        new Color(1f, 0.55f, 0.1f),     // orange
        new Color(0.98f, 0.8f, 0.08f),  // yellow
        new Color(0.3f, 0.77f, 0.37f),  // green
        new Color(0.23f, 0.51f, 0.96f), // blue
        new Color(0.55f, 0.36f, 0.96f), // purple
    };

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        mainImage = GetComponent<Image>();
    }

    private void Start()
    {
        nextIdleTime = Random.Range(2f, 4f);
    }

    private void Update()
    {
        if (isAnimatingTap) return;

        idleTimer += Time.deltaTime;
        if (!isIdleAnimating && idleTimer >= nextIdleTime)
        {
            idleTimer = 0f;
            nextIdleTime = Random.Range(3f, 5f);

            int action;
            do { action = Random.Range(0, 3); } while (action == lastIdleAction);
            lastIdleAction = action;

            switch (action)
            {
                case 0: StartCoroutine(IdleRainbowPulse()); break;
                case 1: StartCoroutine(IdleBounce()); break;
                case 2: StartCoroutine(IdleSparkle()); break;
            }
        }
    }

    // ── Idle Animations ──

    private IEnumerator IdleRainbowPulse()
    {
        isIdleAnimating = true;
        if (mainImage == null) { isIdleAnimating = false; yield break; }

        Color originalColor = mainImage.color;
        for (int i = 0; i < RainbowColors.Length; i++)
        {
            Color target = RainbowColors[i];
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                mainImage.color = Color.Lerp(mainImage.color, target, t / 0.15f);
                yield return null;
            }
        }

        // Return to white
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            mainImage.color = Color.Lerp(mainImage.color, originalColor, elapsed / 0.3f);
            yield return null;
        }
        mainImage.color = originalColor;
        isIdleAnimating = false;
    }

    private IEnumerator IdleBounce()
    {
        isIdleAnimating = true;
        float dur = 0.4f;
        float elapsed = 0f;
        Vector2 startPos = rt.anchoredPosition;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float bounce = Mathf.Sin(t * Mathf.PI);
            rt.localScale = Vector3.one * (1f + 0.06f * bounce);
            rt.anchoredPosition = startPos + new Vector2(0, 6f * bounce);
            yield return null;
        }

        rt.localScale = Vector3.one;
        rt.anchoredPosition = startPos;
        isIdleAnimating = false;
    }

    private IEnumerator IdleSparkle()
    {
        isIdleAnimating = true;
        if (circleSprite != null)
        {
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("ColorSparkle");
                go.transform.SetParent(rt, false);
                var srt = go.AddComponent<RectTransform>();
                float size = Random.Range(8f, 14f);
                srt.sizeDelta = new Vector2(size, size);
                srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = Random.insideUnitCircle * 40f;
                var img = go.AddComponent<Image>();
                img.sprite = circleSprite;
                img.color = RainbowColors[Random.Range(0, RainbowColors.Length)];
                img.raycastTarget = false;
                StartCoroutine(FadeSparkle(srt, img));
                yield return new WaitForSeconds(0.12f);
            }
        }
        yield return new WaitForSeconds(0.6f);
        isIdleAnimating = false;
    }

    private IEnumerator FadeSparkle(RectTransform srt, Image img)
    {
        Vector2 start = srt.anchoredPosition;
        float dur = 0.7f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            srt.anchoredPosition = start + new Vector2(0, 30f * p);
            img.color = new Color(img.color.r, img.color.g, img.color.b, 1f - p);
            srt.localScale = Vector3.one * (1f - p * 0.5f);
            yield return null;
        }
        Destroy(srt.gameObject);
    }

    // ── Tap ──

    public void OnTap()
    {
        if (isAnimatingTap) return;
        StartCoroutine(TapSequence());
    }

    private IEnumerator TapSequence()
    {
        isAnimatingTap = true;
        isIdleAnimating = false;

        Vector2 startPos = rt.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < 0.15f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.15f;
            rt.anchoredPosition = startPos + new Vector2(0, 15f * Mathf.Sin(t * Mathf.PI * 0.5f));
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / 0.2f);
            rt.anchoredPosition = startPos + new Vector2(0, 15f * (1f - t));
            rt.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, t);
            yield return null;
        }

        rt.anchoredPosition = startPos;
        rt.localScale = Vector3.one;

        yield return new WaitForSeconds(0.1f);
        isAnimatingTap = false;

        NavigationManager.GoToColorStudio();
    }
}
