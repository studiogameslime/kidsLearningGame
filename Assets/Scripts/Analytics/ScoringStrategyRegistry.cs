using System.Collections.Generic;

/// <summary>
/// Maps gameId to the correct scoring strategy.
/// Lazily creates strategy instances (they are stateless).
/// </summary>
public static class ScoringStrategyRegistry
{
    private static readonly Dictionary<string, IGameScoringStrategy> _cache
        = new Dictionary<string, IGameScoringStrategy>();

    private static readonly DefaultScoringStrategy _default = new DefaultScoringStrategy();

    public static IGameScoringStrategy Get(string gameId)
    {
        if (string.IsNullOrEmpty(gameId)) return _default;

        if (_cache.TryGetValue(gameId, out var cached))
            return cached;

        var strategy = CreateStrategy(gameId);
        _cache[gameId] = strategy;
        return strategy;
    }

    private static IGameScoringStrategy CreateStrategy(string gameId)
    {
        // Normalize: game IDs may vary in case or have prefixes
        string id = gameId.ToLowerInvariant().Replace("_", "").Replace("-", "");

        if (id.Contains("counting") || id.Contains("count") || id.Contains("findthecount"))
            return new CountingGameScoringStrategy();

        if (id.Contains("memory") || id.Contains("matching"))
            return new MemoryGameScoringStrategy();

        if (id.Contains("simon") || id.Contains("simonsays"))
            return new SimonSaysScoringStrategy();

        if (id.Contains("puzzle") || id.Contains("jigsaw"))
            return new PuzzleGameScoringStrategy();

        if (id.Contains("pattern") || id.Contains("patterncopy"))
            return new PatternCopyScoringStrategy();

        if (id.Contains("letter"))
            return new LetterGameScoringStrategy();

        if (id.Contains("numbermaze"))
            return new NumberMazeScoringStrategy();

        if (id.Contains("oddoneout"))
            return new OddOneOutScoringStrategy();

        if (id.Contains("quantitymatch") || id.Contains("quantity"))
            return new QuantityMatchScoringStrategy();

        if (id.Contains("connectmatch"))
            return new ConnectMatchScoringStrategy();

        if (id.Contains("numbertrain"))
            return new NumberTrainScoringStrategy();

        if (id.Contains("lettertrain"))
            return new LetterTrainScoringStrategy();

        if (id.Contains("fishing"))
            return new DefaultScoringStrategy();

        if (id.Contains("pizza"))
            return new PizzaMakerScoringStrategy();

        if (id.Contains("sizesort"))
            return new SizeSortScoringStrategy();

        return _default;
    }
}
