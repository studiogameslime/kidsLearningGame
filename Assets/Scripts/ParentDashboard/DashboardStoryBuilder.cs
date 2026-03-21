using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates the narrative-driven parent dashboard content.
/// Transforms raw analytics into a personal, scannable story.
/// </summary>
public static class DashboardStoryBuilder
{
    // ═══════════════════════════════════════════════════════════════
    //  DATA STRUCTURES
    // ═══════════════════════════════════════════════════════════════

    public struct StoryData
    {
        // Hero
        public string weeklySummary;
        public string focusNow;

        // Grouped insights (max 2-3 per group)
        public List<string> strengthInsights;
        public List<string> practiceInsights;
        public List<string> behaviorInsights;

        // Strengths vs Practice (simple names)
        public List<string> strengths;
        public List<string> practiceAreas;

        // New advanced insights
        public List<string> confusionPairs;       // "אריה ↔ נמר"
        public string strongLetters;               // "א, מ, ש"
        public string weakLetters;                 // "ד, ר"
        public List<string> levelUpGames;          // ready to advance
        public List<string> keepLevelGames;        // stay current
        public List<string> easierLevelGames;      // needs easier
        public string improvementMetric1;           // "accuracy improved 18%"
        public string improvementMetric2;           // "uses fewer hints"
        public string learningSpeed;                // one sentence
        public List<string> masteryLines;           // 1-2 lines
        public string coloringPrecision;            // one sentence

        // Development overview (sorted strongest to weakest)
        public List<(string name, int score)> categoryBars;

        // Improvements (2-3 short lines)
        public List<string> improvements;

        // Recommendations
        public List<string> recommendedGames;     // "name (reason)"

        // Progress snapshot
        public string accuracyTrend;               // "↑ improving" / "→ stable"
        public string lastScores;                   // "62 → 68 → 74"

        // Bottom
        public string progressHighlight;
        public string suggestedNextStep;
    }

    // ═══════════════════════════════════════════════════════════════
    //  BUILD
    // ═══════════════════════════════════════════════════════════════

    public static StoryData Build(ParentDashboardData data, ChildAnalyticsProfile analytics)
    {
        var story = new StoryData
        {
            strengthInsights = new List<string>(),
            practiceInsights = new List<string>(),
            behaviorInsights = new List<string>(),
            strengths = new List<string>(),
            practiceAreas = new List<string>(),
            confusionPairs = new List<string>(),
            levelUpGames = new List<string>(),
            keepLevelGames = new List<string>(),
            easierLevelGames = new List<string>(),
            masteryLines = new List<string>(),
            categoryBars = new List<(string, int)>(),
            improvements = new List<string>(),
            recommendedGames = new List<string>()
        };

        if (data == null || analytics == null || analytics.totalSessions < 2)
        {
            story.weeklySummary = "\u05E2\u05D3\u05D9\u05D9\u05DF \u05DC\u05D5\u05DE\u05D3\u05D9\u05DD \u05D0\u05EA \u05D3\u05E4\u05D5\u05E1\u05D9 \u05D4\u05DE\u05E9\u05D7\u05E7. \u05DB\u05E9\u05D9\u05E9\u05D7\u05E7 \u05E2\u05D5\u05D3 \u05E7\u05E6\u05EA \u05E0\u05D5\u05DB\u05DC \u05DC\u05E1\u05E4\u05E8 \u05D9\u05D5\u05EA\u05E8.";
            story.focusNow = "\u05D4\u05DE\u05E9\u05D9\u05DB\u05D5 \u05DC\u05E9\u05D7\u05E7 \u05D5\u05DC\u05D7\u05E7\u05D5\u05E8!";
            return story;
        }

        string name = data.profileName ?? "\u05D4\u05D9\u05DC\u05D3";

        // ── Strengths & Practice Areas ──
        foreach (var cat in data.categories)
        {
            if (cat.contributingGamesCount == 0) continue;
            if (cat.score > 55f && cat.confidence >= 0.3f)
                story.strengths.Add(cat.categoryName);
            else if (cat.score < 50f && cat.confidence >= 0.2f)
                story.practiceAreas.Add(cat.categoryName);
        }
        if (story.strengths.Count > 3) story.strengths.RemoveRange(3, story.strengths.Count - 3);
        if (story.practiceAreas.Count > 3) story.practiceAreas.RemoveRange(3, story.practiceAreas.Count - 3);

        // ── Hero Summary ──
        story.weeklySummary = BuildWeeklySummary(name, data, analytics, story.strengths, story.practiceAreas);
        story.focusNow = BuildFocusNow(data, analytics, story.practiceAreas);

        // ── Grouped Insights ──
        BuildGroupedInsights(data, analytics, story);

        // ── Confusion Pairs ──
        BuildConfusionPairs(analytics, story);

        // ── Letter Insights ──
        BuildLetterInsights(analytics, story);

        // ── Difficulty Recommendations ──
        BuildDifficultyRecommendations(data, analytics, story);

        // ── Improvement Metrics ──
        BuildImprovementMetrics(data, analytics, story);

        // ── Learning Speed ──
        story.learningSpeed = BuildLearningSpeed(analytics);

        // ── Current Mastery ──
        BuildMasteryLines(data, story);

        // ── Coloring Precision ──
        story.coloringPrecision = BuildColoringPrecision(analytics);

        // ── Category Bars ──
        BuildCategoryBars(data, story);

        // ── Improvements ──
        BuildImprovements(data, analytics, story);

        // ── Recommended Games ──
        BuildRecommendedGames(data, story);

        // ── Progress Snapshot ──
        BuildProgressSnapshot(data, analytics, story);

        // ── Progress & Next Step ──
        story.progressHighlight = BuildProgressHighlight(data);
        story.suggestedNextStep = BuildNextStep(data, story.practiceAreas, story.strengths);

        return story;
    }

