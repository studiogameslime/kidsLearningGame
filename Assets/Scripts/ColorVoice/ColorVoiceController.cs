using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Main controller for the Color Voice recognition mini-game.
/// Shows a target color, listens for the child to say its name in Hebrew,
/// evaluates the answer, and provides kid-friendly feedback.
/// </summary>
public class ColorVoiceController : MonoBehaviour
{
    [Header("UI References")]
    public Image colorCircle;
    public TextMeshProUGUI colorLabel;
    public Image micIcon;
    public TextMeshProUGUI instructionText;
    public TextMeshProUGUI feedbackText;
    public TextMeshProUGUI progressText;
    public TextMeshProUGUI debugText;  // toggle via inspector for dev

    [Header("Settings")]
    public int totalRounds = 7;
    public float successDelay = 1.5f;
    public float retryDelay = 1.0f;
    public float listenTimeout = 8f;

    [Header("Audio")]
    public AudioClip successClip;
    public AudioClip wrongClip;
    public AudioClip instructionClip; // "?איזה צבע זה"

    // State
    private ISpeechRecognizer recognizer;
    private AudioSource audioSource;
    private ColorPrompt currentColor;
    private int currentRound;
    private int retryCount;
    private bool isRoundActive;
    private Coroutine listenTimeoutCoroutine;
    private Coroutine micPulseCoroutine;

    // Shuffled color order for variety
    private ColorPrompt[] shuffledColors;

    private void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // Create platform-appropriate recognizer
#if UNITY_EDITOR
        var mock = gameObject.AddComponent<MockSpeechRecognizer>();
        recognizer = mock;
#elif UNITY_ANDROID
        var android = gameObject.AddComponent<AndroidSpeechRecognizer>();
        recognizer = android;
#else
        var mock = gameObject.AddComponent<MockSpeechRecognizer>();
        recognizer = mock;
#endif

        recognizer.Initialize("he-IL");

        // Wire events
        recognizer.OnReady += OnRecognizerReady;
        recognizer.OnResults += OnRecognizerResults;
        recognizer.OnPartialResult += OnRecognizerPartial;
        recognizer.OnError += OnRecognizerError;

        // Mute background music (mic-based game)
        BackgroundMusicManager.SetMuted(true);

        // Hide feedback/debug initially
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
        if (debugText != null) debugText.gameObject.SetActive(false);

