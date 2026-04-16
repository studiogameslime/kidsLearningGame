using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Computes the full recommendation chain for each game:
/// baseline by age → adaptive system recommendation → parent override → final value.
///
/// Both access (visibility) and content/difficulty are computed independently.
/// Business logic only — no Hebrew strings. UI layer resolves display text.
/// </summary>
public static class GameRecommendationService
{
    /// <summary>
    /// Computes the full recommendation for a single game.
    /// </summary>
    public static GameRecommendation GetRecommendation(UserProfile profile, GameItemData game)
    {
        var rec = new GameRecommendation();
        if (profile == null || game == null) return rec;

        rec.gameId = game.id;

        // ── Age context ──
        rec.chronologicalAge = profile.age;
        rec.estimatedGlobalAge = profile.estimatedGlobalAge;
        rec.hasConfidentEstimate = EstimatedAgeCalculator.HasSufficientConfidence(profile);
        rec.effectiveContentAge = EstimatedAgeCalculator.GetEffectiveContentAge(profile);
        rec.resolvedContentBucket = EstimatedAgeCalculator.GetResolvedAgeBucket(profile);

        // ── Access: system recommendation (ignoring parent override) ──
        bool inBaseline = AgeBaselineConfig.IsGameInBaseline(rec.resolvedContentBucket, game.id);
        rec.systemRecommendsVisible = inBaseline;
        rec.systemVisibilityReason = inBaseline
            ? VisibilityReasonCode.Visible_WithinAgeRange
            : VisibilityReasonCode.Hidden_NotInAgeBucket;

        // ── Access: parent override ──
        rec.accessOverrideMode = GameVisibilityService.GetOverride(profile, game.id);

        // ── Access: final result ──
        var evalResult = GameVisibilityService.Evaluate(profile, game);
        rec.finalVisible = evalResult.isVisible;
        rec.finalVisibilityReason = evalResult.reasonCode;

        // ── Content/difficulty: baseline by age ──
        int baselineVariant = AgeBaselineConfig.GetBaselineVariant(rec.resolvedContentBucket, game.id);
        rec.baselineVariantValue = baselineVariant;
        rec.baselineDifficulty = baselineVariant > 0
            ? GameDifficultyConfig.BaselineVariantToDifficulty(game.id, baselineVariant)
            : GameDifficultyConfig.GetBaselineInitialDifficulty(game.id, profile);
        rec.baselineVariantLabel = GetVariantLabel(game.id, rec.baselineDifficulty);

        // ── Content/difficulty: current system recommendation (adaptive) ──
        var gp = profile.analytics.GetOrCreateGame(game.id);
        rec.sessionsPlayed = gp.sessionsPlayed;
        rec.estimatedAgeForGame = gp.estimatedAgeForThisGame;

        if (gp.sessionsPlayed == 0)
        {
            // No data yet: system recommendation = baseline
            rec.systemRecommendedDifficulty = rec.baselineDifficulty;
            rec.recommendationSource = ContentRecommendationSource.Baseline;
        }
        else if (!gp.manualDifficultyOverride)
        {
            // Auto mode: system recommendation = current difficulty (set by DifficultyManager)
            rec.systemRecommendedDifficulty = gp.currentDifficulty;
            rec.recommendationSource = gp.currentDifficulty != rec.baselineDifficulty
                ? ContentRecommendationSource.Adaptive
                : ContentRecommendationSource.Baseline;
        }
        else
        {
            // Manual override active: system recommendation = last auto value
            rec.systemRecommendedDifficulty = gp.lastAutoDifficulty > 0
                ? gp.lastAutoDifficulty
                : rec.baselineDifficulty;
            rec.recommendationSource = rec.systemRecommendedDifficulty != rec.baselineDifficulty
                ? ContentRecommendationSource.Adaptive
                : ContentRecommendationSource.Baseline;
        }

        rec.systemRecommendedVariantValue = GetVariantValue(game.id, rec.systemRecommendedDifficulty);
        rec.systemRecommendedVariantLabel = GetVariantLabel(game.id, rec.systemRecommendedDifficulty);

        // ── Content/difficulty: parent override ──
        rec.hasParentContentOverride = gp.manualDifficultyOverride;

        // ── Content/difficulty: final ──
        rec.finalDifficulty = gp.manualDifficultyOverride
            ? gp.currentDifficulty
            : rec.systemRecommendedDifficulty;
        rec.finalVariantValue = GetVariantValue(game.id, rec.finalDifficulty);
        rec.finalVariantLabel = GetVariantLabel(game.id, rec.finalDifficulty);

        // ── Has scalable variant? ──
        rec.hasScalableVariant = HasScalableVariant(game.id);

        // ── Explanation reason codes ──
        rec.accessExplanation = ComputeAccessExplanation(rec);
        rec.contentExplanation = ComputeContentExplanation(rec);

        return rec;
    }

