using UnityEngine;

/// <summary>
/// Scoring strategy for the Pizza Maker game.
/// Emphasizes completion (all steps done) and independence (no hints).
/// Speed is less important since this is a creative, tactile game.
/// </summary>
public class PizzaMakerScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.35f;
    protected override float AccuracyWeight => 0.25f;
    protected override float SpeedWeight => 0.10f;
    protected override float IndependenceWeight => 0.20f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation
        {
            difficultyLevel = 1,
            expectedDurationMin = 15f, expectedDurationMax = 40f,
            expectedMistakesMin = 0f, expectedMistakesMax = 0f,
            expectedActionsMin = 3f, expectedActionsMax = 6f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 4,
            expectedDurationMin = 20f, expectedDurationMax = 50f,
            expectedMistakesMin = 0f, expectedMistakesMax = 1f,
            expectedActionsMin = 6f, expectedActionsMax = 10f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 7,
            expectedDurationMin = 25f, expectedDurationMax = 60f,
            expectedMistakesMin = 0f, expectedMistakesMax = 2f,
            expectedActionsMin = 8f, expectedActionsMax = 14f
        },
        new GameDifficultyExpectation
        {
            difficultyLevel = 10,
            expectedDurationMin = 30f, expectedDurationMax = 75f,
            expectedMistakesMin = 0f, expectedMistakesMax = 3f,
            expectedActionsMin = 10f, expectedActionsMax = 18f
        }
    };

    public override GameDifficultyExpectation GetExpectation(int difficultyLevel)
        => InterpolateExpectation(Anchors, difficultyLevel);
}
