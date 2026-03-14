using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A shadow target slot. Holds the animalId it expects to match.
/// Provides pulse animation to guide the child and match feedback.
/// </summary>
public class ShadowSlot : MonoBehaviour
{
    [HideInInspector] public string animalId;
    [HideInInspector] public bool isMatched;

    private Image shadowImage;
    private RectTransform rt;
    private bool isPulsing;
    private Coroutine pulseCoroutine;

    private void Awake()
    {
        shadowImage = GetComponent<Image>();
        rt = GetComponent<RectTransform>();
    }

    /// <summary>Start a subtle attention pulse on this shadow.</summary>
    public void StartPulse()
    {
        if (isMatched || isPulsing) return;
        isPulsing = true;
        pulseCoroutine = StartCoroutine(PulseLoop());
    }

    /// <summary>Stop the attention pulse.</summary>
    public void StopPulse()
    {
        isPulsing = false;
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    private IEnumerator PulseLoop()
    {
        while (isPulsing && !isMatched)
        {
            // Scale up
            float dur = 0.6f;
            float elapsed = 0f;
            while (elapsed < dur && isPulsing)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                float scale = 1f + 0.06f * Mathf.Sin(t * Mathf.PI);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }

            // Brief pause at rest
            rt.localScale = Vector3.one;
            yield return new WaitForSeconds(0.8f);
        }

        rt.localScale = Vector3.one;
    }

    /// <summary>Play proximity hint when animal is dragged near.</summary>
    public void ShowProximityHint()
    {
        if (isMatched) return;
        StopAllCoroutines();
        isPulsing = false;
        StartCoroutine(ProximityPulse());
    }

    private IEnumerator ProximityPulse()
    {
        float dur = 0.15f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.1f, elapsed / dur);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            rt.localScale = Vector3.one * Mathf.Lerp(1.1f, 1f, elapsed / dur);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}
