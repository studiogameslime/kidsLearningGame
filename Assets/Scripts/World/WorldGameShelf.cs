using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interactive Game Shelf in the World scene — the main entry point for game selection.
/// Playful idle animations (gentle float, glow pulse, subtle rock) attract attention.
/// Tapping opens the game selection screen (MainMenu).
/// </summary>
public class WorldGameShelf : MonoBehaviour
{
    private RectTransform rt;
    private bool isAnimatingTap;
    private bool isIdleAnimating;

    // Idle timing
    private float idleTimer;
    private float nextIdleTime;
    private int lastIdleAction = -1;

    // Continuous subtle animation
    private float breathTime;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Start()
    {
        nextIdleTime = Random.Range(3f, 5f);
        breathTime = Random.Range(0f, Mathf.PI * 2f); // random phase
    }

    private void Update()
    {
        if (isAnimatingTap) return;

        // Continuous gentle breathing (scale 1.0 to 1.02)
        breathTime += Time.deltaTime;
        float breathScale = 1f + Mathf.Sin(breathTime * 1.8f) * 0.015f;
        if (!isIdleAnimating)
            rt.localScale = Vector3.one * breathScale;

        // Periodic attention animations
        idleTimer += Time.deltaTime;
        if (!isIdleAnimating && idleTimer >= nextIdleTime)
        {
            idleTimer = 0f;
            nextIdleTime = Random.Range(4f, 7f);

            int action;
            do { action = Random.Range(0, 3); } while (action == lastIdleAction);
            lastIdleAction = action;

            switch (action)
            {
                case 0: StartCoroutine(IdleBounce()); break;
                case 1: StartCoroutine(IdleRock()); break;
                case 2: StartCoroutine(IdleSparkle()); break;
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
            rt.localScale = Vector3.one * (1f + 0.06f * bounce);
            rt.anchoredPosition = startPos + new Vector2(0, 10f * bounce);
            yield return null;
        }

        rt.localScale = Vector3.one;
        rt.anchoredPosition = startPos;
        isIdleAnimating = false;
    }

    private IEnumerator IdleRock()
    {
        isIdleAnimating = true;
        float dur = 0.5f;
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float decay = 1f - t;
            float angle = Mathf.Sin(t * 14f) * 3f * decay;
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            yield return null;
        }

        rt.localRotation = Quaternion.identity;
        isIdleAnimating = false;
    }

    private IEnumerator IdleSparkle()
    {
        // Quick scale pulse like a sparkle/glint
        isIdleAnimating = true;
        float dur = 0.3f;
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float scale = 1f + 0.08f * Mathf.Sin(t * Mathf.PI);
            rt.localScale = Vector3.one * scale;
            yield return null;
        }

        rt.localScale = Vector3.one;
        isIdleAnimating = false;
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

        Vector2 startPos = rt.anchoredPosition;

        // 1. Bounce up with scale
        float elapsed = 0f;
        while (elapsed < 0.12f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.12f;
            float yOffset = Mathf.Sin(t * Mathf.PI * 0.5f) * 25f;
            rt.anchoredPosition = startPos + new Vector2(0, yOffset);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, t);
            yield return null;
        }

        // 2. Settle back with squash
        elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.2f;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            float yOffset = Mathf.Lerp(25f, 0f, smoothT);
            rt.anchoredPosition = startPos + new Vector2(0, yOffset);

            float scaleX = Mathf.Lerp(1.12f, 1f, smoothT) + 0.04f * Mathf.Sin(t * Mathf.PI);
            float scaleY = Mathf.Lerp(1.12f, 1f, smoothT) - 0.04f * Mathf.Sin(t * Mathf.PI);
            rt.localScale = new Vector3(scaleX, scaleY, 1f);
            yield return null;
        }

        rt.anchoredPosition = startPos;
        rt.localScale = Vector3.one;

        yield return new WaitForSeconds(0.1f);

        isAnimatingTap = false;

        // Open game selection
        NavigationManager.GoToGamesCollection();
    }
}
