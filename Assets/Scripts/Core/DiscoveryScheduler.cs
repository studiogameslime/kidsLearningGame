using UnityEngine;

/// <summary>
/// Determines how many games between discoveries based on total games completed.
/// Stage 1 (first 10 games): every 3 games
/// Stage 2 (10-25 games): every 5 games
/// Stage 3 (25-50 games): every 6-7 games
/// Stage 4 (50+ games): every 8-10 games
/// </summary>
public static class DiscoveryScheduler
{
    public static int CalcNextInterval(JourneyProgress jp)
    {
        int total = jp.totalGamesCompleted;
        if (total < 10) return 3;
        if (total < 25) return 5;
        if (total < 50) return Random.Range(6, 8);
        return Random.Range(8, 11);
    }
}
