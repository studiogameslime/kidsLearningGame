/// <summary>
/// Sock Match difficulty configuration.
/// 12 sock sprites available. More pairs = more socks on the lines.
/// </summary>
public static class SockMatchLevels
{
    public static int PairCount(int difficulty)
    {
        if (difficulty <= 2) return 4;
        if (difficulty <= 4) return 5;
        if (difficulty <= 6) return 6;
        if (difficulty <= 8) return 8;
        return 10;
    }

    public static int[] PickSockIndices(int pairCount)
    {
        var all = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 12; i++) all.Add(i);
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
