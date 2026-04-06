using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central source of truth for age-based game availability and baseline content presets.
///
/// Product rules:
/// - Each age (2-8) has an explicit curated game list
/// - Ages 6-8 inherit the age-5 set exactly
/// - Puzzle and Memory have age-specific baseline piece/card counts
/// - Adaptive difficulty and parent overrides sit on top of this baseline
/// </summary>
public static class AgeBaselineConfig
{
    /// <summary>
    /// A single entry: "game X is available at age Y with baseline variant Z".
    /// </summary>
    [Serializable]
    public struct Entry
    {
        public int age;
        public string gameId;
        public int baselineVariantValue; // e.g. puzzle pieces, memory cards. 0 = not applicable

        public Entry(int age, string gameId, int variant = 0)
        {
            this.age = age;
            this.gameId = gameId;
            this.baselineVariantValue = variant;
        }
    }

    // ── Resolved baseline tables (built once, cached) ──

    private static Dictionary<int, List<Entry>> _resolvedByAge;
    private static readonly object _lock = new object();

    /// <summary>
    /// Returns the resolved game list for the given age bucket.
    /// Ages 6-8 resolve to the age-5 set.
    /// </summary>
    public static List<Entry> GetEntriesForAge(int age)
    {
        EnsureBuilt();
        int bucket = ClampAgeBucket(age);
        return _resolvedByAge.TryGetValue(bucket, out var list) ? list : new List<Entry>();
    }

    /// <summary>
    /// Returns the set of game IDs enabled for the given age bucket.
    /// </summary>
    public static HashSet<string> GetEnabledGameIds(int age)
    {
        var entries = GetEntriesForAge(age);
        var ids = new HashSet<string>();
        foreach (var e in entries)
            ids.Add(e.gameId);
        return ids;
    }

    /// <summary>
    /// Returns the baseline variant value for a specific game at a specific age.
    /// Returns 0 if the game has no variant or is not in that age bucket.
    /// </summary>
    public static int GetBaselineVariant(int age, string gameId)
    {
        var entries = GetEntriesForAge(age);
        foreach (var e in entries)
            if (e.gameId == gameId) return e.baselineVariantValue;
        return 0;
    }

    /// <summary>
    /// Returns whether a game is in the baseline set for the given age.
    /// </summary>
    public static bool IsGameInBaseline(int age, string gameId)
    {
        return GetEnabledGameIds(age).Contains(gameId);
    }

    /// <summary>
    /// Clamps an age to a valid bucket. Ages below 2 → 2. Ages 5-8 → 5 (same set).
    /// </summary>
    public static int ClampAgeBucket(int age)
    {
        if (age < 2) return 2;
        if (age > 5) return 5; // ages 6-8 use age-5 set
        return age;
    }

    /// <summary>
    /// Converts a float effective content age to an integer age bucket.
    /// Uses floor: 2.0-2.99 → 2, 3.0-3.99 → 3, etc.
    /// </summary>
    public static int FloatToAgeBucket(float effectiveAge)
    {
        return ClampAgeBucket(Mathf.FloorToInt(effectiveAge));
    }

    /// <summary>
    /// Returns all supported age buckets (2-8) and their resolved entries, for debugging/validation.
    /// </summary>
    public static Dictionary<int, List<Entry>> GetAllResolvedBuckets()
    {
        EnsureBuilt();

        // Return all ages 2-8, pointing to their resolved entries
        var result = new Dictionary<int, List<Entry>>();
        for (int age = 2; age <= 8; age++)
        {
            int bucket = ClampAgeBucket(age);
            result[age] = _resolvedByAge[bucket];
        }
        return result;
    }

