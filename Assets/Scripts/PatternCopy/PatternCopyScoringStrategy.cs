using UnityEngine;

/// <summary>
/// Scoring strategy for the Pattern Copy game.
/// Accuracy is paramount (correct cells vs total grid), speed scales with grid size.
/// </summary>
public class PatternCopyScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.25f;
    protected override float AccuracyWeight => 0.35f;  // accuracy is key in pattern replication
    protected override float SpeedWeight => 0.15f;
    protected override float IndependenceWeight => 0.10f;
    protected override float DifficultyWeight => 0.15f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation
        {
            difficultyLevel = 1,
            expectedDurationMin = 5f, expectedDurationMax = 15f,
            expectedMistakesMin = 0f, expectedMistakesMax = 1f,
            expectedActionsMin = 2f, expectedActionsMax = 5f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 4,
            expectedDurationMin = 10f, expectedDurationMax = 25f,
            expectedMistakesMin = 0f, expectedMistakesMax = 2f,
            expectedActionsMin = 5f, expectedActionsMax = 12f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 7,
            expectedDurationMin = 20f, expectedDurationMax = 45f,
            expectedMistakesMin = 1f, expectedMistakesMax = 4f,
            expectedActionsMin = 10f, expectedActionsMax = 25f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 10,
            expectedDurationMin = 35f, expectedDurationMax = 70f,
            expectedMistakesMin = 2f, expectedMistakesMax = 6f,
            expectedActionsMin = 18f, expectedActionsMax = 40f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);

    protected override float CalculateAccuracy(GameSessionData session)
    {
        if (session.totalActions <= 0) return session.completed ? 50f : 0f;

        // Use custom metrics for precise accuracy if available
        float correctFilled = session.GetCustom("correctFilled");
        float wrongFilled = session.GetCustom("wrongFilled");
        float missedCells = session.GetCustom("missedCells");
        float undos = session.GetCustom("undoCorrectCount");

        float totalTarget = session.GetCustom("filledCells");
        if (totalTarget <= 0) totalTarget = 1f;

        // Score based on how close the final result was to the target
        // Perfect = all filled, none wrong, none missed
        float fillAccuracy = correctFilled / totalTarget; // 0-1
        float wrongPenalty = wrongFilled / totalTarget;    // 0-1
        float undoPenalty = undos * 0.1f;                  // each undo = 10% penalty

        float score = Mathf.Clamp01(fillAccuracy - wrongPenalty * 0.5f - undoPenalty) * 100f;
        return Mathf.Clamp(score, 0f, 100f);
    }
}
