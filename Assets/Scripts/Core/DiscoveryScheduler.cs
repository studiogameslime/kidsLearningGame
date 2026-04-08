using UnityEngine;

/// <summary>
/// Determines how many games between discoveries based on total games completed.
/// Stage 0 (first 3 discoveries): every game — quickly unlock animals for variety
/// Stage 1 (games 3-10): every 3 games
/// Stage 2 (10-25 games): every 5 games
/// Stage 3 (25-50 games): every 6-7 games
/// Stage 4 (50+ games): every 8-10 games
/// </summary>
public static class DiscoveryScheduler
{
    public static int CalcNextInterval(JourneyProgress jp)
    {
        // First 3 discoveries happen every game — so the child quickly
        // gets animals and the sub-item games (memory/puzzle/coloring) feel varied
        int discovered = (jp.unlockedAnimalIds != null ? jp.unlockedAnimalIds.Count : 0)
                       + (jp.unlockedColorIds != null ? jp.unlockedColorIds.Count : 0);
        int total = jp.totalGamesCompleted;
        Debug.Log($"[Discovery] discovered={discovered}, totalGames={total}, gamesUntilNext={jp.gamesUntilNextDiscovery}");
        if (discovered < 5) return 3; // first discoveries every 3 games (was 1 — too frequent)
        if (total < 10) return 3;
        if (total < 25) return 5;
        if (total < 50) return Random.Range(6, 8);
        return Random.Range(8, 11);
    }
}
