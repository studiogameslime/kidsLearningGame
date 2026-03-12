using UnityEngine;

/// <summary>
/// Determines how many games between discoveries based on total games completed.
/// Early = frequent discoveries. Later = spaced out.
/// </summary>
public static class DiscoveryScheduler
{
    public static int CalcNextInterval(JourneyProgress jp)
    {
        int total = jp.totalGamesCompleted;
        if (total < 6) return 1;
        if (total < 15) return 2;
        if (total < 30) return 3;
        return Random.Range(3, 5);
    }
}
