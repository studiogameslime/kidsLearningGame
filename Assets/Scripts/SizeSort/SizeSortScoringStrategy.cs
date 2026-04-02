public class SizeSortScoringStrategy : BaseScoringStrategy
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
            expectedDurationMin = 10f, expectedDurationMax = 40f,
            expectedMistakesMin = 0f, expectedMistakesMax = 2f,
            expectedActionsMin = 15f, expectedActionsMax = 15f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 5,
            expectedDurationMin = 15f, expectedDurationMax = 50f,
            expectedMistakesMin = 0f, expectedMistakesMax = 4f,
            expectedActionsMin = 15f, expectedActionsMax = 15f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 10,
            expectedDurationMin = 20f, expectedDurationMax = 60f,
            expectedMistakesMin = 1f, expectedMistakesMax = 6f,
            expectedActionsMin = 15f, expectedActionsMax = 15f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);
}
