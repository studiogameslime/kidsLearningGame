using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data models and computation for the Parent Dashboard.
/// Transforms raw ChildAnalyticsProfile into display-ready view models.
/// All Hebrew strings are raw unicode — apply HebrewFixer.Fix() at UI binding time.
/// </summary>

// ── Data Models ─────────────────────────────────────────────────

[Serializable]
public class SessionSummary
{
    public long timestamp;
    public int difficulty;
    public float accuracy;
    public float durationSeconds;
    public int mistakes;
    public bool completed;
}

[Serializable]
public class GameDashboardData
{
    public string gameId;
    public string gameName;

    public float score;
    public float trend;

    public int sessionsPlayed;
    public float totalPlayTime;
    public long lastPlayed;

    public float accuracy;
    public float mistakeRate;
    public float completionRate;
    public float speedScore;
    public float independenceScore;
    public float consistencyScore;

    public int currentDifficulty;
    public int highestDifficulty;
    public int maxStreak;

    public string insightText;
    public List<SessionSummary> recentSessions = new List<SessionSummary>();
    public List<string> categoryNames = new List<string>();
}

[Serializable]
public class CategoryDashboardData
{
    public SkillCategory category;
    public string categoryName;
    public Color color;

    public float score;
    public float trend;
    public float confidence;

    public int contributingGamesCount;
    public string topGameName;
    public string insightText;

    public List<CategoryGameContribution> contributions = new List<CategoryGameContribution>();
}

[Serializable]
public class CategoryGameContribution
{
    public string gameName;
    public float weight;
    public float gameScore;
}

[Serializable]
public class ParentDashboardData
{
    public string profileName;
    public string ageDisplay;

    public float overallScore;
    public string overallScoreLabel;
    public float overallTrend;

    public int totalSessions;
    public string totalPlayTimeDisplay;
    public int gamesPlayedCount;
    public string favoriteGameName;

    public List<CategoryDashboardData> strongestCategories = new List<CategoryDashboardData>();
    public List<CategoryDashboardData> weakestCategories = new List<CategoryDashboardData>();

    public List<GameDashboardData> games = new List<GameDashboardData>();
    public List<CategoryDashboardData> categories = new List<CategoryDashboardData>();
}

// ── Computation ─────────────────────────────────────────────────

public static class ParentDashboardViewModel
{
    // ── Hebrew game names ──
    private static readonly Dictionary<string, string> GameNames = new Dictionary<string, string>
    {
        { "memory",        "\u05DE\u05E9\u05D7\u05E7 \u05D6\u05D9\u05DB\u05E8\u05D5\u05DF" },
        { "puzzle",        "\u05E4\u05D0\u05D6\u05DC" },
        { "coloring",      "\u05E6\u05D1\u05D9\u05E2\u05D4" },
        { "fillthedots",   "\u05D7\u05D1\u05E8 \u05D0\u05EA \u05D4\u05E0\u05E7\u05D5\u05D3\u05D5\u05EA" },
        { "findthecount",  "\u05DB\u05DE\u05D4 \u05D9\u05E9" },
        { "findtheobject", "\u05DE\u05E6\u05D0 \u05D0\u05EA \u05D4\u05D7\u05D9\u05D4" },
        { "shadows",       "\u05D4\u05EA\u05D0\u05DE\u05EA \u05E6\u05DC\u05DC\u05D9\u05DD" },
        { "colormixing",   "\u05E2\u05E8\u05D1\u05D5\u05D1 \u05E6\u05D1\u05E2\u05D9\u05DD" },
        { "colorvoice",    "\u05D3\u05D9\u05D1\u05D5\u05E8 \u05E6\u05D1\u05E2\u05D9\u05DD" },
        { "ballmaze",      "\u05DE\u05D1\u05D5\u05DA \u05D4\u05DB\u05D3\u05D5\u05E8" },
        { "towerbuilder",  "\u05D1\u05E0\u05D4 \u05DE\u05D2\u05D3\u05DC" },
        { "towerstack",    "\u05DE\u05D2\u05D3\u05DC \u05E7\u05D5\u05D1\u05D9\u05D5\u05EA" },
        { "sharedsticker", "\u05DE\u05E6\u05D0 \u05D0\u05EA \u05D4\u05D6\u05D4\u05D4" },
        { "flappybird",    "\u05DE\u05E2\u05D5\u05E3 \u05D4\u05E6\u05D9\u05E4\u05D5\u05E8" },
        { "simonsays",     "\u05D6\u05DB\u05E8\u05D5 \u05D0\u05EA \u05D4\u05E6\u05D1\u05E2\u05D9\u05DD" },
    };

