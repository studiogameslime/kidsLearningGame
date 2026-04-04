public class FruitPuzzleScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.25f;
    protected override float AccuracyWeight => 0.30f;
    protected override float SpeedWeight => 0.20f;
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
            expectedDurationMin = 18f, expectedDurationMax = 55f,
            expectedMistakesMin = 1f, expectedMistakesMax = 5f,
            expectedActionsMin = 9f, expectedActionsMax = 9f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);
}
