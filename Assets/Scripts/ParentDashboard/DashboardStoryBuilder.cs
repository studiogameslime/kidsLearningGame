using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates the narrative-driven parent dashboard content.
/// Transforms raw analytics into a personal story about the child:
/// weekly summary, focus area, key insights, behavioral profile,
/// strengths vs practice areas, progress highlight, and next step.
/// </summary>
public static class DashboardStoryBuilder
{
    public struct StoryData
    {
        public string weeklySummary;       // 1-2 sentence hero text
        public string focusNow;            // One actionable takeaway
        public List<InsightCard> insights; // 4-5 structured insights
        public List<string> howChildPlays; // 3-4 behavioral lines
        public List<string> strengths;     // Top 2-3 strength names
        public List<string> practiceAreas; // Top 2-3 practice area names
        public string progressHighlight;   // One change over time
        public string suggestedNextStep;   // One recommendation
    }

    public struct InsightCard
    {
        public string title;
        public string body;
        public string action;
    }

    public static StoryData Build(ParentDashboardData data, ChildAnalyticsProfile analytics)
    {
        var story = new StoryData
        {
            insights = new List<InsightCard>(),
            howChildPlays = new List<string>(),
            strengths = new List<string>(),
            practiceAreas = new List<string>()
        };

        if (data == null || analytics == null || analytics.totalSessions < 2)
        {
            story.weeklySummary = "\u05E2\u05D3\u05D9\u05D9\u05DF \u05DC\u05D5\u05DE\u05D3\u05D9\u05DD \u05D0\u05EA \u05D3\u05E4\u05D5\u05E1\u05D9 \u05D4\u05DE\u05E9\u05D7\u05E7 \u05E9\u05DC \u05D4\u05D9\u05DC\u05D3. \u05DB\u05E9\u05D9\u05E9\u05D7\u05E7 \u05E2\u05D5\u05D3 \u05E7\u05E6\u05EA \u05E0\u05D5\u05DB\u05DC \u05DC\u05E1\u05E4\u05E8 \u05D9\u05D5\u05EA\u05E8.";
            // עדיין לומדים את דפוסי המשחק של הילד. כשישחק עוד קצת נוכל לספר יותר.
            story.focusNow = "\u05D4\u05DE\u05E9\u05D9\u05DB\u05D5 \u05DC\u05E9\u05D7\u05E7 \u05D5\u05DC\u05D7\u05E7\u05D5\u05E8!";
            // המשיכו לשחק ולחקור!
            return story;
        }

        string childName = data.profileName ?? "\u05D4\u05D9\u05DC\u05D3";

        // ── Build Strengths & Practice Areas ──
        foreach (var cat in data.categories)
        {
            if (cat.contributingGamesCount == 0) continue;
            if (cat.score > 55f && cat.confidence >= 0.3f)
                story.strengths.Add(cat.categoryName);
            else if (cat.score < 50f && cat.confidence >= 0.2f)
                story.practiceAreas.Add(cat.categoryName);
        }
        // Limit
        if (story.strengths.Count > 3) story.strengths.RemoveRange(3, story.strengths.Count - 3);
        if (story.practiceAreas.Count > 3) story.practiceAreas.RemoveRange(3, story.practiceAreas.Count - 3);

        // ── Weekly Summary (Hero) ──
        story.weeklySummary = BuildWeeklySummary(childName, data, analytics, story.strengths, story.practiceAreas);

        // ── Focus Now ──
        story.focusNow = BuildFocusNow(data, analytics, story.practiceAreas);

        // ── Key Insights ──
        story.insights = BuildInsightCards(data, analytics);

        // ── How the Child Plays ──
        story.howChildPlays = BuildBehaviorProfile(analytics);

        // ── Progress Highlight ──
        story.progressHighlight = BuildProgressHighlight(data);

        // ── Suggested Next Step ──
        story.suggestedNextStep = BuildNextStep(data, story.practiceAreas, story.strengths);

        return story;
    }

    // ═══════════════════════════════════════════════════════════════

