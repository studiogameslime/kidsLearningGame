using UnityEngine;

/// <summary>
/// Per-game difficulty progression based on consecutive performance streaks.
///
/// Rules:
///   - 2 consecutive STRONG results → difficulty +1
///   - 2 consecutive WEAK results → difficulty -1
///   - NEUTRAL result → resets both streaks, no change
///
/// Performance evaluation per round:
///   STRONG: few/no mistakes AND fast completion
///   WEAK: many mistakes OR very slow completion
///   NEUTRAL: anything in between
///
/// Each game adapts independently. No cross-game influence.
/// Estimated age is only used for initial difficulty seeding.
/// </summary>
public static class DifficultyManager
{
    private const int MinDifficulty = 1;
    private const int MaxDifficulty = 10;
    private const int StreakRequired = 2;

    /// <summary>
    /// Performance classification for a single round.
    /// </summary>
    public enum PerformanceLevel { Strong, Neutral, Weak }

    /// <summary>
    /// Evaluates a completed session and adjusts difficulty if streak threshold is reached.
    /// Returns true if difficulty was changed.
    /// </summary>
    public static bool Evaluate(GamePerformanceProfile profile)
    {
        if (profile == null) return false;

        // Skip when parent has manually set difficulty
        if (profile.manualDifficultyOverride)
            return false;

        // Need at least 1 session
        int count = profile.recentSessions.Count;
        if (count == 0) return false;

        // Evaluate the most recent session
        var latest = profile.recentSessions[count - 1];
        PerformanceLevel perf = EvaluateSession(latest);

        // Update streaks
        switch (perf)
        {
            case PerformanceLevel.Strong:
                profile.consecutiveStrongResults++;
                profile.consecutiveWeakResults = 0;
                break;
            case PerformanceLevel.Weak:
                profile.consecutiveWeakResults++;
                profile.consecutiveStrongResults = 0;
                break;
            case PerformanceLevel.Neutral:
                profile.consecutiveStrongResults = 0;
                profile.consecutiveWeakResults = 0;
                return false; // no change on neutral
        }

        // Check if streak threshold reached
        if (profile.consecutiveStrongResults >= StreakRequired)
        {
            if (profile.currentDifficulty < MaxDifficulty)
            {
                profile.currentDifficulty++;
                profile.lastAutoDifficulty = profile.currentDifficulty;
                profile.consecutiveStrongResults = 0;
                profile.consecutiveWeakResults = 0;
                if (profile.currentDifficulty > profile.highestDifficultyReached)
                    profile.highestDifficultyReached = profile.currentDifficulty;
                return true;
            }
            profile.consecutiveStrongResults = 0; // cap reached, reset
        }

        if (profile.consecutiveWeakResults >= StreakRequired)
        {
            if (profile.currentDifficulty > MinDifficulty)
            {
                profile.currentDifficulty--;
                profile.lastAutoDifficulty = profile.currentDifficulty;
                profile.consecutiveStrongResults = 0;
                profile.consecutiveWeakResults = 0;
                return true;
            }
            profile.consecutiveWeakResults = 0; // floor reached, reset
        }

        return false;
    }

    /// <summary>
    /// Classifies a session as Strong, Neutral, or Weak based on mistakes and time.
    /// </summary>
    public static PerformanceLevel EvaluateSession(GameSessionData session)
    {
        if (session == null || session.abandoned)
            return PerformanceLevel.Weak;

        if (!session.completed)
            return PerformanceLevel.Weak;

        // ── Mistake-based evaluation ──
        int mistakes = session.mistakes;
        float duration = session.durationSeconds;
        int totalActions = Mathf.Max(session.totalActions, 1);
        float accuracy = (float)session.correctActions / totalActions;

        // Strong: high accuracy (≥85%) and reasonable time
        // Weak: low accuracy (<50%) or very slow or many mistakes
        // Neutral: everything in between

        bool fewMistakes = mistakes <= 1 && accuracy >= 0.85f;
        bool manyMistakes = mistakes >= 4 || accuracy < 0.50f;

        // Time evaluation: use per-game expected durations from scoring strategy
        var strategy = ScoringStrategyRegistry.Get(session.gameId);
        var expect = strategy.GetExpectation(session.difficultyLevel);
        float expectedMax = expect.expectedDurationMax > 0f ? expect.expectedDurationMax : 60f;
        bool verySlow = duration > expectedMax * 2f;

        if (fewMistakes && !verySlow)
            return PerformanceLevel.Strong;

        if (manyMistakes || verySlow)
            return PerformanceLevel.Weak;

        return PerformanceLevel.Neutral;
    }

    /// <summary>
    /// Initializes difficulty for a game the child has never played.
    /// Uses the AgeDifficultyBaseline ScriptableObject.
    /// </summary>
    public static int GetInitialDifficulty(string gameId, int ageMonths)
    {
        var baseline = Resources.Load<AgeDifficultyBaseline>("Analytics/AgeDifficultyBaseline");
        if (baseline != null)
            return baseline.GetStartingDifficulty(gameId, ageMonths);
        return 1;
    }
}
