using UnityEngine;

/// <summary>
/// Determines how many games between discoveries based on total games completed.
/// Stage 1 (first 10 games): every game
/// Stage 2 (10-25 games): every 2 games
/// Stage 3 (25-50 games): every 2-3 games
/// Stage 4 (50+ games): every 3-4 games
/// </summary>
public static class DiscoveryScheduler
{
    public static int CalcNextInterval(JourneyProgress jp)
    {
        int total = jp.totalGamesCompleted;
        if (total < 10) return 1;                      // first 10 games: every game
        if (total < 25) return 2;                      // next 15: every 2 games
        if (total < 50) return Random.Range(2, 4);     // 2 or 3
        return Random.Range(3, 5);                      // 3 or 4
    }
}
