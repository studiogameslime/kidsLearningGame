using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a single user profile with name, avatar, age, and game progress.
/// Serialized to/from JSON for local storage.
/// </summary>
[Serializable]
public class UserProfile
{
    public string id;
    public string displayName;
    public int age;
    public string avatarColorHex = "#90CAF9";
    public string avatarImagePath; // relative path in persistentDataPath, null if using color+initial
    public string nameAudioPath;  // relative path to recorded name audio clip
    public string favoriteAnimalId; // chosen during profile creation (Cat, Dog, or Bear)
    public long createdAt;
    public long lastPlayedAt;
    public GameProgress progress = new GameProgress();
    public JourneyProgress journey = new JourneyProgress();
    public ChildAnalyticsProfile analytics = new ChildAnalyticsProfile();
    public List<SavedDrawing> savedDrawings = new List<SavedDrawing>();
    public List<ParentImage> parentImages = new List<ParentImage>();

    // Adaptive visibility system (defaults safe for old JSON)
    public float estimatedGlobalAge;  // 0 = not yet computed, use chronological age
    public List<GameAccessOverrideData> gameAccessOverrides = new List<GameAccessOverrideData>();

    // Store review
    public bool hasShownStoreReview;

    public UserProfile()
    {
        id = Guid.NewGuid().ToString("N").Substring(0, 8);
        createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        lastPlayedAt = createdAt;
    }

    public Color AvatarColor
    {
        get
        {
            ColorUtility.TryParseHtmlString(avatarColorHex, out Color c);
            return c;
        }
    }

    public string Initial => string.IsNullOrEmpty(displayName) ? "?" : displayName.Substring(0, 1).ToUpper();
}

/// <summary>
/// Tracks journey mode progress — discoveries, unlocks, and per-game stats.
/// </summary>
[Serializable]
public class JourneyProgress
{
    public int totalGamesCompleted;
    public int gamesUntilNextDiscovery;

    public List<string> unlockedAnimalIds = new List<string>();
    public List<string> unlockedColorIds = new List<string>();
    public List<string> unlockedGameIds = new List<string>();

    public List<DiscoveryEntry> discoveryQueue = new List<DiscoveryEntry>();
    public List<GameJourneyStat> gameStats = new List<GameJourneyStat>();

    // Pending rewards to show as gift boxes in WorldScene (empty = none)
    public List<DiscoveryEntry> pendingWorldRewards = new List<DiscoveryEntry>();

    // True after the world intro sound has played once
    public bool hasPlayedWorldIntroSound;

    public GameJourneyStat GetOrCreateStat(string gameId)
    {
        foreach (var s in gameStats)
            if (s.gameId == gameId) return s;
        var stat = new GameJourneyStat { gameId = gameId };
        gameStats.Add(stat);
        return stat;
    }
}

[Serializable]
public class DiscoveryEntry
{
    public string type;   // "animal", "color", "game"
    public string id;     // "Bear", "Red", "Maze"
}

[Serializable]
public class GameJourneyStat
{
    public string gameId;
    public int timesPlayedInJourney;
}

/// <summary>
/// Tracks per-game progress for a single profile.
/// </summary>
[Serializable]
public class GameProgress
{
    public List<GameStat> games = new List<GameStat>();

    public GameStat GetOrCreate(string gameId)
    {
        foreach (var g in games)
            if (g.gameId == gameId) return g;
        var stat = new GameStat { gameId = gameId };
        games.Add(stat);
        return stat;
    }
}

/// <summary>
/// Stats for a single game within a profile.
/// </summary>
[Serializable]
public class GameStat
{
    public string gameId;
    public int timesPlayed;
    public int bestScore;
    public long lastPlayedAt;
}

/// <summary>
/// Parent override for game visibility in child's menu.
/// </summary>
public enum ParentGameAccessMode
{
    Default = 0,       // system decides based on age
    ForcedEnabled = 1, // always show (overrides age filter, not hard locks)
    ForcedDisabled = 2 // always hide
}

[Serializable]
public class GameAccessOverrideData
{
    public string gameId;
    public ParentGameAccessMode accessMode;
}

/// <summary>
/// A saved drawing from the coloring game.
/// </summary>
[Serializable]
public class SavedDrawing
{
    public string imagePath;   // relative path in persistentDataPath
    public string animalId;    // which animal was being colored (or "free")
    public long createdAt;
}

/// <summary>
/// An image uploaded by the parent for the child's games.
/// </summary>
[Serializable]
public class ParentImage
{
    public string imagePath;   // relative path in persistentDataPath
    public string label;       // optional parent-given label
    public long createdAt;
}

/// <summary>
/// Root container for all profiles, serialized as the JSON file.
/// </summary>
[Serializable]
public class ProfileStore
{
    public List<UserProfile> profiles = new List<UserProfile>();
    public string activeProfileId;
}
