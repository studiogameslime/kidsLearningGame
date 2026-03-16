using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data for a single profile's entry in a game leaderboard.
/// </summary>
[Serializable]
public class GameLeaderboardEntryData
{
    public string profileId;
    public string profileName;
    public bool hasPlayedGame;
    public bool isCurrentProfile;

    public int rank;
    public float score;
    public int currentDifficulty;
    public int sessionsPlayed;
    public float completionRate;
    public float mistakeRate;
    public int hintsUsed;
    public long lastPlayed;
}

/// <summary>
/// Leaderboard data for a single game across all profiles.
/// </summary>
[Serializable]
public class GameLeaderboardData
{
    public string gameId;
    public string gameName;
    public List<GameLeaderboardEntryData> entries = new List<GameLeaderboardEntryData>();
}

/// <summary>
/// Builds local family leaderboard data by comparing all profiles for each game.
/// </summary>
public static class LeaderboardBuilder
{
    /// <summary>
    /// Build leaderboard for all games that any profile has played.
    /// Returns one GameLeaderboardData per game, sorted by play frequency.
    /// </summary>
    public static List<GameLeaderboardData> Build()
    {
        var allProfiles = ProfileManager.Instance != null
            ? ProfileManager.Instance.Profiles
            : new List<UserProfile>();

        var activeProfile = ProfileManager.ActiveProfile;
        string activeId = activeProfile != null ? activeProfile.id : "";

        // Collect all game IDs that any profile has played
        var gameIds = new HashSet<string>();
        foreach (var profile in allProfiles)
        {
            foreach (var gp in profile.analytics.games)
            {
                if (gp.sessionsPlayed > 0)
                    gameIds.Add(gp.gameId);
            }
        }

        // Build per-game leaderboards
        var result = new List<GameLeaderboardData>();
        foreach (var gameId in gameIds)
        {
            var board = BuildForGame(gameId, allProfiles, activeId);
            if (board != null)
                result.Add(board);
        }

        // Sort games by total sessions across all profiles (most popular first)
        result.Sort((a, b) =>
        {
            int sessA = 0, sessB = 0;
            foreach (var e in a.entries) sessA += e.sessionsPlayed;
            foreach (var e in b.entries) sessB += e.sessionsPlayed;
            return sessB.CompareTo(sessA);
        });

        return result;
    }

    /// <summary>
    /// Build leaderboard for a single game across all profiles.
    /// Used by in-game trophy button.
    /// </summary>
    public static GameLeaderboardData BuildForGame(string gameId)
    {
        var allProfiles = ProfileManager.Instance != null
            ? ProfileManager.Instance.Profiles
            : new List<UserProfile>();
        var activeProfile = ProfileManager.ActiveProfile;
        string activeId = activeProfile != null ? activeProfile.id : "";
        return BuildForGame(gameId, allProfiles, activeId);
    }

    private static GameLeaderboardData BuildForGame(
        string gameId, List<UserProfile> allProfiles, string activeId)
    {
        var board = new GameLeaderboardData
        {
            gameId = gameId,
            gameName = ParentDashboardViewModel.GetGameName(gameId)
        };

        var played = new List<GameLeaderboardEntryData>();
        var unplayed = new List<GameLeaderboardEntryData>();

        foreach (var profile in allProfiles)
        {
            var entry = new GameLeaderboardEntryData
            {
                profileId = profile.id,
                profileName = profile.displayName ?? "---",
                isCurrentProfile = profile.id == activeId
            };

            // Find game performance for this profile
            GamePerformanceProfile gp = null;
            foreach (var g in profile.analytics.games)
            {
                if (g.gameId == gameId && g.sessionsPlayed > 0)
                {
                    gp = g;
                    break;
                }
            }

            if (gp != null)
            {
                entry.hasPlayedGame = true;
                entry.score = gp.performanceScore;
                entry.currentDifficulty = gp.currentDifficulty;
                entry.sessionsPlayed = gp.sessionsPlayed;
                entry.mistakeRate = gp.mistakeRate;
                entry.lastPlayed = gp.lastPlayedUtc;

                // Compute completion rate from recent sessions
                int completed = 0;
                foreach (var s in gp.recentSessions)
                    if (s.completed) completed++;
                entry.completionRate = gp.recentSessions.Count > 0
                    ? (float)completed / gp.recentSessions.Count : 0f;

                // Sum hints
                int hints = 0;
                foreach (var s in gp.recentSessions)
                    hints += s.hintsUsed;
                entry.hintsUsed = hints;

                played.Add(entry);
            }
            else
            {
                entry.hasPlayedGame = false;
                unplayed.Add(entry);
            }
        }

        // Sort played entries by score descending with tiebreakers
        played.Sort((a, b) =>
        {
            // Primary: score descending
            int cmp = b.score.CompareTo(a.score);
            if (cmp != 0) return cmp;
            // Tiebreaker 1: higher difficulty
            cmp = b.currentDifficulty.CompareTo(a.currentDifficulty);
            if (cmp != 0) return cmp;
            // Tiebreaker 2: higher completion rate
            cmp = b.completionRate.CompareTo(a.completionRate);
            if (cmp != 0) return cmp;
            // Tiebreaker 3: lower mistake rate
            cmp = a.mistakeRate.CompareTo(b.mistakeRate);
            if (cmp != 0) return cmp;
            // Tiebreaker 4: lower hint usage
            cmp = a.hintsUsed.CompareTo(b.hintsUsed);
            if (cmp != 0) return cmp;
            // Tiebreaker 5: more recent activity
            return b.lastPlayed.CompareTo(a.lastPlayed);
        });

        // Assign ranks to played entries
        for (int i = 0; i < played.Count; i++)
            played[i].rank = i + 1;

        // Unplayed get rank 0 (no rank)
        foreach (var e in unplayed)
            e.rank = 0;

        // Combine: played first, then unplayed
        board.entries.AddRange(played);
        board.entries.AddRange(unplayed);

        return board;
    }
}
