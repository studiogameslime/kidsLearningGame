using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aggregated performance data for a single game.
/// Maintains a sliding window of recent sessions and derived scores.
/// </summary>
[Serializable]
public class GamePerformanceProfile
{
    public const int MaxRecentSessions = 20;

    public string gameId;
    public int sessionsPlayed;

    // Derived averages (updated incrementally)
    public float averageAccuracy;
    public float averageDuration;
    public float mistakeRate;

    // Composite scores (0-100)
    public float independenceScore;
    public float speedScore;
    public float performanceScore;

    // Difficulty
    public int currentDifficulty = 1;
    public bool manualDifficultyOverride;
    public int highestDifficultyReached = 1;
    public int lastAutoDifficulty = 1; // last difficulty set by auto-adjust (preserved when parent overrides)

    // Estimated developmental age for this game (0 = not yet computed, dashboard only in V1)
    public float estimatedAgeForThisGame;

    // Trend: positive = improving, negative = declining
    public float improvementTrend;

    // Cooldown: sessions since last difficulty change
    public int sessionsSinceDifficultyChange;

    // Extended stats (persisted, updated on each session)
    public float totalPlayTimeSeconds;
    public long lastPlayedUtc;
    public float bestSessionAccuracy;
    public float fastestCompletionSeconds;
    public int longestSuccessStreak;

    public List<GameSessionData> recentSessions = new List<GameSessionData>();

    public void AddSession(GameSessionData session)
    {
        // Defensive init — Unity JsonUtility may leave lists null after deserialization
        if (recentSessions == null) recentSessions = new List<GameSessionData>();
        session.EnsureInitialized();

        recentSessions.Add(session);
        if (recentSessions.Count > MaxRecentSessions)
            recentSessions.RemoveAt(0);
        sessionsPlayed++;
        sessionsSinceDifficultyChange++;

        // Update extended stats
        totalPlayTimeSeconds += session.durationSeconds;
        lastPlayedUtc = session.startTime;

        if (session.difficultyLevel > highestDifficultyReached)
            highestDifficultyReached = session.difficultyLevel;

        if (session.maxStreak > longestSuccessStreak)
            longestSuccessStreak = session.maxStreak;

        float sessionAccuracy = session.totalActions > 0
            ? (float)session.correctActions / session.totalActions
            : 0f;
        if (sessionAccuracy > bestSessionAccuracy)
            bestSessionAccuracy = sessionAccuracy;

        if (session.completed && session.durationSeconds > 0f)
        {
            if (fastestCompletionSeconds <= 0f || session.durationSeconds < fastestCompletionSeconds)
                fastestCompletionSeconds = session.durationSeconds;
        }

        Recalculate();
    }

    private void Recalculate()
    {
        if (recentSessions == null || recentSessions.Count == 0) return;

        float totalAcc = 0f;
        float totalDur = 0f;
        float totalMistakes = 0f;
        float totalHints = 0f;
        float totalSessionScore = 0f;
        int completedCount = 0;
        int scoredCount = 0;

        foreach (var s in recentSessions)
        {
            float acc = s.totalActions > 0 ? (float)s.correctActions / s.totalActions : 0f;
            totalAcc += acc;
            totalDur += s.durationSeconds;
            totalMistakes += s.mistakes;
            totalHints += s.hintsUsed;
            if (s.completed) completedCount++;
            if (s.sessionScore > 0f)
            {
                totalSessionScore += s.sessionScore;
                scoredCount++;
            }
        }

        int n = recentSessions.Count;
        averageAccuracy = totalAcc / n;
        averageDuration = totalDur / n;
        mistakeRate = totalMistakes / n;

        // Independence: inverse of hint usage
        float avgHints = totalHints / n;
        independenceScore = Mathf.Clamp01(1f - avgHints * 0.15f) * 100f;

        // Speed: use strategy-based speed scores from recent sessions
        var strategy = ScoringStrategyRegistry.Get(gameId);
        float totalSpeed = 0f;
        int speedCount = 0;
        foreach (var s in recentSessions)
        {
            if (s.durationSeconds > 0f)
            {
                var expect = strategy.GetExpectation(s.difficultyLevel);
                float expectedMid = (expect.expectedDurationMin + expect.expectedDurationMax) / 2f;
                if (expectedMid > 0f)
                {
                    float ratio = s.durationSeconds / expectedMid;
                    float spd;
                    if (ratio <= 0.5f) spd = 100f;
                    else if (ratio <= 1f) spd = Mathf.Lerp(100f, 75f, (ratio - 0.5f) / 0.5f);
                    else if (ratio <= 2f) spd = Mathf.Lerp(75f, 25f, (ratio - 1f) / 1f);
                    else spd = Mathf.Lerp(25f, 0f, Mathf.Clamp01((ratio - 2f) / 1f));
                    totalSpeed += spd;
                    speedCount++;
                }
            }
        }
        speedScore = speedCount > 0 ? totalSpeed / speedCount : 50f;

        // Performance score: use strategy-calculated session scores if available
        if (scoredCount > 0)
        {
            // Primary: average of strategy-scored sessions
            performanceScore = totalSessionScore / scoredCount;
        }
        else
        {
            // Fallback for legacy sessions without strategy scores
            float successRate = n > 0 ? (float)completedCount / n : 0f;
            float consistencyScore = CalcConsistency();
            performanceScore = Mathf.Clamp(
                successRate * 100f * 0.30f +
                averageAccuracy * 100f * 0.25f +
                speedScore * 0.15f +
                independenceScore * 0.20f +
                consistencyScore * 0.10f,
                0f, 100f);
        }

        // Improvement trend: compare last 3 vs previous 3
        improvementTrend = CalcTrend();
    }

    private float CalcConsistency()
    {
        int n = Mathf.Min(recentSessions.Count, 5);
        if (n < 2) return 50f;

        float[] scores = new float[n];
        for (int i = 0; i < n; i++)
        {
            var s = recentSessions[recentSessions.Count - n + i];
            scores[i] = s.totalActions > 0 ? (float)s.correctActions / s.totalActions * 100f : 0f;
        }

        float mean = 0f;
        foreach (float v in scores) mean += v;
        mean /= n;

        float variance = 0f;
        foreach (float v in scores) variance += (v - mean) * (v - mean);
        variance /= n;

        float stdDev = Mathf.Sqrt(variance);
        // Low stdDev = high consistency
        return Mathf.Clamp01(1f - stdDev / 50f) * 100f;
    }

    private float CalcTrend()
    {
        int n = recentSessions.Count;
        if (n < 4) return 0f;

        int window = Mathf.Min(3, n / 2);
        float recentAvg = 0f, olderAvg = 0f;

        for (int i = n - window; i < n; i++)
        {
            var s = recentSessions[i];
            // Use session score if available, fall back to accuracy
            recentAvg += s.sessionScore > 0f ? s.sessionScore
                : (s.totalActions > 0 ? (float)s.correctActions / s.totalActions * 100f : 0f);
        }
        recentAvg /= window;

        for (int i = n - window * 2; i < n - window; i++)
        {
            var s = recentSessions[i];
            olderAvg += s.sessionScore > 0f ? s.sessionScore
                : (s.totalActions > 0 ? (float)s.correctActions / s.totalActions * 100f : 0f);
        }
        olderAvg /= window;

        return recentAvg - olderAvg; // positive = improving
    }
}
