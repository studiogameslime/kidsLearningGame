using UnityEngine;

/// <summary>
/// Adjusts per-game difficulty based on recent performance.
/// Scale: 1–10. Uses stability safeguards to prevent oscillation.
/// </summary>
public static class DifficultyManager
{
    private const int MinDifficulty = 1;
    private const int MaxDifficulty = 10;

    // Thresholds
    private const float IncreaseThreshold = 80f;
    private const float DecreaseThreshold = 40f;

    // Stability: minimum sessions between difficulty changes
    private const int CooldownSessions = 2;

    // Minimum recent sessions required to confirm a trend
    private const int TrendConfirmSessions = 3;

    /// <summary>
    /// Evaluates the game profile and adjusts difficulty if warranted.
    /// Returns true if difficulty was changed.
    /// </summary>
    public static bool Evaluate(GamePerformanceProfile profile)
    {
        if (profile == null) return false;

        // Respect cooldown
        if (profile.sessionsSinceDifficultyChange < CooldownSessions)
            return false;

        int recentCount = profile.recentSessions.Count;
        if (recentCount < TrendConfirmSessions)
            return false;

        // Check last N sessions for consistent trend
        int checkCount = Mathf.Min(TrendConfirmSessions, recentCount);
        float avgScore = 0f;
        float avgMistakeRate = 0f;
        int abandonCount = 0;
        int completedCount = 0;

        for (int i = recentCount - checkCount; i < recentCount; i++)
        {
            var s = profile.recentSessions[i];
            float acc = s.totalActions > 0 ? (float)s.correctActions / s.totalActions * 100f : 0f;
            avgScore += acc;
            avgMistakeRate += s.mistakes;
            if (s.abandoned) abandonCount++;
            if (s.completed) completedCount++;
        }
        avgScore /= checkCount;
        avgMistakeRate /= checkCount;

        // Use the overall performance score too
        float perfScore = profile.performanceScore;

        // ── Increase ──
        if (perfScore > IncreaseThreshold && avgScore > IncreaseThreshold
            && avgMistakeRate < 1f && completedCount == checkCount)
        {
            if (profile.currentDifficulty < MaxDifficulty)
            {
                profile.currentDifficulty++;
                profile.sessionsSinceDifficultyChange = 0;
                return true;
            }
        }

        // ── Decrease ──
        if (perfScore < DecreaseThreshold || abandonCount >= 2
            || (avgScore < DecreaseThreshold && avgMistakeRate > 3f))
        {
            if (profile.currentDifficulty > MinDifficulty)
            {
                profile.currentDifficulty--;
                profile.sessionsSinceDifficultyChange = 0;
                return true;
            }
        }

        // ── Stable zone (40–80): no change ──
        return false;
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
