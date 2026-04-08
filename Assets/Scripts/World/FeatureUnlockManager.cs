using System.Collections.Generic;

/// <summary>
/// Central unlock logic for world features based on total stars.
/// Stars are earned 1 per journey game completion and are never consumed.
/// Unlocks are based on lifetime star total.
/// </summary>
public static class FeatureUnlockManager
{
    public enum Feature
    {
        GameCollection,  // always unlocked
        Sandbox,         // 5 stars (sand drawing)
        StickerTree,     // 7 stars
        Gallery,         // 10 stars (easel/drawings)
    }

    private static readonly Dictionary<Feature, int> Thresholds = new Dictionary<Feature, int>
    {
        { Feature.GameCollection, 0 },
        { Feature.Sandbox,        5 },
        { Feature.StickerTree,    7 },
        { Feature.Gallery,        10 },
    };

    /// <summary>Returns whether the feature is unlocked for the active profile.</summary>
    public static bool IsUnlocked(Feature feature)
    {
        int stars = GetTotalStars();
        return stars >= GetThreshold(feature);
    }

    /// <summary>Returns the star threshold required to unlock a feature.</summary>
    public static int GetThreshold(Feature feature)
    {
        return Thresholds.TryGetValue(feature, out int t) ? t : 0;
    }

    /// <summary>Returns how many more stars are needed (0 if already unlocked).</summary>
    public static int GetRemainingStars(Feature feature)
    {
        int needed = GetThreshold(feature) - GetTotalStars();
        return needed > 0 ? needed : 0;
    }

    /// <summary>Returns progress as 0–1 float for a feature.</summary>
    public static float GetProgress(Feature feature)
    {
        int threshold = GetThreshold(feature);
        if (threshold <= 0) return 1f;
        return UnityEngine.Mathf.Clamp01((float)GetTotalStars() / threshold);
    }

    /// <summary>Returns the total stars of the active profile.</summary>
    public static int GetTotalStars()
    {
        var profile = ProfileManager.ActiveProfile;
        return profile?.journey?.totalStars ?? 0;
    }

    /// <summary>
    /// Checks if any new features were unlocked at the current star count.
    /// Returns the list of features that just became unlocked.
    /// Call after awarding a star to detect new unlocks.
    /// </summary>
    public static List<Feature> GetNewlyUnlocked(int previousStarCount)
    {
        int currentStars = GetTotalStars();
        var unlocked = new List<Feature>();

        foreach (var kvp in Thresholds)
        {
            if (kvp.Value > 0 && previousStarCount < kvp.Value && currentStars >= kvp.Value)
                unlocked.Add(kvp.Key);
        }

        return unlocked;
    }
}