    // ═══════════════════════════════════════════════════════════════
    //  BUILDERS
    // ═══════════════════════════════════════════════════════════════

    private static string BuildWeeklySummary(string name, ParentDashboardData data,
        ChildAnalyticsProfile analytics, List<string> strengths, List<string> practice)
    {
        var parts = new List<string>();

        if (strengths.Count >= 2)
            parts.Add($"{name} \u05DE\u05E8\u05D0\u05D4 \u05D1\u05D9\u05D8\u05D7\u05D5\u05DF \u05D2\u05D5\u05D1\u05E8 \u05D1{strengths[0]} \u05D5\u05D1{strengths[1]}");
        else if (strengths.Count == 1)
            parts.Add($"{name} \u05DE\u05E8\u05D0\u05D4 \u05D1\u05D9\u05D8\u05D7\u05D5\u05DF \u05D2\u05D5\u05D1\u05E8 \u05D1{strengths[0]}");

        float avgHints = 0f; int sessCount = 0;
        foreach (var g in analytics.games)
            foreach (var s in g.recentSessions) { avgHints += s.hintsUsed; sessCount++; }
        if (sessCount > 0) avgHints /= sessCount;

        if (avgHints < 0.5f && sessCount >= 3)
            parts.Add("\u05D5\u05D1\u05D3\u05E8\u05DA \u05DB\u05DC\u05DC \u05DE\u05E9\u05D7\u05E7 \u05D1\u05D0\u05D5\u05E4\u05DF \u05E2\u05E6\u05DE\u05D0\u05D9");

        if (practice.Count > 0)
            parts.Add("\u05D7\u05DC\u05E7 \u05DE\u05D4\u05E4\u05E2\u05D9\u05DC\u05D5\u05D9\u05D5\u05EA \u05E2\u05D3\u05D9\u05D9\u05DF \u05DE\u05D0\u05EA\u05D2\u05E8\u05D5\u05EA");

        return parts.Count > 0 ? string.Join(". ", parts) + "."
            : $"{name} \u05DE\u05DE\u05E9\u05D9\u05DA \u05DC\u05E9\u05D7\u05E7 \u05D5\u05DC\u05D4\u05EA\u05E4\u05EA\u05D7!";
    }

