using UnityEngine;

/// <summary>
/// Base class providing shared scoring logic for all game strategies.
/// Subclasses define expectations and optionally override weight distributions.
/// </summary>
public abstract class BaseScoringStrategy : IGameScoringStrategy
{
    // Default weights — subclasses can override
    protected virtual float SuccessWeight => 0.30f;
    protected virtual float AccuracyWeight => 0.25f;
    protected virtual float SpeedWeight => 0.20f;
    protected virtual float IndependenceWeight => 0.15f;
    protected virtual float DifficultyWeight => 0.10f;

    public abstract GameDifficultyExpectation GetExpectation(int difficultyLevel);

    public SessionScoreBreakdown CalculateSessionScore(GameSessionData session)
    {
        var expect = GetExpectation(session.difficultyLevel);
        var breakdown = new SessionScoreBreakdown();

        breakdown.successWeight = SuccessWeight;
        breakdown.accuracyWeight = AccuracyWeight;
        breakdown.speedWeight = SpeedWeight;
        breakdown.independenceWeight = IndependenceWeight;
        breakdown.difficultyWeight = DifficultyWeight;

        // 1. Success (did the child complete the game?)
        breakdown.successScore = session.completed ? 100f : (session.abandoned ? 10f : 30f);

        // 2. Accuracy (correct actions / total actions)
        breakdown.accuracyScore = CalculateAccuracy(session);

        // 3. Speed (actual duration vs expected duration range)
        breakdown.speedScore = CalculateSpeed(session, expect);

        // 4. Independence (hint usage penalty)
        breakdown.independenceScore = CalculateIndependence(session);

        // 5. Difficulty bonus (harder levels get a bonus)
        breakdown.difficultyBonus = CalculateDifficultyBonus(session.difficultyLevel);

        // Weighted combination
        breakdown.finalScore = Mathf.Clamp(
            breakdown.successScore * SuccessWeight +
            breakdown.accuracyScore * AccuracyWeight +
            breakdown.speedScore * SpeedWeight +
            breakdown.independenceScore * IndependenceWeight +
            breakdown.difficultyBonus * DifficultyWeight,
            0f, 100f);

        return breakdown;
    }

    protected virtual float CalculateAccuracy(GameSessionData session)
    {
        if (session.totalActions <= 0) return session.completed ? 80f : 0f;
        float accuracy = (float)session.correctActions / session.totalActions;
        return Mathf.Clamp(accuracy * 100f, 0f, 100f);
    }

    protected float CalculateSpeed(GameSessionData session, GameDifficultyExpectation expect)
    {
        if (session.durationSeconds <= 0f) return 50f;

        float expectedMid = (expect.expectedDurationMin + expect.expectedDurationMax) / 2f;
        if (expectedMid <= 0f) return 50f;

        float speedRatio = session.durationSeconds / expectedMid;

        // speedRatio < 1 = faster than expected (good)
        // speedRatio = 1 = exactly as expected
        // speedRatio > 1 = slower than expected (penalty)
        // Map: 0.5 → 100, 1.0 → 75, 1.5 → 50, 2.0 → 25, 3.0+ → 0
        float score;
        if (speedRatio <= 0.5f)
            score = 100f;
        else if (speedRatio <= 1f)
            score = Mathf.Lerp(100f, 75f, (speedRatio - 0.5f) / 0.5f);
        else if (speedRatio <= 2f)
            score = Mathf.Lerp(75f, 25f, (speedRatio - 1f) / 1f);
        else
            score = Mathf.Lerp(25f, 0f, Mathf.Clamp01((speedRatio - 2f) / 1f));

        return Mathf.Clamp(score, 0f, 100f);
    }

    protected float CalculateIndependence(GameSessionData session)
    {
        if (session.hintsUsed <= 0) return 100f;
        // Each hint costs ~15 points, clamped
        return Mathf.Clamp(100f - session.hintsUsed * 15f, 0f, 100f);
    }

    protected float CalculateDifficultyBonus(int difficultyLevel)
    {
        // Scale 1-10 → 10-100
        return Mathf.Clamp(difficultyLevel * 10f, 10f, 100f);
    }

