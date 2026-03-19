/// <summary>
/// Scoring strategy for the Number Train game.
/// Accuracy matters most (correct wagon placement).
/// </summary>
public class NumberTrainScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.25f;
    protected override float AccuracyWeight => 0.35f;
    protected override float SpeedWeight => 0.15f;
    protected override float IndependenceWeight => 0.15f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation
        {
            difficultyLevel = 1,
            expectedDurationMin = 5f, expectedDurationMax = 20f,
            expectedMistakesMin = 0f, expectedMistakesMax = 1f,
            expectedActionsMin = 1f, expectedActionsMax = 3f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 4,
            expectedDurationMin = 8f, expectedDurationMax = 30f,
            expectedMistakesMin = 0f, expectedMistakesMax = 2f,
            expectedActionsMin = 2f, expectedActionsMax = 5f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 7,
            expectedDurationMin = 10f, expectedDurationMax = 40f,
            expectedMistakesMin = 1f, expectedMistakesMax = 3f,
            expectedActionsMin = 3f, expectedActionsMax = 7f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 10,
            expectedDurationMin = 15f, expectedDurationMax = 50f,
            expectedMistakesMin = 1f, expectedMistakesMax = 4f,
            expectedActionsMin = 3f, expectedActionsMax = 8f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);
}
