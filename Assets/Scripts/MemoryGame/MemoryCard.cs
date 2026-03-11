using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

/// <summary>
/// Controls a single memory card: displays front/back, handles flip animation, and tap input.
/// Attach to the card prefab root which must have a Button component.
/// </summary>
[RequireComponent(typeof(Button))]
public class MemoryCard : MonoBehaviour
{
    [Header("References (set in prefab)")]
    public Image cardImage;         // the inner image that shows front or back
    public Image frameBorder;       // optional white border/frame
    public RectTransform flipRoot;  // the transform we scale for flip animation

    // Runtime state
    [NonSerialized] public int PairId;           // cards with the same PairId are a match
    [NonSerialized] public bool IsMatched;       // true once successfully matched
    [NonSerialized] public bool IsFaceUp;        // currently showing the front

    private Sprite frontSprite;
    private Sprite backSprite;
    private Button button;
    private Action<MemoryCard> onCardClicked;
    private Coroutine flipRoutine;
    private float baseRotation;

    private const float FlipDuration = 0.25f;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);
    }

    /// <summary>
    /// Initialise the card with its data. Called once by MemoryGameController.
    /// </summary>
    public void Setup(int pairId, Sprite front, Sprite back, Action<MemoryCard> clickCallback)
    {
        PairId = pairId;
        frontSprite = front;
        backSprite = back;
        onCardClicked = clickCallback;

        IsMatched = false;
        IsFaceUp = false;

        // Start face-down
        cardImage.sprite = backSprite;
    }

    /// <summary>
    /// Apply a small random rotation to make the grid feel playful (like the reference image).
    /// </summary>
    public void SetRandomRotation(float maxDegrees)
    {
        baseRotation = UnityEngine.Random.Range(-maxDegrees, maxDegrees);
        transform.localEulerAngles = new Vector3(0, 0, baseRotation);
    }

    private void HandleClick()
    {
        if (IsFaceUp || IsMatched) return;
        onCardClicked?.Invoke(this);
    }

    /// <summary>Flip the card to face-up with animation.</summary>
    public void FlipToFront(Action onComplete = null)
    {
        if (flipRoutine != null) StopCoroutine(flipRoutine);
        flipRoutine = StartCoroutine(FlipAnimation(backSprite, frontSprite, true, onComplete));
    }

    /// <summary>Flip the card back to face-down with animation.</summary>
    public void FlipToBack(Action onComplete = null)
    {
        if (flipRoutine != null) StopCoroutine(flipRoutine);
        flipRoutine = StartCoroutine(FlipAnimation(frontSprite, backSprite, false, onComplete));
    }

    /// <summary>Play bounce then fade out and disappear.</summary>
    public void PlayMatchAndHide()
    {
        Lock();
        StartCoroutine(MatchBounceAndHide());
    }

    private IEnumerator FlipAnimation(Sprite fromSprite, Sprite toSprite, bool toFaceUp, Action onComplete)
    {
        float half = FlipDuration * 0.5f;
        Vector3 originalScale = flipRoot.localScale;
        Vector3 flatScale = new Vector3(0f, originalScale.y, originalScale.z);

        // First half: scale X to 0
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / half);
            float eased = 1f - Mathf.Cos(progress * Mathf.PI * 0.5f); // ease-in
            flipRoot.localScale = Vector3.Lerp(originalScale, flatScale, eased);
            yield return null;
        }

        // Swap sprite at midpoint
        cardImage.sprite = toSprite;
        IsFaceUp = toFaceUp;

        // Second half: scale X back to 1
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / half);
            float eased = Mathf.Sin(progress * Mathf.PI * 0.5f); // ease-out
            flipRoot.localScale = Vector3.Lerp(flatScale, originalScale, eased);
            yield return null;
        }

        flipRoot.localScale = originalScale;
        flipRoutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator MatchBounceAndHide()
    {
        Vector3 orig = flipRoot.localScale;
        Vector3 big = orig * 1.15f;
        float dur = 0.15f;

        // Scale up
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            flipRoot.localScale = Vector3.Lerp(orig, big, t / dur);
            yield return null;
        }

        // Scale back
        t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            flipRoot.localScale = Vector3.Lerp(big, orig, t / dur);
            yield return null;
        }

        flipRoot.localScale = orig;

        // Fade out
        var canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        float fadeDur = 0.3f;
        t = 0f;
        while (t < fadeDur)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeDur);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>Disable interaction (used when card is matched).</summary>
    public void Lock()
    {
        button.interactable = false;
    }
}
