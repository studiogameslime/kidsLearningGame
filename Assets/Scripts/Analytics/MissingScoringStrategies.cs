/// <summary>
/// Scoring strategies for games that were using DefaultScoringStrategy.
/// Each strategy defines 3 anchor points (easy/medium/hard) for expected
/// duration, mistakes, and actions at difficulty levels 1, 5, and 10.
/// </summary>

// ── Shadows (Shadow Match) ──
public class ShadowMatchScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.30f;
    protected override float AccuracyWeight => 0.30f;
    protected override float SpeedWeight => 0.15f;
    protected override float IndependenceWeight => 0.15f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation { difficultyLevel = 1, expectedDurationMin = 5f, expectedDurationMax = 20f, expectedMistakesMin = 0f, expectedMistakesMax = 2f, expectedActionsMin = 4f, expectedActionsMax = 4f },
        new GameDifficultyExpectation { difficultyLevel = 5, expectedDurationMin = 8f, expectedDurationMax = 30f, expectedMistakesMin = 0f, expectedMistakesMax = 3f, expectedActionsMin = 5f, expectedActionsMax = 5f },
        new GameDifficultyExpectation { difficultyLevel = 10, expectedDurationMin = 10f, expectedDurationMax = 40f, expectedMistakesMin = 1f, expectedMistakesMax = 5f, expectedActionsMin = 6f, expectedActionsMax = 6f }
    };
    public override GameDifficultyExpectation GetExpectation(int d) => InterpolateExpectation(Anchors, d);
}

// ── Color Mixing ──
public class ColorMixingScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.35f;
    protected override float AccuracyWeight => 0.25f;
    protected override float SpeedWeight => 0.10f;
    protected override float IndependenceWeight => 0.20f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation { difficultyLevel = 1, expectedDurationMin = 8f, expectedDurationMax = 25f, expectedMistakesMin = 0f, expectedMistakesMax = 2f, expectedActionsMin = 3f, expectedActionsMax = 3f },
        new GameDifficultyExpectation { difficultyLevel = 5, expectedDurationMin = 10f, expectedDurationMax = 35f, expectedMistakesMin = 0f, expectedMistakesMax = 3f, expectedActionsMin = 4f, expectedActionsMax = 4f },
        new GameDifficultyExpectation { difficultyLevel = 10, expectedDurationMin = 12f, expectedDurationMax = 45f, expectedMistakesMin = 1f, expectedMistakesMax = 4f, expectedActionsMin = 5f, expectedActionsMax = 5f }
    };
    public override GameDifficultyExpectation GetExpectation(int d) => InterpolateExpectation(Anchors, d);
}

// ── Tower Builder ──
public class TowerBuilderScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.35f;
    protected override float AccuracyWeight => 0.25f;
    protected override float SpeedWeight => 0.15f;
    protected override float IndependenceWeight => 0.15f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation { difficultyLevel = 1, expectedDurationMin = 10f, expectedDurationMax = 30f, expectedMistakesMin = 0f, expectedMistakesMax = 3f, expectedActionsMin = 5f, expectedActionsMax = 5f },
        new GameDifficultyExpectation { difficultyLevel = 5, expectedDurationMin = 15f, expectedDurationMax = 45f, expectedMistakesMin = 1f, expectedMistakesMax = 5f, expectedActionsMin = 8f, expectedActionsMax = 8f },
        new GameDifficultyExpectation { difficultyLevel = 10, expectedDurationMin = 20f, expectedDurationMax = 60f, expectedMistakesMin = 2f, expectedMistakesMax = 8f, expectedActionsMin = 12f, expectedActionsMax = 12f }
    };
    public override GameDifficultyExpectation GetExpectation(int d) => InterpolateExpectation(Anchors, d);
}

// ── Ball Maze ──
public class BallMazeScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.40f;
    protected override float AccuracyWeight => 0.10f;
    protected override float SpeedWeight => 0.25f;
    protected override float IndependenceWeight => 0.15f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation { difficultyLevel = 1, expectedDurationMin = 10f, expectedDurationMax = 40f, expectedMistakesMin = 0f, expectedMistakesMax = 0f, expectedActionsMin = 1f, expectedActionsMax = 1f },
        new GameDifficultyExpectation { difficultyLevel = 5, expectedDurationMin = 15f, expectedDurationMax = 60f, expectedMistakesMin = 0f, expectedMistakesMax = 0f, expectedActionsMin = 1f, expectedActionsMax = 1f },
        new GameDifficultyExpectation { difficultyLevel = 10, expectedDurationMin = 20f, expectedDurationMax = 90f, expectedMistakesMin = 0f, expectedMistakesMax = 0f, expectedActionsMin = 1f, expectedActionsMax = 1f }
    };
    public override GameDifficultyExpectation GetExpectation(int d) => InterpolateExpectation(Anchors, d);
}

