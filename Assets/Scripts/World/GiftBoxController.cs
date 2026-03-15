using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animated gift box that reveals a reward (animal or balloon) when tapped.
/// Tinted with the child's profile color. States: Idle, Opening, RewardReveal, Finished.
/// </summary>
public class GiftBoxController : MonoBehaviour
{
    public enum State { Idle, Opening, RewardReveal, Finished }

    [Header("References")]
    public Image boxImage;
    public Image glowImage;
    public Sprite circleSprite;

    [Header("Reward Data")]
    public DiscoveryEntry reward;

    // Callbacks set by WorldController
    public System.Action<GiftBoxController> onRewardRevealed;

    public State CurrentState { get; private set; } = State.Idle;

    private RectTransform rt;
    private Vector2 basePosition;
    private float idleTime;
    private float sparkleTimer;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Start()
    {
        basePosition = rt.anchoredPosition;

        // Tint box with child's profile color
        Color profileColor = Color.white;
        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
            profileColor = profile.AvatarColor;

        if (boxImage != null)
            boxImage.color = profileColor;

        // Setup glow
        if (glowImage != null)
        {
            glowImage.color = new Color(1f, 0.95f, 0.7f, 0.25f);
            glowImage.raycastTarget = false;
        }

        sparkleTimer = Random.Range(0.5f, 1.5f);
    }

    private void Update()
    {
        if (CurrentState != State.Idle) return;

        idleTime += Time.deltaTime;

        // Bounce: small up/down from base position
        float bounceY = Mathf.Sin(idleTime * 2.5f) * 8f;
        rt.anchoredPosition = new Vector2(basePosition.x, basePosition.y + bounceY);

        // Rotation: gentle -3 to +3 degrees
        float rot = Mathf.Sin(idleTime * 1.8f) * 3f;
        rt.localEulerAngles = new Vector3(0, 0, rot);

        // Scale pulse: 1 to 1.05
        float pulse = 1f + Mathf.Sin(idleTime * 3f) * 0.05f;
        rt.localScale = Vector3.one * pulse;

        // Glow pulse
        if (glowImage != null)
        {
            float glowAlpha = 0.15f + Mathf.Sin(idleTime * 2f) * 0.1f;
            var c = glowImage.color;
            glowImage.color = new Color(c.r, c.g, c.b, glowAlpha);
            float glowScale = 1.8f + Mathf.Sin(idleTime * 1.5f) * 0.2f;
            glowImage.rectTransform.localScale = Vector3.one * glowScale;
        }

        // Occasional sparkles
        sparkleTimer -= Time.deltaTime;
        if (sparkleTimer <= 0f)
        {
            SpawnSparkle();
            sparkleTimer = Random.Range(0.4f, 1.2f);
        }
    }

    /// <summary>Called by WorldInputHandler when the gift box is tapped.</summary>
    public void OnTap()
    {
        if (CurrentState != State.Idle) return;
        StartCoroutine(OpenSequence());
    }

    private IEnumerator OpenSequence()
    {
        CurrentState = State.Opening;

        // Stop idle animation
        rt.localEulerAngles = Vector3.zero;
        rt.localScale = Vector3.one;

        // Fade out glow during opening
        StartCoroutine(FadeOutGlow(0.3f));

        // Strong bounce
        yield return Bounce(1f, 1.3f, 0.12f);
        yield return Bounce(1.3f, 0.9f, 0.08f);
        yield return Bounce(0.9f, 1.15f, 0.08f);

        // Shake (lid opening feel)
        for (int i = 0; i < 6; i++)
        {
            float angle = (i % 2 == 0) ? 8f : -8f;
            rt.localEulerAngles = new Vector3(0, 0, angle);
            yield return new WaitForSeconds(0.04f);
        }
        rt.localEulerAngles = Vector3.zero;

        // Big burst of sparkles
        for (int i = 0; i < 20; i++)
            SpawnSparkle();

        yield return new WaitForSeconds(0.15f);

        // Scale up then disappear
        yield return Bounce(1f, 1.4f, 0.1f);

        CurrentState = State.RewardReveal;

        // Notify WorldController to spawn the reward
        onRewardRevealed?.Invoke(this);

        // Shrink and vanish
        yield return ScaleTo(1.4f, 0f, 0.25f);

        CurrentState = State.Finished;

        // Destroy glow object (it's a sibling, not a child)
        DestroyGlow();

        Destroy(gameObject);
    }

    private IEnumerator FadeOutGlow(float dur)
    {
        if (glowImage == null) yield break;

        Color startColor = glowImage.color;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(startColor.a, 0f, t / dur);
            glowImage.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }
    }

    private void DestroyGlow()
    {
        if (glowImage != null)
        {
            Destroy(glowImage.gameObject);
            glowImage = null;
        }
    }

    private void OnDestroy()
    {
        // Safety net: always clean up glow when gift is destroyed
        DestroyGlow();
    }

    private IEnumerator Bounce(float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(from, to, t / dur);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one * to;
    }

    private IEnumerator ScaleTo(float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / dur);
            float s = Mathf.Lerp(from, to, p);
            rt.localScale = Vector3.one * s;
            if (boxImage != null)
            {
                var c = boxImage.color;
                boxImage.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0f, p));
            }
            yield return null;
        }
    }

    private void SpawnSparkle()
    {
        var parent = rt.parent as RectTransform;
        if (parent == null) return;

        var go = new GameObject("Sparkle");
        go.transform.SetParent(parent, false);
        var sparkRT = go.AddComponent<RectTransform>();
        sparkRT.anchorMin = Vector2.zero;
        sparkRT.anchorMax = Vector2.zero;
        sparkRT.pivot = new Vector2(0.5f, 0.5f);

        float offsetX = Random.Range(-80f, 80f);
        float offsetY = Random.Range(-40f, 100f);
        sparkRT.anchoredPosition = rt.anchoredPosition + new Vector2(offsetX, offsetY);
        float size = Random.Range(8f, 18f);
        sparkRT.sizeDelta = new Vector2(size, size);

        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = new Color(1f, 0.95f, 0.7f, 0.9f);

        StartCoroutine(AnimateSparkle(sparkRT, img));
    }

    private IEnumerator AnimateSparkle(RectTransform sparkRT, Image img)
    {
        float lifetime = Random.Range(0.3f, 0.7f);
        float t = 0f;
        Vector2 startPos = sparkRT.anchoredPosition;
        float driftY = Random.Range(20f, 50f);
        Color startColor = img.color;

        while (t < lifetime)
        {
            t += Time.deltaTime;
            float p = t / lifetime;
            sparkRT.anchoredPosition = startPos + new Vector2(0, driftY * p);
            sparkRT.localScale = Vector3.one * (1f - p * 0.5f);
            img.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * (1f - p));
            yield return null;
        }

        Destroy(sparkRT.gameObject);
    }
}