    // ── Hebrew category names + colors ──
    private static readonly Dictionary<SkillCategory, (string name, Color color)> CategoryInfo =
        new Dictionary<SkillCategory, (string, Color)>
    {
        { SkillCategory.Memory,              ("\u05D6\u05D9\u05DB\u05E8\u05D5\u05DF",              HexColor("#7C4DFF")) },
        { SkillCategory.FineMotor,           ("\u05DE\u05D5\u05D8\u05D5\u05E8\u05D9\u05E7\u05D4 \u05E2\u05D3\u05D9\u05E0\u05D4", HexColor("#FF6D00")) },
        { SkillCategory.VisualMatching,      ("\u05D4\u05EA\u05D0\u05DE\u05D4 \u05D7\u05D6\u05D5\u05EA\u05D9\u05EA", HexColor("#00BFA5")) },
        { SkillCategory.Attention,           ("\u05E7\u05E9\u05D1 \u05D5\u05E8\u05D9\u05DB\u05D5\u05D6", HexColor("#F9A825")) },
        { SkillCategory.SpatialReasoning,    ("\u05D7\u05E9\u05D9\u05D1\u05D4 \u05DE\u05E8\u05D7\u05D1\u05D9\u05EA", HexColor("#2979FF")) },
        { SkillCategory.Numbers,             ("\u05DE\u05E1\u05E4\u05E8\u05D9\u05DD",              HexColor("#00C853")) },
        { SkillCategory.ColorsAndShapes,     ("\u05E6\u05D1\u05E2\u05D9\u05DD \u05D5\u05E6\u05D5\u05E8\u05D5\u05EA", HexColor("#EC407A")) },
        { SkillCategory.ProblemSolving,      ("\u05E4\u05EA\u05E8\u05D5\u05DF \u05D1\u05E2\u05D9\u05D5\u05EA", HexColor("#7E57C2")) },
        { SkillCategory.InstructionFollowing,("\u05D4\u05E7\u05E9\u05D1\u05D4 \u05DC\u05D4\u05D5\u05E8\u05D0\u05D5\u05EA", HexColor("#00ACC1")) },
        { SkillCategory.ReactionSpeed,       ("\u05DE\u05D4\u05D9\u05E8\u05D5\u05EA \u05EA\u05D2\u05D5\u05D1\u05D4", HexColor("#F4511E")) },
    };

    public static string GetGameName(string gameId)
    {
        return GameNames.TryGetValue(gameId, out string name) ? name : gameId;
    }

    public static string GetCategoryName(SkillCategory cat)
    {
        return CategoryInfo.TryGetValue(cat, out var info) ? info.name : cat.ToString();
    }

    public static Color GetCategoryColor(SkillCategory cat)
    {
        return CategoryInfo.TryGetValue(cat, out var info) ? info.color : Color.gray;
    }

    // ── Main computation ──

    public static ParentDashboardData Build()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return null;

        var analytics = profile.analytics;
        var data = new ParentDashboardData();

        // Header
        data.profileName = profile.displayName ?? "---";
        data.ageDisplay = profile.age > 0 ? $"{profile.age}" : "---";

        // Overall
        data.overallScore = analytics.globalScore;
        data.overallScoreLabel = GetScoreLabel(analytics.globalScore);
        data.overallTrend = 0f;

        // Totals
        data.totalSessions = analytics.totalSessions;
        data.totalPlayTimeDisplay = FormatPlayTime(analytics.totalPlayTime);
        data.gamesPlayedCount = 0;
        foreach (var g in analytics.games)
            if (g.sessionsPlayed > 0) data.gamesPlayedCount++;

        data.favoriteGameName = analytics.favoriteGames.Count > 0
            ? GetGameName(analytics.favoriteGames[0])
            : "---";

        // Compute overall trend from category trends
        float trendSum = 0f;
        int trendCount = 0;
        foreach (var cat in analytics.categories)
        {
            if (cat.contributingGames > 0)
            {
                trendSum += cat.trend;
                trendCount++;
            }
        }
        if (trendCount > 0) data.overallTrend = trendSum / trendCount;

        // Games
        var mapping = Resources.Load<GameCategoryMapping>("Analytics/GameCategoryMapping");
        foreach (var gp in analytics.games)
        {
            if (gp.sessionsPlayed == 0) continue;
            data.games.Add(BuildGameData(gp, mapping));
        }
        data.games.Sort((a, b) => b.sessionsPlayed.CompareTo(a.sessionsPlayed));

        // Categories
        foreach (SkillCategory cat in Enum.GetValues(typeof(SkillCategory)))
        {
            var cp = analytics.GetOrCreateCategory(cat);
            data.categories.Add(BuildCategoryData(cp, analytics, mapping));
        }

        // Strongest / Weakest (from categories with data)
        var withData = new List<CategoryDashboardData>();
        foreach (var c in data.categories)
            if (c.contributingGamesCount > 0) withData.Add(c);
        withData.Sort((a, b) => b.score.CompareTo(a.score));

