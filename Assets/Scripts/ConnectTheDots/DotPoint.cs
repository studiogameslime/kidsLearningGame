using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A single numbered dot in the Connect-the-Dots game.
/// Redesigned with glowing visuals, spark effects, and polished animations.
///
/// Visual states:
/// - Future: small, semi-transparent, subtle
/// - Next: larger, glowing ring pulse, guides the child
/// - Activated: green pop with spark burst
/// </summary>
public class DotPoint : MonoBehaviour
{
    [Header("References")]
    public Image dotImage;
    public Image ringImage;
    public Image glowImage;
    public TextMeshProUGUI numberText;

    [HideInInspector] public int dotIndex;
    [HideInInspector] public Vector2 normalizedPosition;

    private bool isActivated;
    private Vector3 baseScale = Vector3.one;

    // Star-like colors for night sky theme
    private static readonly Color FutureDotColor = new Color(0.8f, 0.85f, 0.95f, 0.35f);
    private static readonly Color NextColor = new Color(0.4f, 0.95f, 0.45f, 1f);       // bright green
    private static readonly Color NextGlowColor = new Color(0.3f, 0.9f, 0.4f, 0.45f); // green glow
    private static readonly Color ActiveColor = new Color(1f, 0.95f, 0.7f, 1f);       // warm golden
    private static readonly Color ActiveGlowColor = new Color(1f, 0.90f, 0.4f, 0.5f);
    private static readonly Color RingColor = new Color(1f, 0.95f, 0.7f, 0.5f);

    public void Init(int index, int displayNumber, Action<DotPoint> tapCallback)
    {
        dotIndex = index;
        isActivated = false;
        baseScale = Vector3.one;

        if (numberText != null)
        {
            numberText.text = displayNumber.ToString();
            numberText.fontSize = 32;
            numberText.fontStyle = FontStyles.Bold;
            numberText.color = new Color(1f, 1f, 1f, 0.4f);

            // Dark outline for readability against bright star glow
            numberText.outlineWidth = 0.35f;
            numberText.outlineColor = new Color(0.05f, 0.05f, 0.15f, 0.9f);
        }

        // Start as future dot (small, dim star)
        if (dotImage != null)
        {
            dotImage.color = FutureDotColor;
            dotImage.raycastTarget = false;
        }

        if (ringImage != null)
        {
            ringImage.color = RingColor;
            ringImage.gameObject.SetActive(false);
        }

        if (glowImage != null)
        {
            glowImage.color = new Color(1f, 1f, 1f, 0f);
            glowImage.gameObject.SetActive(false);
        }

        // Future dots are slightly smaller
        transform.localScale = Vector3.one * 0.75f;

        var rootImg = GetComponent<Image>();
        if (rootImg != null)
            rootImg.raycastTarget = false;
    }

    public void SetAsNext()
    {
        // Scale up to full size
        transform.localScale = Vector3.one;

        if (dotImage != null)
            dotImage.color = NextColor;

        if (numberText != null)
            numberText.color = Color.white;

        if (glowImage != null)
        {
            glowImage.gameObject.SetActive(true);
            glowImage.color = NextGlowColor;
        }

        if (ringImage != null)
        {
            ringImage.gameObject.SetActive(true);
            StartCoroutine(PulseRing());
        }

        StartCoroutine(GentleBreathe());
    }

    public void Activate()
    {
        isActivated = true;
        StopAllCoroutines();

        if (dotImage != null)
            dotImage.color = ActiveColor;

        if (numberText != null)
            numberText.color = Color.white;

        if (ringImage != null)
            ringImage.gameObject.SetActive(false);

        if (glowImage != null)
        {
            glowImage.gameObject.SetActive(true);
            glowImage.color = ActiveGlowColor;
        }

        StartCoroutine(ActivateBounce());
    }

    public void SetInactive()
    {
        if (dotImage != null)
            dotImage.color = FutureDotColor;
        if (ringImage != null)
            ringImage.gameObject.SetActive(false);
        if (glowImage != null)
            glowImage.gameObject.SetActive(false);
    }

    private IEnumerator ActivateBounce()
    {
        Vector3 orig = Vector3.one;
        Vector3 big = orig * 1.5f;

        // Pop up
        float t = 0f;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(orig, big, t / 0.1f);
            yield return null;
        }

        // Settle back with overshoot
        t = 0f;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            float p = t / 0.15f;
            float s = 1.5f - 0.5f * p - 0.15f * Mathf.Sin(p * Mathf.PI);
            transform.localScale = orig * s;
            yield return null;
        }

        transform.localScale = orig;

        // Fade glow after activation
        if (glowImage != null)
        {
            t = 0f;
            Color gc = glowImage.color;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                glowImage.color = new Color(gc.r, gc.g, gc.b, gc.a * (1f - t / 0.5f));
                yield return null;
            }
            glowImage.gameObject.SetActive(false);
        }
    }

    private IEnumerator PulseRing()
    {
        while (true)
        {
            float t = 0f;
            float dur = 0.9f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = t / dur;
                float a = 0.15f + 0.5f * Mathf.Sin(p * Mathf.PI);
                float scale = 1f + 0.08f * Mathf.Sin(p * Mathf.PI);
                if (ringImage != null)
                {
                    ringImage.color = new Color(0.3f, 0.9f, 0.4f, a);
                    ringImage.transform.localScale = Vector3.one * scale;
                }
                yield return null;
            }
        }
    }

    private IEnumerator GentleBreathe()
    {
        float phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        while (!isActivated)
        {
            float s = 1f + 0.04f * Mathf.Sin(Time.time * 2.5f + phase);
            transform.localScale = Vector3.one * s;
            yield return null;
        }
    }
}
