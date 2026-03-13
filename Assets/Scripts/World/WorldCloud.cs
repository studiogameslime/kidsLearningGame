using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A single moving cloud in the World scene. Moves right-to-left.
/// Tappable with a cute bounce reaction.
/// </summary>
public class WorldCloud : MonoBehaviour
{
    public float speed;
    public float leftBound;  // x position at which we're off-screen left

    private RectTransform rt;
    private bool isBouncing;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (rt == null) return;
        rt.anchoredPosition += new Vector2(-speed * Time.deltaTime, 0f);
    }

    public bool IsOffScreen()
    {
        if (rt == null) return true;
        return rt.anchoredPosition.x < leftBound;
    }

    public void OnTap()
    {
        if (isBouncing) return;
        StartCoroutine(BounceReaction());
    }

    private IEnumerator BounceReaction()
    {
        isBouncing = true;

        // Scale pop
        float dur = 0.12f;
        float elapsed = 0f;
        Vector3 big = Vector3.one * 1.15f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.one, big, elapsed / dur);
            yield return null;
        }

        // Small upward nudge
        Vector2 startPos = rt.anchoredPosition;
        float nudge = 15f;
        elapsed = 0f;
        dur = 0.15f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            rt.anchoredPosition = startPos + new Vector2(0, nudge * Mathf.Sin(t * Mathf.PI));
            transform.localScale = Vector3.Lerp(big, Vector3.one, t);
            yield return null;
        }

        rt.anchoredPosition = startPos;
        transform.localScale = Vector3.one;
        isBouncing = false;
    }
}
