/// <summary>
/// Scoring strategy for the Number Maze game.
/// Weights: accuracy 35%, success 25%, speed 15%, independence 15%, difficulty 10%.
/// </summary>
public class NumberMazeScoringStrategy : BaseScoringStrategy
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
            expectedDurationMin = 8f, expectedDurationMax = 25f,
            expectedMistakesMin = 0f, expectedMistakesMax = 2f,
            expectedActionsMin = 5f, expectedActionsMax = 10f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 4,
            expectedDurationMin = 15f, expectedDurationMax = 40f,
            expectedMistakesMin = 1f, expectedMistakesMax = 3f,
            expectedActionsMin = 7f, expectedActionsMax = 15f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 7,
            expectedDurationMin = 25f, expectedDurationMax = 60f,
            expectedMistakesMin = 2f, expectedMistakesMax = 5f,
            expectedActionsMin = 12f, expectedActionsMax = 25f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 10,
            expectedDurationMin = 40f, expectedDurationMax = 90f,
            expectedMistakesMin = 3f, expectedMistakesMax = 7f,
            expectedActionsMin = 15f, expectedActionsMax = 35f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);
}
