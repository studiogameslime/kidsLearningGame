using System.Collections;
using UnityEngine;

/// <summary>
/// Shared base class for all mini-games. Owns the common lifecycle:
/// stats collector creation, difficulty loading, completion guard,
/// confetti/feedback triggering, round looping, and journey integration.
///
/// Games inherit and override hooks: OnGameInit, OnRoundSetup, OnRoundCleanup,
/// OnBeforeComplete, OnAfterComplete. Games call CompleteRound() when the
/// player wins a round.
///
/// Works with existing infrastructure: GameCompletionBridge, ConfettiController,
/// GameStatsCollector, SoundLibrary, JourneyManager — does not replace them.
/// </summary>
public abstract class BaseMiniGame : MonoBehaviour
{
    // ── Configuration (set by derived class in OnGameInit) ─────────

    [Header("Base Mini Game")]
    [SerializeField] protected int totalRounds = 1;
    [SerializeField] protected bool isEndless;

    // Feedback toggles (defaults match current game behavior)
    protected bool playConfettiOnRoundWin = false;   // confetti per round (most games: false)
    protected bool playConfettiOnSessionWin = true;   // confetti when all rounds done (most games: true)
    protected bool playWinSound = true;               // SoundLibrary.PlayRandomFeedback()
    protected bool showSummaryOnComplete = true;      // show success panel after final round
    protected string contentCategory = "";            // SessionContent.Animals, etc.

    // Timing
    protected float delayBeforeNextRound = 0.3f;
    protected float delayAfterFinalRound = 1.0f;

    // ── State (read-only for derived classes) ──────────────────────

    public enum GameState { Initializing, Playing, Completing, WaitingForTransition }

    protected GameState CurrentState { get; private set; } = GameState.Initializing;
    protected int CurrentRound { get; private set; }
    protected int Difficulty { get; private set; } = 1;
    protected GameStatsCollector Stats { get; private set; }
    protected string GameId { get; private set; }

    /// <summary>True during completion sequence — games should block input when this is true.</summary>
    protected bool IsInputLocked => CurrentState == GameState.Completing
                                 || CurrentState == GameState.WaitingForTransition;

    // Tutorial hand (auto-found if present in scene)
    private TutorialHand _tutorialHand;

    // ── Unity Lifecycle ────────────────────────────────────────────

    protected virtual void Start()
    {
        // Read game identity
        GameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : GetFallbackGameId();

        // Load difficulty
        Difficulty = GameDifficultyConfig.GetLevel(GameId);

        // Find tutorial hand if present
        _tutorialHand = FindObjectOfType<TutorialHand>();

        // Let derived class configure
        OnGameInit();

        // Start first round
        CurrentRound = 0;
        SetupNewRound();

        CurrentState = GameState.Playing;
    }

    protected virtual void Update()
    {
        if (CurrentState == GameState.Playing)
            OnGameplayUpdate();
    }

    // ── Hooks for Derived Classes ──────────────────────────────────

    /// <summary>
    /// Called once at start. Set config fields here:
    /// totalRounds, playConfettiOnSessionWin, contentCategory, etc.
    /// Also build any one-time UI that persists across rounds.
    /// </summary>
    protected virtual void OnGameInit() { }

    /// <summary>
    /// Called at the start of each round. Generate content, spawn objects,
    /// reset per-round state. Stats collector is already created.
    /// </summary>
    protected virtual void OnRoundSetup() { }

    /// <summary>
    /// Called from Update() only while state == Playing.
    /// Use for continuous gameplay checks (e.g. drag detection).
    /// </summary>
    protected virtual void OnGameplayUpdate() { }

    /// <summary>
    /// Called just before completion feedback plays.
    /// Last chance to set custom stats, scores, etc.
    /// </summary>
    protected virtual void OnBeforeComplete() { }

    /// <summary>
    /// Called after completion feedback has played.
    /// Do visual cleanup, disable game UI, etc.
    /// Return a coroutine-style delay if you need extra animation time.
    /// </summary>
    protected virtual IEnumerator OnAfterComplete() { yield break; }

    /// <summary>
    /// Called before the next round starts (teardown spawned objects).
    /// </summary>
    protected virtual void OnRoundCleanup() { }

    /// <summary>
    /// Called when the player exits mid-game (back/home button).
    /// Override to add custom cleanup. Always call base.OnGameExit().
    /// </summary>
    protected virtual void OnGameExit()
    {
        if (Stats != null && CurrentState == GameState.Playing)
            Stats.Abandon();
    }

    /// <summary>Override to provide a fallback game ID if GameContext is empty.</summary>
    protected virtual string GetFallbackGameId() => "unknown";

    /// <summary>Override to provide content ID for stats (e.g. animal category key).</summary>
    protected virtual string GetContentId()
    {
        return GameContext.CurrentSelection?.categoryKey;
    }

    // ── Protected API for Derived Classes ──────────────────────────

    /// <summary>
    /// Call this when the player wins/completes the current round.
    /// Handles: guard against duplicates, feedback, stats, round advance.
    /// This is the ONLY way to trigger completion — do NOT call confetti directly.
    /// </summary>
    protected void CompleteRound()
    {
        if (CurrentState != GameState.Playing) return; // duplicate guard
        CurrentState = GameState.Completing;
        StartCoroutine(CompletionSequence());
    }