    private static string BuildFocusNow(ParentDashboardData data, ChildAnalyticsProfile analytics, List<string> practice)
    {
        foreach (var g in analytics.games)
        {
            if (g.recentSessions == null || g.recentSessions.Count < 3) continue;
            var bi = BehavioralPatternAnalyzer.AnalyzeGameHistory(g);
            foreach (var b in bi)
            {
                if (b.patternKey == "focus_drop")
                    return "\u05E0\u05E1\u05D5 \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E7\u05E6\u05E8\u05D9\u05DD \u05DB\u05D3\u05D9 \u05DC\u05E9\u05DE\u05D5\u05E8 \u05E2\u05DC \u05E8\u05D9\u05DB\u05D5\u05D6.";
                if (b.patternKey == "impulsive")
                    return "\u05E2\u05D5\u05D3\u05D3\u05D5 \u05D0\u05EA \u05D4\u05D9\u05DC\u05D3 \u05DC\u05E7\u05D7\u05EA \u05D0\u05EA \u05D4\u05D6\u05DE\u05DF \u05DC\u05E4\u05E0\u05D9 \u05DC\u05D7\u05D9\u05E6\u05D4.";
            }
        }

        if (practice.Count > 0)
            return $"\u05D4\u05DE\u05E9\u05D9\u05DB\u05D5 \u05DC\u05EA\u05E8\u05D2\u05DC \u05E4\u05E2\u05D9\u05DC\u05D5\u05D9\u05D5\u05EA \u05D1\u05EA\u05D7\u05D5\u05DD {practice[0]}.";

        if (data.overallScore > 70f)
            return "\u05E0\u05E1\u05D5 \u05DC\u05D4\u05E2\u05DC\u05D5\u05EA \u05D0\u05EA \u05E8\u05DE\u05EA \u05D4\u05E7\u05D5\u05E9\u05D9 \u05D1\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E9\u05D4\u05D9\u05DC\u05D3 \u05E9\u05D5\u05DC\u05D8 \u05D1\u05D4\u05DD.";

        return "\u05D4\u05DE\u05E9\u05D9\u05DB\u05D5 \u05DC\u05E9\u05D7\u05E7 \u05DE\u05D2\u05D5\u05D5\u05DF \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD!";
    }

    private static void BuildGroupedInsights(ParentDashboardData data, ChildAnalyticsProfile analytics, StoryData story)
    {
        var seenKeys = new HashSet<string>();

        foreach (var g in analytics.games)
        {
            if (g.recentSessions == null || g.recentSessions.Count < 2) continue;
            var bi = BehavioralPatternAnalyzer.AnalyzeGameHistory(g);
            foreach (var b in bi)
            {
                if (seenKeys.Contains(b.patternKey)) continue;
                if (b.confidence == BehavioralPatternAnalyzer.ConfidenceLevel.Low) continue;
                seenKeys.Add(b.patternKey);

                switch (b.type)
                {
                    case BehavioralPatternAnalyzer.InsightType.Strength:
                        if (story.strengthInsights.Count < 3) story.strengthInsights.Add(b.insight);
                        break;
                    case BehavioralPatternAnalyzer.InsightType.Improvement:
                        if (story.strengthInsights.Count < 3) story.strengthInsights.Add(b.insight);
                        break;
                    case BehavioralPatternAnalyzer.InsightType.PracticeArea:
                        if (story.practiceInsights.Count < 3) story.practiceInsights.Add(b.insight);
                        break;
                    case BehavioralPatternAnalyzer.InsightType.BehaviorPattern:
                        if (story.behaviorInsights.Count < 3) story.behaviorInsights.Add(b.insight);
                        break;
                }
            }
        }
    }

    private static void BuildConfusionPairs(ChildAnalyticsProfile analytics, StoryData story)
    {
        // Find repeated mistakes on same targets across games
        var targetErrors = new Dictionary<string, int>();
        foreach (var g in analytics.games)
        {
            foreach (var s in g.recentSessions)
            {
                s.EnsureInitialized();
                foreach (var a in s.actions)
                {
                    if (!a.correct && !string.IsNullOrEmpty(a.targetId))
                    {
                        if (!targetErrors.ContainsKey(a.targetId)) targetErrors[a.targetId] = 0;
                        targetErrors[a.targetId]++;
                    }
                }
            }
        }

        // Find top confusion targets (3+ errors)
        var sorted = new List<KeyValuePair<string, int>>(targetErrors);
        sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

        for (int i = 0; i < Mathf.Min(3, sorted.Count); i++)
        {
            if (sorted[i].Value >= 3)
                story.confusionPairs.Add(sorted[i].Key);
        }
    }