    private static string BuildWeeklySummary(string name, ParentDashboardData data,
        ChildAnalyticsProfile analytics, List<string> strengths, List<string> practice)
    {
        var parts = new List<string>();

        // Strength mention
        if (strengths.Count >= 2)
            parts.Add($"{name} \u05DE\u05E8\u05D0\u05D4 \u05D1\u05D9\u05D8\u05D7\u05D5\u05DF \u05D2\u05D5\u05D1\u05E8 \u05D1{strengths[0]} \u05D5\u05D1{strengths[1]}");
            // מראה ביטחון גובר ב... וב...
        else if (strengths.Count == 1)
            parts.Add($"{name} \u05DE\u05E8\u05D0\u05D4 \u05D1\u05D9\u05D8\u05D7\u05D5\u05DF \u05D2\u05D5\u05D1\u05E8 \u05D1{strengths[0]}");

        // Independence
        float avgHints = 0f;
        int sessCount = 0;
        foreach (var g in analytics.games)
            foreach (var s in g.recentSessions) { avgHints += s.hintsUsed; sessCount++; }
        if (sessCount > 0) avgHints /= sessCount;

        if (avgHints < 0.5f && sessCount >= 3)
            parts.Add("\u05D5\u05D1\u05D3\u05E8\u05DA \u05DB\u05DC\u05DC \u05DE\u05E9\u05D7\u05E7 \u05D1\u05D0\u05D5\u05E4\u05DF \u05E2\u05E6\u05DE\u05D0\u05D9");
            // ובדרך כלל משחק באופן עצמאי

        // Challenge mention
        if (practice.Count > 0)
            parts.Add($"\u05D7\u05DC\u05E7 \u05DE\u05D4\u05E4\u05E2\u05D9\u05DC\u05D5\u05D9\u05D5\u05EA \u05D4\u05D0\u05E8\u05D5\u05DB\u05D5\u05EA \u05D9\u05D5\u05EA\u05E8 \u05E2\u05D3\u05D9\u05D9\u05DF \u05DE\u05D0\u05EA\u05D2\u05E8\u05D5\u05EA");
            // חלק מהפעילויות הארוכות יותר עדיין מאתגרות

        if (parts.Count == 0)
            return $"{name} \u05DE\u05DE\u05E9\u05D9\u05DA \u05DC\u05E9\u05D7\u05E7 \u05D5\u05DC\u05D4\u05EA\u05E4\u05EA\u05D7!";
            // ממשיך לשחק ולהתפתח!

        return string.Join(". ", parts) + ".";
    }

    private static string BuildFocusNow(ParentDashboardData data, ChildAnalyticsProfile analytics,
        List<string> practice)
    {
        // Prefer practice area
        if (practice.Count > 0)
        {
            // Check for specific behavioral pattern
            foreach (var g in analytics.games)
            {
                if (g.recentSessions == null || g.recentSessions.Count < 3) continue;
                var bi = BehavioralPatternAnalyzer.AnalyzeGameHistory(g);
                foreach (var b in bi)
                {
                    if (b.patternKey == "focus_drop")
                        return "\u05D4\u05DE\u05DC\u05E6\u05D4 \u05DC\u05D4\u05E9\u05D1\u05D5\u05E2: \u05E0\u05E1\u05D5 \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E7\u05E6\u05E8\u05D9\u05DD \u05DB\u05D3\u05D9 \u05DC\u05E9\u05DE\u05D5\u05E8 \u05E2\u05DC \u05E8\u05D9\u05DB\u05D5\u05D6.";
                        // המלצה להשבוע: נסו משחקים קצרים כדי לשמור על ריכוז.
                    if (b.patternKey == "impulsive")
                        return "\u05D4\u05DE\u05DC\u05E6\u05D4 \u05DC\u05D4\u05E9\u05D1\u05D5\u05E2: \u05E2\u05D5\u05D3\u05D3\u05D5 \u05D0\u05EA \u05D4\u05D9\u05DC\u05D3 \u05DC\u05E7\u05D7\u05EA \u05D0\u05EA \u05D4\u05D6\u05DE\u05DF \u05DC\u05E4\u05E0\u05D9 \u05DC\u05D7\u05D9\u05E6\u05D4.";
                        // המלצה להשבוע: עודדו את הילד לקחת את הזמן לפני לחיצה.
                }
            }

            return $"\u05D4\u05DE\u05DC\u05E6\u05D4 \u05DC\u05D4\u05E9\u05D1\u05D5\u05E2: \u05D4\u05DE\u05E9\u05D9\u05DB\u05D5 \u05DC\u05EA\u05E8\u05D2\u05DC \u05E4\u05E2\u05D9\u05DC\u05D5\u05D9\u05D5\u05EA \u05D1\u05EA\u05D7\u05D5\u05DD {practice[0]}.";
            // המלצה להשבוע: המשיכו לתרגל פעילויות בתחום ...
        }

        // No practice areas — suggest progression
        if (data.overallScore > 70f)
            return "\u05D4\u05DE\u05DC\u05E6\u05D4 \u05DC\u05D4\u05E9\u05D1\u05D5\u05E2: \u05E0\u05E1\u05D5 \u05DC\u05D4\u05E2\u05DC\u05D5\u05EA \u05D0\u05EA \u05E8\u05DE\u05EA \u05D4\u05E7\u05D5\u05E9\u05D9 \u05D1\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E9\u05D4\u05D9\u05DC\u05D3 \u05E9\u05D5\u05DC\u05D8 \u05D1\u05D4\u05DD.";
            // המלצה להשבוע: נסו להעלות את רמת הקושי במשחקים שהילד שולט בהם.

        return "\u05D4\u05DE\u05DC\u05E6\u05D4 \u05DC\u05D4\u05E9\u05D1\u05D5\u05E2: \u05D4\u05DE\u05E9\u05D9\u05DB\u05D5 \u05DC\u05E9\u05D7\u05E7 \u05DE\u05D2\u05D5\u05D5\u05DF \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD!";
        // המלצה להשבוע: המשיכו לשחק מגוון משחקים!
    }