    /// <summary>
    /// Interpolate expectations between two defined anchor points for intermediate difficulty levels.
    /// </summary>
    protected GameDifficultyExpectation InterpolateExpectation(
        GameDifficultyExpectation[] anchors, int difficultyLevel)
    {
        if (anchors.Length == 0) return new GameDifficultyExpectation();
        if (anchors.Length == 1) return anchors[0];

        // Find surrounding anchors
        for (int i = 0; i < anchors.Length - 1; i++)
        {
            if (difficultyLevel <= anchors[i].difficultyLevel)
                return anchors[i];
            if (difficultyLevel <= anchors[i + 1].difficultyLevel)
            {
                float t = (float)(difficultyLevel - anchors[i].difficultyLevel)
                    / (anchors[i + 1].difficultyLevel - anchors[i].difficultyLevel);
                return GameDifficultyExpectation.Lerp(anchors[i], anchors[i + 1], t);
            }
        }
        return anchors[anchors.Length - 1];
    }
}

// ═══════════════════════════════════════════════════════════════
//  COUNTING GAME
// ═══════════════════════════════════════════════════════════════

public class CountingGameScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.25f;
    protected override float AccuracyWeight => 0.35f;  // accuracy matters most here
    protected override float SpeedWeight => 0.15f;
    protected override float IndependenceWeight => 0.15f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation {
            difficultyLevel = 1,
            expectedDurationMin = 3f, expectedDurationMax = 6f,
            expectedMistakesMin = 0f, expectedMistakesMax = 0f,
            expectedActionsMin = 1f, expectedActionsMax = 1f
        },
        new GameDifficultyExpectation {
            difficultyLevel = 4,
            expectedDurationMin = 5f, expectedDurationMax = 10f,
            expectedMistakesMin = 0f, expectedMistakesMax = 1f,
            expectedActionsMin = 1f, expectedActionsMax = 2f
        },
        new GameDifficultyExpectation {
            difficultyLevel = 7,
            expectedDurationMin = 8f, expectedDurationMax = 15f,
            expectedMistakesMin = 1f, expectedMistakesMax = 2f,
            expectedActionsMin = 1f, expectedActionsMax = 3f
        },
        new GameDifficultyExpectation {
            difficultyLevel = 10,
            expectedDurationMin = 12f, expectedDurationMax = 20f,
            expectedMistakesMin = 1f, expectedMistakesMax = 3f,
            expectedActionsMin = 1f, expectedActionsMax = 4f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);

    protected override float CalculateAccuracy(GameSessionData session)
    {
        // Counting game: 1 correct answer required, but child may tap wrong numbers
        if (session.totalActions <= 0) return session.completed ? 50f : 0f;

        // First try = 100%, each wrong attempt reduces sharply
        // 1 action (correct) = 100%
        // 2 actions (1 wrong + 1 correct) = 50%
        // 3 actions = 33%
        // 5 actions = 20%
        float accuracy = 1f / session.totalActions;
        return Mathf.Clamp(accuracy * 100f, 0f, 100f);
    }
}

// ═══════════════════════════════════════════════════════════════
//  MEMORY GAME
// ═══════════════════════════════════════════════════════════════

public class MemoryGameScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.20f;
    protected override float AccuracyWeight => 0.30f;
    protected override float SpeedWeight => 0.20f;
    protected override float IndependenceWeight => 0.10f;
    protected override float DifficultyWeight => 0.20f;  // difficulty matters more here

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation {
            difficultyLevel = 1,
            expectedDurationMin = 6f, expectedDurationMax = 12f,
            expectedMistakesMin = 0f, expectedMistakesMax = 2f,
            expectedActionsMin = 4f, expectedActionsMax = 8f
        },
        new GameDifficultyExpectation {
            difficultyLevel = 4,
            expectedDurationMin = 15f, expectedDurationMax = 30f,
            expectedMistakesMin = 2f, expectedMistakesMax = 4f,
            expectedActionsMin = 6f, expectedActionsMax = 16f
        },
        new GameDifficultyExpectation {
            difficultyLevel = 7,
            expectedDurationMin = 30f, expectedDurationMax = 60f,
            expectedMistakesMin = 4f, expectedMistakesMax = 8f,
            expectedActionsMin = 9f, expectedActionsMax = 30f
        },
        new GameDifficultyExpectation {
            difficultyLevel = 10,
            expectedDurationMin = 50f, expectedDurationMax = 90f,
            expectedMistakesMin = 6f, expectedMistakesMax = 12f,
            expectedActionsMin = 12f, expectedActionsMax = 40f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);

    protected override float CalculateAccuracy(GameSessionData session)
    {
        // Memory game: a "match attempt" is flipping 2 cards.
        // correctActions = matched pairs, totalActions = total flip-pairs attempted
        if (session.totalActions <= 0) return session.completed ? 50f : 0f;

        float accuracy = (float)session.correctActions / session.totalActions;
        return Mathf.Clamp(accuracy * 100f, 0f, 100f);
    }
}