    private static void BuildLetterInsights(ChildAnalyticsProfile analytics, StoryData story)
    {
        // Look for letter game data
        foreach (var g in analytics.games)
        {
            if (g.gameId != "letters" || g.recentSessions.Count < 3) continue;

            var correct = new Dictionary<string, int>();
            var wrong = new Dictionary<string, int>();

            foreach (var s in g.recentSessions)
            {
                s.EnsureInitialized();
                foreach (var a in s.actions)
                {
                    if (string.IsNullOrEmpty(a.targetId)) continue;
                    if (a.correct)
                    {
                        if (!correct.ContainsKey(a.targetId)) correct[a.targetId] = 0;
                        correct[a.targetId]++;
                    }
                    else
                    {
                        if (!wrong.ContainsKey(a.targetId)) wrong[a.targetId] = 0;
                        wrong[a.targetId]++;
                    }
                }
            }

            // Strong letters: correct 3+ times, 0 wrong
            var strongList = new List<string>();
            foreach (var kv in correct)
                if (kv.Value >= 3 && (!wrong.ContainsKey(kv.Key) || wrong[kv.Key] == 0))
                    strongList.Add(kv.Key);
            if (strongList.Count > 5) strongList.RemoveRange(5, strongList.Count - 5);
            if (strongList.Count > 0) story.strongLetters = string.Join(", ", strongList);

            // Weak letters: wrong 2+ times
            var weakList = new List<string>();
            foreach (var kv in wrong)
                if (kv.Value >= 2) weakList.Add(kv.Key);
            if (weakList.Count > 5) weakList.RemoveRange(5, weakList.Count - 5);
            if (weakList.Count > 0) story.weakLetters = string.Join(", ", weakList);

            break;
        }
    }

    private static void BuildDifficultyRecommendations(ParentDashboardData data, ChildAnalyticsProfile analytics, StoryData story)
    {
        foreach (var game in data.games)
        {
            if (game.sessionsPlayed < 3) continue;

            string gameName = game.gameName;
            float score = game.score;
            float trend = game.trend;

            if (score > 80f && trend >= 0)
                story.levelUpGames.Add(gameName);
            else if (score < 40f || (score < 55f && trend < -2f))
                story.easierLevelGames.Add(gameName);
            else
                story.keepLevelGames.Add(gameName);
        }

        if (story.levelUpGames.Count > 3) story.levelUpGames.RemoveRange(3, story.levelUpGames.Count - 3);
        if (story.keepLevelGames.Count > 3) story.keepLevelGames.RemoveRange(3, story.keepLevelGames.Count - 3);
        if (story.easierLevelGames.Count > 3) story.easierLevelGames.RemoveRange(3, story.easierLevelGames.Count - 3);
    }

    private static void BuildImprovementMetrics(ParentDashboardData data, ChildAnalyticsProfile analytics, StoryData story)
    {
        // Find best accuracy improvement across games
        float bestAccImprovement = 0f;
        foreach (var g in analytics.games)
        {
            if (g.recentSessions.Count < 4) continue;
            int halfN = g.recentSessions.Count / 2;
            float earlyAcc = 0f, lateAcc = 0f;
            for (int i = 0; i < halfN; i++)
            {
                var s = g.recentSessions[i];
                earlyAcc += s.totalActions > 0 ? (float)s.correctActions / s.totalActions : 0f;
            }
            for (int i = halfN; i < g.recentSessions.Count; i++)
            {
                var s = g.recentSessions[i];
                lateAcc += s.totalActions > 0 ? (float)s.correctActions / s.totalActions : 0f;
            }
            earlyAcc /= halfN;
            lateAcc /= (g.recentSessions.Count - halfN);
            float diff = (lateAcc - earlyAcc) * 100f;
            if (diff > bestAccImprovement) bestAccImprovement = diff;
        }

        if (bestAccImprovement > 10f)
            story.improvementMetric1 = $"\u05D4\u05D3\u05D9\u05D5\u05E7 \u05D4\u05E9\u05EA\u05E4\u05E8 \u05D1{bestAccImprovement:F0}%";
            // הדיוק השתפר ב...%

        // Hint trend
        float earlyHints = 0f, lateHints = 0f;
        int earlyCount = 0, lateCount = 0;
        foreach (var g in analytics.games)
        {
            if (g.recentSessions.Count < 4) continue;
            int halfN = g.recentSessions.Count / 2;
            for (int i = 0; i < halfN; i++) { earlyHints += g.recentSessions[i].hintsUsed; earlyCount++; }
            for (int i = halfN; i < g.recentSessions.Count; i++) { lateHints += g.recentSessions[i].hintsUsed; lateCount++; }
        }
        if (earlyCount > 0 && lateCount > 0)
        {
            float earlyAvg = earlyHints / earlyCount;
            float lateAvg = lateHints / lateCount;
            if (earlyAvg > 1f && lateAvg < earlyAvg * 0.6f)
                story.improvementMetric2 = "\u05DE\u05E9\u05EA\u05DE\u05E9 \u05D1\u05E4\u05D7\u05D5\u05EA \u05E8\u05DE\u05D6\u05D9\u05DD \u05DE\u05D1\u05E2\u05D1\u05E8";
                // משתמש בפחות רמזים מבעבר
        }
    }