    private static List<InsightCard> BuildInsightCards(ParentDashboardData data, ChildAnalyticsProfile analytics)
    {
        var cards = new List<InsightCard>();

        // Get behavioral insights across all games
        var allBehavioral = new List<BehavioralPatternAnalyzer.BehavioralInsight>();
        var seenKeys = new HashSet<string>();

        foreach (var g in analytics.games)
        {
            if (g.recentSessions == null || g.recentSessions.Count < 2) continue;
            var bi = BehavioralPatternAnalyzer.AnalyzeGameHistory(g);
            foreach (var b in bi)
            {
                if (!seenKeys.Contains(b.patternKey) && b.confidence != BehavioralPatternAnalyzer.ConfidenceLevel.Low)
                {
                    seenKeys.Add(b.patternKey);
                    allBehavioral.Add(b);
                }
            }
        }

        allBehavioral.Sort((a, b) => b.priority.CompareTo(a.priority));

        // Convert top behavioral insights to cards
        foreach (var b in allBehavioral)
        {
            if (cards.Count >= 4) break;

            string title, body, action;
            switch (b.patternKey)
            {
                case "learning_curve":
                    title = "\u05DC\u05D5\u05DE\u05D3 \u05EA\u05D5\u05DA \u05DB\u05D3\u05D9 \u05DE\u05E9\u05D7\u05E7";
                    body = "\u05D4\u05D9\u05DC\u05D3 \u05E6\u05E8\u05D9\u05DA \u05D6\u05DE\u05DF \u05DC\u05D4\u05D1\u05D9\u05DF \u05D0\u05EA \u05D4\u05DE\u05E9\u05D9\u05DE\u05D4 \u05D0\u05D1\u05DC \u05DE\u05E9\u05EA\u05E4\u05E8 \u05D1\u05DE\u05D4\u05DC\u05DA \u05D4\u05DE\u05E9\u05D7\u05E7.";
                    action = "\u05EA\u05E0\u05D5 \u05DC\u05D5 \u05D6\u05DE\u05DF \u05D1\u05D4\u05EA\u05D7\u05DC\u05D4 \u05D1\u05DC\u05D9 \u05DC\u05D7\u05E5.";
                    break;
                case "focus_drop":
                    title = "\u05D4\u05E8\u05D9\u05DB\u05D5\u05D6 \u05D9\u05D5\u05E8\u05D3 \u05D1\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D0\u05E8\u05D5\u05DB\u05D9\u05DD";
                    body = "\u05D4\u05D9\u05DC\u05D3 \u05DE\u05EA\u05D7\u05D9\u05DC \u05D7\u05D6\u05E7 \u05D0\u05D1\u05DC \u05D4\u05D1\u05D9\u05E6\u05D5\u05E2\u05D9\u05DD \u05D9\u05D5\u05E8\u05D3\u05D9\u05DD \u05D1\u05D4\u05DE\u05E9\u05DA.";
                    action = "\u05E0\u05E1\u05D5 \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E7\u05E6\u05E8\u05D9\u05DD \u05E9\u05DC 2-3 \u05D3\u05E7\u05D5\u05EA.";
                    break;
                case "mastery":
                    title = "\u05E9\u05DC\u05D9\u05D8\u05D4 \u05D8\u05D5\u05D1\u05D4";
                    body = "\u05D4\u05D9\u05DC\u05D3 \u05DE\u05E9\u05D7\u05E7 \u05D1\u05D1\u05D9\u05D8\u05D7\u05D5\u05DF \u05D5\u05D1\u05D3\u05D9\u05D5\u05E7 \u05D2\u05D1\u05D5\u05D4 \u05D1\u05E4\u05E2\u05D9\u05DC\u05D5\u05EA \u05D4\u05D6\u05D5.";
                    action = "\u05D0\u05E4\u05E9\u05E8 \u05DC\u05D4\u05E2\u05DC\u05D5\u05EA \u05D0\u05EA \u05E8\u05DE\u05EA \u05D4\u05E7\u05D5\u05E9\u05D9.";
                    break;
                case "persistence":
                    title = "\u05D4\u05EA\u05DE\u05D3\u05D4 \u05D9\u05E4\u05D4";
                    body = "\u05D4\u05D9\u05DC\u05D3 \u05DC\u05D0 \u05DE\u05D5\u05D5\u05EA\u05E8 \u05D0\u05D7\u05E8\u05D9 \u05D8\u05E2\u05D5\u05D9\u05D5\u05EA \u05D5\u05DE\u05DE\u05E9\u05D9\u05DA \u05DC\u05E0\u05E1\u05D5\u05EA.";
                    action = "\u05E2\u05D5\u05D3\u05D3\u05D5 \u05D0\u05D5\u05EA\u05D5 \u05E2\u05DC \u05D4\u05DE\u05D0\u05DE\u05E5!";
                    break;
                case "impulsive":
                    title = "\u05DC\u05D5\u05D7\u05E5 \u05DE\u05D4\u05E8 \u05D1\u05DC\u05D9 \u05DC\u05D7\u05E9\u05D5\u05D1";
                    body = "\u05D4\u05D9\u05DC\u05D3 \u05E0\u05D5\u05D8\u05D4 \u05DC\u05D4\u05D2\u05D9\u05D1 \u05DE\u05D4\u05E8 \u05DE\u05D4 \u05E9\u05DE\u05D5\u05D1\u05D9\u05DC \u05DC\u05D9\u05D5\u05EA\u05E8 \u05D8\u05E2\u05D5\u05D9\u05D5\u05EA.";
                    action = "\u05E2\u05D5\u05D3\u05D3\u05D5 \u05D0\u05D5\u05EA\u05D5 \u05DC\u05E7\u05D7\u05EA \u05D0\u05EA \u05D4\u05D6\u05DE\u05DF.";
                    break;
                case "repeated_confusion":
                case "recurring_confusion":
                    title = "\u05E0\u05E7\u05D5\u05D3\u05D4 \u05DC\u05EA\u05E8\u05D2\u05D5\u05DC";
                    body = "\u05D4\u05D9\u05DC\u05D3 \u05D7\u05D5\u05D6\u05E8 \u05E2\u05DC \u05D0\u05D5\u05EA\u05D4 \u05D8\u05E2\u05D5\u05EA \u05DE\u05E1\u05E4\u05E8 \u05E4\u05E2\u05DE\u05D9\u05DD.";
                    action = "\u05E0\u05E1\u05D5 \u05DC\u05EA\u05E8\u05D2\u05DC \u05D0\u05EA \u05D4\u05E0\u05D5\u05E9\u05D0 \u05D4\u05D6\u05D4 \u05D1\u05D9\u05D7\u05D3.";
                    break;
                case "hint_dependence":
                    title = "\u05E0\u05E2\u05D6\u05E8 \u05D1\u05E8\u05DE\u05D6\u05D9\u05DD";
                    body = "\u05D4\u05D9\u05DC\u05D3 \u05DE\u05E9\u05EA\u05DE\u05E9 \u05D1\u05E8\u05DE\u05D6\u05D9\u05DD \u05DC\u05E2\u05D9\u05EA\u05D9\u05DD \u05E7\u05E8\u05D5\u05D1\u05D5\u05EA.";
                    action = "\u05D6\u05D4 \u05D8\u05D1\u05E2\u05D9 \u05D1\u05EA\u05D4\u05DC\u05D9\u05DA \u05D4\u05DC\u05DE\u05D9\u05D3\u05D4.";
                    break;
                case "too_easy":
                    title = "\u05DE\u05D5\u05DB\u05DF \u05DC\u05D0\u05EA\u05D2\u05E8";
                    body = "\u05D4\u05D9\u05DC\u05D3 \u05DE\u05E1\u05D9\u05D9\u05DD \u05DE\u05D4\u05E8 \u05D5\u05D1\u05D3\u05D9\u05D5\u05E7 \u05D2\u05D1\u05D5\u05D4.";
                    action = "\u05D0\u05E4\u05E9\u05E8 \u05DC\u05D4\u05E2\u05DC\u05D5\u05EA \u05D0\u05EA \u05E8\u05DE\u05EA \u05D4\u05E7\u05D5\u05E9\u05D9.";
                    break;
                case "hesitation":
                    title = "\u05E6\u05E8\u05D9\u05DA \u05E2\u05D5\u05D3 \u05EA\u05E8\u05D2\u05D5\u05DC";
                    body = "\u05D4\u05E4\u05E2\u05D9\u05DC\u05D5\u05EA \u05E2\u05D3\u05D9\u05D9\u05DF \u05DE\u05D0\u05EA\u05D2\u05E8\u05EA \u05D5\u05D3\u05D5\u05E8\u05E9\u05EA \u05D1\u05E0\u05D9\u05D9\u05EA \u05D1\u05D9\u05D8\u05D7\u05D5\u05DF.";
                    action = "\u05E9\u05D7\u05E7\u05D5 \u05D9\u05D7\u05D3 \u05DB\u05D3\u05D9 \u05DC\u05EA\u05EA \u05D4\u05E8\u05D2\u05E9\u05EA \u05D4\u05E6\u05DC\u05D7\u05D4.";
                    break;
                default:
                    title = b.insight.Length > 30 ? b.insight.Substring(0, 30) + "..." : b.insight;
                    body = b.insight;
                    action = "";
                    break;
            }

            cards.Add(new InsightCard { title = title, body = body, action = action });
        }

        // Fill with rule-based if not enough
        if (cards.Count < 3 && data.insights != null)
        {
            foreach (var ins in data.insights)
            {
                if (cards.Count >= 4) break;
                cards.Add(new InsightCard
                {
                    title = ins.titleHebrew,
                    body = ins.descriptionHebrew,
                    action = ""
                });
            }
        }

        return cards;
    }

