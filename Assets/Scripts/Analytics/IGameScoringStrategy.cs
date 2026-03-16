using UnityEngine;

/// <summary>
/// Defines difficulty-aware expected performance values for a game at a given difficulty level.
/// Scoring strategies compare actual session metrics against these expectations.
/// </summary>
[System.Serializable]
public struct GameDifficultyExpectation
{
    public int difficultyLevel;
    public float expectedDurationMin;
    public float expectedDurationMax;
    public float expectedMistakesMin;
    public float expectedMistakesMax;
    public float expectedActionsMin;
    public float expectedActionsMax;

    /// <summary>Lerp between min/max by a 0-1 factor for smooth scaling.</summary>
    public static GameDifficultyExpectation Lerp(GameDifficultyExpectation a, GameDifficultyExpectation b, float t)
    {
        return new GameDifficultyExpectation
        {
            difficultyLevel = Mathf.RoundToInt(Mathf.Lerp(a.difficultyLevel, b.difficultyLevel, t)),
            expectedDurationMin = Mathf.Lerp(a.expectedDurationMin, b.expectedDurationMin, t),
            expectedDurationMax = Mathf.Lerp(a.expectedDurationMax, b.expectedDurationMax, t),
            expectedMistakesMin = Mathf.Lerp(a.expectedMistakesMin, b.expectedMistakesMin, t),
            expectedMistakesMax = Mathf.Lerp(a.expectedMistakesMax, b.expectedMistakesMax, t),
            expectedActionsMin = Mathf.Lerp(a.expectedActionsMin, b.expectedActionsMin, t),
            expectedActionsMax = Mathf.Lerp(a.expectedActionsMax, b.expectedActionsMax, t)
        };
    }
}

/// <summary>
/// Breakdown of a session score into individual dimensions for debugging and transparency.
/// </summary>
public struct SessionScoreBreakdown
{
    public float successScore;      // 0-100: did the child complete?
    public float accuracyScore;     // 0-100: correct vs total actions
    public float speedScore;        // 0-100: actual time vs expected time
    public float independenceScore; // 0-100: how many hints used
    public float difficultyBonus;   // 0-100: bonus for harder levels
    public float finalScore;        // weighted combination

    public float successWeight;
    public float accuracyWeight;
    public float speedWeight;
    public float independenceWeight;
    public float difficultyWeight;
}

/// <summary>
/// Interface for game-specific scoring strategies.
/// Each game implements its own logic to evaluate session performance
/// relative to difficulty-based expectations.
/// </summary>
public interface IGameScoringStrategy
{
    /// <summary>Calculate a detailed score breakdown for a single session.</summary>
    SessionScoreBreakdown CalculateSessionScore(GameSessionData session);

    /// <summary>Get the expected performance values for a given difficulty level.</summary>
    GameDifficultyExpectation GetExpectation(int difficultyLevel);
}
