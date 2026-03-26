/// <summary>
/// Scoring strategy for the Letter Tracing game.
/// </summary>
public class LetterTracingScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.30f;
    protected override float AccuracyWeight => 0.30f;
    protected override float SpeedWeight => 0.15f;
    protected override float IndependenceWeight => 0.15f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation
        {
            difficultyLevel = 1,
            expectedDurationMin = 5f, expectedDurationMax = 25f,
            expectedMistakesMin = 0f, expectedMistakesMax = 1f,
            expectedActionsMin = 1f, expectedActionsMax = 3f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 4,
            expectedDurationMin = 8f, expectedDurationMax = 35f,
            expectedMistakesMin = 0f, expectedMistakesMax = 2f,
            expectedActionsMin = 2f, expectedActionsMax = 5f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 7,
            expectedDurationMin = 10f, expectedDurationMax = 45f,
            expectedMistakesMin = 1f, expectedMistakesMax = 3f,
            expectedActionsMin = 3f, expectedActionsMax = 7f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 10,
            expectedDurationMin = 15f, expectedDurationMax = 55f,
            expectedMistakesMin = 1f, expectedMistakesMax = 4f,
            expectedActionsMin = 4f, expectedActionsMax = 10f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);
}
