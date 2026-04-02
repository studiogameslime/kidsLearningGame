using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interactive Aquarium icon in the World scene (center screen).
/// Playful idle animations (bounce, sway, bubbles) attract attention.
/// Tapping opens the Aquarium collectible scene.
/// </summary>
public class WorldAquarium : MonoBehaviour
{
    public Sprite circleSprite; // shared circle sprite for bubble particles

    private RectTransform rt;
    private bool isAnimatingTap;
    private bool isIdleAnimating;

    // Idle timing
    private float idleTimer;
    private float nextIdleTime;
    private int lastIdleAction = -1;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Start()
    {
        nextIdleTime = Random.Range(2f, 4f);
    }

    private void Update()
    {
        if (isAnimatingTap) return;

        idleTimer += Time.deltaTime;
        if (!isIdleAnimating && idleTimer >= nextIdleTime)
        {
            idleTimer = 0f;
            nextIdleTime = Random.Range(3f, 5f);

            int action;
            do { action = Random.Range(0, 3); } while (action == lastIdleAction);
            lastIdleAction = action;

            switch (action)
            {
                case 0: StartCoroutine(IdleBounce()); break;
                case 1: StartCoroutine(IdleSway()); break;
                case 2: StartCoroutine(IdleBubbles()); break;
            }
        }
    }

    // ── Idle Animations ──

    private IEnumerator IdleBounce()
    {
        isIdleAnimating = true;
        float dur = 0.4f;
        float elapsed = 0f;
        Vector2 startPos = rt.anchoredPosition;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float bounce = Mathf.Sin(t * Mathf.PI);
            rt.localScale = Vector3.one * (1f + 0.05f * bounce);
            rt.anchoredPosition = startPos + new Vector2(0, 6f * bounce);
            yield return null;
        }

        rt.localScale = Vector3.one;
        rt.anchoredPosition = startPos;
        isIdleAnimating = false;
    }

    private IEnumerator IdleSway()
    {
        isIdleAnimating = true;
        float dur = 1.5f;
        float elapsed = 0f;
        float maxAngle = 2f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float angle = Mathf.Sin(t * Mathf.PI * 2f) * maxAngle * (1f - t * 0.3f);
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            yield return null;
        }

        rt.localRotation = Quaternion.identity;
        isIdleAnimating = false;
    }

    private IEnumerator IdleBubbles()
    {
        isIdleAnimating = true;

        int count = Random.Range(2, 4);
        for (int i = 0; i < count; i++)
        {
            SpawnBubble();
            yield return new WaitForSeconds(0.15f);
        }

        yield return new WaitForSeconds(0.8f);
        isIdleAnimating = false;
    }

    private void SpawnBubble()
    {
        var go = new GameObject("AquaBubble");
        go.transform.SetParent(rt, false);

        var bubbleRT = go.AddComponent<RectTransform>();
        float size = Random.Range(8f, 16f);
        bubbleRT.sizeDelta = new Vector2(size, size);
        bubbleRT.anchorMin = bubbleRT.anchorMax = new Vector2(0.5f, 0.5f);
        float xOff = Random.Range(-30f, 30f);
        bubbleRT.anchoredPosition = new Vector2(xOff, rt.sizeDelta.y * 0.3f);

        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = new Color(0.7f, 0.9f, 1f, 0.6f);
        img.raycastTarget = false;

        StartCoroutine(AnimateBubble(bubbleRT, img));
    }

    private IEnumerator AnimateBubble(RectTransform bubbleRT, Image img)
    {
        float dur = Random.Range(0.8f, 1.2f);
        float elapsed = 0f;
        Vector2 start = bubbleRT.anchoredPosition;
        float swayPhase = Random.Range(0f, Mathf.PI * 2f);

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float y = start.y + 60f * t;
            float x = start.x + Mathf.Sin(swayPhase + t * Mathf.PI * 3f) * 8f;
            bubbleRT.anchoredPosition = new Vector2(x, y);
            img.color = new Color(img.color.r, img.color.g, img.color.b, 0.6f * (1f - t));
            yield return null;
        }

        Destroy(bubbleRT.gameObject);
    }

    // ── Tap ──

    public void OnTap()
    {
        if (isAnimatingTap) return;
        StartCoroutine(TapSequence());
    }

    private IEnumerator TapSequence()
    {
        isAnimatingTap = true;
        isIdleAnimating = false;
        rt.localRotation = Quaternion.identity;

        // Bounce up
        float elapsed = 0f;
        Vector2 startPos = rt.anchoredPosition;

        while (elapsed < 0.15f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.15f;
            float yOffset = Mathf.Sin(t * Mathf.PI * 0.5f) * 20f;
            rt.anchoredPosition = startPos + new Vector2(0, yOffset);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, t);
            yield return null;
        }

        // Squash back
        elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.2f;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float yOffset = Mathf.Lerp(20f, 0f, smoothT);
            rt.anchoredPosition = startPos + new Vector2(0, yOffset);
            rt.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, smoothT);
            yield return null;
        }

        rt.anchoredPosition = startPos;
        rt.localScale = Vector3.one;

        yield return new WaitForSeconds(0.1f);
        isAnimatingTap = false;

        NavigationManager.GoToAquarium();
    }
}
