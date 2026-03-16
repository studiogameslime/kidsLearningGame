using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Computes derived metrics (engagement, exploration, play style, hint dependence,
/// persistence, difficulty balance, confidence) and generates parent-facing insights
/// and badges. All visible strings are Hebrew.
/// </summary>
public static class InsightsEngine
{
    // ═══════════════════════════════════════════════════════════════
    //  ENGAGEMENT SCORE (0-100)
    // ═══════════════════════════════════════════════════════════════

    public static float ComputeEngagement(ChildAnalyticsProfile analytics)
    {
        if (analytics.totalSessions == 0) return 0f;

        // Sessions per week (approximate from total time span)
        float weeklyRate = ComputeWeeklySessionRate(analytics);
        float freqScore = Mathf.Clamp01(weeklyRate / 10f) * 30f; // up to 30 pts

        // Completion rate across all games
        float completionRate = ComputeOverallCompletionRate(analytics);
        float completionScore = completionRate * 30f; // up to 30 pts

        // Abandonment penalty
        float abandonRate = ComputeOverallAbandonRate(analytics);
        float abandonPenalty = abandonRate * 20f; // up to -20 pts

        // Return frequency (unique games played / total games available)
        int gamesPlayed = 0;
        foreach (var g in analytics.games)
            if (g.sessionsPlayed > 0) gamesPlayed++;
        float returnScore = Mathf.Clamp01(gamesPlayed / 8f) * 20f; // up to 20 pts

        return Mathf.Clamp(freqScore + completionScore - abandonPenalty + returnScore, 0f, 100f);
    }

    public static string EngagementLabel(float score)
    {
        if (score >= 70f) return "\u05DE\u05E2\u05D5\u05E8\u05D1\u05D5\u05EA \u05D2\u05D1\u05D5\u05D4\u05D4"; // מעורבות גבוהה
        if (score >= 40f) return "\u05DE\u05E2\u05D5\u05E8\u05D1\u05D5\u05EA \u05D1\u05D9\u05E0\u05D5\u05E0\u05D9\u05EA"; // מעורבות בינונית
        return "\u05DE\u05E2\u05D5\u05E8\u05D1\u05D5\u05EA \u05E0\u05DE\u05D5\u05DB\u05D4"; // מעורבות נמוכה
    }

    // ═══════════════════════════════════════════════════════════════
    //  EXPLORATION SCORE
    // ═══════════════════════════════════════════════════════════════

    public static string ExplorationLabel(ChildAnalyticsProfile analytics)
    {
        if (analytics.totalSessions < 3) return "";

        int uniqueGames = 0;
        int maxSessions = 0;
        int totalSessions = 0;
        foreach (var g in analytics.games)
        {
            if (g.sessionsPlayed > 0) uniqueGames++;
            if (g.sessionsPlayed > maxSessions) maxSessions = g.sessionsPlayed;
            totalSessions += g.sessionsPlayed;
        }

        if (uniqueGames == 0) return "";

        // Concentration ratio: how much of play is on the top game
        float concentration = totalSessions > 0 ? (float)maxSessions / totalSessions : 0f;

        if (uniqueGames >= 6 && concentration < 0.35f)
            return "\u05D0\u05D5\u05D4\u05D1 \u05DC\u05D7\u05E7\u05D5\u05E8 \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D7\u05D3\u05E9\u05D9\u05DD"; // אוהב לחקור משחקים חדשים
        if (concentration > 0.6f)
            return "\u05DE\u05E2\u05D3\u05D9\u05E3 \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05DE\u05D5\u05DB\u05E8\u05D9\u05DD"; // מעדיף משחקים מוכרים
        return "\u05DE\u05E9\u05DC\u05D1 \u05D1\u05D9\u05DF \u05DE\u05D5\u05DB\u05E8 \u05DC\u05D7\u05D3\u05E9"; // משלב בין מוכר לחדש
    }

    // ═══════════════════════════════════════════════════════════════
    //  PLAY STYLE
    // ═══════════════════════════════════════════════════════════════

    public static string PlayStyleLabel(ChildAnalyticsProfile analytics)
    {
        if (analytics.totalSessions < 3) return "";

        float avgDuration = analytics.totalPlayTime / Mathf.Max(1, analytics.totalSessions);

        if (avgDuration < 60f)
            return "\u05DE\u05E9\u05D7\u05E7 \u05D1\u05E4\u05E8\u05E7\u05D9 \u05D6\u05DE\u05DF \u05E7\u05E6\u05E8\u05D9\u05DD"; // משחק בפרקי זמן קצרים
        if (avgDuration > 180f)
            return "\u05DE\u05E9\u05D7\u05E7 \u05DE\u05DE\u05D5\u05E7\u05D3"; // משחק ממוקד
        return "\u05E0\u05DB\u05E0\u05E1 \u05DC\u05E2\u05D9\u05EA\u05D9\u05DD \u05E7\u05E8\u05D5\u05D1\u05D5\u05EA \u05DC\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E7\u05E6\u05E8\u05D9\u05DD"; // נכנס לעיתים קרובות למשחקים קצרים
    }

    // ═══════════════════════════════════════════════════════════════
    //  HINT DEPENDENCE (per game)
    // ═══════════════════════════════════════════════════════════════

    public static string HintDependenceLabel(GamePerformanceProfile game)
    {
        if (game.recentSessions.Count < 2) return "";

        float totalHints = 0f;
        int count = 0;
        foreach (var s in game.recentSessions)
        {
            totalHints += s.hintsUsed;
            count++;
        }
        float avgHints = count > 0 ? totalHints / count : 0f;

        if (avgHints < 0.5f)
            return "\u05DB\u05DE\u05E2\u05D8 \u05DC\u05DC\u05D0 \u05E2\u05D6\u05E8\u05D4"; // כמעט ללא עזרה
        if (avgHints < 2f)
            return "\u05DC\u05E2\u05D9\u05EA\u05D9\u05DD \u05E0\u05E2\u05D6\u05E8"; // לעיתים נעזר
        return "\u05D6\u05E7\u05D5\u05E7 \u05DC\u05E2\u05D6\u05E8\u05D4 \u05DC\u05E2\u05D9\u05EA\u05D9\u05DD \u05E7\u05E8\u05D5\u05D1\u05D5\u05EA"; // זקוק לעזרה לעיתים קרובות
    }

    // ═══════════════════════════════════════════════════════════════
    //  PERSISTENCE (per game)
    // ═══════════════════════════════════════════════════════════════

    public static string PersistenceLabel(GamePerformanceProfile game)
    {
        if (game.recentSessions.Count < 2) return "";

        int completedAfterMistakes = 0;
        int sessionsWithMistakes = 0;
        int abandoned = 0;

        foreach (var s in game.recentSessions)
        {
            if (s.mistakes > 0)
            {
                sessionsWithMistakes++;
                if (s.completed) completedAfterMistakes++;
            }
            if (s.abandoned) abandoned++;
        }

        int n = game.recentSessions.Count;
        float abandonRate = (float)abandoned / n;

        if (abandonRate > 0.5f)
            return "\u05E0\u05D5\u05D8\u05D4 \u05DC\u05E2\u05E6\u05D5\u05E8 \u05DE\u05D5\u05E7\u05D3\u05DD"; // נוטה לעצור מוקדם
        if (sessionsWithMistakes > 0 && (float)completedAfterMistakes / sessionsWithMistakes > 0.7f)
            return "\u05DE\u05EA\u05DE\u05D5\u05D3\u05D3 \u05D9\u05E4\u05D4 \u05E2\u05DD \u05D8\u05E2\u05D9\u05D5\u05EA"; // מתמודד יפה עם טעויות
        return "\u05D4\u05EA\u05DE\u05D3\u05D4 \u05D2\u05D1\u05D5\u05D4\u05D4"; // התמדה גבוהה
    }

    // ═══════════════════════════════════════════════════════════════
    //  DIFFICULTY BALANCE (per game)
    // ═══════════════════════════════════════════════════════════════

    public static string DifficultyBalanceLabel(GamePerformanceProfile game)
    {
        if (game.recentSessions.Count < 2) return "";

        int n = Mathf.Min(5, game.recentSessions.Count);
        float avgAcc = 0f;
        float avgMistakes = 0f;
        int abandoned = 0;
        int completed = 0;

        for (int i = game.recentSessions.Count - n; i < game.recentSessions.Count; i++)
        {
            var s = game.recentSessions[i];
            avgAcc += s.totalActions > 0 ? (float)s.correctActions / s.totalActions : 0f;
            avgMistakes += s.mistakes;
            if (s.abandoned) abandoned++;
            if (s.completed) completed++;
        }
        avgAcc /= n;
        avgMistakes /= n;

        if (avgAcc > 0.95f && avgMistakes < 0.5f)
            return "\u05E7\u05DC \u05DE\u05D3\u05D9"; // קל מדי
        if (abandoned > n / 2 || avgAcc < 0.3f)
            return "\u05E7\u05E9\u05D4 \u05DE\u05D3\u05D9"; // קשה מדי
        if (avgAcc < 0.5f || avgMistakes > 3f)
            return "\u05DE\u05D0\u05EA\u05D2\u05E8"; // מאתגר
        return "\u05DE\u05D0\u05D5\u05D6\u05DF"; // מאוזן
    }

    // ═══════════════════════════════════════════════════════════════
    //  TREND LABELS
    // ═══════════════════════════════════════════════════════════════

    public static string TrendLabel(float trend)
    {
        if (trend > 2f) return "\u05D1\u05DE\u05D2\u05DE\u05EA \u05E9\u05D9\u05E4\u05D5\u05E8"; // במגמת שיפור
        if (trend < -2f) return "\u05D3\u05D5\u05E8\u05E9 \u05E2\u05D5\u05D3 \u05EA\u05E8\u05D2\u05D5\u05DC"; // דורש עוד תרגול
        return "\u05D9\u05E6\u05D9\u05D1"; // יציב
    }

    // ═══════════════════════════════════════════════════════════════
    //  CONFIDENCE LABELS
    // ═══════════════════════════════════════════════════════════════

    public static string ConfidenceLabel(float confidence)
    {
        if (confidence >= 0.7f) return "\u05D1\u05D9\u05D8\u05D7\u05D5\u05DF \u05D2\u05D1\u05D5\u05D4"; // ביטחון גבוה
        if (confidence >= 0.35f) return "\u05D1\u05D9\u05D8\u05D7\u05D5\u05DF \u05D1\u05D9\u05E0\u05D5\u05E0\u05D9"; // ביטחון בינוני
        return "\u05D1\u05D9\u05D8\u05D7\u05D5\u05DF \u05E0\u05DE\u05D5\u05DA"; // ביטחון נמוך
    }

    // ═══════════════════════════════════════════════════════════════
    //  THIS WEEK STATS
    // ═══════════════════════════════════════════════════════════════

    public static float GetThisWeekPlayTime(ChildAnalyticsProfile analytics)
    {
        long weekAgo = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 7 * 86400;
        float total = 0f;
        foreach (var g in analytics.games)
        {
            foreach (var s in g.recentSessions)
            {
                if (s.startTime >= weekAgo)
                    total += s.durationSeconds;
            }
        }
        return total;
    }

    public static int GetThisWeekSessions(ChildAnalyticsProfile analytics)
    {
        long weekAgo = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 7 * 86400;
        int count = 0;
        foreach (var g in analytics.games)
        {
            foreach (var s in g.recentSessions)
            {
                if (s.startTime >= weekAgo) count++;
            }
        }
        return count;
    }

    // ═══════════════════════════════════════════════════════════════
    //  PARENT INSIGHTS (3-5 Hebrew sentences)
    // ═══════════════════════════════════════════════════════════════

    public static List<ParentInsight> GenerateInsights(
        ChildAnalyticsProfile analytics,
        List<GameDashboardData> games,
        List<CategoryDashboardData> categories)
    {
        var insights = new List<ParentInsight>();

        if (analytics.totalSessions < 2)
        {
            insights.Add(new ParentInsight
            {
                type = "insufficient_data",
                titleHebrew = "\u05DE\u05EA\u05D7\u05D9\u05DC\u05D9\u05DD \u05DC\u05DC\u05DE\u05D5\u05D3",
                descriptionHebrew = "\u05E2\u05D3\u05D9\u05D9\u05DF \u05DC\u05D5\u05DE\u05D3\u05D9\u05DD \u05D0\u05EA \u05D3\u05E4\u05D5\u05E1\u05D9 \u05D4\u05DE\u05E9\u05D7\u05E7 \u05E9\u05DC \u05D4\u05D9\u05DC\u05D3",
                // מתחילים ללמוד / עדיין לומדים את דפוסי המשחק של הילד
                iconId = "info",
                priority = 0
            });
            return insights;
        }

        // 1. Strongest skills
        var strong = GetStrongestCategories(categories, 2);
        if (strong.Count >= 2)
        {
            insights.Add(new ParentInsight
            {
                type = "strongest_skills",
                titleHebrew = "\u05DB\u05D9\u05E9\u05D5\u05E8\u05D9\u05DD \u05D7\u05D6\u05E7\u05D9\u05DD",
                descriptionHebrew = $"\u05D4\u05D9\u05DC\u05D3 \u05DE\u05E8\u05D0\u05D4 \u05E9\u05DC\u05D9\u05D8\u05D4 \u05D8\u05D5\u05D1\u05D4 \u05D1\u05DE\u05D9\u05D5\u05D7\u05D3 \u05D1{strong[0].categoryName} \u05D5\u05D1{strong[1].categoryName}",
                // כישורים חזקים / הילד מראה שליטה טובה במיוחד ב... וב...
                iconId = "star",
                priority = 10
            });
        }
        else if (strong.Count == 1)
        {
            insights.Add(new ParentInsight
            {
                type = "strongest_skills",
                titleHebrew = "\u05DB\u05D9\u05E9\u05D5\u05E8\u05D9\u05DD \u05D7\u05D6\u05E7\u05D9\u05DD",
                descriptionHebrew = $"\u05D4\u05D9\u05DC\u05D3 \u05DE\u05E8\u05D0\u05D4 \u05E9\u05DC\u05D9\u05D8\u05D4 \u05D8\u05D5\u05D1\u05D4 \u05D1{strong[0].categoryName}",
                iconId = "star",
                priority = 10
            });
        }

        // 2. Developing skills
        var weak = GetWeakestCategories(categories, 2);
        if (weak.Count > 0 && weak[0].confidence >= 0.3f)
        {
            string names = weak.Count >= 2
                ? $"{weak[0].categoryName} \u05D5{weak[1].categoryName}"
                : weak[0].categoryName;
            insights.Add(new ParentInsight
            {
                type = "developing_skills",
                titleHebrew = "\u05EA\u05D7\u05D5\u05DE\u05D9\u05DD \u05D1\u05EA\u05E8\u05D2\u05D5\u05DC",
                descriptionHebrew = $"{names} \u05E2\u05D3\u05D9\u05D9\u05DF \u05E0\u05DE\u05E6\u05D0\u05D9\u05DD \u05D1\u05EA\u05D4\u05DC\u05D9\u05DA \u05EA\u05E8\u05D2\u05D5\u05DC",
                // תחומים בתרגול / ... עדיין נמצאים בתהליך תרגול
                iconId = "growth",
                priority = 8
            });
        }

        // 3. Favorite game
        if (games.Count > 0)
        {
            var fav = games[0]; // sorted by sessions
            if (fav.sessionsPlayed >= 3)
            {
                insights.Add(new ParentInsight
                {
                    type = "favorite_game",
                    titleHebrew = "\u05DE\u05E9\u05D7\u05E7 \u05D0\u05D4\u05D5\u05D1",
                    descriptionHebrew = $"\u05E0\u05E8\u05D0\u05D4 \u05E9\u05D4\u05D9\u05DC\u05D3 \u05E0\u05D4\u05E0\u05D4 \u05D1\u05DE\u05D9\u05D5\u05D7\u05D3 \u05DE\u05D4\u05DE\u05E9\u05D7\u05E7 '{fav.gameName}'",
                    // משחק אהוב / נראה שהילד נהנה במיוחד מהמשחק '...'
                    iconId = "heart",
                    priority = 7,
                    relatedGameId = fav.gameId
                });
            }
        }

        // 4. Play style
        string playStyle = PlayStyleLabel(analytics);
        if (!string.IsNullOrEmpty(playStyle))
        {
            insights.Add(new ParentInsight
            {
                type = "play_style",
                titleHebrew = "\u05E1\u05D2\u05E0\u05D5\u05DF \u05DE\u05E9\u05D7\u05E7",
                descriptionHebrew = $"\u05D4\u05D9\u05DC\u05D3 \u05E0\u05D5\u05D8\u05D4 \u05DC\u05E9\u05D7\u05E7: {playStyle}",
                // סגנון משחק / הילד נוטה לשחק: ...
                iconId = "play",
                priority = 5
            });
        }

        // 5. Improvement insight (find a game that's improving)
        GameDashboardData improvingGame = null;
        float bestTrend = 0f;
        foreach (var g in games)
        {
            if (g.trend > bestTrend && g.sessionsPlayed >= 4)
            {
                bestTrend = g.trend;
                improvingGame = g;
            }
        }
        if (improvingGame != null && bestTrend > 2f)
        {
            insights.Add(new ParentInsight
            {
                type = "improvement",
                titleHebrew = "\u05E9\u05D9\u05E4\u05D5\u05E8",
                descriptionHebrew = $"\u05E0\u05D9\u05DB\u05E8\u05EA \u05D4\u05EA\u05E7\u05D3\u05DE\u05D5\u05EA \u05D9\u05E4\u05D4 \u05D1\u05DE\u05E9\u05D7\u05E7 '{improvingGame.gameName}' \u05D1\u05D9\u05DE\u05D9\u05DD \u05D4\u05D0\u05D7\u05E8\u05D5\u05E0\u05D9\u05DD",
                // שיפור / ניכרת התקדמות יפה במשחק '...' בימים האחרונים
                iconId = "trending_up",
                priority = 9,
                relatedGameId = improvingGame.gameId
            });
        }

        // 6. Independence insight
        float totalHints = 0f;
        int totalSess = 0;
        foreach (var g in analytics.games)
        {
            foreach (var s in g.recentSessions)
            {
                totalHints += s.hintsUsed;
                totalSess++;
            }
        }
        if (totalSess >= 3)
        {
            float avgHints = totalHints / totalSess;
            if (avgHints < 0.5f)
            {
                insights.Add(new ParentInsight
                {
                    type = "independence",
                    titleHebrew = "\u05E2\u05E6\u05DE\u05D0\u05D5\u05EA",
                    descriptionHebrew = "\u05D1\u05E8\u05D5\u05D1 \u05D4\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D4\u05D9\u05DC\u05D3 \u05E4\u05D5\u05E2\u05DC \u05D1\u05D0\u05D5\u05E4\u05DF \u05E2\u05E6\u05DE\u05D0\u05D9 \u05E2\u05DD \u05DE\u05E2\u05D8 \u05E2\u05D6\u05E8\u05D4",
                    // עצמאות / ברוב המשחקים הילד פועל באופן עצמאי עם מעט עזרה
                    iconId = "independence",
                    priority = 6
                });
            }
        }

        // Sort by priority descending and return top 5
        insights.Sort((a, b) => b.priority.CompareTo(a.priority));
        if (insights.Count > 5)
            insights.RemoveRange(5, insights.Count - 5);

        return insights;
    }

    // ═══════════════════════════════════════════════════════════════
    //  BADGES
    // ═══════════════════════════════════════════════════════════════

    public static List<ParentBadge> GenerateBadges(ChildAnalyticsProfile analytics)
    {
        var badges = new List<ParentBadge>();

        foreach (var g in analytics.games)
        {
            if (g.sessionsPlayed < 5) continue;

            string badgeTitle = null;
            string badgeSub = null;
            string gameId = g.gameId;

            if (gameId == "memory" && g.performanceScore > 60f)
            {
                badgeTitle = "\u05D7\u05D5\u05E7\u05E8 \u05D4\u05D6\u05D9\u05DB\u05E8\u05D5\u05DF"; // חוקר הזיכרון
                badgeSub = $"{g.sessionsPlayed} \u05DE\u05E9\u05D7\u05E7\u05D9 \u05D6\u05D9\u05DB\u05E8\u05D5\u05DF"; // X משחקי זיכרון
            }
            else if (gameId == "puzzle" && g.performanceScore > 60f)
            {
                badgeTitle = "\u05E4\u05D5\u05EA\u05E8 \u05D4\u05E4\u05D0\u05D6\u05DC\u05D9\u05DD"; // פותר הפאזלים
                badgeSub = $"{g.sessionsPlayed} \u05E4\u05D0\u05D6\u05DC\u05D9\u05DD \u05E9\u05D4\u05D5\u05E8\u05DB\u05D1\u05D5"; // X פאזלים שהורכבו
            }
            else if ((gameId == "findthecount" || gameId == "fillthedots") && g.performanceScore > 60f)
            {
                badgeTitle = "\u05D0\u05D5\u05D4\u05D1 \u05DE\u05E1\u05E4\u05E8\u05D9\u05DD"; // אוהב מספרים
                badgeSub = "\u05E6\u05D9\u05D5\u05DF \u05D2\u05D1\u05D5\u05D4 \u05D1\u05DE\u05E9\u05D7\u05E7\u05D9 \u05DE\u05E1\u05E4\u05E8\u05D9\u05DD"; // ציון גבוה במשחקי מספרים
            }
            else if (g.performanceScore > 75f)
            {
                badgeTitle = $"\u05DE\u05E6\u05D8\u05D9\u05D9\u05DF \u05D1{ParentDashboardViewModel.GetGameName(gameId)}"; // מצטיין ב...
                badgeSub = $"\u05E6\u05D9\u05D5\u05DF {g.performanceScore:F0}"; // ציון X
            }

            if (badgeTitle != null)
            {
                badges.Add(new ParentBadge
                {
                    id = $"game_{gameId}",
                    titleHebrew = badgeTitle,
                    subtitleHebrew = badgeSub,
                    iconId = "trophy",
                    relatedGameId = gameId
                });
            }
        }

        // Persistence badge
        int highPersistenceGames = 0;
        foreach (var g in analytics.games)
        {
            if (g.sessionsPlayed < 3) continue;
            int abandoned = 0;
            foreach (var s in g.recentSessions) if (s.abandoned) abandoned++;
            if ((float)abandoned / g.recentSessions.Count < 0.2f)
                highPersistenceGames++;
        }
        if (highPersistenceGames >= 3)
        {
            badges.Add(new ParentBadge
            {
                id = "persistence",
                titleHebrew = "\u05DE\u05EA\u05E7\u05D3\u05DD \u05D1\u05D4\u05EA\u05DE\u05D3\u05D4", // מתקדם בהתמדה
                subtitleHebrew = "\u05DE\u05E1\u05D9\u05D9\u05DD \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D1\u05D4\u05E6\u05DC\u05D7\u05D4", // מסיים משחקים בהצלחה
                iconId = "medal"
            });
        }

        // Explorer badge
        int uniqueGames = 0;
        foreach (var g in analytics.games)
            if (g.sessionsPlayed > 0) uniqueGames++;
        if (uniqueGames >= 6)
        {
            badges.Add(new ParentBadge
            {
                id = "explorer",
                titleHebrew = "\u05E9\u05D7\u05E7\u05DF \u05E1\u05E7\u05E8\u05DF", // שחקן סקרן
                subtitleHebrew = $"\u05E9\u05D9\u05D7\u05E7 \u05D1{uniqueGames} \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E9\u05D5\u05E0\u05D9\u05DD", // שיחק ב-X משחקים שונים
                iconId = "compass"
            });
        }

        return badges;
    }

    // ═══════════════════════════════════════════════════════════════
    //  INTERNAL HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static float ComputeWeeklySessionRate(ChildAnalyticsProfile analytics)
    {
        if (analytics.totalSessions == 0) return 0f;

        // Find earliest and latest session timestamps
        long earliest = long.MaxValue;
        long latest = 0;
        foreach (var g in analytics.games)
        {
            foreach (var s in g.recentSessions)
            {
                if (s.startTime < earliest) earliest = s.startTime;
                if (s.startTime > latest) latest = s.startTime;
            }
        }

        if (earliest == long.MaxValue || latest == 0) return analytics.totalSessions;

        float weeks = Mathf.Max(1f, (latest - earliest) / (7f * 86400f));
        return analytics.totalSessions / weeks;
    }

    private static float ComputeOverallCompletionRate(ChildAnalyticsProfile analytics)
    {
        int completed = 0, total = 0;
        foreach (var g in analytics.games)
        {
            foreach (var s in g.recentSessions)
            {
                total++;
                if (s.completed) completed++;
            }
        }
        return total > 0 ? (float)completed / total : 0f;
    }

    private static float ComputeOverallAbandonRate(ChildAnalyticsProfile analytics)
    {
        int abandoned = 0, total = 0;
        foreach (var g in analytics.games)
        {
            foreach (var s in g.recentSessions)
            {
                total++;
                if (s.abandoned) abandoned++;
            }
        }
        return total > 0 ? (float)abandoned / total : 0f;
    }

    private static List<CategoryDashboardData> GetStrongestCategories(
        List<CategoryDashboardData> categories, int count)
    {
        var withData = new List<CategoryDashboardData>();
        foreach (var c in categories)
            if (c.contributingGamesCount > 0 && c.score > 50f && c.confidence >= 0.3f)
                withData.Add(c);
        withData.Sort((a, b) => b.score.CompareTo(a.score));
        if (withData.Count > count) withData.RemoveRange(count, withData.Count - count);
        return withData;
    }

    private static List<CategoryDashboardData> GetWeakestCategories(
        List<CategoryDashboardData> categories, int count)
    {
        var withData = new List<CategoryDashboardData>();
        foreach (var c in categories)
            if (c.contributingGamesCount > 0 && c.score < 60f && c.confidence >= 0.2f)
                withData.Add(c);
        withData.Sort((a, b) => a.score.CompareTo(b.score));
        if (withData.Count > count) withData.RemoveRange(count, withData.Count - count);
        return withData;
    }
}