    private static List<string> BuildBehaviorProfile(ChildAnalyticsProfile analytics)
    {
        var lines = new List<string>();

        // Learning style
        string exploration = InsightsEngine.ExplorationLabel(analytics);
        if (!string.IsNullOrEmpty(exploration))
            lines.Add(exploration);

        // Pace
        if (analytics.totalSessions >= 3)
        {
            float avgDur = analytics.totalPlayTime / Mathf.Max(1, analytics.totalSessions);
            if (avgDur < 60f)
                lines.Add("\u05DE\u05E9\u05D7\u05E7 \u05D1\u05E7\u05E6\u05D1 \u05DE\u05D4\u05D9\u05E8"); // משחק בקצב מהיר
            else if (avgDur > 180f)
                lines.Add("\u05DE\u05E9\u05D7\u05E7 \u05D1\u05E7\u05E6\u05D1 \u05D9\u05E6\u05D9\u05D1"); // משחק בקצב יציב
            else
                lines.Add("\u05DE\u05E9\u05D7\u05E7 \u05D1\u05E7\u05E6\u05D1 \u05DE\u05DE\u05D5\u05E6\u05E2"); // משחק בקצב ממוצע
        }

        // Independence
        float totalHints = 0f;
        int totalSess = 0;
        foreach (var g in analytics.games)
            foreach (var s in g.recentSessions) { totalHints += s.hintsUsed; totalSess++; }
        if (totalSess >= 3)
        {
            float avg = totalHints / totalSess;
            if (avg < 0.5f)
                lines.Add("\u05D1\u05D3\u05E8\u05DA \u05DB\u05DC\u05DC \u05DE\u05E9\u05D7\u05E7 \u05D1\u05D0\u05D5\u05E4\u05DF \u05E2\u05E6\u05DE\u05D0\u05D9"); // בדרך כלל משחק באופן עצמאי
            else if (avg < 2f)
                lines.Add("\u05DC\u05E4\u05E2\u05DE\u05D9\u05DD \u05E0\u05E2\u05D6\u05E8 \u05D1\u05E8\u05DE\u05D6\u05D9\u05DD"); // לפעמים נעזר ברמזים
            else
                lines.Add("\u05E0\u05E2\u05D6\u05E8 \u05D1\u05E8\u05DE\u05D6\u05D9\u05DD \u05DC\u05E2\u05D9\u05EA\u05D9\u05DD \u05E7\u05E8\u05D5\u05D1\u05D5\u05EA"); // נעזר ברמזים לעיתים קרובות
        }

        // Persistence
        string persistence = "";
        int persGames = 0;
        foreach (var g in analytics.games)
        {
            if (g.recentSessions.Count < 2) continue;
            int abandoned = 0;
            foreach (var s in g.recentSessions) if (s.abandoned) abandoned++;
            if ((float)abandoned / g.recentSessions.Count < 0.15f)
                persGames++;
        }
        if (persGames >= 2)
            lines.Add("\u05DE\u05DE\u05E9\u05D9\u05DA \u05DC\u05E0\u05E1\u05D5\u05EA \u05D0\u05D7\u05E8\u05D9 \u05D8\u05E2\u05D5\u05D9\u05D5\u05EA"); // ממשיך לנסות אחרי טעויות

        if (lines.Count > 4) lines.RemoveRange(4, lines.Count - 4);
        return lines;
    }