    /// <summary>
    /// Validates the baseline configuration. Logs errors for issues.
    /// Call from editor setup or tests.
    /// </summary>
    public static bool Validate()
    {
        EnsureBuilt();
        bool valid = true;

        for (int age = 2; age <= 8; age++)
        {
            int bucket = ClampAgeBucket(age);
            if (!_resolvedByAge.ContainsKey(bucket) || _resolvedByAge[bucket].Count == 0)
            {
                Debug.LogError($"[AgeBaseline] Age {age} (bucket {bucket}) has no games configured");
                valid = false;
                continue;
            }

            var entries = _resolvedByAge[bucket];
            var ids = new HashSet<string>();
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.gameId))
                {
                    Debug.LogError($"[AgeBaseline] Age {age}: entry with empty gameId");
                    valid = false;
                }
                if (!ids.Add(e.gameId))
                {
                    Debug.LogError($"[AgeBaseline] Age {age}: duplicate entry for '{e.gameId}'");
                    valid = false;
                }
            }

            // Puzzle and Memory must have valid baseline variant values
            foreach (var e in entries)
            {
                if ((e.gameId == "puzzle" || e.gameId == "memory") && e.baselineVariantValue <= 0)
                {
                    Debug.LogError($"[AgeBaseline] Age {age}: '{e.gameId}' must have a positive baselineVariantValue");
                    valid = false;
                }
            }
        }

        // Ages 6-8 must resolve identically to age 5
        var age5 = GetEnabledGameIds(5);
        for (int age = 6; age <= 8; age++)
        {
            var ageN = GetEnabledGameIds(age);
            if (!age5.SetEquals(ageN))
            {
                Debug.LogError($"[AgeBaseline] Age {age} game set differs from age 5 — expected identical");
                valid = false;
            }
        }

        if (valid)
            Debug.Log("[AgeBaseline] Validation passed for all ages 2-8");

        return valid;
    }

    /// <summary>
    /// Logs the full resolved baseline for debugging.
    /// </summary>
    public static void LogBaseline()
    {
        EnsureBuilt();
        for (int age = 2; age <= 8; age++)
        {
            int bucket = ClampAgeBucket(age);
            var entries = _resolvedByAge[bucket];
            var gameList = new System.Text.StringBuilder();
            foreach (var e in entries)
            {
                gameList.Append(e.gameId);
                if (e.baselineVariantValue > 0)
                    gameList.Append($"({e.baselineVariantValue})");
                gameList.Append(", ");
            }
            Debug.Log($"[AgeBaseline] Age {age} (bucket {bucket}): {entries.Count} games — {gameList}");
        }
    }

    // ── Configuration ──────────────────────────────────────────

    private static void EnsureBuilt()
    {
        if (_resolvedByAge != null) return;
        lock (_lock)
        {
            if (_resolvedByAge != null) return;
            _resolvedByAge = BuildResolvedTable();
        }
    }

    private static Dictionary<int, List<Entry>> BuildResolvedTable()
    {
        var table = new Dictionary<int, List<Entry>>();

        // ── Age 2 ──
        table[2] = new List<Entry>
        {
            new Entry(2, "coloring"),
            new Entry(2, "puzzle",         4),
            new Entry(2, "shadows"),
            new Entry(2, "memory",         4),
            new Entry(2, "towerbuilder"),
            new Entry(2, "oddoneout"),          // very different animals, easy visual match
            new Entry(2, "quantitymatch", 3),   // quantities 1-3
            new Entry(2, "laundrysorting"),
        };

        // ── Age 3 ──
        table[3] = new List<Entry>
        {
            new Entry(3, "coloring"),
            new Entry(3, "puzzle",         9),
            new Entry(3, "findtheobject"),
            new Entry(3, "fillthedots"),
            new Entry(3, "sharedsticker"),
            new Entry(3, "simonsays"),
            new Entry(3, "memory",         8),
            new Entry(3, "ballmaze"),
            new Entry(3, "findthecount"),
            new Entry(3, "colormixing"),
            new Entry(3, "shadows"),
            new Entry(3, "towerbuilder"),
            new Entry(3, "oddoneout"),          // still easy pool
            new Entry(3, "quantitymatch", 3),   // quantities 1-3
            new Entry(3, "numbertrain",   5),   // 5 wagons, 1 missing

            new Entry(3, "laundrysorting"),

            // new Entry(3, "pizzamaker"), // hidden for v1
        };

        // ── Age 4 ──
        table[4] = new List<Entry>
        {
            new Entry(4, "coloring"),
            new Entry(4, "puzzle",         16),
            new Entry(4, "findtheobject"),
            new Entry(4, "fillthedots"),
            new Entry(4, "sharedsticker"),
            new Entry(4, "simonsays"),
            new Entry(4, "memory",         16),
            new Entry(4, "ballmaze"),
            new Entry(4, "findthecount"),
            new Entry(4, "colormixing"),
            new Entry(4, "flappybird"),
            new Entry(4, "towerbuilder"),
            new Entry(4, "shadows"),
            new Entry(4, "oddoneout"),          // medium pool, more similar animals
            new Entry(4, "quantitymatch", 5),   // quantities up to 5
            new Entry(4, "numbertrain",   6),   // 6 wagons, 2 missing
            new Entry(4, "lettertrain",  5),   // 5 wagons, 1 missing letter, early alphabet
            new Entry(4, "fishing"),                // shape recognition + tapping
            new Entry(4, "sizesort",     1),  // very different sizes
            new Entry(4, "colorsort",    1),  // 2 distinct colors
            new Entry(4, "colorcatch",   1),  // 2 colors, 5 catches
            new Entry(4, "bakery"),                   // creative sandbox
            new Entry(4, "sockmatch"),                // match sock pairs
            new Entry(4, "fruitpuzzle",  1),  // 2x2 grid
            new Entry(4, "numbermaze",    10),  // target 10, 5x3 grid
            new Entry(4, "patterncopy",   3),   // 3x3 grid
            new Entry(4, "letters",       3),   // 2-3 letter words
            new Entry(4, "connectmatch",  2),   // 2x2 grid, short path

            new Entry(4, "laundrysorting"),

            // new Entry(4, "pizzamaker"), // hidden for v1
        };

        // ── Age 5 (also used for ages 6-8) ──
        table[5] = new List<Entry>
        {
            new Entry(5, "coloring"),
            new Entry(5, "puzzle",         25),
            new Entry(5, "findtheobject"),
            new Entry(5, "fillthedots"),
            new Entry(5, "sharedsticker"),
            new Entry(5, "simonsays"),
            new Entry(5, "memory",         24),
            new Entry(5, "ballmaze"),
            new Entry(5, "findthecount"),
            new Entry(5, "colormixing"),
            new Entry(5, "flappybird"),
            new Entry(5, "towerbuilder"),
            new Entry(5, "shadows"),
            new Entry(5, "oddoneout"),          // all animals pool
            new Entry(5, "quantitymatch", 8),   // quantities up to 8
            new Entry(5, "numbertrain",   7),   // 7 wagons, 3 missing
            new Entry(5, "lettertrain",  7),   // 7 wagons, 3 missing letters, full alphabet
            new Entry(5, "sizesort",     5),   // moderate size differences
            new Entry(5, "colorsort",    5),  // confusing warm tones
            new Entry(5, "colorcatch",   5),  // 3 colors, 8 catches
            new Entry(5, "fruitpuzzle",  5),  // 2x3 grid
            new Entry(5, "numbermaze",    15),  // target 15, 6x4 grid
            new Entry(5, "patterncopy",   5),   // 5x5 grid
            new Entry(5, "letters",       4),   // up to 4 letter words
            new Entry(5, "connectmatch",  3),   // 3x3 grid, longer path

            new Entry(5, "laundrysorting"),
            new Entry(5, "fishing"),
            new Entry(5, "bakery"),
            new Entry(5, "sockmatch"),

            // new Entry(5, "pizzamaker"), // hidden for v1
        };

        // Ages 6-8 resolve via ClampAgeBucket → 5. No separate entries needed.

        return table;
    }
}
