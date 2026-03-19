/// <summary>
/// Scoring strategy for the Odd One Out game.
/// Accuracy is king — each round is a single decision.
/// Weights: accuracy 40%, success 25%, speed 15%, independence 10%, difficulty 10%.
/// </summary>
public class OddOneOutScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.25f;
    protected override float AccuracyWeight => 0.40f;
    protected override float SpeedWeight => 0.15f;
    protected override float IndependenceWeight => 0.10f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation
        {
            difficultyLevel = 1,
            expectedDurationMin = 2f, expectedDurationMax = 8f,
            expectedMistakesMin = 0f, expectedMistakesMax = 1f,
            expectedActionsMin = 1f, expectedActionsMax = 2f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 4,
            expectedDurationMin = 3f, expectedDurationMax = 10f,
            expectedMistakesMin = 0f, expectedMistakesMax = 1f,
            expectedActionsMin = 1f, expectedActionsMax = 3f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 7,
            expectedDurationMin = 3f, expectedDurationMax = 12f,
            expectedMistakesMin = 0f, expectedMistakesMax = 2f,
            expectedActionsMin = 1f, expectedActionsMax = 3f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 10,
            expectedDurationMin = 4f, expectedDurationMax = 15f,
            expectedMistakesMin = 0f, expectedMistakesMax = 2f,
            expectedActionsMin = 1f, expectedActionsMax = 4f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);
}