    private static string BuildLearningSpeed(ChildAnalyticsProfile analytics)
    {
        int fastLearners = 0, slowLearners = 0;
        foreach (var g in analytics.games)
        {
            if (g.recentSessions.Count < 3) continue;
            // Check if accuracy jumped fast (first 2 sessions vs rest)
            float first2 = 0f; int fc = 0;
            float rest = 0f; int rc = 0;
            for (int i = 0; i < g.recentSessions.Count; i++)
            {
                var s = g.recentSessions[i];
                float acc = s.totalActions > 0 ? (float)s.correctActions / s.totalActions : 0f;
                if (i < 2) { first2 += acc; fc++; }
                else { rest += acc; rc++; }
            }
            if (fc > 0 && rc > 0)
            {
                if (rest / rc > first2 / fc + 0.15f) fastLearners++;
                else if (rest / rc < first2 / fc - 0.1f) slowLearners++;
            }
        }

        if (fastLearners >= 2)
            return "\u05DC\u05D5\u05DE\u05D3 \u05DE\u05D4\u05E8 \u05D0\u05D7\u05E8\u05D9 \u05DB\u05DE\u05D4 \u05E0\u05D9\u05E1\u05D9\u05D5\u05E0\u05D5\u05EA";
            // לומד מהר אחרי כמה ניסיונות
        if (slowLearners >= 2)
            return "\u05DC\u05D5\u05DE\u05D3 \u05D1\u05D4\u05D3\u05E8\u05D2\u05D4 \u05D3\u05E8\u05DA \u05D7\u05D6\u05E8\u05D4 \u05D5\u05EA\u05E8\u05D2\u05D5\u05DC";
            // לומד בהדרגה דרך חזרה ותרגול

        return "";
    }

    private static void BuildMasteryLines(ParentDashboardData data, StoryData story)
    {
        foreach (var g in data.games)
        {
            if (g.sessionsPlayed < 5 || g.score < 75f) continue;
            story.masteryLines.Add($"\u05E9\u05DC\u05D9\u05D8\u05D4 \u05D8\u05D5\u05D1\u05D4 \u05D1{g.gameName}");
            // שליטה טובה ב...
            if (story.masteryLines.Count >= 2) break;
        }
    }

    private static string BuildColoringPrecision(ChildAnalyticsProfile analytics)
    {
        foreach (var g in analytics.games)
        {
            if (g.gameId != "coloring" || g.recentSessions.Count < 2) continue;
            float avgAcc = 0f; int count = 0;
            foreach (var s in g.recentSessions)
            {
                if (s.totalActions > 0) { avgAcc += (float)s.correctActions / s.totalActions; count++; }
            }
            if (count > 0) avgAcc /= count;

            if (avgAcc > 0.8f)
                return "\u05E6\u05D5\u05D1\u05E2 \u05D1\u05E8\u05D5\u05D1 \u05D1\u05EA\u05D5\u05DA \u05D4\u05D2\u05D1\u05D5\u05DC\u05D5\u05EA";
                // צובע ברוב בתוך הגבולות
            else
                return "\u05E2\u05D3\u05D9\u05D9\u05DF \u05DE\u05E9\u05EA\u05E4\u05E8 \u05D1\u05D3\u05D9\u05D5\u05E7 \u05D4\u05E6\u05D1\u05D9\u05E2\u05D4";
                // עדיין משתפר בדיוק הצביעה
        }
        return "";
    }

    private static string BuildProgressHighlight(ParentDashboardData data)
    {
        float bestTrend = 0f; string bestGame = null;
        foreach (var g in data.games)
        {
            if (g.trend > bestTrend && g.sessionsPlayed >= 3)
            { bestTrend = g.trend; bestGame = g.gameName; }
        }

        if (bestTrend > 2f && bestGame != null)
            return $"\u05E0\u05D9\u05DB\u05E8\u05EA \u05D4\u05EA\u05E7\u05D3\u05DE\u05D5\u05EA \u05D1{bestGame} \u05D1\u05D4\u05E9\u05D5\u05D5\u05D0\u05D4 \u05DC\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E7\u05D5\u05D3\u05DE\u05D9\u05DD.";

        if (data.overallTrend > 0)
            return "\u05D4\u05D1\u05D9\u05E6\u05D5\u05E2\u05D9\u05DD \u05D4\u05DB\u05DC\u05DC\u05D9\u05D9\u05DD \u05D1\u05DE\u05D2\u05DE\u05EA \u05E9\u05D9\u05E4\u05D5\u05E8.";

        return "";
    }

