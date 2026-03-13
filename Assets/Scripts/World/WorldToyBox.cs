using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interactive Toy Box in the World scene — the main entry point for playing games.
/// Playful idle animations (bounce, shake, wiggle) attract attention.
/// Tapping starts the journey via JourneyManager.
/// </summary>
public class WorldToyBox : MonoBehaviour
{
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

            // Pick a random action different from last
            int action;
            do { action = Random.Range(0, 3); } while (action == lastIdleAction);
            lastIdleAction = action;

            switch (action)
            {
                case 0: StartCoroutine(IdleBounce()); break;
                case 1: StartCoroutine(IdleShake()); break;
                case 2: StartCoroutine(IdleWiggle()); break;
            }
        }
    }

    // ── Idle Animations ──

    private IEnumerator IdleBounce()
    {
        // Scale 1 → 1.07 → 1 with a slight Y lift
        isIdleAnimating = true;
        float dur = 0.35f;
        float elapsed = 0f;
        Vector2 startPos = rt.anchoredPosition;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float bounce = Mathf.Sin(t * Mathf.PI);
            rt.localScale = Vector3.one * (1f + 0.07f * bounce);
            rt.anchoredPosition = startPos + new Vector2(0, 8f * bounce);
            yield return null;
        }

        rt.localScale = Vector3.one;
        rt.anchoredPosition = startPos;
        isIdleAnimating = false;
    }

    private IEnumerator IdleShake()
    {
        // Quick left-right rotation shake
        isIdleAnimating = true;
        float dur = 0.4f;
        float elapsed = 0f;
        float freq = 18f;
        float maxAngle = 4f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float decay = 1f - t; // fades out
            float angle = Mathf.Sin(t * freq) * maxAngle * decay;
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            yield return null;
        }

        rt.localRotation = Quaternion.identity;
        isIdleAnimating = false;
    }

    private IEnumerator IdleWiggle()
    {
        // Lid wiggle: quick up-down bounces simulating toys popping out
        isIdleAnimating = true;
        Vector2 startPos = rt.anchoredPosition;

        for (int i = 0; i < 3; i++)
        {
            float dur = 0.1f;
            float elapsed = 0f;
            float height = (3 - i) * 5f; // decreasing bounce height

            // Up
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                rt.anchoredPosition = startPos + new Vector2(0, height * Mathf.Sin(t * Mathf.PI * 0.5f));
                rt.localScale = new Vector3(1f - 0.02f * (3 - i), 1f + 0.03f * (3 - i), 1f) * Mathf.Lerp(1f, 1f, t);
                yield return null;
            }

            // Down
            elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                rt.anchoredPosition = startPos + new Vector2(0, height * (1f - t));
                rt.localScale = Vector3.Lerp(
                    new Vector3(1f - 0.02f * (3 - i), 1f + 0.03f * (3 - i), 1f),
                    Vector3.one, t);
                yield return null;
            }
        }

        rt.anchoredPosition = startPos;
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
        StopCoroutine(nameof(IdleBounce));
        StopCoroutine(nameof(IdleShake));
        StopCoroutine(nameof(IdleWiggle));
        rt.localRotation = Quaternion.identity;

        SoundLibrary.PlayRandomFeedback();

        // 1. Bounce up
        float elapsed = 0f;
        Vector2 startPos = rt.anchoredPosition;

        while (elapsed < 0.15f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.15f;
            float yOffset = Mathf.Sin(t * Mathf.PI * 0.5f) * 30f;
            rt.anchoredPosition = startPos + new Vector2(0, yOffset);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, t);
            yield return null;
        }

        // 2. Squash back down
        elapsed = 0f;
        while (elapsed < 0.25f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.25f;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            float yOffset = Mathf.Lerp(30f, 0f, smoothT);
            rt.anchoredPosition = startPos + new Vector2(0, yOffset);

            float scaleX = Mathf.Lerp(1.15f, 1f, smoothT) + 0.05f * Mathf.Sin(t * Mathf.PI);
            float scaleY = Mathf.Lerp(1.15f, 1f, smoothT) - 0.05f * Mathf.Sin(t * Mathf.PI);
            rt.localScale = new Vector3(scaleX, scaleY, 1f);
            yield return null;
        }

        rt.anchoredPosition = startPos;
        rt.localScale = Vector3.one;

        yield return new WaitForSeconds(0.15f);

        isAnimatingTap = false;

        if (JourneyManager.Instance != null)
            JourneyManager.Instance.StartJourney();
        else
            Debug.LogWarning("WorldToyBox: JourneyManager not found.");
    }
}
