/// <summary>
/// Sock Match difficulty configuration.
/// Maps difficulty (1-10) to pair count.
/// 12 sock sprites = 6 unique pairs (each sock appears twice).
/// </summary>
public static class SockMatchLevels
{
    /// <summary>Number of pairs for the given difficulty.</summary>
    public static int PairCount(int difficulty)
    {
        if (difficulty <= 2) return 2;
        if (difficulty <= 4) return 3;
        if (difficulty <= 6) return 4;
        if (difficulty <= 8) return 5;
        return 6;
    }

    /// <summary>
    /// Picks random sock indices for the given pair count.
    /// Returns array of length pairCount — each value is a sock index (0-11).
    /// The game should create TWO of each to form pairs.
    /// Socks 0-11 are individual designs; pairs are created by using the same sprite twice.
    /// </summary>
    public static int[] PickSockIndices(int pairCount)
    {
        var all = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 12; i++) all.Add(i);
        // Shuffle
        for (int i = all.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var tmp = all[i]; all[i] = all[j]; all[j] = tmp;
        }
        var result = new int[pairCount];
        for (int i = 0; i < pairCount; i++) result[i] = all[i % all.Count];
        return result;
    }
}
