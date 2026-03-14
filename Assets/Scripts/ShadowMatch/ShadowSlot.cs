using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A shadow target slot. Holds the animalId it expects to match.
/// Shadows remain stable and passive — no attention-calling animation.
/// Only provides a brief proximity pulse when the correct animal is dragged near.
/// </summary>
public class ShadowSlot : MonoBehaviour
{
    [HideInInspector] public string animalId;
    [HideInInspector] public bool isMatched;

    private RectTransform rt;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    /// <summary>Play proximity hint when animal is dragged near.</summary>
    public void ShowProximityHint()
    {
        if (isMatched) return;
        StopAllCoroutines();
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
