/// <summary>
/// Scoring strategy for Connect and Match.
/// Accuracy is primary (correct dot sequence), independence matters (hint usage).
/// </summary>
public class ConnectMatchScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.25f;
    protected override float AccuracyWeight => 0.30f;
    protected override float SpeedWeight => 0.15f;
    protected override float IndependenceWeight => 0.20f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation
        {
            difficultyLevel = 1,
            expectedDurationMin = 5f, expectedDurationMax = 20f,
            expectedMistakesMin = 0f, expectedMistakesMax = 2f,
            expectedActionsMin = 3f, expectedActionsMax = 6f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 4,
            expectedDurationMin = 8f, expectedDurationMax = 30f,
            expectedMistakesMin = 0f, expectedMistakesMax = 3f,
            expectedActionsMin = 4f, expectedActionsMax = 10f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 7,
            expectedDurationMin = 12f, expectedDurationMax = 45f,
            expectedMistakesMin = 1f, expectedMistakesMax = 4f,
            expectedActionsMin = 6f, expectedActionsMax = 15f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 10,
            expectedDurationMin = 18f, expectedDurationMax = 60f,
            expectedMistakesMin = 1f, expectedMistakesMax = 6f,
            expectedActionsMin = 8f, expectedActionsMax = 20f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);
}
