using System.Collections;
using UnityEngine;

/// <summary>
/// A slot on the baking tray where a specific cookie shape belongs.
/// Slots are static (no idle animation) — they look like indented shapes.
/// Only reacts with a subtle hint pulse when the correct cookie is dragged near.
/// </summary>
public class BakerySlot : MonoBehaviour
{
    public int cookieId;
    public bool isMatched;

    private RectTransform rt;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    public RectTransform RT => rt;

    /// <summary>Quick scale pulse when the correct cookie hovers nearby.</summary>
    public void ShowProximityHint()
    {
        if (isMatched) return;
        StopAllCoroutines();
        StartCoroutine(HintPulse());
    }

    private IEnumerator HintPulse()
    {
        float dur = 0.2f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float s = 1f + 0.1f * Mathf.Sin(t / dur * Mathf.PI);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}