    /// <summary>Record a correct action. If isLast=true, plays Win Level sound instead of Correct.</summary>
    protected void RecordCorrect(string tag = null, string targetId = null, bool isLast = false)
    {
        Stats?.RecordCorrect(tag, targetId);
        if (isLast)
        {
            if (_winLevelClip == null) _winLevelClip = Resources.Load<AudioClip>("Sounds/WinLevel");
            if (_winLevelClip != null) BackgroundMusicManager.PlayOneShot(_winLevelClip);
        }
        else
        {
            if (_correctClip == null) _correctClip = Resources.Load<AudioClip>("Sounds/Correct");
            if (_correctClip != null) BackgroundMusicManager.PlayOneShot(_correctClip);
        }
    }

    /// <summary>Shorthand: record a mistake + play error sound.</summary>
    protected void RecordMistake(string tag = null, string targetId = null)
    {
        Stats?.RecordMistake(tag, targetId);
        if (_errorClip == null) _errorClip = Resources.Load<AudioClip>("Sounds/Error");
        if (_errorClip != null) BackgroundMusicManager.PlayOneShot(_errorClip);
    }

    /// <summary>Shorthand: record a hint usage.</summary>
    protected void RecordHint()
    {
        Stats?.RecordHint();
    }

    /// <summary>Dismiss the tutorial hand overlay (call on first player interaction).</summary>
    protected void DismissTutorial()
    {
        if (_tutorialHand != null) _tutorialHand.Dismiss();
    }

    /// <summary>Access the tutorial hand to position it on real game elements.</summary>
    protected TutorialHand TutorialHand => _tutorialHand;

    /// <summary>
    /// Play a sparkle/stars effect on a UI element (use after correct answers).
    /// Also shows a floating "+1" score popup.
    /// </summary>
    private static AudioClip _correctClip;
    private static AudioClip _errorClip;
    private static AudioClip _winLevelClip;

    protected void PlayCorrectEffect(RectTransform target)
    {
        UIEffects.SpawnSparkles(target);
    }

    /// <summary>
    /// Show a floating score popup near a UI element (e.g. "+1", "+5", "Great!").
    /// </summary>
    protected void ShowFloatingScore(RectTransform target, string text = "+1")
    {
        FloatingScore.Show(target, text);
    }

    /// <summary>
    /// Shake a UI element briefly (use after wrong answers).
    /// </summary>
    protected void PlayWrongEffect(RectTransform target)
    {
        UIEffects.Shake(this, target);
    }

    /// <summary>Navigate home / main menu. Abandons stats if still playing.</summary>
    protected void ExitGame()
    {
        OnGameExit();
        NavigationManager.GoToMainMenu();
    }

    // ── Internal Flow ──────────────────────────────────────────────

    private void SetupNewRound()
    {
        // Create fresh stats collector for this round
        string contentId = GetContentId();
        if (!string.IsNullOrEmpty(contentId) && !string.IsNullOrEmpty(contentCategory))
            Stats = new GameStatsCollector(GameId, contentId, contentCategory, isEndless);
        else
            Stats = new GameStatsCollector(GameId);

        // Register with bridge so confetti auto-triggers analytics
        if (GameCompletionBridge.Instance != null)
            GameCompletionBridge.Instance.ActiveCollector = Stats;

        Stats.SetTotalRoundsPlanned(isEndless ? 0 : totalRounds);
        Stats.SetCustom("difficulty", Difficulty);

        OnRoundSetup();
    }

    private bool _summaryPlayAgain;

    private IEnumerator CompletionSequence()
    {
        // Let game set final stats
        OnBeforeComplete();
        Stats?.RecordRoundComplete();

        // Register this round's session immediately (every round = 1 session)
        Stats?.Finalize(completed: true);

        bool isFinalRound = !isEndless && (CurrentRound + 1 >= totalRounds);
        bool shouldPlayConfetti = isFinalRound ? playConfettiOnSessionWin : playConfettiOnRoundWin;

        // Lock navigation during celebration
        if (shouldPlayConfetti && GameCompletionBridge.Instance != null)
            GameCompletionBridge.Instance.LockNavigation();

        // Confetti BEFORE exit animations so player sees it with the game content still visible
        if (shouldPlayConfetti && ConfettiController.Instance != null)
            ConfettiController.Instance.Play();

        // Game-specific post-completion visuals (exit animations, cleanup)
        yield return StartCoroutine(OnAfterComplete());

        // ── Wait for ALL sounds and effects to finish before advancing ──
        // 1. Wait a moment for win level sound to be audible
        yield return new WaitForSeconds(0.8f);

        // 2. Alin voice feedback — wait for full clip to finish
        float feedbackDuration = 0f;
        if (playWinSound)
            feedbackDuration = SoundLibrary.PlayRandomFeedbackWithDuration();
        if (feedbackDuration > 0f)
            yield return new WaitForSeconds(feedbackDuration + 0.4f);
        else
            yield return new WaitForSeconds(0.3f); // brief pause even without feedback

        // 3. Wait for confetti to finish BEFORE any round/discovery logic
        while (ConfettiController.Instance != null && ConfettiController.Instance.IsPlaying)
            yield return null;

        // ── Award star + check discovery BEFORE advancing ──
        if (shouldPlayConfetti && GameCompletionBridge.Instance != null)
        {
            bool discoveryLoaded = GameCompletionBridge.Instance.AwardAndCheckDiscovery();
            if (discoveryLoaded)
                yield break; // DiscoveryReveal scene is loading — do NOT start a new round
        }

        // ── Advance to next round (always continue) ──
        CurrentRound++;
        if (CurrentRound >= totalRounds && !isEndless)
            CurrentRound = 0;

        OnRoundCleanup();
        SetupNewRound();
        CurrentState = GameState.Playing;
    }
}
