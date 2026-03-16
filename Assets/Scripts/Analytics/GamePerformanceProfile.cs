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

    // Composite scores (0–100)
    public float independenceScore;
    public float speedScore;
    public float performanceScore;

    // Difficulty
    public int currentDifficulty = 1;

    // Trend: positive = improving, negative = declining
    public float improvementTrend;

    // Cooldown: sessions since last difficulty change
    public int sessionsSinceDifficultyChange;

    public List<GameSessionData> recentSessions = new List<GameSessionData>();

    public void AddSession(GameSessionData session)
    {
        recentSessions.Add(session);
        if (recentSessions.Count > MaxRecentSessions)
            recentSessions.RemoveAt(0);
        sessionsPlayed++;
        sessionsSinceDifficultyChange++;
        Recalculate();
    }

    private void Recalculate()
    {
        if (recentSessions.Count == 0) return;

        float totalAcc = 0f;
        float totalDur = 0f;
        float totalMistakes = 0f;
        float totalHints = 0f;
        int completedCount = 0;

        foreach (var s in recentSessions)
        {
            float acc = s.totalActions > 0 ? (float)s.correctActions / s.totalActions : 0f;
            totalAcc += acc;
            totalDur += s.durationSeconds;
            totalMistakes += s.mistakes;
            totalHints += s.hintsUsed;
            if (s.completed) completedCount++;
        }

        int n = recentSessions.Count;
        averageAccuracy = totalAcc / n;
        averageDuration = totalDur / n;
        mistakeRate = totalMistakes / n;

        // Independence: inverse of hint usage (0 hints = 100, many hints = low)
        float avgHints = totalHints / n;
        independenceScore = Mathf.Clamp01(1f - avgHints * 0.25f) * 100f;

        // Speed: based on action intervals (lower = faster = higher score)
        float avgInterval = 0f;
        int intervalCount = 0;
        foreach (var s in recentSessions)
        {
            if (s.averageActionInterval > 0f)
            {
                avgInterval += s.averageActionInterval;
                intervalCount++;
            }
        }
        if (intervalCount > 0)
        {
            avgInterval /= intervalCount;
            // Map 0–10s interval to 100–0 score
            speedScore = Mathf.Clamp01(1f - avgInterval / 10f) * 100f;
        }

        // Performance score: weighted composite
        float successRate = n > 0 ? (float)completedCount / n : 0f;
        float consistencyScore = CalcConsistency();

        performanceScore = Mathf.Clamp(
            successRate * 100f * 0.30f +
            averageAccuracy * 100f * 0.25f +
            speedScore * 0.15f +
            independenceScore * 0.20f +
            consistencyScore * 0.10f,
            0f, 100f);

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
            recentAvg += s.totalActions > 0 ? (float)s.correctActions / s.totalActions : 0f;
        }
        recentAvg /= window;

        for (int i = n - window * 2; i < n - window; i++)
        {
            var s = recentSessions[i];
            olderAvg += s.totalActions > 0 ? (float)s.correctActions / s.totalActions : 0f;
        }
        olderAvg /= window;

        return (recentAvg - olderAvg) * 100f; // positive = improving
    }
}
