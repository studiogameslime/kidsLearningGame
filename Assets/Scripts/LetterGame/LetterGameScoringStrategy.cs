/// <summary>
/// Scoring strategy for the Hebrew First Letter game.
/// Weights: accuracy 35%, success 30%, speed 15%, independence 10%, difficulty 10%.
/// </summary>
public class LetterGameScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.30f;
    protected override float AccuracyWeight => 0.35f;
    protected override float SpeedWeight => 0.15f;
    protected override float IndependenceWeight => 0.10f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation
        {
            difficultyLevel = 1,
            expectedDurationMin = 3f, expectedDurationMax = 8f,
            expectedMistakesMin = 0f, expectedMistakesMax = 1f,
            expectedActionsMin = 1f, expectedActionsMax = 2f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 4,
            expectedDurationMin = 5f, expectedDurationMax = 12f,
            expectedMistakesMin = 0f, expectedMistakesMax = 2f,
            expectedActionsMin = 1f, expectedActionsMax = 3f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 7,
            expectedDurationMin = 6f, expectedDurationMax = 15f,
            expectedMistakesMin = 1f, expectedMistakesMax = 3f,
            expectedActionsMin = 1f, expectedActionsMax = 3f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 10,
            expectedDurationMin = 8f, expectedDurationMax = 18f,
            expectedMistakesMin = 1f, expectedMistakesMax = 4f,
            expectedActionsMin = 1f, expectedActionsMax = 4f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);
}
