using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central analytics engine. Receives game session results, updates all profiles,
/// adjusts difficulty, and persists to the active profile.
///
/// DontDestroyOnLoad singleton. Games call RegisterGameSession() on completion.
/// </summary>
public class StatsManager : MonoBehaviour
{
    public static StatsManager Instance { get; private set; }

    private GameCategoryMapping _categoryMapping;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("StatsManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<StatsManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private GameCategoryMapping GetCategoryMapping()
    {
        if (_categoryMapping != null) return _categoryMapping;
        _categoryMapping = Resources.Load<GameCategoryMapping>("Analytics/GameCategoryMapping");
        return _categoryMapping;
    }

    // ── Public API ──────────────────────────────────────────────

    /// <summary>
    /// Main entry point. Call after a game session ends.
    /// Updates game profile, category scores, global score, difficulty, and saves.
    /// </summary>
    public void RegisterGameSession(GameSessionData session)
    {
        if (session == null || string.IsNullOrEmpty(session.gameId)) return;

        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        var analytics = profile.analytics;

        // 1. Score the session using the game's scoring strategy
        var strategy = ScoringStrategyRegistry.Get(session.gameId);
        var breakdown = strategy.CalculateSessionScore(session);
        session.sessionScore = breakdown.finalScore;

        // 2. Update game performance profile
        var gameProfile = analytics.GetOrCreateGame(session.gameId);

        // Initialize difficulty on first play (skip if parent manually set difficulty)
        if (gameProfile.sessionsPlayed == 0 && !gameProfile.manualDifficultyOverride)
            gameProfile.currentDifficulty = DifficultyManager.GetInitialDifficulty(session.gameId, profile.age * 12);

        gameProfile.AddSession(session);

        // 3. Update totals
        analytics.totalSessions++;
        analytics.totalPlayTime += session.durationSeconds;

        // 4. Update category profiles
        UpdateCategories(analytics);

        // 5. Update global score
        UpdateGlobalScore(analytics);

        // 6. Update favorites
        analytics.UpdateFavorites();

        // 7. Adjust difficulty
        bool diffChanged = DifficultyManager.Evaluate(gameProfile);

        // 8. Save
        ProfileManager.Instance?.Save();

        // Detailed score breakdown log
        var expect = strategy.GetExpectation(session.difficultyLevel);
        Debug.Log($"[Analytics][ScoreBreakdown]\n" +
            $"  Game={session.gameId}\n" +
            $"  Difficulty={session.difficultyLevel}\n" +
            $"  Duration={session.durationSeconds:F1}s  (expected {expect.expectedDurationMin:F0}-{expect.expectedDurationMax:F0}s)\n" +
            $"  Actions={session.totalActions}  Correct={session.correctActions}  Mistakes={session.mistakes}  Hints={session.hintsUsed}\n" +
            $"  ──────────────────────────\n" +
            $"  SuccessScore   = {breakdown.successScore:F0}  (w={breakdown.successWeight:P0})\n" +
            $"  AccuracyScore  = {breakdown.accuracyScore:F0}  (w={breakdown.accuracyWeight:P0})\n" +
            $"  SpeedScore     = {breakdown.speedScore:F0}  (w={breakdown.speedWeight:P0})\n" +
            $"  Independence   = {breakdown.independenceScore:F0}  (w={breakdown.independenceWeight:P0})\n" +
            $"  DifficultyBonus= {breakdown.difficultyBonus:F0}  (w={breakdown.difficultyWeight:P0})\n" +
            $"  ──────────────────────────\n" +
            $"  SessionScore   = {breakdown.finalScore:F0}\n" +
            $"  ProfileScore   = {gameProfile.performanceScore:F0}/100\n" +
            $"  Difficulty     = {gameProfile.currentDifficulty}/10{(diffChanged ? " (CHANGED)" : "")}\n" +
            $"  Sessions       = {gameProfile.sessionsPlayed}\n" +
            $"  Global         = {analytics.globalScore:F0}/100\n" +
            $"  Trend          = {(gameProfile.improvementTrend >= 0 ? "+" : "")}{gameProfile.improvementTrend:F1}");
    }

    /// <summary>Returns the current difficulty for a game. Initializes if first time.</summary>
    public int GetGameDifficulty(string gameId)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return 1;

        var gameProfile = profile.analytics.GetOrCreateGame(gameId);
        if (gameProfile.sessionsPlayed == 0 && !gameProfile.manualDifficultyOverride)
        {
            gameProfile.currentDifficulty = DifficultyManager.GetInitialDifficulty(gameId, profile.age * 12);
        }
        return gameProfile.currentDifficulty;
    }

    // ── Parent Dashboard API ────────────────────────────────────

    public GamePerformanceProfile GetGameProfile(string gameId)
    {
        var profile = ProfileManager.ActiveProfile;
        return profile?.analytics.GetOrCreateGame(gameId);
    }

    public CategoryProfile GetCategoryProfile(SkillCategory category)
    {
        var profile = ProfileManager.ActiveProfile;
        return profile?.analytics.GetOrCreateCategory(category);
    }

    public float GetGlobalScore()
    {
        var profile = ProfileManager.ActiveProfile;
        return profile?.analytics.globalScore ?? 0f;
    }

    public List<GameSessionData> GetRecentSessions(string gameId)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return new List<GameSessionData>();
        var gp = profile.analytics.GetOrCreateGame(gameId);
        return gp.recentSessions;
    }

    // ── Internal ────────────────────────────────────────────────

    private void UpdateCategories(ChildAnalyticsProfile analytics)
    {
        var mapping = GetCategoryMapping();
        if (mapping == null) return;

        // For each category, compute weighted score from contributing games
        var allCategories = System.Enum.GetValues(typeof(SkillCategory));
        foreach (SkillCategory cat in allCategories)
        {
            var catProfile = analytics.GetOrCreateCategory(cat);

            float totalWeight = 0f;
            float weightedScore = 0f;
            float weightedTrend = 0f;
            int contributing = 0;
            int totalSessions = 0;

            foreach (var entry in mapping.entries)
            {
                float catWeight = 0f;
                foreach (var w in entry.weights)
                {
                    if (w.category == cat)
                    {
                        catWeight = w.weight;
                        break;
                    }
                }
                if (catWeight <= 0f) continue;

                var gameProfile = analytics.GetOrCreateGame(entry.gameId);
                if (gameProfile.sessionsPlayed == 0) continue;

                weightedScore += gameProfile.performanceScore * catWeight;
                weightedTrend += gameProfile.improvementTrend * catWeight;
                totalWeight += catWeight;
                totalSessions += gameProfile.sessionsPlayed;
                contributing++;
            }

            catProfile.contributingGames = contributing;
            catProfile.totalWeightedSessions = totalSessions;

            if (totalWeight > 0f)
            {
                catProfile.categoryScore = weightedScore / totalWeight;
                catProfile.trend = weightedTrend / totalWeight;
            }

            // Confidence: ramps up with session count (10 sessions = 0.5, 30+ = ~1.0)
            catProfile.confidence = Mathf.Clamp01(totalSessions / 30f);
        }
    }

    private void UpdateGlobalScore(ChildAnalyticsProfile analytics)
    {
        if (analytics.categories.Count == 0)
        {
            analytics.globalScore = 0f;
            return;
        }

        float total = 0f;
        float totalConfidence = 0f;

        foreach (var cat in analytics.categories)
        {
            if (cat.contributingGames == 0) continue;
            total += cat.categoryScore * cat.confidence;
            totalConfidence += cat.confidence;
        }

        analytics.globalScore = totalConfidence > 0f ? total / totalConfidence : 0f;
    }
}