// ── Shared Sticker (Spot It) ──
public class SharedStickerScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.30f;
    protected override float AccuracyWeight => 0.30f;
    protected override float SpeedWeight => 0.15f;
    protected override float IndependenceWeight => 0.15f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation { difficultyLevel = 1, expectedDurationMin = 5f, expectedDurationMax = 20f, expectedMistakesMin = 0f, expectedMistakesMax = 2f, expectedActionsMin = 5f, expectedActionsMax = 5f },
        new GameDifficultyExpectation { difficultyLevel = 5, expectedDurationMin = 8f, expectedDurationMax = 30f, expectedMistakesMin = 0f, expectedMistakesMax = 3f, expectedActionsMin = 8f, expectedActionsMax = 8f },
        new GameDifficultyExpectation { difficultyLevel = 10, expectedDurationMin = 10f, expectedDurationMax = 45f, expectedMistakesMin = 1f, expectedMistakesMax = 5f, expectedActionsMin = 10f, expectedActionsMax = 10f }
    };
    public override GameDifficultyExpectation GetExpectation(int d) => InterpolateExpectation(Anchors, d);
}

// ── Flappy Bird ──
public class FlappyBirdScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.40f;
    protected override float AccuracyWeight => 0.10f;
    protected override float SpeedWeight => 0.20f;
    protected override float IndependenceWeight => 0.15f;
    protected override float DifficultyWeight => 0.15f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation { difficultyLevel = 1, expectedDurationMin = 10f, expectedDurationMax = 30f, expectedMistakesMin = 0f, expectedMistakesMax = 3f, expectedActionsMin = 5f, expectedActionsMax = 5f },
        new GameDifficultyExpectation { difficultyLevel = 5, expectedDurationMin = 15f, expectedDurationMax = 45f, expectedMistakesMin = 1f, expectedMistakesMax = 5f, expectedActionsMin = 10f, expectedActionsMax = 10f },
        new GameDifficultyExpectation { difficultyLevel = 10, expectedDurationMin = 20f, expectedDurationMax = 60f, expectedMistakesMin = 2f, expectedMistakesMax = 8f, expectedActionsMin = 15f, expectedActionsMax = 15f }
    };
    public override GameDifficultyExpectation GetExpectation(int d) => InterpolateExpectation(Anchors, d);
}

// ── Bakery ──
public class BakeryScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.30f;
    protected override float AccuracyWeight => 0.30f;
    protected override float SpeedWeight => 0.15f;
    protected override float IndependenceWeight => 0.15f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation { difficultyLevel = 1, expectedDurationMin = 8f, expectedDurationMax = 25f, expectedMistakesMin = 0f, expectedMistakesMax = 2f, expectedActionsMin = 4f, expectedActionsMax = 4f },
        new GameDifficultyExpectation { difficultyLevel = 5, expectedDurationMin = 10f, expectedDurationMax = 35f, expectedMistakesMin = 0f, expectedMistakesMax = 3f, expectedActionsMin = 6f, expectedActionsMax = 6f },
        new GameDifficultyExpectation { difficultyLevel = 10, expectedDurationMin = 12f, expectedDurationMax = 45f, expectedMistakesMin = 1f, expectedMistakesMax = 5f, expectedActionsMin = 8f, expectedActionsMax = 8f }
    };
    public override GameDifficultyExpectation GetExpectation(int d) => InterpolateExpectation(Anchors, d);
}

// ── Sock Match ──
public class SockMatchScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.30f;
    protected override float AccuracyWeight => 0.30f;
    protected override float SpeedWeight => 0.15f;
    protected override float IndependenceWeight => 0.15f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation { difficultyLevel = 1, expectedDurationMin = 8f, expectedDurationMax = 25f, expectedMistakesMin = 0f, expectedMistakesMax = 2f, expectedActionsMin = 4f, expectedActionsMax = 4f },
        new GameDifficultyExpectation { difficultyLevel = 5, expectedDurationMin = 10f, expectedDurationMax = 35f, expectedMistakesMin = 0f, expectedMistakesMax = 3f, expectedActionsMin = 6f, expectedActionsMax = 6f },
        new GameDifficultyExpectation { difficultyLevel = 10, expectedDurationMin = 12f, expectedDurationMax = 45f, expectedMistakesMin = 1f, expectedMistakesMax = 5f, expectedActionsMin = 8f, expectedActionsMax = 8f }
    };
    public override GameDifficultyExpectation GetExpectation(int d) => InterpolateExpectation(Anchors, d);
}

