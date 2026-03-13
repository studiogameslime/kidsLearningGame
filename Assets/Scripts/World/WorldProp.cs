using System.Collections;
using UnityEngine;

/// <summary>
/// An interactive tree or bush in the World scene.
/// Tapping causes a playful sway/bounce animation.
/// </summary>
public class WorldProp : MonoBehaviour
{
    public enum PropType { Tree, Bush }
    public PropType propType = PropType.Tree;

    private RectTransform rt;
    private bool isAnimating;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    public void OnTap()
    {
        if (isAnimating) return;

        if (propType == PropType.Tree)
            StartCoroutine(TreeSway());
        else
            StartCoroutine(BushBounce());
    }

    private IEnumerator TreeSway()
    {
        isAnimating = true;

        // Sway left-right using rotation
        float dur = 0.5f;
        float elapsed = 0f;
        float maxAngle = 8f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float angle = Mathf.Sin(t * Mathf.PI * 3f) * maxAngle * (1f - t);
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            yield return null;
        }

        rt.localRotation = Quaternion.identity;
        isAnimating = false;
    }

    private IEnumerator BushBounce()
    {
        isAnimating = true;

        // Squash-stretch bounce
        float dur = 0.35f;
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float scaleX = 1f + Mathf.Sin(t * Mathf.PI * 2f) * 0.15f * (1f - t);
            float scaleY = 1f - Mathf.Sin(t * Mathf.PI * 2f) * 0.12f * (1f - t);
            transform.localScale = new Vector3(scaleX, scaleY, 1f);
            yield return null;
        }

        transform.localScale = Vector3.one;
        isAnimating = false;
    }
}
