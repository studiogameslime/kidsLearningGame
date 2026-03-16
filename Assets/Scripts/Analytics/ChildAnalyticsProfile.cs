using System;
using System.Collections.Generic;

/// <summary>
/// Top-level analytics container for a child's profile.
/// Stored inside UserProfile and persisted to JSON.
/// Exposes all data needed by a future Parent Dashboard UI.
/// </summary>
[Serializable]
public class ChildAnalyticsProfile
{
    public float globalScore;

    public List<CategoryProfile> categories = new List<CategoryProfile>();
    public List<GamePerformanceProfile> games = new List<GamePerformanceProfile>();

    public int totalSessions;
    public float totalPlayTime;
    public int totalBubblesPopped;

    /// <summary>Game IDs sorted by play frequency (most played first).</summary>
    public List<string> favoriteGames = new List<string>();

    // ── Lookup helpers (not serialized — use lists for JSON compat) ──

    public GamePerformanceProfile GetOrCreateGame(string gameId)
    {
        foreach (var g in games)
            if (g.gameId == gameId) return g;
        var profile = new GamePerformanceProfile { gameId = gameId };
        games.Add(profile);
        return profile;
    }

    public CategoryProfile GetOrCreateCategory(SkillCategory cat)
    {
        foreach (var c in categories)
            if (c.category == cat) return c;
        var profile = new CategoryProfile { category = cat };
        categories.Add(profile);
        return profile;
    }

    public void UpdateFavorites()
    {
        // Sort by sessions played descending, take top 5
        var sorted = new List<GamePerformanceProfile>(games);
        sorted.Sort((a, b) => b.sessionsPlayed.CompareTo(a.sessionsPlayed));

        favoriteGames.Clear();
        int count = sorted.Count < 5 ? sorted.Count : 5;
        for (int i = 0; i < count; i++)
        {
            if (sorted[i].sessionsPlayed > 0)
                favoriteGames.Add(sorted[i].gameId);
        }
    }
}
