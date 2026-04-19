using System.Collections;
using System.Collections.Generic;
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
    protected string contentCategory = "";            // SessionContent.Animals, etc.

    // Timing
    protected float delayBeforeNextRound = 0.3f;
    protected float delayAfterFinalRound = 1.0f;
    private float _gameSceneStartTime; // tracks total time in this game scene

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

        // Firebase: log game started + screen view (enables engagement time tracking per game)
        _gameSceneStartTime = Time.realtimeSinceStartup;
        FirebaseAnalyticsManager.LogGameStarted(GameId, Difficulty);
        FirebaseAnalyticsManager.LogScreenView($"game_{GameId}");

        // Track first game ever played (once per profile)
        var profile = ProfileManager.ActiveProfile;
        if (profile != null && (profile.journey?.totalGamesCompleted ?? 0) == 0
            && !PlayerPrefs.HasKey($"first_game_{profile.id}"))
        {
            PlayerPrefs.SetInt($"first_game_{profile.id}", 1);
            FirebaseAnalyticsManager.LogFirstGamePlayed(GameId);
        }

        // Reset auto-switch counter for this fresh game session
        ResetAutoSwitchForNewSession();

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
        if (StickerPopup.IsActive) return;
        if (CurrentState == GameState.Completing) return; // block exit during celebration/completion
        if (BubbleTransition.IsActive) return; // block if already transitioning
        float duration = Time.realtimeSinceStartup - _gameSceneStartTime;
        FirebaseAnalyticsManager.LogGameExited(GameId);
        FirebaseAnalyticsManager.LogGameSessionDuration(GameId, duration);
        TwoPlayerManager.End();
        // Reset auto-switch counter so re-entering this game starts fresh
        _roundsInCurrentGame = 0;
        _lastGameId = null;
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


    private IEnumerator CompletionSequence()
    {
        // Let game set final stats
        OnBeforeComplete();
        Stats?.RecordRoundComplete();

        // Register this round's session immediately (every round = 1 session)
        // Must finalize BEFORE logging to Firebase so the scoring strategy computes the real score.
        Stats?.Finalize(completed: true);

        // Firebase: log completion with real score and real duration
        int mistakes = Stats != null ? Stats.Mistakes : 0;
        float sessionScore = Stats != null ? Stats.SessionScore : 0f;
        float duration = Time.realtimeSinceStartup - _gameSceneStartTime;
        FirebaseAnalyticsManager.LogGameCompleted(GameId, Difficulty, sessionScore, mistakes, duration);

        // Check milestones
        CheckMilestones();

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

        // ── Award star + sticker + check discovery BEFORE advancing ──
        // In 2-player mode: skip rewards (stars, stickers, discoveries) but count games for both players
        if (TwoPlayerManager.IsActive)
        {
            GameCompletionBridge.IncrementGameCount(TwoPlayerManager.Player1);
            GameCompletionBridge.IncrementGameCount(TwoPlayerManager.Player2);
        }
        else if (GameCompletionBridge.Instance != null)
        {
            bool discoveryLoaded = shouldPlayConfetti
                ? GameCompletionBridge.Instance.AwardAndCheckDiscovery()
                : false;

            // Award sticker even without confetti (sticker pacing is independent)
            if (!shouldPlayConfetti)
                GameCompletionBridge.Instance.AwardStickerOnly();

            // Show balloon popup — sticker or achievement (not both, achievement takes priority)
            string awardedAchievement = GameCompletionBridge.Instance.LastAwardedAchievementId;
            string awardedSticker = GameCompletionBridge.Instance.LastAwardedStickerId;
            string popupStickerId = awardedAchievement ?? awardedSticker;

            if (!string.IsNullOrEmpty(popupStickerId))
            {
                // Sticker added to collection ONLY when balloon is popped
                StickerPopup.OnStickerCollected = (id) => GameCompletionBridge.CollectSticker(id);
                yield return StartCoroutine(StickerPopup.Show(popupStickerId));
                StickerPopup.OnStickerCollected = null;
            }

            if (discoveryLoaded)
                yield break; // DiscoveryReveal scene is loading — do NOT start a new round
        }

        // ── Auto-switch check (before advancing round) ──
        if (ShouldAutoSwitch())
        {
            AutoSwitchToNextGame();
            yield break;
        }

        // ── Advance to next round (always continue) ──
        CurrentRound++;
        if (CurrentRound >= totalRounds && !isEndless)
            CurrentRound = 0;

        OnRoundCleanup();
        SetupNewRound();
        CurrentState = GameState.Playing;
    }

    // ── Auto-Switch Games ──

    private static int _roundsInCurrentGame;
    private static string _lastGameId;
    private static readonly List<string> _visitedGamesThisSession = new List<string>();
    private const int RoundsBeforeSwitch = 5;
    private const int MaxVisitedBeforeReset = 15;

    // Games that should never auto-switch (creative/sandbox)
    private static readonly HashSet<string> NoSwitchGames = new HashSet<string>
    {
        "coloring"
    };

    /// <summary>Reset auto-switch counter when a game scene starts fresh.</summary>
    private void ResetAutoSwitchForNewSession()
    {
        // Always reset counter when entering a game from menu (not from auto-switch)
        // Auto-switch sets _lastGameId to the next game before loading, so if they match
        // it means we arrived via auto-switch and counter is already 0.
        // If _lastGameId doesn't match, it's a fresh menu entry.
        if (_lastGameId != GameId)
        {
            _roundsInCurrentGame = 0;
            _lastGameId = GameId;
        }

        // Prevent unbounded growth
        if (_visitedGamesThisSession.Count > MaxVisitedBeforeReset)
            _visitedGamesThisSession.Clear();
    }

    /// <summary>Call on profile switch to prevent cross-profile counter leaking.</summary>
    public static void ResetAutoSwitchState()
    {
        _roundsInCurrentGame = 0;
        _lastGameId = null;
        _visitedGamesThisSession.Clear();
    }

    private bool ShouldAutoSwitch()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null || !profile.autoSwitchGames) return false;
        if (TwoPlayerManager.IsActive) return false;
        if (NoSwitchGames.Contains(GameId)) return false;
        if (isEndless) return false; // endless games: player chose to keep playing

        _roundsInCurrentGame++;

        Debug.Log($"[AutoSwitch] {GameId}: round {_roundsInCurrentGame}/{RoundsBeforeSwitch}");
        return _roundsInCurrentGame >= RoundsBeforeSwitch;
    }

    private void AutoSwitchToNextGame()
    {
        // Mark this game as visited
        if (!_visitedGamesThisSession.Contains(GameId))
            _visitedGamesThisSession.Add(GameId);

        // Find the game database
        var db = Resources.Load<GameDatabase>("GameDatabase");
        if (db == null)
        {
            var dbs = Resources.FindObjectsOfTypeAll<GameDatabase>();
            if (dbs.Length > 0) db = dbs[0];
        }
        if (db == null)
        {
            // Last resort: find via any scene object that holds it
            var home = Object.FindObjectOfType<HomeController>();
            if (home != null) db = home.gameDatabase;
            if (db == null)
            {
                var pdCtrl = Object.FindObjectOfType<ParentDashboardController>();
                if (pdCtrl != null) db = pdCtrl.gameDatabase;
            }
        }
        if (db == null || db.games == null)
        {
            Debug.LogWarning("[AutoSwitch] GameDatabase not found — cannot switch");
            return;
        }

        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        // Build list of eligible games (visible, not current, not endless/sandbox, not recently visited)
        var candidates = new List<GameItemData>();
        var fallbacks = new List<GameItemData>(); // visited but still eligible

        Debug.Log($"[AutoSwitch] Found GameDatabase with {db.games.Count} games");

        foreach (var game in db.games)
        {
            if (game == null) { Debug.Log("[AutoSwitch] null game entry"); continue; }
            if (game.id == GameId) continue;
            if (NoSwitchGames.Contains(game.id)) continue;

            var visibility = GameVisibilityService.Evaluate(profile, game);
            Debug.Log($"[AutoSwitch] Game {game.id}: visible={visibility.isVisible}");
            if (!visibility.isVisible) continue;

            if (_visitedGamesThisSession.Contains(game.id))
                fallbacks.Add(game);
            else
                candidates.Add(game);
        }

        // Prefer unvisited, fall back to visited if all exhausted
        var pool = candidates.Count > 0 ? candidates : fallbacks;
        Debug.Log($"[AutoSwitch] candidates={candidates.Count}, fallbacks={fallbacks.Count}, pool={pool.Count}");
        if (pool.Count == 0)
        {
            Debug.LogWarning("[AutoSwitch] No eligible games to switch to!");
            return;
        }

        // Pick random from pool
        var nextGame = pool[Random.Range(0, pool.Count)];

        // Reset counter for next game
        _roundsInCurrentGame = 0;
        _lastGameId = nextGame.id;

        // Log and navigate
        FirebaseAnalyticsManager.LogParentChangedSetting("auto_switch_triggered",
            $"{GameId}_to_{nextGame.id}");

        Debug.Log($"[AutoSwitch] Switching from {GameId} to {nextGame.id} ({nextGame.targetSceneName})");

        // Exit current game cleanly
        OnGameExit();
        GameContext.CurrentGame = nextGame;
        GameContext.CurrentSelection = null;
        BubbleTransition.LoadScene(nextGame.targetSceneName);
    }

    // ── Milestones ──

    private static readonly int[] GameMilestones = { 10, 25, 50, 100, 200, 500 };
    private static readonly int[] AnimalMilestones = { 5, 10, 15, 19 };
    private static readonly int[] ColorMilestones = { 3, 5, 8, 11 };

    private void CheckMilestones()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        int games = profile.journey?.totalGamesCompleted ?? 0;

        string key = $"milestone_games_{profile.id}";
        int lastLogged = PlayerPrefs.GetInt(key, 0);

        foreach (int m in GameMilestones)
        {
            if (games >= m && lastLogged < m)
            {
                FirebaseAnalyticsManager.LogMilestoneReached("games_played", m);
                PlayerPrefs.SetInt(key, m);
                break; // one milestone per completion
            }
        }

        int animals = profile.journey?.unlockedAnimalIds?.Count ?? 0;
        string aKey = $"milestone_animals_{profile.id}";
        int lastAnimal = PlayerPrefs.GetInt(aKey, 0);
        foreach (int m in AnimalMilestones)
        {
            if (animals >= m && lastAnimal < m)
            {
                FirebaseAnalyticsManager.LogMilestoneReached("animals_discovered", m);
                PlayerPrefs.SetInt(aKey, m);
                break;
            }
        }

        int colors = profile.journey?.unlockedColorIds?.Count ?? 0;
        string cKey = $"milestone_colors_{profile.id}";
        int lastColor = PlayerPrefs.GetInt(cKey, 0);
        foreach (int m in ColorMilestones)
        {
            if (colors >= m && lastColor < m)
            {
                FirebaseAnalyticsManager.LogMilestoneReached("colors_discovered", m);
                PlayerPrefs.SetInt(cKey, m);
                break;
            }
        }
    }
}
