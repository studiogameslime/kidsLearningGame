using UnityEngine;

/// <summary>
/// Centralized difficulty configuration for all games.
/// Maps difficulty level (1-10) to concrete gameplay parameters.
/// Each game calls the relevant method at round start.
/// </summary>
public static class GameDifficultyConfig
{
    /// <summary>
    /// Get the current difficulty level for a game from the analytics system.
    /// Falls back to 1 if no profile exists.
    /// </summary>
    public static int GetLevel(string gameId)
    {
        if (StatsManager.Instance != null)
            return StatsManager.Instance.GetGameDifficulty(gameId);
        return 1;
    }

    // ═══════════════════════════════════════════════════════════
    //  PUZZLE GAME
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the grid size for the puzzle game.
    /// Difficulty 1-3 → 2 (4 pieces), 4-6 → 3 (9 pieces), 7-9 → 4 (16 pieces), 10 → 5 (25 pieces)
    /// </summary>
    public static int PuzzleGridSize(int difficulty)
    {
        if (difficulty <= 3) return 2;
        if (difficulty <= 6) return 3;
        if (difficulty <= 9) return 4;
        return 5;
    }

    // ═══════════════════════════════════════════════════════════
    //  MEMORY GAME
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns (columns, rows, pairs) for the memory game grid.
    /// Difficulty 1-2  → 4 cards  (2 pairs)
    /// Difficulty 3-4  → 8 cards  (4 pairs)
    /// Difficulty 5-6  → 12 cards (6 pairs)
    /// Difficulty 7-8  → 16 cards (8 pairs)
    /// Difficulty 9    → 20 cards (10 pairs)
    /// Difficulty 10   → 24 cards (12 pairs)
    /// </summary>
    public static void MemoryGridConfig(int difficulty, out int cols, out int rows, out int pairs)
    {
        if (difficulty <= 2)      { cols = 2; rows = 2; pairs = 2; }   // 4 cards
        else if (difficulty <= 4) { cols = 4; rows = 2; pairs = 4; }   // 8 cards
        else if (difficulty <= 6) { cols = 4; rows = 3; pairs = 6; }   // 12 cards
        else if (difficulty <= 8) { cols = 4; rows = 4; pairs = 8; }   // 16 cards
        else if (difficulty <= 9) { cols = 5; rows = 4; pairs = 10; }  // 20 cards
        else                      { cols = 6; rows = 4; pairs = 12; }  // 24 cards
    }

    // ═══════════════════════════════════════════════════════════
    //  COUNTING GAME
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns (min, max) animal count for the counting game.
    /// Difficulty 1-2 → 1-3, 3-4 → 3-5, 5-6 → 5-7, 7-8 → 7-9, 9-10 → 9-12
    /// </summary>
    public static void CountingRange(int difficulty, out int min, out int max)
    {
        if (difficulty <= 2)      { min = 1; max = 3; }
        else if (difficulty <= 4) { min = 3; max = 5; }
        else if (difficulty <= 6) { min = 5; max = 7; }
        else if (difficulty <= 8) { min = 7; max = 9; }
        else                      { min = 9; max = 12; }
    }

    // ═══════════════════════════════════════════════════════════
    //  FIND THE ANIMAL
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Animal difficulty tiers based on approximate visual alpha coverage.
    /// Easy = large visible area, Hard = small visible area.
    /// </summary>
    public enum AnimalTier { Easy, Medium, Hard }

    private static readonly string[] EasyAnimals =
        { "Elephant", "Giraffe", "Horse", "Cow", "Lion" };
    private static readonly string[] MediumAnimals =
        { "Dog", "Cat", "Sheep", "Monkey", "Donkey", "Bear", "Zebra" };
    private static readonly string[] HardAnimals =
        { "Chicken", "Duck", "Bird", "Fish", "Frog", "Snake", "Turtle" };

    /// <summary>
    /// Returns which animal tier to use for a given difficulty level.
    /// Difficulty 1-3 → Easy, 4-7 → Medium, 8-10 → Hard
    /// </summary>
    public static AnimalTier FindAnimalTier(int difficulty)
    {
        if (difficulty <= 3) return AnimalTier.Easy;
        if (difficulty <= 7) return AnimalTier.Medium;
        return AnimalTier.Hard;
    }

