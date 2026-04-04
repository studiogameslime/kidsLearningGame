public class ColorSortScoringStrategy : BaseScoringStrategy
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
            expectedDurationMin = 8f, expectedDurationMax = 30f,
            expectedMistakesMin = 0f, expectedMistakesMax = 2f,
            expectedActionsMin = 4f, expectedActionsMax = 4f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 5,
            expectedDurationMin = 12f, expectedDurationMax = 40f,
            expectedMistakesMin = 0f, expectedMistakesMax = 3f,
            expectedActionsMin = 6f, expectedActionsMax = 6f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 10,
            expectedDurationMin = 15f, expectedDurationMax = 50f,
            expectedMistakesMin = 1f, expectedMistakesMax = 5f,
            expectedActionsMin = 8f, expectedActionsMax = 8f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);
}
