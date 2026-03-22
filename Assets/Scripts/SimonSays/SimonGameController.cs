using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simon Says color memory game controller.
/// Shows a sequence of colored buttons lighting up, player repeats it.
/// Each successful round adds one more color to the sequence.
/// Wooden toy board aesthetic with 4 large colored buttons.
/// </summary>
public class SimonGameController : BaseMiniGame
{
    [Header("Board")]
    public RectTransform boardRT;
    public Image boardImage;

    [Header("Buttons")]
    public Image[] colorButtons;          // Red, Yellow, Blue, Green
    public Button[] colorButtonComponents;
    public Image[] glowOverlays;          // glow layer on each button

    [Header("UI")]
    public TextMeshProUGUI roundText;
    public RectTransform playArea;

    [Header("Sprites")]
    public Sprite circleSprite;

    [Header("Settings")]
    public float sequenceStepDuration = 0.7f;
    public float sequencePauseDuration = 0.25f;
    public float inputGlowDuration = 0.45f;

    // Button colors
    private static readonly Color[] ButtonColors =
    {
        new Color(0.90f, 0.22f, 0.21f),  // Red
        new Color(0.98f, 0.80f, 0.18f),  // Yellow
        new Color(0.25f, 0.47f, 0.85f),  // Blue
        new Color(0.30f, 0.69f, 0.31f),  // Green
    };

    private static readonly Color[] GlowColors =
    {
        new Color(1f, 0.55f, 0.55f, 0.9f),  // Red glow
        new Color(1f, 0.93f, 0.55f, 0.9f),  // Yellow glow
        new Color(0.55f, 0.70f, 1f, 0.9f),  // Blue glow
        new Color(0.55f, 0.90f, 0.55f, 0.9f),// Green glow
    };

    private static readonly string[] ColorNames = { "Red", "Yellow", "Blue", "Green" };

    // State
    private readonly List<int> sequence = new List<int>();
    private int inputIndex;
    private int currentRound;
    private bool isPlayingSequence;
    private bool isAcceptingInput;
    private bool isAnimating;
    private int bestRound;
    private int _startSequenceLength = 2;
    private float _speedMultiplier = 1f;

    // ── BaseMiniGame Hooks ──────────────────────────────────────

    protected override string GetFallbackGameId() => "simonsays";

    protected override void OnGameInit()
    {
        isEndless = true;
        playConfettiOnRoundWin = false;
        playConfettiOnSessionWin = false;
        delayBeforeNextRound = 0f;

        // Apply difficulty
        _startSequenceLength = GameDifficultyConfig.SimonStartSequence(Difficulty);
        _speedMultiplier = GameDifficultyConfig.SimonSpeedMultiplier(Difficulty);
        Debug.Log($"[Difficulty] Game=simon Level={Difficulty} Sequence={_startSequenceLength} Speed={_speedMultiplier:F2}x");
    }

    protected override void OnRoundSetup()
    {
        // Load best score
        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
        {
            var stat = profile.progress.GetOrCreate("simonsays");
            bestRound = stat.bestScore;
        }

        // Wire button clicks and disable built-in color transitions
        for (int i = 0; i < colorButtonComponents.Length; i++)
        {
            int idx = i;
            colorButtonComponents[i].onClick.RemoveAllListeners();
            colorButtonComponents[i].onClick.AddListener(() => OnColorPressed(idx));
            // Prevent Button's own color tint from fighting our glow animation
            colorButtonComponents[i].transition = Selectable.Transition.None;
        }

        // Init glow overlays to transparent
        for (int i = 0; i < glowOverlays.Length; i++)
            glowOverlays[i].color = new Color(GlowColors[i].r, GlowColors[i].g, GlowColors[i].b, 0f);

        // Start first round after brief delay
        StartCoroutine(StartGameDelayed());
    }

    private IEnumerator StartGameDelayed()
    {
        yield return new WaitForSeconds(0.8f);

        // Position tutorial hand on the first color button
        if (TutorialHand != null && colorButtons.Length > 0)
        {
            var btnRT = colorButtons[0].GetComponent<RectTransform>();
            Vector2 localPos = TutorialHand.GetLocalCenter(btnRT);
            TutorialHand.SetPosition(localPos);
        }

        StartNewGame();
    }

    private void StartNewGame()
    {
        sequence.Clear();
        currentRound = 0;
        StartNextRound();
    }

    private void StartNextRound()
    {
        currentRound++;
        inputIndex = 0;
        isAcceptingInput = false;

        // Add random colors until sequence reaches target length
        // Starting length from difficulty, then +1 each round
        int targetLen = _startSequenceLength + (currentRound - 1);
        while (sequence.Count < targetLen)
            sequence.Add(Random.Range(0, 4));

        UpdateRoundText();
        StartCoroutine(PlaySequence());
    }

    private void UpdateRoundText()
    {
        if (roundText != null)
            roundText.text = currentRound.ToString();
    }

    // ── SEQUENCE PLAYBACK ──────────────────────────────────────

    private IEnumerator PlaySequence()
    {
        isPlayingSequence = true;
        SetButtonsInteractable(false);

        yield return new WaitForSeconds(0.5f);

        for (int i = 0; i < sequence.Count; i++)
        {
            int colorIdx = sequence[i];
            yield return StartCoroutine(FlashButton(colorIdx, sequenceStepDuration / _speedMultiplier));
            yield return new WaitForSeconds(sequencePauseDuration / _speedMultiplier);
        }

        isPlayingSequence = false;
        isAcceptingInput = true;
        inputIndex = 0;
        SetButtonsInteractable(true);
    }

