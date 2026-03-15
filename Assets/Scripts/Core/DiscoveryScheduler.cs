using UnityEngine;

/// <summary>
/// Determines how many games between discoveries based on total games completed.
/// Stage 1 (first 20 games): every 4-5 games
/// Stage 2 (20-60 games): every 6-7 games
/// Stage 3 (60+ games): every 8-10 games
/// </summary>
public static class DiscoveryScheduler
{
    public static int CalcNextInterval(JourneyProgress jp)
    {
        int total = jp.totalGamesCompleted;
        if (total < 20) return Random.Range(4, 6);   // 4 or 5
        if (total < 60) return Random.Range(6, 8);   // 6 or 7
        return Random.Range(8, 11);                    // 8, 9, or 10
    }
}
