using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Evaluates game visibility for a child using the age-bucket baseline system.
///
/// Evaluation layers:
/// 1. Missing data guard
/// 2. Parent force-disabled
/// 3. Parent force-enabled (overrides baseline, not hard locks)
/// 4. Age bucket baseline check (is game in the child's resolved age bucket?)
///
/// Business logic only — no Hebrew strings. UI layer resolves display text.
/// </summary>
public static class GameVisibilityService
{
    /// <summary>
    /// Evaluates whether a game should be visible to the child.
    /// Uses the resolved age bucket from AgeBaselineConfig as the source of truth.
    /// </summary>
    public static GameVisibilityResult Evaluate(UserProfile profile, GameItemData game)
    {
        // Layer 1: Missing data guard
        if (game == null || string.IsNullOrEmpty(game.id))
            return new GameVisibilityResult(false, VisibilityReasonCode.Hidden_MissingData, VisibilitySource.MissingData);

        if (profile == null)
            return new GameVisibilityResult(false, VisibilityReasonCode.Hidden_MissingData, VisibilitySource.MissingData);

        // Layer 2: Parent force-disabled (highest priority hide)
        var overrideMode = GetOverride(profile, game.id);
        if (overrideMode == ParentGameAccessMode.ForcedDisabled)
            return new GameVisibilityResult(false, VisibilityReasonCode.Hidden_ParentForceDisabled, VisibilitySource.ParentOverride);

        // Layer 3: Parent force-enabled (overrides baseline, not hard locks)
        if (overrideMode == ParentGameAccessMode.ForcedEnabled)
            return new GameVisibilityResult(true, VisibilityReasonCode.Visible_ParentForceEnabled, VisibilitySource.ParentOverride);

        // Layer 4: Age bucket baseline check
        int ageBucket = EstimatedAgeCalculator.GetResolvedAgeBucket(profile);
        bool inBaseline = AgeBaselineConfig.IsGameInBaseline(ageBucket, game.id);

        if (inBaseline)
            return new GameVisibilityResult(true, VisibilityReasonCode.Visible_WithinAgeRange, VisibilitySource.AgeFilter);
        else
            return new GameVisibilityResult(false, VisibilityReasonCode.Hidden_NotInAgeBucket, VisibilitySource.AgeFilter);
    }

    /// <summary>
    /// Returns only the games that should be visible to the child.
    /// Single entry point for menu filtering — do not duplicate logic elsewhere.
    /// </summary>
    public static List<GameItemData> GetVisibleGames(UserProfile profile, List<GameItemData> allGames)
    {
        var result = new List<GameItemData>();
        if (allGames == null) return result;

        foreach (var game in allGames)
        {
            var eval = Evaluate(profile, game);
            if (eval.isVisible)
                result.Add(game);
        }
        return result;
    }

    /// <summary>
    /// Gets the parent's visibility override for a specific game.
    /// Returns Default if no override exists.
    /// </summary>
    public static ParentGameAccessMode GetOverride(UserProfile profile, string gameId)
    {
        if (profile == null || profile.gameAccessOverrides == null) return ParentGameAccessMode.Default;

        foreach (var o in profile.gameAccessOverrides)
        {
            if (o.gameId == gameId)
                return o.accessMode;
        }
        return ParentGameAccessMode.Default;
    }

    /// <summary>
    /// Sets or updates the parent's visibility override for a game.
    /// Setting to Default removes the override entry.
    /// </summary>
    public static void SetOverride(UserProfile profile, string gameId, ParentGameAccessMode mode)
    {
        if (profile == null || string.IsNullOrEmpty(gameId)) return;

        if (profile.gameAccessOverrides == null)
            profile.gameAccessOverrides = new List<GameAccessOverrideData>();

        // Find existing
        for (int i = 0; i < profile.gameAccessOverrides.Count; i++)
        {
            if (profile.gameAccessOverrides[i].gameId == gameId)
            {
                if (mode == ParentGameAccessMode.Default)
                    profile.gameAccessOverrides.RemoveAt(i); // Remove override
                else
                    profile.gameAccessOverrides[i].accessMode = mode;
                return;
            }
        }

        // Add new override (only if not Default)
        if (mode != ParentGameAccessMode.Default)
        {
            profile.gameAccessOverrides.Add(new GameAccessOverrideData
            {
                gameId = gameId,
                accessMode = mode
            });
        }
    }

    /// <summary>
    /// Logs the current visibility state for debugging.
    /// </summary>
    public static void LogVisibilityState(UserProfile profile, List<GameItemData> allGames)
    {
        if (profile == null || allGames == null) return;

        float effectiveAge = EstimatedAgeCalculator.GetEffectiveContentAge(profile);
        int ageBucket = EstimatedAgeCalculator.GetResolvedAgeBucket(profile);
        bool hasConfidence = EstimatedAgeCalculator.HasSufficientConfidence(profile);

        Debug.Log($"[Visibility] Profile='{profile.displayName}' " +
            $"ChronologicalAge={profile.age} " +
            $"EstimatedGlobalAge={profile.estimatedGlobalAge:F1} " +
            $"EffectiveAge={effectiveAge:F1} " +
            $"AgeBucket={ageBucket} " +
            $"Confidence={(hasConfidence ? "sufficient" : "insufficient — using chronological age")}");

        var visibleIds = new List<string>();
        var hiddenIds = new List<string>();

        foreach (var game in allGames)
        {
            var result = Evaluate(profile, game);
            if (result.isVisible)
                visibleIds.Add(game.id);
            else
                hiddenIds.Add($"{game.id}({result.reasonCode})");
        }

        Debug.Log($"[Visibility] Visible ({visibleIds.Count}): {string.Join(", ", visibleIds)}");
        if (hiddenIds.Count > 0)
            Debug.Log($"[Visibility] Hidden ({hiddenIds.Count}): {string.Join(", ", hiddenIds)}");
    }
}