    private IEnumerator FlashButton(int idx, float duration)
    {
        SoundLibrary.PlayColorName(ColorNames[idx]);

        // Smooth ease-in-out using sine curve
        float riseTime = duration * 0.3f;
        float holdTime = duration * 0.35f;
        float fallTime = duration * 0.35f;

        Vector3 origScale = Vector3.one;
        Vector3 bigScale = origScale * 1.12f;
        float maxAlpha = GlowColors[idx].a;

        // Also brighten the button color itself for a warm glow feel
        Color baseColor = colorButtons[idx].color;
        Color brightColor = Color.Lerp(baseColor, Color.white, 0.35f);

        // Rise: glow fades in, scale grows, color brightens
        float elapsed = 0f;
        while (elapsed < riseTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / riseTime));
            SetGlowAlpha(idx, t * maxAlpha);
            colorButtons[idx].rectTransform.localScale = Vector3.Lerp(origScale, bigScale, t);
            colorButtons[idx].color = Color.Lerp(baseColor, brightColor, t);
            yield return null;
        }
        SetGlowAlpha(idx, maxAlpha);
        colorButtons[idx].rectTransform.localScale = bigScale;
        colorButtons[idx].color = brightColor;

        // Hold at peak
        yield return new WaitForSeconds(holdTime);

        // Fall: smooth fade out
        elapsed = 0f;
        while (elapsed < fallTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fallTime));
            SetGlowAlpha(idx, maxAlpha * (1f - t));
            colorButtons[idx].rectTransform.localScale = Vector3.Lerp(bigScale, origScale, t);
            colorButtons[idx].color = Color.Lerp(brightColor, baseColor, t);
            yield return null;
        }
        SetGlowAlpha(idx, 0f);
        colorButtons[idx].rectTransform.localScale = origScale;
        colorButtons[idx].color = baseColor;
    }

    private void SetGlowAlpha(int idx, float alpha)
    {
        var c = GlowColors[idx];
        glowOverlays[idx].color = new Color(c.r, c.g, c.b, alpha);
    }

    // ── PLAYER INPUT ───────────────────────────────────────────

    private void OnColorPressed(int idx)
    {
        if (!isAcceptingInput || isAnimating) return;

        DismissTutorial();

        if (idx == sequence[inputIndex])
        {
            // Correct
            Stats?.RecordCorrect();
            StartCoroutine(CorrectInput(idx));
        }
        else
        {
            // Wrong
            Stats?.RecordMistake();
            Stats?.SetCustom("roundReached", currentRound);
            StartCoroutine(WrongInput());
        }
    }

    private IEnumerator CorrectInput(int idx)
    {
        isAnimating = true;
        SetButtonsInteractable(false);

        // Quick glow feedback
        yield return StartCoroutine(FlashButton(idx, inputGlowDuration));

        inputIndex++;

        if (inputIndex >= sequence.Count)
        {
            // Completed the full sequence
            yield return StartCoroutine(RoundComplete());
        }
        else
        {
            isAnimating = false;
            SetButtonsInteractable(true);
        }
    }

    private IEnumerator RoundComplete()
    {
        isAcceptingInput = false;

        // Board bounce
        yield return StartCoroutine(BoardBounce());

        // Confetti every 3 rounds
        if (currentRound % 3 == 0)
            ConfettiController.Instance?.Play();

        // Save progress
        SaveProgress();

        // Record round complete for stats tracking
        CompleteRound();

        yield return new WaitForSeconds(0.6f);

        isAnimating = false;
        StartNextRound();
    }

    private IEnumerator WrongInput()
    {
        isAcceptingInput = false;
        isAnimating = true;
        SetButtonsInteractable(false);

        // Board shake
        yield return StartCoroutine(BoardShake());

        yield return new WaitForSeconds(0.8f);

        // Save progress
        SaveProgress();

        // Restart
        isAnimating = false;
        StartNewGame();
    }

    // ── ANIMATIONS ─────────────────────────────────────────────

    private IEnumerator BoardBounce()
    {
        Vector3 orig = boardRT.localScale;
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Bounce curve: overshoot then settle
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.04f;
            boardRT.localScale = orig * scale;
            yield return null;
        }
        boardRT.localScale = orig;
    }

    private IEnumerator BoardShake()
    {
        Vector2 origPos = boardRT.anchoredPosition;
        float duration = 0.4f;
        float elapsed = 0f;
        float magnitude = 12f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float decay = 1f - t;
            float offsetX = Mathf.Sin(t * 40f) * magnitude * decay;
            boardRT.anchoredPosition = origPos + new Vector2(offsetX, 0);
            yield return null;
        }
        boardRT.anchoredPosition = origPos;
    }

    // ── HELPERS ────────────────────────────────────────────────

    private void SetButtonsInteractable(bool interactable)
    {
        for (int i = 0; i < colorButtonComponents.Length; i++)
            colorButtonComponents[i].interactable = interactable;
    }

    private void SaveProgress()
    {
        if (currentRound > bestRound)
            bestRound = currentRound;

        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
        {
            var stat = profile.progress.GetOrCreate("simonsays");
            stat.timesPlayed++;
            if (currentRound > stat.bestScore) stat.bestScore = currentRound;
            stat.lastPlayedAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ProfileManager.Instance?.Save();
        }
    }

    // ── NAVIGATION ─────────────────────────────────────────────

    public void OnHomePressed() => ExitGame();
}
