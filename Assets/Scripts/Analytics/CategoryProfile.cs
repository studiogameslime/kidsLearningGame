using System;

/// <summary>
/// Aggregated performance for a single skill category.
/// Score is computed from weighted game scores that map to this category.
/// </summary>
[Serializable]
public class CategoryProfile
{
    public SkillCategory category;

    /// <summary>Weighted average score across contributing games (0-100).</summary>
    public float categoryScore;

    /// <summary>Positive = improving, negative = declining.</summary>
    public float trend;

    /// <summary>Number of games that contribute to this category.</summary>
    public int contributingGames;

    /// <summary>Confidence level (0-1) based on session count. More data = higher confidence.</summary>
    public float confidence;

    /// <summary>Total weighted sessions across all contributing games.</summary>
    public int totalWeightedSessions;
}