    /// <summary>
    /// Returns the list of animal IDs allowed for a given difficulty level.
    /// </summary>
    public static string[] GetAnimalPool(int difficulty)
    {
        switch (FindAnimalTier(difficulty))
        {
            case AnimalTier.Easy: return EasyAnimals;
            case AnimalTier.Medium: return MediumAnimals;
            case AnimalTier.Hard: return HardAnimals;
            default: return EasyAnimals;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  CONNECT THE DOTS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Maps difficulty 1-10 to the existing 0-2 difficulty tiers used by DotShapeData.
    /// Difficulty 1-3 → 0 (simple), 4-6 → 1 (medium), 7-10 → 2 (complex)
    /// </summary>
    public static int ConnectDotsTier(int difficulty)
    {
        if (difficulty <= 3) return 0;
        if (difficulty <= 6) return 1;
        return 2;
    }

    // ═══════════════════════════════════════════════════════════
    //  SIMON SAYS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the starting sequence length for the Simon game.
    /// Difficulty 1-2 → 2, 3-4 → 3, 5-6 → 4, 7-8 → 5, 9-10 → 6
    /// </summary>
    public static int SimonStartSequence(int difficulty)
    {
        if (difficulty <= 2) return 2;
        if (difficulty <= 4) return 3;
        if (difficulty <= 6) return 4;
        if (difficulty <= 8) return 5;
        return 6;
    }

    /// <summary>
    /// Returns the playback speed multiplier for Simon game.
    /// Higher difficulty → slightly faster playback.
    /// </summary>
    public static float SimonSpeedMultiplier(int difficulty)
    {
        // 1.0 at difficulty 1, up to 1.4 at difficulty 10
        return 1f + (difficulty - 1) * 0.05f;
    }

    // ═══════════════════════════════════════════════════════════
    //  DIFFICULTY IMPACT LABELS (Hebrew, raw unicode)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns a Hebrew string explaining what a difficulty level means
    /// for a specific game. Used by the Parent Dashboard.
    /// </summary>
    public static string GetDifficultyImpactLabel(string gameId, int difficulty)
    {
        if (string.IsNullOrEmpty(gameId)) return $"\u05E8\u05DE\u05D4 {difficulty}"; // רמה X

        string id = gameId.ToLowerInvariant().Replace("_", "").Replace("-", "");

        // Puzzle
        if (id.Contains("puzzle") || id.Contains("jigsaw"))
        {
            int grid = PuzzleGridSize(difficulty);
            int pieces = grid * grid;
            return $"\u05E4\u05D0\u05D6\u05DC \u05E9\u05DC {pieces} \u05D7\u05DC\u05E7\u05D9\u05DD"; // פאזל של X חלקים
        }

        // Memory
        if (id.Contains("memory") || id.Contains("matching"))
        {
            int cols, rows, pairs;
            MemoryGridConfig(difficulty, out cols, out rows, out pairs);
            int cards = pairs * 2;
            return $"{cards} \u05E7\u05DC\u05E4\u05D9\u05DD"; // X קלפים
        }

        // Counting
        if (id.Contains("counting") || id.Contains("count") || id.Contains("findthecount"))
        {
            int min, max;
            CountingRange(difficulty, out min, out max);
            return $"{min}\u2013{max} \u05D7\u05D9\u05D5\u05EA \u05E2\u05DC \u05D4\u05DE\u05E1\u05DA"; // X–Y חיות על המסך
        }

        // Find the animal
        if (id.Contains("findtheobject") || id.Contains("findtheanimal") || id.Contains("findanimal"))
        {
            var tier = FindAnimalTier(difficulty);
            switch (tier)
            {
                case AnimalTier.Easy:
                    return "\u05D7\u05D9\u05D5\u05EA \u05D2\u05D3\u05D5\u05DC\u05D5\u05EA \u05E9\u05E7\u05DC \u05DC\u05D6\u05D4\u05D5\u05EA"; // חיות גדולות שקל לזהות
                case AnimalTier.Medium:
                    return "\u05D7\u05D9\u05D5\u05EA \u05D1\u05D2\u05D5\u05D3\u05DC \u05D1\u05D9\u05E0\u05D5\u05E0\u05D9"; // חיות בגודל בינוני
                case AnimalTier.Hard:
                    return "\u05D7\u05D9\u05D5\u05EA \u05E7\u05D8\u05E0\u05D5\u05EA \u05E9\u05E7\u05E9\u05D4 \u05D9\u05D5\u05EA\u05E8 \u05DC\u05D6\u05D4\u05D5\u05EA"; // חיות קטנות שקשה יותר לזהות
            }
        }

        // Simon
        if (id.Contains("simon") || id.Contains("simonsays"))
        {
            int seq = SimonStartSequence(difficulty);
            return $"\u05E8\u05E6\u05E3 \u05E9\u05DC {seq} \u05E6\u05E2\u05D3\u05D9\u05DD"; // רצף של X צעדים
        }

        // Connect the dots
        if (id.Contains("fillthedots") || id.Contains("connectthedots") || id.Contains("dots"))
        {
            int tier = ConnectDotsTier(difficulty);
            switch (tier)
            {
                case 0: return "\u05E6\u05D5\u05E8\u05D4 \u05E4\u05E9\u05D5\u05D8\u05D4"; // צורה פשוטה
                case 1: return "\u05E6\u05D5\u05E8\u05D4 \u05D1\u05E8\u05DE\u05EA \u05E7\u05D5\u05E9\u05D9 \u05D1\u05D9\u05E0\u05D5\u05E0\u05D9\u05EA"; // צורה ברמת קושי בינונית
                default: return "\u05E6\u05D5\u05E8\u05D4 \u05DE\u05D5\u05E8\u05DB\u05D1\u05EA"; // צורה מורכבת
            }
        }

        // Fallback
        return $"\u05E8\u05DE\u05D4 {difficulty}"; // רמה X
    }

    // ═══════════════════════════════════════════════════════════
    //  BASELINE VARIANT → INITIAL DIFFICULTY
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the initial difficulty level for a game based on the age baseline variant value.
    /// This maps product-defined content sizes (e.g. 4 pieces, 8 cards) back to the difficulty scale.
    /// Used when initializing difficulty for a new game based on the child's age bucket.
    /// </summary>
    public static int BaselineVariantToDifficulty(string gameId, int variantValue)
    {
        if (variantValue <= 0) return 1;

        // Puzzle: variant = piece count → grid size → difficulty
        if (gameId == "puzzle")
        {
            if (variantValue <= 4) return 1;        // 2x2 = 4 pieces → difficulty 1-3
            if (variantValue <= 9) return 4;         // 3x3 = 9 pieces → difficulty 4-6
            if (variantValue <= 16) return 7;        // 4x4 = 16 pieces → difficulty 7-9
            return 10;                               // 5x5 = 25 pieces → difficulty 10
        }

        // Memory: variant = card count → difficulty
        if (gameId == "memory")
        {
            if (variantValue <= 4) return 1;         // 4 cards → difficulty 1-2
            if (variantValue <= 8) return 3;          // 8 cards → difficulty 3-4
            if (variantValue <= 12) return 5;         // 12 cards → difficulty 5-6
            if (variantValue <= 16) return 7;         // 16 cards → difficulty 7-8
            return 9;                                 // 20-24 cards → difficulty 9-10
        }

        return 1;
    }

    /// <summary>
    /// Returns the initial difficulty for a game using the age baseline system.
    /// Checks the child's resolved age bucket for a baseline variant value.
    /// Falls back to DifficultyManager.GetInitialDifficulty if no baseline variant exists.
    /// </summary>
    public static int GetBaselineInitialDifficulty(string gameId, UserProfile profile)
    {
        if (profile == null) return 1;

        int ageBucket = EstimatedAgeCalculator.GetResolvedAgeBucket(profile);
        int variant = AgeBaselineConfig.GetBaselineVariant(ageBucket, gameId);

        if (variant > 0)
            return BaselineVariantToDifficulty(gameId, variant);

        // Fallback for games without baseline variants
        return DifficultyManager.GetInitialDifficulty(gameId, profile.age * 12);
    }

    /// <summary>
    /// Returns the recommended (auto) difficulty for a game.
    /// This is what the system would choose without manual override.
    /// Uses lastAutoDifficulty when parent has manually overridden.
    /// </summary>
    public static int GetRecommendedDifficulty(string gameId)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return 1;

        var gp = profile.analytics.GetOrCreateGame(gameId);
        if (gp.sessionsPlayed == 0)
            return GetBaselineInitialDifficulty(gameId, profile);

        // If manual override active, return the last auto-set value
        if (gp.manualDifficultyOverride && gp.lastAutoDifficulty > 0)
            return gp.lastAutoDifficulty;

        return gp.currentDifficulty;
    }
}