        ShuffleColors();
        currentRound = 0;
        StartCoroutine(WaitForInitThenStart());
    }

    private void OnDestroy()
    {
        BackgroundMusicManager.SetMuted(false);

        if (recognizer != null)
        {
            recognizer.OnReady -= OnRecognizerReady;
            recognizer.OnResults -= OnRecognizerResults;
            recognizer.OnPartialResult -= OnRecognizerPartial;
            recognizer.OnError -= OnRecognizerError;
            recognizer.Destroy();
        }
    }

    // ── Round Flow ──

    private void ShuffleColors()
    {
        shuffledColors = new ColorPrompt[ColorVoiceData.Colors.Length];
        System.Array.Copy(ColorVoiceData.Colors, shuffledColors, ColorVoiceData.Colors.Length);

        // Fisher-Yates shuffle
        for (int i = shuffledColors.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = shuffledColors[i];
            shuffledColors[i] = shuffledColors[j];
            shuffledColors[j] = temp;
        }
    }

    private IEnumerator WaitForInitThenStart()
    {
        // Wait for recognizer to finish async initialization (Android runs on UI thread)
        float waited = 0f;
        while (!recognizer.IsInitialized && waited < 5f)
        {
            yield return null;
            waited += Time.deltaTime;
        }

        if (!recognizer.IsInitialized)
            Debug.LogWarning("[ColorVoice] Speech recognizer failed to initialize after 5s");

        StartCoroutine(StartRoundSequence());
    }

    private IEnumerator StartRoundSequence()
    {
        // Pick color for this round
        currentColor = shuffledColors[currentRound % shuffledColors.Length];
        retryCount = 0;

        // Update progress
        if (progressText != null)
            progressText.text = $"{currentRound + 1}/{totalRounds}";

        // Hide old feedback
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);

        // Animate color circle entrance
        yield return AnimateColorEntrance();

        // Play instruction audio if available
        if (instructionClip != null)
        {
            audioSource.PlayOneShot(instructionClip);
            yield return new WaitForSeconds(instructionClip.length + 0.2f);
        }
        else
        {
            yield return new WaitForSeconds(0.3f);
        }

        // Show instruction text
        if (instructionText != null)
        {
            instructionText.text = HebrewFixer.Fix("?\u05D0\u05D9\u05D6\u05D4 \u05E6\u05D1\u05E2 \u05D6\u05D4"); // ?איזה צבע זה
            instructionText.gameObject.SetActive(true);
        }

        // Begin listening
        BeginListening();
    }

    private void BeginListening()
    {
        isRoundActive = true;

        // Start mic pulse animation
        if (micIcon != null)
        {
            micIcon.gameObject.SetActive(true);
            if (micPulseCoroutine != null) StopCoroutine(micPulseCoroutine);
            micPulseCoroutine = StartCoroutine(PulseMicIcon());
        }

        // Start recognition
        recognizer.StartListening();

        // Start timeout
        if (listenTimeoutCoroutine != null) StopCoroutine(listenTimeoutCoroutine);
        listenTimeoutCoroutine = StartCoroutine(ListenTimeoutCoroutine());
    }

    private void StopListeningUI()
    {
        isRoundActive = false;

        if (micPulseCoroutine != null)
        {
            StopCoroutine(micPulseCoroutine);
            micPulseCoroutine = null;
        }
        if (micIcon != null)
        {
            micIcon.transform.localScale = Vector3.one;
            micIcon.gameObject.SetActive(false);
        }
        if (listenTimeoutCoroutine != null)
        {
            StopCoroutine(listenTimeoutCoroutine);
            listenTimeoutCoroutine = null;
        }

        recognizer.StopListening();
    }

    // ── Recognizer Callbacks ──

    private void OnRecognizerReady()
    {
        Debug.Log("[ColorVoice] Recognizer ready, listening...");
    }

    private void OnRecognizerResults(string[] results)
    {
        if (!isRoundActive) return;

        if (debugText != null)
        {
            debugText.gameObject.SetActive(true);
            debugText.isRightToLeftText = false;
            debugText.text = results.Length > 0 ? HebrewFixer.Fix(string.Join(" | ", results)) : "";
        }

        // Log raw results with char codes for debugging
        foreach (var r in results)
        {
            var codes = new System.Text.StringBuilder();
            foreach (char c in r) codes.Append($"U+{(int)c:X4} ");
            Debug.Log($"[ColorVoice] Result: \"{r}\" chars: {codes}");
        }
        Debug.Log($"[ColorVoice] Target: \"{currentColor.hebrewName}\" normalized: \"{ColorVoiceData.Normalize(currentColor.hebrewName)}\"");

        // Check if correct
        if (ColorVoiceData.IsMatch(currentColor, results))
        {
            StopListeningUI();
            StartCoroutine(OnCorrectAnswer());
        }
        else
        {
            // Check if they said a different color
            var spokenColor = ColorVoiceData.FindSpokenColor(results);
            if (spokenColor != null)
            {
                // They said a real color, just the wrong one
                StopListeningUI();
                StartCoroutine(OnWrongColor(spokenColor));
            }
            else
            {
                // Unrecognized speech — retry
                StopListeningUI();
                StartCoroutine(OnUnrecognized());
            }
        }
    }

    private void OnRecognizerPartial(string partial)
    {
        if (debugText != null)
        {
            debugText.gameObject.SetActive(true);
            debugText.isRightToLeftText = false;
            debugText.text = HebrewFixer.Fix(partial);
        }

        // Android Hebrew recognition often only sends partials without final results.
        // Check partial matches so the game responds immediately when the child says the right word.
        if (!isRoundActive || string.IsNullOrEmpty(partial)) return;

        string[] partialAsArray = new[] { partial };
        if (ColorVoiceData.IsMatch(currentColor, partialAsArray))
        {
            Debug.Log($"[ColorVoice] Partial match accepted: \"{partial}\"");
            StopListeningUI();
            StartCoroutine(OnCorrectAnswer());
        }
    }

    private void OnRecognizerError(string error)
    {
        if (!isRoundActive) return;

        Debug.Log($"[ColorVoice] Recognition error: {error}");

        // For speech_timeout and no_match, silently restart
        if (error == "speech_timeout" || error == "no_match")
        {
            StopListeningUI();
            StartCoroutine(RestartListeningAfterDelay(0.3f));
            return;
        }

        // For other errors, show gentle prompt
        StopListeningUI();
        StartCoroutine(OnUnrecognized());
    }

    private IEnumerator ListenTimeoutCoroutine()
    {
        yield return new WaitForSeconds(listenTimeout);
        if (!isRoundActive) yield break;

        StopListeningUI();
        StartCoroutine(OnTimeoutPrompt());
    }

    // ── Feedback ──

    private IEnumerator OnCorrectAnswer()
    {
        // Happy feedback
        if (feedbackText != null)
        {
            feedbackText.text = HebrewFixer.Fix("!\u05DB\u05DC \u05D4\u05DB\u05D1\u05D5\u05D3"); // !כל הכבוד
            feedbackText.color = new Color(0.2f, 0.7f, 0.2f);
            feedbackText.gameObject.SetActive(true);
        }
        if (instructionText != null)
            instructionText.gameObject.SetActive(false);

        // Success sound
        if (successClip != null) audioSource.PlayOneShot(successClip);

        // Confetti!
        ConfettiController.Instance.Play();

        // Bounce animation on color circle
        yield return BounceAnimation(colorCircle.rectTransform);

        yield return new WaitForSeconds(successDelay);

        // Next round or complete
        currentRound++;
        if (currentRound >= totalRounds)
        {
            // Reshuffle and restart
            ShuffleColors();
            currentRound = 0;
        }

        StartCoroutine(StartRoundSequence());
    }

    private IEnumerator OnWrongColor(ColorPrompt spokenColor)
    {
        retryCount++;

        if (feedbackText != null)
        {
            feedbackText.text = HebrewFixer.Fix("\u05E0\u05E1\u05D5 \u05E9\u05D5\u05D1"); // נסו שוב
            feedbackText.color = new Color(0.9f, 0.5f, 0.2f);
            feedbackText.gameObject.SetActive(true);
        }

        if (wrongClip != null) audioSource.PlayOneShot(wrongClip);

        // Gentle shake
        yield return ShakeAnimation(colorCircle.rectTransform);

        yield return new WaitForSeconds(retryDelay);

        if (feedbackText != null) feedbackText.gameObject.SetActive(false);

        // Resume listening
        BeginListening();
    }

    private IEnumerator OnUnrecognized()
    {
        retryCount++;

        if (feedbackText != null)
        {
            feedbackText.text = HebrewFixer.Fix("\u05EA\u05D2\u05D9\u05D3\u05D5 \u05D0\u05EA \u05D4\u05E6\u05D1\u05E2"); // תגידו את הצבע
            feedbackText.color = new Color(0.5f, 0.5f, 0.5f);
            feedbackText.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(retryDelay);

        if (feedbackText != null) feedbackText.gameObject.SetActive(false);

        BeginListening();
    }

    private IEnumerator OnTimeoutPrompt()
    {
        if (feedbackText != null)
        {
            feedbackText.text = HebrewFixer.Fix("?\u05D0\u05D9\u05D6\u05D4 \u05E6\u05D1\u05E2 \u05D6\u05D4"); // ?איזה צבע זה
            feedbackText.color = new Color(0.5f, 0.5f, 0.5f);
            feedbackText.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(1.0f);

        if (feedbackText != null) feedbackText.gameObject.SetActive(false);

        BeginListening();
    }

    private IEnumerator RestartListeningAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        BeginListening();
    }

    // ── Animations ──

    private IEnumerator AnimateColorEntrance()
    {
        if (colorCircle == null) yield break;

        // Set color
        colorCircle.color = currentColor.color;
        if (colorLabel != null)
        {
            colorLabel.text = HebrewFixer.Fix(currentColor.hebrewName);
            colorLabel.isRightToLeftText = false;
        }

        // Scale-up bounce entrance
        var rt = colorCircle.rectTransform;
        float duration = 0.4f;
        float t = 0;

        rt.localScale = Vector3.zero;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            // Overshoot bounce: goes to 1.15 then settles to 1.0
            float scale = p < 0.7f
                ? Mathf.Lerp(0f, 1.15f, p / 0.7f)
                : Mathf.Lerp(1.15f, 1f, (p - 0.7f) / 0.3f);
            rt.localScale = Vector3.one * scale;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator BounceAnimation(RectTransform rt)
    {
        if (rt == null) yield break;

        float duration = 0.3f;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            float scale = 1f + 0.15f * Mathf.Sin(p * Mathf.PI);
            rt.localScale = Vector3.one * scale;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator ShakeAnimation(RectTransform rt)
    {
        if (rt == null) yield break;

        Vector2 orig = rt.anchoredPosition;
        float duration = 0.3f;
        float t = 0;
        float amplitude = 15f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float offset = Mathf.Sin(t * 40f) * amplitude * (1f - t / duration);
            rt.anchoredPosition = orig + new Vector2(offset, 0);
            yield return null;
        }
        rt.anchoredPosition = orig;
    }

    private IEnumerator PulseMicIcon()
    {
        if (micIcon == null) yield break;

        float speed = 2f;
        float minScale = 0.85f;
        float maxScale = 1.15f;

        while (true)
        {
            float t = (Mathf.Sin(Time.time * speed * Mathf.PI) + 1f) / 2f;
            float scale = Mathf.Lerp(minScale, maxScale, t);
            micIcon.transform.localScale = Vector3.one * scale;

            // Also pulse alpha slightly
            var c = micIcon.color;
            c.a = Mathf.Lerp(0.6f, 1f, t);
            micIcon.color = c;

            yield return null;
        }
    }

    // ── Navigation ──

    public void OnHomePressed()
    {
        StopListeningUI();
        NavigationManager.GoToMainMenu();
    }

    public void OnRestartPressed()
    {
        StopListeningUI();
        StopAllCoroutines();
        ShuffleColors();
        currentRound = 0;
        StartCoroutine(StartRoundSequence());
    }
}