    private static string BuildNextStep(ParentDashboardData data, List<string> practice, List<string> strengths)
    {
        if (practice.Count > 0)
            return $"\u05D4\u05DE\u05E9\u05D9\u05DB\u05D5 \u05E2\u05DD \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D1\u05EA\u05D7\u05D5\u05DD {practice[0]}.";
        if (strengths.Count > 0 && data.overallScore > 65f)
            return "\u05E0\u05E1\u05D5 \u05DC\u05D4\u05E2\u05DC\u05D5\u05EA \u05D0\u05EA \u05E8\u05DE\u05EA \u05D4\u05E7\u05D5\u05E9\u05D9.";
        return "\u05D4\u05DE\u05E9\u05D9\u05DB\u05D5 \u05DC\u05E9\u05D7\u05E7 \u05DE\u05D2\u05D5\u05D5\u05DF \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD!";
    }

    private static void BuildCategoryBars(ParentDashboardData data, StoryData story)
    {
        var list = new List<(string name, int score)>();
        foreach (var cat in data.categories)
        {
            if (cat.contributingGamesCount == 0) continue;
            list.Add((cat.categoryName, Mathf.RoundToInt(cat.score)));
        }
        list.Sort((a, b) => b.score.CompareTo(a.score));
        if (list.Count > 6) list.RemoveRange(6, list.Count - 6);
        story.categoryBars = list;
    }

    private static void BuildImprovements(ParentDashboardData data, ChildAnalyticsProfile analytics, StoryData story)
    {
        if (!string.IsNullOrEmpty(story.improvementMetric1))
            story.improvements.Add(story.improvementMetric1);
        if (!string.IsNullOrEmpty(story.improvementMetric2))
            story.improvements.Add(story.improvementMetric2);

        // Check for speed improvement
        foreach (var g in data.games)
        {
            if (g.sessionsPlayed < 4 || g.trend <= 2f) continue;
            story.improvements.Add($"\u05DE\u05E9\u05EA\u05E4\u05E8 \u05D1{g.gameName}");
            // משתפר ב...
            break;
        }

        if (story.improvements.Count > 3) story.improvements.RemoveRange(3, story.improvements.Count - 3);
    }

    private static void BuildRecommendedGames(ParentDashboardData data, StoryData story)
    {
        foreach (var g in story.easierLevelGames)
            story.recommendedGames.Add($"{g} (\u05E8\u05DE\u05D4 \u05E7\u05DC\u05D4)");
            // (רמה קלה)

        // Already sorted by score in levelUpGames
        foreach (var g in story.levelUpGames)
            story.recommendedGames.Add($"{g} \u2014 \u05DE\u05D5\u05DB\u05DF \u05DC\u05D4\u05E2\u05DC\u05D5\u05EA \u05E8\u05DE\u05D4");
            // — מוכן להעלות רמה

        if (story.recommendedGames.Count > 4) story.recommendedGames.RemoveRange(4, story.recommendedGames.Count - 4);
    }

    private static void BuildProgressSnapshot(ParentDashboardData data, ChildAnalyticsProfile analytics, StoryData story)
    {
        // Accuracy trend
        if (data.overallTrend > 2f)
            story.accuracyTrend = "\u2191 \u05D1\u05DE\u05D2\u05DE\u05EA \u05E9\u05D9\u05E4\u05D5\u05E8"; // ↑ במגמת שיפור
        else if (data.overallTrend < -2f)
            story.accuracyTrend = "\u2193 \u05D3\u05D5\u05E8\u05E9 \u05E2\u05D5\u05D3 \u05EA\u05E8\u05D2\u05D5\u05DC"; // ↓ דורש עוד תרגול
        else
            story.accuracyTrend = "\u2192 \u05D9\u05E6\u05D9\u05D1"; // → יציב

        // Last 3 session scores
        var recentScores = new List<int>();
        foreach (var g in analytics.games)
        {
            for (int i = Mathf.Max(0, g.recentSessions.Count - 3); i < g.recentSessions.Count; i++)
                recentScores.Add(Mathf.RoundToInt(g.recentSessions[i].sessionScore));
        }
        // Take last 3 overall
        if (recentScores.Count > 3) recentScores.RemoveRange(0, recentScores.Count - 3);
        if (recentScores.Count >= 2)
            story.lastScores = string.Join(" \u2192 ", recentScores); // → separator
    }
}
