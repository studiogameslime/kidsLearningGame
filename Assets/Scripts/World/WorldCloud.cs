using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A single moving cloud in the World scene. Moves left or right based on speed sign.
/// Positive speed = moving right, negative speed = moving left.
/// Tappable: squash-bounce + cute UI rain shower from the cloud.
/// </summary>
public class WorldCloud : MonoBehaviour
{
    public float speed;       // positive = right, negative = left
    public float leftBound;   // x position at which we're off-screen left
    public float rightBound;  // x position at which we're off-screen right

    private RectTransform rt;
    private bool isBouncing;

    private static readonly Color[] RainColors = {
        new Color(0.55f, 0.78f, 0.98f, 0.75f), // light blue
        new Color(0.45f, 0.70f, 0.95f, 0.70f), // medium blue
        new Color(0.60f, 0.82f, 1.00f, 0.65f), // pale blue
    };

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (rt == null) return;
        rt.anchoredPosition += new Vector2(speed * Time.deltaTime, 0f);
    }

    public Vector2 GetPosition()
    {
        return rt != null ? rt.anchoredPosition : Vector2.zero;
    }

    public bool IsOffScreen()
    {
        if (rt == null) return true;
        float x = rt.anchoredPosition.x;
        return x < leftBound || x > rightBound;
    }

    public void OnTap()
    {
        if (isBouncing) return;
        StartCoroutine(TapReaction());
    }

    private IEnumerator TapReaction()
    {
        isBouncing = true;

        // Squash-bounce
        float uniformScale = transform.localScale.z;
        Vector3 target = Vector3.one * uniformScale;
        Vector3 squash = new Vector3(1.2f * uniformScale, 0.85f * uniformScale, uniformScale);

        float dur = 0.1f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(target, squash, elapsed / dur);
            yield return null;
        }

        elapsed = 0f;
        dur = 0.15f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            transform.localScale = Vector3.Lerp(squash, target, t);
            yield return null;
        }
        transform.localScale = target;

        // Spawn rain drops
        SpawnRain();

        isBouncing = false;
    }

    private void SpawnRain()
    {
        if (rt == null || rt.parent == null) return;
        // Get cloud's actual position in parent space (anchor + offset)
        float cloudX = rt.anchoredPosition.x;
        float cloudY = rt.anchoredPosition.y;
        float cloudHalfW = rt.sizeDelta.x * rt.localScale.x * 0.35f;

        int dropCount = Random.Range(8, 14);
        for (int i = 0; i < dropCount; i++)
        {
            StartCoroutine(AnimateRainDrop(i, cloudX, cloudY, cloudHalfW));
        }
    }

    private IEnumerator AnimateRainDrop(int index, float cloudX, float cloudY, float cloudHalfW)
    {
        // Stagger start
        yield return new WaitForSeconds(index * 0.04f);

        var dropGO = new GameObject($"Rain{index}");
        dropGO.transform.SetParent(rt.parent, false);
        var dropRT = dropGO.AddComponent<RectTransform>();
        dropRT.anchorMin = Vector2.zero;
        dropRT.anchorMax = Vector2.zero;
        dropRT.pivot = new Vector2(0.5f, 0.5f);

        // Raindrop size — thin elongated drop
        float w = Random.Range(3f, 5f);
        float h = Random.Range(8f, 14f);
        dropRT.sizeDelta = new Vector2(w, h);

        // Start from bottom of cloud, spread across cloud width
        float startX = cloudX + Random.Range(-cloudHalfW, cloudHalfW);
        float startY = cloudY - rt.sizeDelta.y * rt.localScale.y * 0.3f;
        dropRT.anchoredPosition = new Vector2(startX, startY);

        var img = dropGO.AddComponent<Image>();
        Color dropColor = RainColors[Random.Range(0, RainColors.Length)];
        img.color = dropColor;
        img.raycastTarget = false;

        // Fall animation
        float dur = Random.Range(0.6f, 1.0f);
        float elapsed = 0f;
        float fallDistance = Random.Range(100f, 180f);
        float driftX = Random.Range(-15f, 15f);
        Vector2 startPos = dropRT.anchoredPosition;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;

            // Accelerating fall
            float fallY = fallDistance * t * t;
            float drift = driftX * t;
            dropRT.anchoredPosition = new Vector2(startPos.x + drift, startPos.y - fallY);

            // Fade out in last 40%
            if (t > 0.6f)
            {
                float fadeT = (t - 0.6f) / 0.4f;
                img.color = new Color(dropColor.r, dropColor.g, dropColor.b, dropColor.a * (1f - fadeT));
            }

            yield return null;
        }

        Destroy(dropGO);
    }
}
