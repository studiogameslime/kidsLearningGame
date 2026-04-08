using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Xylophone icon in the World scene. Tap to open the xylophone sandbox.
/// </summary>
public class WorldXylophone : MonoBehaviour
{
    private RectTransform rt;
    private bool isAnimating;

    private void Start()
    {
        rt = GetComponent<RectTransform>();
        StartCoroutine(IdleBounce());
    }

    public void OnTap()
    {
        if (isAnimating) return;
        StartCoroutine(TapSequence());
    }

    private IEnumerator TapSequence()
    {
        isAnimating = true;

        // Bounce animation
        float elapsed = 0f;
        float dur = 0.15f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float scale = 1f + Mathf.Sin(elapsed / dur * Mathf.PI) * 0.2f;
            rt.localScale = Vector3.one * scale;
            yield return null;
        }
        rt.localScale = Vector3.one;

        yield return new WaitForSeconds(0.1f);
        NavigationManager.GoToXylophone();
        isAnimating = false;
    }

    private IEnumerator IdleBounce()
    {
        while (true)
        {
            if (!isAnimating)
            {
                float t = Time.time;
                float bounce = 1f + Mathf.Sin(t * 1.5f) * 0.03f;
                float sway = Mathf.Sin(t * 0.8f) * 1.5f;
                rt.localScale = Vector3.one * bounce;
                rt.localRotation = Quaternion.Euler(0, 0, sway);
            }
            yield return null;
        }
    }
}
