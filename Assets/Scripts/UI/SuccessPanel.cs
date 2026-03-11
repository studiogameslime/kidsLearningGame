using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Reusable success/completion panel shown when a mini-game is completed.
/// Shows stars, confetti particles, and Play Again / Home buttons.
/// Start hidden (gameObject inactive). Call Show() to animate in.
/// </summary>
public class SuccessPanel : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup panelGroup;
    public RectTransform starsContainer;
    public Image[] stars;
    public ParticleSystem confetti;
    public Button playAgainButton;
    public Button homeButton;

    [Header("Animation")]
    public float fadeInDuration = 0.4f;
    public float starDelay = 0.2f;

    private System.Action onPlayAgain;
    private System.Action onHome;

    private void Awake()
    {
        if (playAgainButton != null)
            playAgainButton.onClick.AddListener(() => onPlayAgain?.Invoke());
        if (homeButton != null)
            homeButton.onClick.AddListener(() => onHome?.Invoke());
    }

    public void Show(System.Action playAgainCallback, System.Action homeCallback)
    {
        onPlayAgain = playAgainCallback;
        onHome = homeCallback;

        gameObject.SetActive(true);

        if (panelGroup != null)
            panelGroup.alpha = 0f;

        // Hide stars initially
        if (stars != null)
        {
            foreach (var star in stars)
            {
                if (star != null)
                    star.transform.localScale = Vector3.zero;
            }
        }

        StartCoroutine(AnimateIn());
    }

    private IEnumerator AnimateIn()
    {
        // Fade in background
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            if (panelGroup != null)
                panelGroup.alpha = Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }
        if (panelGroup != null)
            panelGroup.alpha = 1f;

        // Pop in stars one by one
        if (stars != null)
        {
            foreach (var star in stars)
            {
                if (star == null) continue;
                yield return new WaitForSeconds(starDelay);
                StartCoroutine(PopIn(star.transform, 0.3f));
            }
        }

        // Play confetti
        if (confetti != null)
        {
            confetti.gameObject.SetActive(true);
            confetti.Play();
        }
    }

    private IEnumerator PopIn(Transform target, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            // Overshoot ease
            float scale = 1f + 0.3f * Mathf.Sin(p * Mathf.PI);
            if (p >= 1f) scale = 1f;
            target.localScale = Vector3.one * scale;
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    public void Hide()
    {
        if (confetti != null)
            confetti.Stop();
        gameObject.SetActive(false);
    }
}
