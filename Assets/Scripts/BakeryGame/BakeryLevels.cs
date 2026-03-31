/// <summary>
/// Bakery Game difficulty configuration.
/// Maps difficulty (1-10) to cookie/slot count and picks random cookie indices.
/// </summary>
public static class BakeryLevels
{
    public static int CookieCount(int difficulty)
    {
        if (difficulty <= 3) return 4;
        if (difficulty <= 6) return 6;
        return 8;
    }

    /// <summary>Picks <paramref name="count"/> distinct random cookie indices from 0-7.</summary>
    public static int[] PickCookieIndices(int count)
    {
        var all = new System.Collections.Generic.List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
        // Fisher-Yates shuffle
        for (int i = all.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var tmp = all[i]; all[i] = all[j]; all[j] = tmp;
        }
        var result = new int[count];
        for (int i = 0; i < count; i++) result[i] = all[i];
        return result;
    }
}