// ═══════════════════════════════════════════════════════════════
//  SIMON SAYS
// ═══════════════════════════════════════════════════════════════

public class SimonSaysScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.25f;
    protected override float AccuracyWeight => 0.25f;
    protected override float SpeedWeight => 0.15f;
    protected override float IndependenceWeight => 0.10f;
    protected override float DifficultyWeight => 0.25f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation {
            difficultyLevel = 1,
            expectedDurationMin = 3f, expectedDurationMax = 8f,
            expectedMistakesMin = 0f, expectedMistakesMax = 0f,
            expectedActionsMin = 2f, expectedActionsMax = 3f
        },
        new GameDifficultyExpectation {
            difficultyLevel = 4,
            expectedDurationMin = 10f, expectedDurationMax = 25f,
            expectedMistakesMin = 0f, expectedMistakesMax = 1f,
            expectedActionsMin = 8f, expectedActionsMax = 15f
        },
        new GameDifficultyExpectation {
            difficultyLevel = 7,
            expectedDurationMin = 25f, expectedDurationMax = 50f,
            expectedMistakesMin = 1f, expectedMistakesMax = 3f,
            expectedActionsMin = 15f, expectedActionsMax = 30f
        },
        new GameDifficultyExpectation {
            difficultyLevel = 10,
            expectedDurationMin = 40f, expectedDurationMax = 80f,
            expectedMistakesMin = 2f, expectedMistakesMax = 5f,
            expectedActionsMin = 25f, expectedActionsMax = 50f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);
}

// ═══════════════════════════════════════════════════════════════
//  PUZZLE GAME
// ═══════════════════════════════════════════════════════════════

public class PuzzleGameScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.30f;
    protected override float AccuracyWeight => 0.20f;
    protected override float SpeedWeight => 0.20f;
    protected override float IndependenceWeight => 0.15f;
    protected override float DifficultyWeight => 0.15f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation {
            difficultyLevel = 1,
            expectedDurationMin = 10f, expectedDurationMax = 25f,
            expectedMistakesMin = 0f, expectedMistakesMax = 2f,
            expectedActionsMin = 4f, expectedActionsMax = 6f
        },
        new GameDifficultyExpectation {
            difficultyLevel = 4,
            expectedDurationMin = 20f, expectedDurationMax = 45f,
            expectedMistakesMin = 1f, expectedMistakesMax = 4f,
            expectedActionsMin = 9f, expectedActionsMax = 15f
        },
        new GameDifficultyExpectation {
            difficultyLevel = 7,
            expectedDurationMin = 35f, expectedDurationMax = 70f,
            expectedMistakesMin = 2f, expectedMistakesMax = 6f,
            expectedActionsMin = 12f, expectedActionsMax = 25f
        },
        new GameDifficultyExpectation {
            difficultyLevel = 10,
            expectedDurationMin = 50f, expectedDurationMax = 100f,
            expectedMistakesMin = 3f, expectedMistakesMax = 8f,
            expectedActionsMin = 16f, expectedActionsMax = 35f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);
}

// ═══════════════════════════════════════════════════════════════
//  DEFAULT / FALLBACK STRATEGY
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Generic strategy for games that don't have a dedicated scoring model yet.
/// Uses reasonable defaults that scale with difficulty.
/// </summary>
public class DefaultScoringStrategy : BaseScoringStrategy
{
    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation {
            difficultyLevel = 1,
            expectedDurationMin = 5f, expectedDurationMax = 15f,
            expectedMistakesMin = 0f, expectedMistakesMax = 1f,
            expectedActionsMin = 1f, expectedActionsMax = 5f
        },
        new GameDifficultyExpectation {
            difficultyLevel = 5,
            expectedDurationMin = 15f, expectedDurationMax = 40f,
            expectedMistakesMin = 1f, expectedMistakesMax = 3f,
            expectedActionsMin = 5f, expectedActionsMax = 15f
        },
        new GameDifficultyExpectation {
            difficultyLevel = 10,
            expectedDurationMin = 30f, expectedDurationMax = 80f,
            expectedMistakesMin = 2f, expectedMistakesMax = 6f,
            expectedActionsMin = 10f, expectedActionsMax = 30f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);
}
