using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps game IDs to sticker category prefixes.
/// Sticker IDs use sprite names: "animal_dog", "balloon_red", "letter_א", "number_3", "ocean_aquatiumstickers_4".
///
/// Special cases:
/// - Animals (animal_) and Balloons (balloon_) are awarded from discovery scratch cards —
///   the specific sticker matches the discovered animal/color by sprite name.
/// - Nature (nature_) stickers come only from the sticker tree.
/// - Games not in the map don't award stickers.
/// </summary>
public static class StickerCatalog
{
    // Game ID → sticker category prefix
    private static readonly Dictionary<string, string> GameToCategory = new Dictionary<string, string>
    {
        // אותיות (letter_)
        { "letters",        "letter_" },
        { "letterbubbles",  "letter_" },
        { "lettertrain",    "letter_" },

        // מספרים (number_)
        { "numbertrain",    "number_" },
        { "numbermaze",     "number_" },
        { "findthecount",   "number_" },
        { "quantitymatch",  "number_" },

        // אוכל (food_)
        { "bakery",         "food_" },
        { "laundrysorting", "food_" },
        { "sizesort",       "food_" },

        // יצירה (art_)
        { "coloring",       "art_" },
        { "sandDrawing",    "art_" },

        // כלי תחבורה (vehicle_)
        { "vehiclepuzzle",  "vehicle_" },

        // ים (ocean_)
        { "fishing",        "ocean_" },

        // פירות (food_)
        { "halfpuzzle",     "food_" },
    };

    /// <summary>
    /// Get the sticker category prefix for a game. Returns null if this game doesn't award stickers.
    /// </summary>
    public static string GetCategoryForGame(string gameId)
    {
        if (string.IsNullOrEmpty(gameId)) return null;
        string prefix;
        return GameToCategory.TryGetValue(gameId, out prefix) ? prefix : null;
    }

    /// <summary>
    /// Get the specific sticker ID for a discovery (animal or color from scratch card).
    /// Maps discovery ID to sprite name: "Duck" → "animal_duck", "Red" → "balloon_red".
    /// </summary>
    public static string GetStickerForDiscovery(string type, string id)
    {
        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id)) return null;
        if (type == "animal") return $"animal_{id.ToLower()}";
        if (type == "color")  return $"balloon_{id.ToLower()}";
        return null;
    }

    // ── Achievement Stickers ──

    private static readonly int[] AchievementThresholds = { 10, 30, 50 };
    private static readonly string[] AchievementTiers = { "bronze", "silver", "gold" };

    /// <summary>
    /// Check if a game has reached a new achievement tier.
    /// Returns the new sticker ID (e.g. "achievement_fishing_gold") or null.
    /// Automatically removes lower tier from collectedIds when upgrading.
    /// </summary>
    public static string CheckAchievement(string gameId, int timesPlayed, List<string> collectedIds)
    {
        if (string.IsNullOrEmpty(gameId)) return null;

        // Find highest tier reached
        int tierIndex = -1;
        for (int i = AchievementThresholds.Length - 1; i >= 0; i--)
        {
            if (timesPlayed >= AchievementThresholds[i])
            {
                tierIndex = i;
                break;
            }
        }

        if (tierIndex < 0) return null; // hasn't reached 10 yet

        string newId = $"achievement_{gameId}_{AchievementTiers[tierIndex]}";
        if (collectedIds.Contains(newId)) return null; // already has this tier

        // Remove lower tiers
        for (int i = 0; i < tierIndex; i++)
        {
            string lowerId = $"achievement_{gameId}_{AchievementTiers[i]}";
            collectedIds.Remove(lowerId);
        }

        return newId;
    }

    /// <summary>
    /// Get the current achievement tier for a game (0=none, 1=bronze, 2=silver, 3=gold).
    /// </summary>
    public static int GetAchievementTier(string gameId, List<string> collectedIds)
    {
        if (string.IsNullOrEmpty(gameId) || collectedIds == null) return 0;
        for (int i = AchievementTiers.Length - 1; i >= 0; i--)
        {
            if (collectedIds.Contains($"achievement_{gameId}_{AchievementTiers[i]}"))
                return i + 1;
        }
        return 0;
    }

    /// <summary>
    /// Pick a random uncollected sticker from the given category.
    /// Uses StickerSpriteBank to know all available stickers.
    /// Returns null if all stickers in this category are already collected.
    /// </summary>
    public static string PickRandomSticker(string prefix, List<string> collectedIds)
    {
        var allIds = StickerSpriteBank.GetAllIds(prefix);
        var available = new List<string>();
        foreach (var id in allIds)
        {
            if (!collectedIds.Contains(id))
                available.Add(id);
        }
        if (available.Count == 0) return null;
        return available[Random.Range(0, available.Count)];
    }
}
