/// <summary>
/// Scoring strategy for Letter Bubbles — letter recognition with tapping accuracy.
/// Weights emphasize accuracy (correct letter identification) and success.
/// </summary>
public class LetterBubblesScoringStrategy : BaseScoringStrategy
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
            expectedActionsMin = 2f, expectedActionsMax = 2f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 5,
            expectedDurationMin = 15f, expectedDurationMax = 50f,
            expectedMistakesMin = 0f, expectedMistakesMax = 4f,
            expectedActionsMin = 3f, expectedActionsMax = 3f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 10,
            expectedDurationMin = 20f, expectedDurationMax = 65f,
            expectedMistakesMin = 1f, expectedMistakesMax = 6f,
            expectedActionsMin = 4f, expectedActionsMax = 4f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int d) => InterpolateExpectation(Anchors, d);
}