        int strongCount = Mathf.Min(3, withData.Count);
        for (int i = 0; i < strongCount; i++)
            data.strongestCategories.Add(withData[i]);

        int weakStart = Mathf.Max(strongCount, withData.Count - 3);
        for (int i = withData.Count - 1; i >= weakStart && i >= 0; i--)
        {
            if (!data.strongestCategories.Contains(withData[i]))
                data.weakestCategories.Add(withData[i]);
        }

        return data;
    }

    private static GameDashboardData BuildGameData(GamePerformanceProfile gp, GameCategoryMapping mapping)
    {
        var gd = new GameDashboardData
        {
            gameId = gp.gameId,
            gameName = GetGameName(gp.gameId),
            score = gp.performanceScore,
            trend = gp.improvementTrend,
            sessionsPlayed = gp.sessionsPlayed,
            totalPlayTime = 0f,
            lastPlayed = 0,
            accuracy = gp.averageAccuracy,
            mistakeRate = gp.mistakeRate,
            speedScore = gp.speedScore,
            independenceScore = gp.independenceScore,
            currentDifficulty = gp.currentDifficulty,
            highestDifficulty = gp.currentDifficulty,
        };

        // Aggregate from recent sessions
        int completed = 0;
        int maxStreak = 0;
        foreach (var s in gp.recentSessions)
        {
            gd.totalPlayTime += s.durationSeconds;
            if (s.startTime > gd.lastPlayed) gd.lastPlayed = s.startTime;
            if (s.completed) completed++;
            if (s.difficultyLevel > gd.highestDifficulty)
                gd.highestDifficulty = s.difficultyLevel;
            if (s.maxStreak > maxStreak) maxStreak = s.maxStreak;

            gd.recentSessions.Add(new SessionSummary
            {
                timestamp = s.startTime,
                difficulty = s.difficultyLevel,
                accuracy = s.totalActions > 0 ? (float)s.correctActions / s.totalActions : 0f,
                durationSeconds = s.durationSeconds,
                mistakes = s.mistakes,
                completed = s.completed
            });
        }

        gd.maxStreak = maxStreak;
        gd.completionRate = gp.recentSessions.Count > 0
            ? (float)completed / gp.recentSessions.Count : 0f;

        // Consistency from performance profile calculation
        gd.consistencyScore = gp.performanceScore; // simplified

        // Categories this game contributes to
        if (mapping != null)
        {
            var weights = mapping.GetWeights(gp.gameId);
            if (weights != null)
            {
                foreach (var w in weights)
                    gd.categoryNames.Add(GetCategoryName(w.category));
            }
        }

        gd.insightText = GenerateGameInsight(gd);
        return gd;
    }

    private static CategoryDashboardData BuildCategoryData(
        CategoryProfile cp, ChildAnalyticsProfile analytics, GameCategoryMapping mapping)
    {
        var cd = new CategoryDashboardData
        {
            category = cp.category,
            categoryName = GetCategoryName(cp.category),
            color = GetCategoryColor(cp.category),
            score = cp.categoryScore,
            trend = cp.trend,
            confidence = cp.confidence,
            contributingGamesCount = cp.contributingGames,
        };

        // Contributing games
        if (mapping != null)
        {
            float topScore = -1f;
            foreach (var entry in mapping.entries)
            {
                float catWeight = 0f;
                foreach (var w in entry.weights)
                    if (w.category == cp.category) { catWeight = w.weight; break; }
                if (catWeight <= 0f) continue;

                var gp = analytics.GetOrCreateGame(entry.gameId);
                if (gp.sessionsPlayed == 0) continue;

                cd.contributions.Add(new CategoryGameContribution
                {
                    gameName = GetGameName(entry.gameId),
                    weight = catWeight,
                    gameScore = gp.performanceScore
                });

                if (gp.performanceScore > topScore)
                {
                    topScore = gp.performanceScore;
                    cd.topGameName = GetGameName(entry.gameId);
                }
            }
        }

        cd.insightText = GenerateCategoryInsight(cd);
        return cd;
    }

    // ── Insight generation ──

    private static string GenerateGameInsight(GameDashboardData gd)
    {
        if (gd.sessionsPlayed < 2)
            return "\u05E2\u05D5\u05D3 \u05DC\u05D0 \u05DE\u05E1\u05E4\u05D9\u05E7 \u05E0\u05EA\u05D5\u05E0\u05D9\u05DD"; // עוד לא מספיק נתונים
        if (gd.trend > 5f)
            return "\u05DE\u05E8\u05D0\u05D4 \u05E9\u05D9\u05E4\u05D5\u05E8 \u05DE\u05E9\u05DE\u05E2\u05D5\u05EA\u05D9"; // מראה שיפור משמעותי
        if (gd.score > 80f)
            return "\u05D1\u05D9\u05E6\u05D5\u05E2\u05D9\u05DD \u05DE\u05E6\u05D5\u05D9\u05E0\u05D9\u05DD!"; // ביצועים מצוינים!
        if (gd.score > 60f)
            return "\u05D4\u05EA\u05E7\u05D3\u05DE\u05D5\u05EA \u05D9\u05E6\u05D9\u05D1\u05D4"; // התקדמות יציבה
        if (gd.completionRate < 0.5f)
            return "\u05E6\u05E8\u05D9\u05DA \u05E2\u05D5\u05D3 \u05EA\u05E8\u05D2\u05D5\u05DC"; // צריך עוד תרגול
        return "\u05DE\u05DE\u05E9\u05D9\u05DA \u05DC\u05D4\u05EA\u05E4\u05EA\u05D7"; // ממשיך להתפתח
    }

    private static string GenerateCategoryInsight(CategoryDashboardData cd)
    {
        if (cd.contributingGamesCount == 0)
            return "\u05E2\u05D5\u05D3 \u05DC\u05D0 \u05E0\u05D5\u05E1\u05D4"; // עוד לא נוסה
        if (cd.score > 80f)
            return "\u05DB\u05D9\u05E9\u05D5\u05E8\u05D9\u05DD \u05D7\u05D6\u05E7\u05D9\u05DD \u05DE\u05D0\u05D5\u05D3"; // כישורים חזקים מאוד
        if (cd.score > 60f)
            return "\u05DE\u05E8\u05D0\u05D4 \u05D9\u05DB\u05D5\u05DC\u05EA \u05D8\u05D5\u05D1\u05D4"; // מראה יכולת טובה
        if (cd.trend > 3f)
            return "\u05DE\u05E9\u05EA\u05E4\u05E8 \u05D1\u05D4\u05EA\u05DE\u05D3\u05D4"; // משתפר בהתמדה
        return "\u05E6\u05E8\u05D9\u05DA \u05E2\u05D5\u05D3 \u05EA\u05E8\u05D2\u05D5\u05DC"; // צריך עוד תרגול
    }

    // ── Helpers ──

    public static string GetScoreLabel(float score)
    {
        if (score >= 80f) return "\u05D4\u05EA\u05E7\u05D3\u05DE\u05D5\u05EA \u05DE\u05E2\u05D5\u05DC\u05D4"; // התקדמות מעולה
        if (score >= 60f) return "\u05DC\u05DE\u05D9\u05D3\u05D4 \u05D9\u05E6\u05D9\u05D1\u05D4"; // למידה יציבה
        if (score >= 40f) return "\u05DE\u05E4\u05EA\u05D7 \u05DB\u05D9\u05E9\u05D5\u05E8\u05D9\u05DD"; // מפתח כישורים
        if (score > 0f)  return "\u05E6\u05E8\u05D9\u05DA \u05E2\u05D5\u05D3 \u05EA\u05E8\u05D2\u05D5\u05DC"; // צריך עוד תרגול
        return "\u05E2\u05D5\u05D3 \u05DC\u05D0 \u05D4\u05EA\u05D7\u05D9\u05DC"; // עוד לא התחיל
    }

    public static string FormatPlayTime(float seconds)
    {
        if (seconds < 60f) return "\u05E4\u05D7\u05D5\u05EA \u05DE\u05D3\u05E7\u05D4"; // פחות מדקה
        int mins = Mathf.FloorToInt(seconds / 60f);
        if (mins < 60) return $"{mins} \u05D3\u05E7\u05D5\u05EA"; // X דקות
        int hours = mins / 60;
        int remMins = mins % 60;
        if (remMins == 0) return $"{hours} \u05E9\u05E2\u05D5\u05EA"; // X שעות
        return $"{hours} \u05E9\u05E2\u05D5\u05EA {remMins} \u05D3\u05E7\u05D5\u05EA"; // X שעות Y דקות
    }

    public static string FormatDate(long unixTimestamp)
    {
        if (unixTimestamp <= 0) return "---";
        var dt = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).LocalDateTime;
        var now = DateTime.Now;
        if (dt.Date == now.Date) return "\u05D4\u05D9\u05D5\u05DD"; // היום
        if (dt.Date == now.Date.AddDays(-1)) return "\u05D0\u05EA\u05DE\u05D5\u05DC"; // אתמול
        return $"{dt.Day}/{dt.Month}";
    }

    public static Color ScoreColor(float score)
    {
        if (score >= 70f) return HexColor("#27AE60");
        if (score >= 40f) return HexColor("#F39C12");
        return HexColor("#E74C3C");
    }

    public static string TrendArrow(float trend)
    {
        if (trend > 2f) return "\u2191"; // ↑
        if (trend < -2f) return "\u2193"; // ↓
        return "\u2194"; // ↔
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