// ── Laundry Sorting ──
public class LaundrySortingScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.30f;
    protected override float AccuracyWeight => 0.30f;
    protected override float SpeedWeight => 0.15f;
    protected override float IndependenceWeight => 0.15f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation { difficultyLevel = 1, expectedDurationMin = 10f, expectedDurationMax = 30f, expectedMistakesMin = 0f, expectedMistakesMax = 2f, expectedActionsMin = 4f, expectedActionsMax = 4f },
        new GameDifficultyExpectation { difficultyLevel = 5, expectedDurationMin = 12f, expectedDurationMax = 40f, expectedMistakesMin = 0f, expectedMistakesMax = 4f, expectedActionsMin = 6f, expectedActionsMax = 6f },
        new GameDifficultyExpectation { difficultyLevel = 10, expectedDurationMin = 15f, expectedDurationMax = 50f, expectedMistakesMin = 1f, expectedMistakesMax = 6f, expectedActionsMin = 8f, expectedActionsMax = 8f }
    };
    public override GameDifficultyExpectation GetExpectation(int d) => InterpolateExpectation(Anchors, d);
}

// ── Fishing ──
public class FishingScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.35f;
    protected override float AccuracyWeight => 0.25f;
    protected override float SpeedWeight => 0.20f;
    protected override float IndependenceWeight => 0.10f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation { difficultyLevel = 1, expectedDurationMin = 15f, expectedDurationMax = 40f, expectedMistakesMin = 0f, expectedMistakesMax = 2f, expectedActionsMin = 5f, expectedActionsMax = 5f },
        new GameDifficultyExpectation { difficultyLevel = 5, expectedDurationMin = 20f, expectedDurationMax = 55f, expectedMistakesMin = 0f, expectedMistakesMax = 4f, expectedActionsMin = 5f, expectedActionsMax = 5f },
        new GameDifficultyExpectation { difficultyLevel = 10, expectedDurationMin = 25f, expectedDurationMax = 70f, expectedMistakesMin = 1f, expectedMistakesMax = 6f, expectedActionsMin = 5f, expectedActionsMax = 5f }
    };
    public override GameDifficultyExpectation GetExpectation(int d) => InterpolateExpectation(Anchors, d);
}

// ── Spin Puzzle ──
public class SpinPuzzleScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.30f;
    protected override float AccuracyWeight => 0.20f;
    protected override float SpeedWeight => 0.25f;
    protected override float IndependenceWeight => 0.15f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation { difficultyLevel = 1, expectedDurationMin = 5f, expectedDurationMax = 20f, expectedMistakesMin = 0f, expectedMistakesMax = 0f, expectedActionsMin = 4f, expectedActionsMax = 12f },
        new GameDifficultyExpectation { difficultyLevel = 5, expectedDurationMin = 10f, expectedDurationMax = 40f, expectedMistakesMin = 0f, expectedMistakesMax = 0f, expectedActionsMin = 9f, expectedActionsMax = 27f },
        new GameDifficultyExpectation { difficultyLevel = 10, expectedDurationMin = 20f, expectedDurationMax = 60f, expectedMistakesMin = 0f, expectedMistakesMax = 0f, expectedActionsMin = 16f, expectedActionsMax = 48f }
    };
    public override GameDifficultyExpectation GetExpectation(int d) => InterpolateExpectation(Anchors, d);
}

// ── Coloring (creative — high success weight, low speed/accuracy) ──
public class ColoringScoringStrategy : BaseScoringStrategy
{
    protected override float SuccessWeight => 0.50f;
    protected override float AccuracyWeight => 0.10f;
    protected override float SpeedWeight => 0.05f;
    protected override float IndependenceWeight => 0.25f;
    protected override float DifficultyWeight => 0.10f;

    private static readonly GameDifficultyExpectation[] Anchors =
    {
        new GameDifficultyExpectation { difficultyLevel = 1, expectedDurationMin = 20f, expectedDurationMax = 120f, expectedMistakesMin = 0f, expectedMistakesMax = 0f, expectedActionsMin = 1f, expectedActionsMax = 1f },
        new GameDifficultyExpectation { difficultyLevel = 5, expectedDurationMin = 20f, expectedDurationMax = 120f, expectedMistakesMin = 0f, expectedMistakesMax = 0f, expectedActionsMin = 1f, expectedActionsMax = 1f },
        new GameDifficultyExpectation { difficultyLevel = 10, expectedDurationMin = 20f, expectedDurationMax = 120f, expectedMistakesMin = 0f, expectedMistakesMax = 0f, expectedActionsMin = 1f, expectedActionsMax = 1f }
    };
    public override GameDifficultyExpectation GetExpectation(int d) => InterpolateExpectation(Anchors, d);
}
