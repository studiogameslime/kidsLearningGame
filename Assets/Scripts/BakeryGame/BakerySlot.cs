using System.Collections;
using UnityEngine;

/// <summary>
/// A slot on the baking tray where a specific cookie shape belongs.
/// Displays a faded cookie silhouette and pulses gently until matched.
/// </summary>
public class BakerySlot : MonoBehaviour
{
    public int cookieId;
    public bool isMatched;

    private RectTransform rt;
    private Coroutine pulseCoroutine;
    private float phaseOffset;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        phaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    public RectTransform RT => rt;

    public void StartIdlePulse()
    {
        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseLoop());
    }

    public void StopIdlePulse()
    {
        if (pulseCoroutine != null) { StopCoroutine(pulseCoroutine); pulseCoroutine = null; }
        if (rt != null) rt.localScale = Vector3.one;
    }

    public void ShowProximityHint()
    {
        if (isMatched) return;
        StartCoroutine(HintPulse());
    }

    private IEnumerator PulseLoop()
    {
        float t = phaseOffset;
        while (!isMatched)
        {
            t += Time.deltaTime;
            float s = 1f + 0.04f * Mathf.Sin(t * 2.5f);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator HintPulse()
    {
        float dur = 0.2f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float s = 1f + 0.12f * Mathf.Sin(t / dur * Mathf.PI);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}