    /// <summary>
    /// Computes recommendations for all games in the database.
    /// </summary>
    public static List<GameRecommendation> GetAllRecommendations(UserProfile profile, List<GameItemData> allGames)
    {
        var results = new List<GameRecommendation>();
        if (allGames == null) return results;

        foreach (var game in allGames)
        {
            if (game == null) continue;
            results.Add(GetRecommendation(profile, game));
        }
        return results;
    }

    // ── Variant helpers ──────────────────────────────────────────

    /// <summary>
    /// Returns whether a game has a scalable variant (puzzle pieces, memory cards, etc.).
    /// </summary>
    public static bool HasScalableVariant(string gameId)
    {
        if (string.IsNullOrEmpty(gameId)) return false;
        switch (gameId)
        {
            case "puzzle":
            case "memory":
            case "findthecount":
            case "simonsays":
            case "fillthedots":
            case "findtheobject":
            case "spinpuzzle":
            case "patterncopy":
            case "numbertrain":
            case "lettertrain":
            case "quantitymatch":
            case "numbermaze":
            case "connectmatch":
            case "letters":
            case "letterbubbles":
            case "vehiclepuzzle":
            case "colorcatch":
            case "colorsort":
            case "sizesort":
            case "oddoneout":
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns the concrete variant value for a difficulty level.
    /// E.g., puzzle difficulty 7 → 16 (pieces), memory difficulty 3 → 8 (cards).
    /// Returns 0 for games without a meaningful variant.
    /// </summary>
    public static int GetVariantValue(string gameId, int difficulty)
    {
        switch (gameId)
        {
            case "puzzle":
                int grid = GameDifficultyConfig.PuzzleGridSize(difficulty);
                return grid * grid;
            case "spinpuzzle":
                int spGrid = GameDifficultyConfig.SpinPuzzleGridSize(difficulty);
                return spGrid * spGrid;
            case "memory":
                GameDifficultyConfig.MemoryGridConfig(difficulty, out _, out _, out int pairs);
                return pairs * 2;
            case "findthecount":
                GameDifficultyConfig.CountingRange(difficulty, out _, out int max);
                return max;
            case "simonsays":
                return GameDifficultyConfig.SimonStartSequence(difficulty);
            case "patterncopy":
                return GameDifficultyConfig.PatternCopyGridSize(difficulty);
            case "numbertrain":
                GameDifficultyConfig.NumberTrainConfig(difficulty, out int ntWagons, out _);
                return ntWagons;
            case "lettertrain":
                GameDifficultyConfig.LetterTrainConfig(difficulty, out int ltWagons, out _);
                return ltWagons;
            case "quantitymatch":
                GameDifficultyConfig.QuantityMatchRange(difficulty, out _, out int qMax);
                return qMax;
            case "numbermaze":
                GameDifficultyConfig.NumberMazeGridConfig(difficulty, out _, out _, out int pathLen);
                return pathLen;
            case "connectmatch":
                GameDifficultyConfig.ConnectMatchConfig(difficulty, out int cmGrid, out _);
                return cmGrid;
            case "letters":
                return GameDifficultyConfig.LetterGameMaxWordLength(difficulty);
            default:
                return difficulty; // games where variant = difficulty directly
        }
    }

    /// <summary>
    /// Returns the human-readable label for the difficulty impact of a game.
    /// Delegates to GameDifficultyConfig.GetDifficultyImpactLabel.
    /// </summary>
    public static string GetVariantLabel(string gameId, int difficulty)
    {
        return GameDifficultyConfig.GetDifficultyImpactLabel(gameId, difficulty);
    }

    // ── Explanation computation ───────────────────────────────────

    private static RecommendationExplanation ComputeAccessExplanation(GameRecommendation rec)
    {
        switch (rec.accessOverrideMode)
        {
            case ParentGameAccessMode.ForcedEnabled:
                return RecommendationExplanation.ParentForceEnabled;
            case ParentGameAccessMode.ForcedDisabled:
                return RecommendationExplanation.ParentForceDisabled;
            default:
                return rec.systemRecommendsVisible
                    ? RecommendationExplanation.RecommendedForAgeBucket
                    : RecommendationExplanation.HiddenNotInAgeBucket;
        }
    }

    private static RecommendationExplanation ComputeContentExplanation(GameRecommendation rec)
    {
        if (rec.hasParentContentOverride)
            return RecommendationExplanation.ParentCustomDifficulty;

        if (rec.sessionsPlayed == 0)
            return RecommendationExplanation.UsingBaselineByAge;

        if (rec.recommendationSource == ContentRecommendationSource.Adaptive)
        {
            if (rec.systemRecommendedDifficulty > rec.baselineDifficulty)
                return RecommendationExplanation.IncreasedByPerformance;
            if (rec.systemRecommendedDifficulty < rec.baselineDifficulty)
                return RecommendationExplanation.ReducedByPerformance;
        }

        return RecommendationExplanation.UsingBaselineByAge;
    }
}

/// <summary>
/// Full recommendation data for a single game, containing both system recommendation and final state.
/// </summary>
public class GameRecommendation
{
    public string gameId;

    // ── Age context ──
    public int chronologicalAge;
    public float estimatedGlobalAge;
    public bool hasConfidentEstimate;
    public float effectiveContentAge;
    public int resolvedContentBucket;

    // ── Access recommendation ──
    public bool systemRecommendsVisible;
    public VisibilityReasonCode systemVisibilityReason;
    public ParentGameAccessMode accessOverrideMode;
    public bool finalVisible;
    public VisibilityReasonCode finalVisibilityReason;

    // ── Content/difficulty: baseline by age ──
    public int baselineDifficulty;
    public int baselineVariantValue;       // e.g. 4, 8, 16, 25
    public string baselineVariantLabel;    // e.g. "8 קלפים"

    // ── Content/difficulty: current system recommendation ──
    public int systemRecommendedDifficulty;
    public int systemRecommendedVariantValue;
    public string systemRecommendedVariantLabel;
    public ContentRecommendationSource recommendationSource;

    // ── Content/difficulty: parent override ──
    public bool hasParentContentOverride;

    // ── Content/difficulty: final ──
    public int finalDifficulty;
    public int finalVariantValue;
    public string finalVariantLabel;

    // ── Meta ──
    public bool hasScalableVariant;
    public int sessionsPlayed;
    public float estimatedAgeForGame;

    // ── Explanations ──
    public RecommendationExplanation accessExplanation;
    public RecommendationExplanation contentExplanation;
}

/// <summary>
/// Where the content recommendation originates from.
/// </summary>
public enum ContentRecommendationSource
{
    Baseline,   // age-based default
    Adaptive    // adjusted by gameplay analytics
}

/// <summary>
/// Human-readable reason codes for dashboard explanations.
/// Mapped to Hebrew strings in the UI layer.
/// </summary>
public enum RecommendationExplanation
{
    RecommendedForAgeBucket,
    HiddenNotInAgeBucket,
    ParentForceEnabled,
    ParentForceDisabled,
    UsingBaselineByAge,
    IncreasedByPerformance,
    ReducedByPerformance,
    ParentCustomDifficulty,
}
