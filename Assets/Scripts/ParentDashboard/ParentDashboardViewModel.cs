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
    public string trendLabel;

    public int sessionsPlayed;
    public float totalPlayTime;
    public string totalPlayTimeDisplay;
    public string averageSessionDisplay;
    public long lastPlayed;
    public string lastPlayedDisplay;

    public float accuracy;
    public float mistakeRate;
    public float completionRate;
    public float speedScore;
    public float independenceScore;
    public float consistencyScore;

    public int currentDifficulty;
    public int highestDifficulty;
    public bool manualDifficultyOverride;
    public string difficultyModeLabel;
    public string difficultyBalanceLabel;

    // Difficulty impact display
    public int recommendedDifficulty;
    public string activeDifficultyImpact;      // Hebrew: what active difficulty does
    public string recommendedDifficultyImpact;  // Hebrew: what recommended difficulty does

    public int maxStreak;

    public string hintUsageLabel;
    public string persistenceLabel;

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
    public string trendLabel;
    public float confidence;
    public string confidenceLabel;

    public int contributingGamesCount;
    public string topGameName;
    public string insightText;
    public string summaryText;

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

    // Overall
    public float overallScore;
    public string overallScoreLabel;
    public float overallTrend;
    public string overallTrendLabel;

    // Totals
    public int totalSessions;
    public string totalPlayTimeDisplay;
    public int gamesPlayedCount;
    public string favoriteGameName;
    public int totalBubblesPopped;

    // This week
    public int thisWeekSessions;
    public string thisWeekPlayTimeDisplay;

    // Engagement
    public float engagementScore;
    public string engagementLabel;

    // Exploration + play style
    public string explorationLabel;
    public string playStyleLabel;

    // Strengths / weaknesses
    public List<CategoryDashboardData> strongestCategories = new List<CategoryDashboardData>();
    public List<CategoryDashboardData> weakestCategories = new List<CategoryDashboardData>();

    // Insights + badges
    public List<ParentInsight> insights = new List<ParentInsight>();
    public List<ParentBadge> badges = new List<ParentBadge>();

    // Lists
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
        { "bubblepop",     "\u05E4\u05E7\u05E2 \u05D1\u05D5\u05E2\u05D5\u05EA" },
        { "maze",          "\u05DE\u05D1\u05D5\u05DA" },
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

        // Overall trend
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
        data.overallTrend = trendCount > 0 ? trendSum / trendCount : 0f;
        data.overallTrendLabel = InsightsEngine.TrendLabel(data.overallTrend);

        // Totals
        data.totalSessions = analytics.totalSessions;
        data.totalPlayTimeDisplay = FormatPlayTime(analytics.totalPlayTime);
        data.gamesPlayedCount = 0;
        foreach (var g in analytics.games)
            if (g.sessionsPlayed > 0) data.gamesPlayedCount++;

        data.favoriteGameName = analytics.favoriteGames.Count > 0
            ? GetGameName(analytics.favoriteGames[0])
            : "---";
        data.totalBubblesPopped = analytics.totalBubblesPopped;

        // This week
        data.thisWeekSessions = InsightsEngine.GetThisWeekSessions(analytics);
        data.thisWeekPlayTimeDisplay = FormatPlayTime(InsightsEngine.GetThisWeekPlayTime(analytics));

        // Engagement
        data.engagementScore = InsightsEngine.ComputeEngagement(analytics);
        data.engagementLabel = InsightsEngine.EngagementLabel(data.engagementScore);

        // Exploration + play style
        data.explorationLabel = InsightsEngine.ExplorationLabel(analytics);
        data.playStyleLabel = InsightsEngine.PlayStyleLabel(analytics);

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

        // Strongest / Weakest
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

        // Insights + badges
        data.insights = InsightsEngine.GenerateInsights(analytics, data.games, data.categories);
        data.badges = InsightsEngine.GenerateBadges(analytics);

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
            trendLabel = InsightsEngine.TrendLabel(gp.improvementTrend),
            sessionsPlayed = gp.sessionsPlayed,
            totalPlayTime = gp.totalPlayTimeSeconds,
            totalPlayTimeDisplay = FormatPlayTime(gp.totalPlayTimeSeconds),
            lastPlayed = gp.lastPlayedUtc,
            lastPlayedDisplay = FormatDate(gp.lastPlayedUtc),
            accuracy = gp.averageAccuracy,
            mistakeRate = gp.mistakeRate,
            speedScore = gp.speedScore,
            independenceScore = gp.independenceScore,
            currentDifficulty = gp.currentDifficulty,
            highestDifficulty = gp.highestDifficultyReached,
            maxStreak = gp.longestSuccessStreak,
            manualDifficultyOverride = gp.manualDifficultyOverride,
            hintUsageLabel = InsightsEngine.HintDependenceLabel(gp),
            persistenceLabel = InsightsEngine.PersistenceLabel(gp),
            difficultyBalanceLabel = InsightsEngine.DifficultyBalanceLabel(gp),
        };

        // Average session
        gd.averageSessionDisplay = gp.sessionsPlayed > 0
            ? FormatPlayTime(gp.totalPlayTimeSeconds / gp.sessionsPlayed)
            : "---";

        // Difficulty mode label
        gd.difficultyModeLabel = gp.manualDifficultyOverride
            ? "\u05D9\u05D3\u05E0\u05D9" // ידני
            : "\u05D0\u05D5\u05D8\u05D5\u05DE\u05D8\u05D9"; // אוטומטי

        // Difficulty impact labels
        gd.activeDifficultyImpact = GameDifficultyConfig.GetDifficultyImpactLabel(gp.gameId, gp.currentDifficulty);
        gd.recommendedDifficulty = gp.currentDifficulty; // stored before override
        gd.recommendedDifficultyImpact = GameDifficultyConfig.GetDifficultyImpactLabel(gp.gameId, gd.recommendedDifficulty);

        // Aggregate from recent sessions for completion rate and legacy fields
        int completed = 0;
        foreach (var s in gp.recentSessions)
        {
            if (s.completed) completed++;

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

        gd.completionRate = gp.recentSessions.Count > 0
            ? (float)completed / gp.recentSessions.Count : 0f;

        gd.consistencyScore = gp.performanceScore;

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
            trendLabel = InsightsEngine.TrendLabel(cp.trend),
            confidence = cp.confidence,
            confidenceLabel = InsightsEngine.ConfidenceLabel(cp.confidence),
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
        cd.summaryText = GenerateCategorySummary(cd);
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

    private static string GenerateCategorySummary(CategoryDashboardData cd)
    {
        if (cd.contributingGamesCount == 0)
            return "\u05E2\u05D3\u05D9\u05D9\u05DF \u05D0\u05D9\u05DF \u05DE\u05E1\u05E4\u05D9\u05E7 \u05E0\u05EA\u05D5\u05E0\u05D9\u05DD \u05DC\u05D4\u05E6\u05D2\u05D4"; // עדיין אין מספיק נתונים להצגה
        if (cd.confidence < 0.3f)
            return "\u05E6\u05E8\u05D9\u05DA \u05E2\u05D5\u05D3 \u05E0\u05EA\u05D5\u05E0\u05D9\u05DD \u05DC\u05D4\u05E2\u05E8\u05DB\u05D4 \u05DE\u05D3\u05D5\u05D9\u05E7\u05EA"; // צריך עוד נתונים להערכה מדויקת
        if (cd.score > 80f)
            return $"\u05DE\u05E8\u05D0\u05D4 \u05E9\u05DC\u05D9\u05D8\u05D4 \u05D8\u05D5\u05D1\u05D4 \u05D1{cd.categoryName}"; // מראה שליטה טובה ב...
        if (cd.score > 60f)
            return $"\u05DE\u05EA\u05E7\u05D3\u05DD \u05D9\u05E4\u05D4 \u05D1{cd.categoryName}"; // מתקדם יפה ב...
        return $"\u05E2\u05D3\u05D9\u05D9\u05DF \u05DE\u05EA\u05E8\u05D2\u05DC \u05D1{cd.categoryName}"; // עדיין מתרגל ב...
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
