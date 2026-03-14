using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A small decorative background prop in the Shadow Match scene.
/// Does a subtle bounce or sway when tapped. Non-distracting.
/// </summary>
public class ShadowMatchProp : MonoBehaviour, IPointerClickHandler
{
    private RectTransform rt;
    private bool isAnimating;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isAnimating) return;
        StartCoroutine(TinyBounce());
    }

    private IEnumerator TinyBounce()
    {
        isAnimating = true;

        // Small squish down then spring up
        float dur = 0.12f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float sx = 1f + 0.06f * t;
            float sy = 1f - 0.08f * t;
            rt.localScale = new Vector3(sx, sy, 1f);
            yield return null;
        }

        elapsed = 0f;
        dur = 0.25f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            float bounce = Mathf.Sin(t * Mathf.PI * 1.5f) * (1f - t);
            float sx = Mathf.Lerp(1.06f, 1f, t) - 0.03f * bounce;
            float sy = Mathf.Lerp(0.92f, 1f, t) + 0.04f * bounce;
            rt.localScale = new Vector3(sx, sy, 1f);
            yield return null;
        }

        rt.localScale = Vector3.one;
        isAnimating = false;
    }
}
