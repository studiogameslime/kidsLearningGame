public class ColorCatchScoringStrategy : BaseScoringStrategy
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
            expectedDurationMin = 10f, expectedDurationMax = 35f,
            expectedMistakesMin = 0f, expectedMistakesMax = 2f,
            expectedActionsMin = 5f, expectedActionsMax = 5f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 5,
            expectedDurationMin = 15f, expectedDurationMax = 45f,
            expectedMistakesMin = 0f, expectedMistakesMax = 4f,
            expectedActionsMin = 8f, expectedActionsMax = 8f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 10,
            expectedDurationMin = 20f, expectedDurationMax = 60f,
            expectedMistakesMin = 1f, expectedMistakesMax = 6f,
            expectedActionsMin = 12f, expectedActionsMax = 12f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);
}
