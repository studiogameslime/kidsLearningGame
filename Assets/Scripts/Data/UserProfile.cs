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
    public long createdAt;
    public long lastPlayedAt;
    public GameProgress progress = new GameProgress();

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
/// Root container for all profiles, serialized as the JSON file.
/// </summary>
[Serializable]
public class ProfileStore
{
    public List<UserProfile> profiles = new List<UserProfile>();
    public string activeProfileId;
}
