using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Computes estimated developmental age per game and globally.
/// Uses difficulty level as primary signal, mapped to an age estimate.
/// All ages clamped to 2-8 range.
/// </summary>
public static class EstimatedAgeCalculator
{
    private const float MinAge = 2f;
    private const float MaxAge = 8f;
    private const float SmoothingFactor = 0.2f;

    // Minimum data required before estimated age affects visibility
    private const int MinSessionsForConfidence = 3;
    private const int MinGamesForConfidence = 2;

    // Runtime-only smoothed effective age cache (not persisted)
    private static readonly Dictionary<string, float> _smoothedAgeCache = new Dictionary<string, float>();

    /// <summary>
    /// Maps a difficulty level (1-10) to an estimated developmental age.
    /// </summary>
    public static float DifficultyToAge(int difficulty)
    {
        // Linear mapping: difficulty 1 → 2.0, difficulty 10 → 7.0
        float age = 2f + (difficulty - 1) * (5f / 9f);
        return Mathf.Clamp(age, MinAge, MaxAge);
    }

    /// <summary>
    /// Updates the per-game estimated age based on current difficulty and performance.
    /// Called after each session registration.
    /// </summary>
    public static void UpdatePerGameAge(GamePerformanceProfile gameProfile)
    {
        if (gameProfile == null || gameProfile.sessionsPlayed == 0) return;

        float baseAge = DifficultyToAge(gameProfile.currentDifficulty);

        // Adjust slightly based on performance: high performance nudges age up
        float perfAdjustment = 0f;
        if (gameProfile.performanceScore > 80f)
            perfAdjustment = 0.3f;
        else if (gameProfile.performanceScore < 40f)
            perfAdjustment = -0.3f;

        gameProfile.estimatedAgeForThisGame = Mathf.Clamp(baseAge + perfAdjustment, MinAge, MaxAge);
    }

    /// <summary>
    /// Updates the global estimated age from all per-game estimates.
    /// Weights: more sessions → higher weight, more recent → higher weight.
    /// </summary>
    public static void UpdateGlobalAge(UserProfile profile)
    {
        if (profile == null) return;
        var analytics = profile.analytics;
        if (analytics == null || analytics.games == null || analytics.games.Count == 0) return;

        float weightedSum = 0f;
        float totalWeight = 0f;

        foreach (var gp in analytics.games)
        {
            if (gp.sessionsPlayed == 0 || gp.estimatedAgeForThisGame <= 0f) continue;

            // Weight by sessions played (log scale to avoid single-game dominance)
            float sessionWeight = Mathf.Log(1 + gp.sessionsPlayed);

            // Recency boost: games played recently get higher weight
            if (gp.lastPlayedUtc > 0)
            {
                long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                float daysSincePlay = (now - gp.lastPlayedUtc) / 86400f;
                float recencyMultiplier = daysSincePlay < 7f ? 1.5f : (daysSincePlay < 30f ? 1f : 0.7f);
                sessionWeight *= recencyMultiplier;
            }

            weightedSum += gp.estimatedAgeForThisGame * sessionWeight;
            totalWeight += sessionWeight;
        }

        if (totalWeight > 0f)
            profile.estimatedGlobalAge = Mathf.Clamp(weightedSum / totalWeight, MinAge, MaxAge);
    }

    /// <summary>
    /// Returns whether there is enough gameplay data to trust the estimated age.
    /// Requires minimum sessions across minimum different games.
    /// </summary>
    public static bool HasSufficientConfidence(UserProfile profile)
    {
        if (profile == null || profile.analytics == null) return false;
        if (profile.analytics.totalSessions < MinSessionsForConfidence) return false;

        int gamesPlayed = 0;
        foreach (var gp in profile.analytics.games)
        {
            if (gp.sessionsPlayed > 0) gamesPlayed++;
            if (gamesPlayed >= MinGamesForConfidence) return true;
        }
        return false;
    }

    /// <summary>
    /// Central method: returns the effective content age used for visibility filtering.
    /// Falls back to chronological age if confidence is insufficient.
    /// Smooths transitions to avoid abrupt content changes.
    /// </summary>
    public static float GetEffectiveContentAge(UserProfile profile)
    {
        if (profile == null) return MinAge;

        // Fallback: chronological age when no/insufficient data
        if (!HasSufficientConfidence(profile) || profile.estimatedGlobalAge <= 0f)
            return Mathf.Clamp(profile.age, MinAge, MaxAge);

        // Smooth transition from cached value
        float target = profile.estimatedGlobalAge;
        float current;

        if (_smoothedAgeCache.TryGetValue(profile.id, out current))
        {
            current = Mathf.Lerp(current, target, SmoothingFactor);
        }
        else
        {
            // First call this session: start from target (no animation needed)
            current = target;
        }

        current = Mathf.Clamp(current, MinAge, MaxAge);
        _smoothedAgeCache[profile.id] = current;
        return current;
    }

    /// <summary>
    /// Returns the resolved age bucket for visibility/content decisions.
    /// Converts the effective content age (float) to an integer bucket via AgeBaselineConfig.
    /// This is the single method all visibility logic should use to determine the content tier.
    /// </summary>
    public static int GetResolvedAgeBucket(UserProfile profile)
    {
        float effectiveAge = GetEffectiveContentAge(profile);
        return AgeBaselineConfig.FloatToAgeBucket(effectiveAge);
    }

    /// <summary>
    /// Returns the baseline variant value (e.g. puzzle pieces, memory cards) for a game
    /// at the child's current resolved age bucket.
    /// Returns 0 if the game has no variant or is not in the bucket.
    /// </summary>
    public static int GetBaselineVariant(UserProfile profile, string gameId)
    {
        int bucket = GetResolvedAgeBucket(profile);
        return AgeBaselineConfig.GetBaselineVariant(bucket, gameId);
    }

    /// <summary>
    /// Resets the smoothed age cache (call on profile switch or app restart).
    /// </summary>
    public static void ResetCache()
    {
        _smoothedAgeCache.Clear();
    }
}