    private static string BuildProgressHighlight(ParentDashboardData data)
    {
        // Find best improving game
        float bestTrend = 0f;
        string bestGame = null;
        foreach (var g in data.games)
        {
            if (g.trend > bestTrend && g.sessionsPlayed >= 3)
            {
                bestTrend = g.trend;
                bestGame = g.gameName;
            }
        }

        if (bestTrend > 2f && bestGame != null)
            return $"\u05E0\u05D9\u05DB\u05E8\u05EA \u05D4\u05EA\u05E7\u05D3\u05DE\u05D5\u05EA \u05D1{bestGame} \u05D1\u05D4\u05E9\u05D5\u05D5\u05D0\u05D4 \u05DC\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E7\u05D5\u05D3\u05DE\u05D9\u05DD.";
            // ניכרת התקדמות ב... בהשוואה למשחקים קודמים.

        if (data.overallTrend > 0)
            return "\u05D4\u05D1\u05D9\u05E6\u05D5\u05E2\u05D9\u05DD \u05D4\u05DB\u05DC\u05DC\u05D9\u05D9\u05DD \u05D1\u05DE\u05D2\u05DE\u05EA \u05E9\u05D9\u05E4\u05D5\u05E8.";
            // הביצועים הכלליים במגמת שיפור.

        return "";
    }

    private static string BuildNextStep(ParentDashboardData data, List<string> practice, List<string> strengths)
    {
        if (practice.Count > 0)
            return $"\u05D4\u05DE\u05E9\u05D9\u05DB\u05D5 \u05E2\u05DD \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D1\u05EA\u05D7\u05D5\u05DD {practice[0]}.";
            // המשיכו עם משחקים בתחום ...

        if (strengths.Count > 0 && data.overallScore > 65f)
            return "\u05E0\u05E1\u05D5 \u05DC\u05D4\u05E2\u05DC\u05D5\u05EA \u05D0\u05EA \u05E8\u05DE\u05EA \u05D4\u05E7\u05D5\u05E9\u05D9 \u05D1\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E9\u05D4\u05D9\u05DC\u05D3 \u05E9\u05D5\u05DC\u05D8 \u05D1\u05D4\u05DD.";
            // נסו להעלות את רמת הקושי במשחקים שהילד שולט בהם.

        return "\u05D4\u05DE\u05E9\u05D9\u05DB\u05D5 \u05DC\u05E9\u05D7\u05E7 \u05DE\u05D2\u05D5\u05D5\u05DF \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD!";
        // המשיכו לשחק מגוון משחקים!
    }
}
