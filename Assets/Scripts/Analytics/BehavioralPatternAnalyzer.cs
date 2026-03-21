using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Behavioral Pattern Analysis Layer.
///
/// Transforms raw GameSessionData into compressed behavioral summaries,
/// then detects patterns and generates parent-friendly insights.
///
/// Patterns are classified as:
///   Session Patterns  — detected from a single session
///   Trend Patterns    — require multiple sessions over time
///
/// Tone: supportive, observational, non-clinical, concise, never diagnostic.
/// Based only on gameplay behavior — no assumptions beyond the data.
/// </summary>
public static class BehavioralPatternAnalyzer
{
    // ═══════════════════════════════════════════════════════════════
    //  DATA STRUCTURES
    // ═══════════════════════════════════════════════════════════════

    public enum InsightType { Strength, Improvement, PracticeArea, BehaviorPattern }
    public enum ConfidenceLevel { Low, Medium, High }
    public enum PatternScope { Session, Trend }

    [Serializable]
    public struct BehavioralInsight
    {
        public string patternKey;
        public string insight;
        public InsightType type;
        public ConfidenceLevel confidence;
        public string evidence;
        public float priority;
        public PatternScope scope;
    }

    /// <summary>
    /// Compressed behavioral summary built from one or more sessions.
    /// </summary>
    public struct SessionSummary
    {
        // Performance
        public float score;
        public float accuracy;
        public int mistakes;
        public int hintsUsed;
        public int sessionCount;

        // Timing
        public float timeToFirstAction;
        public float timeToFirstCorrect;
        public float averageActionInterval;
        public float longestPause;
        public float totalDuration;
        public float activePlayDuration;

        // Behavior
        public int maxStreak;
        public int retries;
        public bool completed;
        public bool abandoned;

        // Within-session halves comparison
        public float firstHalfAccuracy;
        public float secondHalfAccuracy;
        public float firstHalfSpeed;   // avg interval in first half
        public float secondHalfSpeed;  // avg interval in second half

        // Derived patterns
        public bool slowStart;
        public bool improvesOverTime;
        public bool declinesOverTime;
        public bool highPauseFrequency;
        public bool fastAccurate;

        // Error patterns
        public string repeatedMistakeTarget;   // dominant repeated target, null if none
        public int repeatedMistakeCount;        // how many times
        public string repeatedMistakeTag;       // repeated incorrect action tag, null if none
        public bool confusionIsRecurring;       // true if same confusion across multiple sessions
    }

    // ═══════════════════════════════════════════════════════════════
    //  STEP 1: BUILD SUMMARY
    // ═══════════════════════════════════════════════════════════════

    public static SessionSummary BuildSummary(GameSessionData session)
    {
        session.EnsureInitialized();

        var s = new SessionSummary
        {
            score = session.sessionScore,
            accuracy = session.totalActions > 0 ? (float)session.correctActions / session.totalActions : 0f,
            mistakes = session.mistakes,
            hintsUsed = session.hintsUsed,
            sessionCount = 1,
            timeToFirstAction = session.timeToFirstAction,
            timeToFirstCorrect = session.timeToFirstCorrect,
            averageActionInterval = session.averageActionInterval,
            longestPause = session.longestPause,
            totalDuration = session.durationSeconds,
            activePlayDuration = session.activePlayDuration > 0 ? session.activePlayDuration : session.durationSeconds,
            maxStreak = session.maxStreak,
            retries = session.retries,
            completed = session.completed,
            abandoned = session.abandoned
        };

        // Derived flags
        s.slowStart = session.timeToFirstCorrect > 5f;
        s.highPauseFrequency = session.longestPause > 8f;
        s.fastAccurate = session.durationSeconds < 30f && s.accuracy > 0.9f && session.mistakes == 0;

        // Analyze action log halves
        if (session.actions != null && session.actions.Count >= 6)
        {
            int half = session.actions.Count / 2;
            int fc = 0, ft = 0, sc = 0, st = 0;
            float firstIntervalSum = 0f, secondIntervalSum = 0f;
            int firstIntervalCount = 0, secondIntervalCount = 0;

            for (int i = 0; i < session.actions.Count; i++)
            {
                if (i < half)
                {
                    ft++;
                    if (session.actions[i].correct) fc++;
                    if (i > 0)
                    {
                        firstIntervalSum += session.actions[i].timestamp - session.actions[i - 1].timestamp;
                        firstIntervalCount++;
                    }
                }
                else
                {
                    st++;
                    if (session.actions[i].correct) sc++;
                    if (i > half)
                    {
                        secondIntervalSum += session.actions[i].timestamp - session.actions[i - 1].timestamp;
                        secondIntervalCount++;
                    }
                }
            }

            s.firstHalfAccuracy = ft > 0 ? (float)fc / ft : 0f;
            s.secondHalfAccuracy = st > 0 ? (float)sc / st : 0f;
            s.firstHalfSpeed = firstIntervalCount > 0 ? firstIntervalSum / firstIntervalCount : 0f;
            s.secondHalfSpeed = secondIntervalCount > 0 ? secondIntervalSum / secondIntervalCount : 0f;
            s.improvesOverTime = s.secondHalfAccuracy > s.firstHalfAccuracy + 0.15f;
            s.declinesOverTime = s.firstHalfAccuracy > s.secondHalfAccuracy + 0.15f;

            // Error pattern extraction
            var targetMistakes = new Dictionary<string, int>();
            var tagMistakes = new Dictionary<string, int>();

            foreach (var a in session.actions)
            {
                if (!a.correct)
                {
                    if (!string.IsNullOrEmpty(a.targetId))
                    {
                        if (!targetMistakes.ContainsKey(a.targetId)) targetMistakes[a.targetId] = 0;
                        targetMistakes[a.targetId]++;
                    }
                    if (!string.IsNullOrEmpty(a.tag))
                    {
                        if (!tagMistakes.ContainsKey(a.tag)) tagMistakes[a.tag] = 0;
                        tagMistakes[a.tag]++;
                    }
                }
            }

            // Find dominant repeated mistake
            foreach (var kv in targetMistakes)
            {
                if (kv.Value >= 3 && kv.Value > s.repeatedMistakeCount)
                {
                    s.repeatedMistakeTarget = kv.Key;
                    s.repeatedMistakeCount = kv.Value;
                }
            }
            foreach (var kv in tagMistakes)
            {
                if (kv.Value >= 3)
                {
                    s.repeatedMistakeTag = kv.Key;
                    break;
                }
            }
        }

        return s;
    }

    /// <summary>
    /// Build aggregated summary from multiple sessions (same game).
    /// Enables trend pattern detection.
    /// </summary>
    public static SessionSummary BuildAggregatedSummary(List<GameSessionData> sessions)
    {
        if (sessions == null || sessions.Count == 0) return new SessionSummary();

        var latest = sessions[sessions.Count - 1];
        var summary = BuildSummary(latest);
        summary.sessionCount = sessions.Count;

        // Aggregate averages
        float totalAcc = 0f, totalDur = 0f;
        int totalMistakes = 0, totalHints = 0, totalRetries = 0;
        int completedCount = 0, abandonedCount = 0;

        foreach (var s in sessions)
        {
            totalAcc += s.totalActions > 0 ? (float)s.correctActions / s.totalActions : 0f;
            totalMistakes += s.mistakes;
            totalHints += s.hintsUsed;
            totalDur += s.durationSeconds;
            totalRetries += s.retries;
            if (s.completed) completedCount++;
            if (s.abandoned) abandonedCount++;
        }

        int n = sessions.Count;
        summary.accuracy = totalAcc / n;
        summary.mistakes = Mathf.RoundToInt((float)totalMistakes / n);
        summary.hintsUsed = Mathf.RoundToInt((float)totalHints / n);
        summary.totalDuration = totalDur / n;
        summary.retries = Mathf.RoundToInt((float)totalRetries / n);
        summary.completed = completedCount > n / 2;
        summary.abandoned = abandonedCount > n / 2;

        // Cross-session trends (need 4+ sessions)
        if (n >= 4)
        {
            int halfN = n / 2;
            float earlyScore = 0f, lateScore = 0f;
            float earlyHints = 0f, lateHints = 0f;

            for (int i = 0; i < halfN; i++)
            {
                earlyScore += sessions[i].sessionScore;
                earlyHints += sessions[i].hintsUsed;
            }
            for (int i = halfN; i < n; i++)
            {
                lateScore += sessions[i].sessionScore;
                lateHints += sessions[i].hintsUsed;
            }

            earlyScore /= halfN;
            lateScore /= (n - halfN);
            earlyHints /= halfN;
            lateHints /= (n - halfN);

            summary.improvesOverTime = lateScore > earlyScore + 5f;
            summary.declinesOverTime = earlyScore > lateScore + 5f;

            // Recurring confusion: same mistake target appears in multiple sessions
            var crossSessionTargets = new Dictionary<string, int>();
            foreach (var sess in sessions)
            {
                sess.EnsureInitialized();
                var sessionTargets = new HashSet<string>();
                foreach (var a in sess.actions)
                {
                    if (!a.correct && !string.IsNullOrEmpty(a.targetId))
                        sessionTargets.Add(a.targetId);
                }
                foreach (var t in sessionTargets)
                {
                    if (!crossSessionTargets.ContainsKey(t)) crossSessionTargets[t] = 0;
                    crossSessionTargets[t]++;
                }
            }
            foreach (var kv in crossSessionTargets)
            {
                if (kv.Value >= 3)
                {
                    summary.confusionIsRecurring = true;
                    summary.repeatedMistakeTarget = kv.Key;
                    break;
                }
            }
        }

        return summary;
    }

    // ═══════════════════════════════════════════════════════════════
    //  STEP 2: ANALYZE PATTERNS
    // ═══════════════════════════════════════════════════════════════

    public static List<BehavioralInsight> Analyze(SessionSummary s)
    {
        var insights = new List<BehavioralInsight>();
        bool multiSession = s.sessionCount >= 3;

        // ── 1. Learning Curve (Session) ──
        if (s.slowStart && s.improvesOverTime)
        {
            insights.Add(new BehavioralInsight
            {
                patternKey = "learning_curve",
                insight = "\u05D4\u05D9\u05DC\u05D3 \u05D4\u05E6\u05E8\u05D9\u05DA \u05D6\u05DE\u05DF \u05DC\u05D4\u05D1\u05D9\u05DF \u05D0\u05EA \u05D4\u05DE\u05E9\u05D9\u05DE\u05D4 \u05D0\u05D1\u05DC \u05D4\u05E8\u05D0\u05D4 \u05E9\u05D9\u05E4\u05D5\u05E8 \u05D1\u05E8\u05D5\u05E8 \u05D1\u05DE\u05D4\u05DC\u05DA \u05D4\u05E4\u05E2\u05D9\u05DC\u05D5\u05EA",
                // הילד הצריך זמן להבין את המשימה אבל הראה שיפור ברור במהלך הפעילות
                type = InsightType.Improvement,
                confidence = ConfidenceLevel.High,
                evidence = "Slow time to first correct + increasing accuracy in second half",
                priority = 9,
                scope = PatternScope.Session
            });
        }

        // ── 2. Focus Drop (Session) ──
        if (!s.slowStart && s.declinesOverTime)
        {
            insights.Add(new BehavioralInsight
            {
                patternKey = "focus_drop",
                insight = "\u05D4\u05D9\u05DC\u05D3 \u05D4\u05EA\u05D7\u05D9\u05DC \u05D1\u05D1\u05D9\u05D8\u05D7\u05D5\u05DF \u05D0\u05D1\u05DC \u05D4\u05D1\u05D9\u05E6\u05D5\u05E2\u05D9\u05DD \u05D4\u05E4\u05DB\u05D5 \u05E4\u05D7\u05D5\u05EA \u05E2\u05E7\u05D1\u05D9\u05D9\u05DD \u05D1\u05D4\u05DE\u05E9\u05DA \u05D4\u05E4\u05E2\u05D9\u05DC\u05D5\u05EA",
                // הילד התחיל בביטחון אבל הביצועים הפכו פחות עקביים בהמשך הפעילות
                type = InsightType.BehaviorPattern,
                confidence = s.secondHalfAccuracy < 0.4f ? ConfidenceLevel.High : ConfidenceLevel.Medium,
                evidence = $"First half accuracy {s.firstHalfAccuracy:P0} → second half {s.secondHalfAccuracy:P0}",
                priority = 7,
                scope = PatternScope.Session
            });
        }

        // ── 3. Thinks Before Acting (Session) ──
        if (s.timeToFirstAction > 8f && s.accuracy > 0.7f)
        {
            insights.Add(new BehavioralInsight
            {
                patternKey = "thinks_first",
                insight = "\u05E0\u05E8\u05D0\u05D4 \u05E9\u05D4\u05D9\u05DC\u05D3 \u05DC\u05D5\u05E7\u05D7 \u05D6\u05DE\u05DF \u05DC\u05D7\u05E9\u05D5\u05D1 \u05DC\u05E4\u05E0\u05D9 \u05E9\u05D4\u05D5\u05D0 \u05E4\u05D5\u05E2\u05DC, \u05DE\u05D4 \u05E9\u05E2\u05E9\u05D5\u05D9 \u05DC\u05EA\u05DE\u05D5\u05DA \u05D1\u05D1\u05D9\u05E6\u05D5\u05E2\u05D9\u05DD \u05DE\u05D3\u05D5\u05D9\u05E7\u05D9\u05DD",
                // נראה שהילד לוקח זמן לחשוב לפני שהוא פועל, מה שעשוי לתמוך בביצועים מדויקים
                type = InsightType.BehaviorPattern,
                confidence = s.accuracy > 0.85f ? ConfidenceLevel.High : ConfidenceLevel.Medium,
                evidence = $"Time to first action: {s.timeToFirstAction:F1}s, accuracy: {s.accuracy:P0}",
                priority = 6,
                scope = PatternScope.Session
            });
        }

        // ── 4. Hesitation / Struggle (Session) ──
        if (s.highPauseFrequency && s.accuracy < 0.5f && s.timeToFirstCorrect > 6f)
        {
            insights.Add(new BehavioralInsight
            {
                patternKey = "hesitation",
                insight = "\u05E4\u05E2\u05D9\u05DC\u05D5\u05EA \u05D6\u05D5 \u05E2\u05E9\u05D5\u05D9\u05D4 \u05E2\u05D3\u05D9\u05D9\u05DF \u05DC\u05D3\u05E8\u05D5\u05E9 \u05EA\u05E8\u05D2\u05D5\u05DC \u05DE\u05D5\u05E0\u05D7\u05D4 \u05E0\u05D5\u05E1\u05E3 \u05D5\u05D1\u05E0\u05D9\u05D9\u05EA \u05D1\u05D9\u05D8\u05D7\u05D5\u05DF",
                // פעילות זו עשויה עדיין לדרוש תרגול מונחה נוסף ובניית ביטחון
                type = InsightType.PracticeArea,
                confidence = ConfidenceLevel.Medium,
                evidence = $"Long pauses ({s.longestPause:F0}s), low accuracy ({s.accuracy:P0}), slow first correct ({s.timeToFirstCorrect:F1}s)",
                priority = 8,
                scope = PatternScope.Session
            });
        }

        // ── 5. Mastery (Session) ──
        if (s.accuracy > 0.9f && s.maxStreak >= 5 && s.hintsUsed == 0 && s.retries == 0)
        {
            insights.Add(new BehavioralInsight
            {
                patternKey = "mastery",
                insight = "\u05D4\u05D9\u05DC\u05D3 \u05DE\u05E8\u05D0\u05D4 \u05E9\u05DC\u05D9\u05D8\u05D4 \u05D5\u05D1\u05D9\u05D8\u05D7\u05D5\u05DF \u05D7\u05D6\u05E7 \u05D1\u05E4\u05E2\u05D9\u05DC\u05D5\u05EA \u05D4\u05D6\u05D5",
                // הילד מראה שליטה וביטחון חזק בפעילות הזו
                type = InsightType.Strength,
                confidence = ConfidenceLevel.High,
                evidence = $"Accuracy {s.accuracy:P0}, streak {s.maxStreak}, no hints, no retries",
                priority = 10,
                scope = PatternScope.Session
            });
        }

        // ── 6. Possibly Too Easy (Trend preferred) ──
        if (s.fastAccurate)
        {
            var conf = multiSession ? ConfidenceLevel.High : ConfidenceLevel.Medium;
            insights.Add(new BehavioralInsight
            {
                patternKey = "too_easy",
                insight = "\u05E4\u05E2\u05D9\u05DC\u05D5\u05EA \u05D6\u05D5 \u05E2\u05E9\u05D5\u05D9\u05D4 \u05DC\u05D4\u05D9\u05D5\u05EA \u05E7\u05DC\u05D4 \u05E7\u05E6\u05EA \u05DC\u05D9\u05DC\u05D3 \u05D5\u05D0\u05E4\u05E9\u05E8 \u05DC\u05D4\u05DE\u05E9\u05D9\u05DA \u05E2\u05DD \u05D2\u05E8\u05E1\u05D4 \u05DE\u05D0\u05EA\u05D2\u05E8\u05EA \u05D9\u05D5\u05EA\u05E8",
                // פעילות זו עשויה להיות קלה קצת לילד ואפשר להמשיך עם גרסה מאתגרת יותר
                type = InsightType.BehaviorPattern,
                confidence = conf,
                evidence = $"Duration {s.totalDuration:F0}s, accuracy {s.accuracy:P0}, 0 mistakes",
                priority = 7,
                scope = multiSession ? PatternScope.Trend : PatternScope.Session
            });
        }

        // ── 7. Persistence (Session) ──
        if (s.retries > 0 && s.completed)
        {
            insights.Add(new BehavioralInsight
            {
                patternKey = "persistence",
                insight = "\u05D4\u05D9\u05DC\u05D3 \u05D4\u05DE\u05E9\u05D9\u05DA \u05DC\u05E0\u05E1\u05D5\u05EA \u05D0\u05D7\u05E8\u05D9 \u05D8\u05E2\u05D5\u05D9\u05D5\u05EA \u05D5\u05D4\u05E8\u05D0\u05D4 \u05D4\u05EA\u05DE\u05D3\u05D4 \u05D8\u05D5\u05D1\u05D4",
                // הילד המשיך לנסות אחרי טעויות והראה התמדה טובה
                type = InsightType.Strength,
                confidence = ConfidenceLevel.High,
                evidence = $"{s.retries} retries, still completed",
                priority = 8,
                scope = PatternScope.Session
            });
        }

        // ── 8. Impulsive Play (Session) ──
        if (s.averageActionInterval > 0 && s.averageActionInterval < 1f && s.accuracy < 0.5f)
        {
            insights.Add(new BehavioralInsight
            {
                patternKey = "impulsive",
                insight = "\u05D4\u05D9\u05DC\u05D3 \u05E0\u05D5\u05D8\u05D4 \u05DC\u05D4\u05D2\u05D9\u05D1 \u05DE\u05D4\u05E8 \u05D1\u05E4\u05E2\u05D9\u05DC\u05D5\u05EA \u05D6\u05D5, \u05DE\u05D4 \u05E9\u05E2\u05DC\u05D5\u05DC \u05DC\u05D4\u05D5\u05D1\u05D9\u05DC \u05DC\u05D9\u05D5\u05EA\u05E8 \u05D8\u05E2\u05D5\u05D9\u05D5\u05EA",
                // הילד נוטה להגיב מהר בפעילות זו, מה שעלול להוביל ליותר טעויות
                type = InsightType.BehaviorPattern,
                confidence = ConfidenceLevel.Medium,
                evidence = $"Avg action interval: {s.averageActionInterval:F1}s, accuracy: {s.accuracy:P0}",
                priority = 6,
                scope = PatternScope.Session
            });
        }

        // ── 9. Repeated Confusion (Session or Trend) ──
        if (!string.IsNullOrEmpty(s.repeatedMistakeTarget))
        {
            bool recurring = s.confusionIsRecurring;
            insights.Add(new BehavioralInsight
            {
                patternKey = recurring ? "recurring_confusion" : "repeated_confusion",
                insight = recurring
                    ? "\u05D4\u05D9\u05DC\u05D3 \u05E2\u05E9\u05D5\u05D9 \u05DC\u05D4\u05E8\u05D5\u05D5\u05D9\u05D7 \u05DE\u05EA\u05E8\u05D2\u05D5\u05DC \u05E0\u05D5\u05E1\u05E3 \u05E2\u05DD \u05DE\u05D5\u05E9\u05D2 \u05DE\u05E1\u05D5\u05D9\u05DD \u05D1\u05E4\u05E2\u05D9\u05DC\u05D5\u05EA \u05D4\u05D6\u05D5"
                    // הילד עשוי להרוויח מתרגול נוסף עם מושג מסוים בפעילות הזו
                    : "\u05D4\u05D9\u05DC\u05D3 \u05D7\u05D6\u05E8 \u05E2\u05DC \u05D0\u05D5\u05EA\u05D4 \u05D8\u05E2\u05D5\u05EA \u05DE\u05E1\u05E4\u05E8 \u05E4\u05E2\u05DE\u05D9\u05DD \u2014 \u05D9\u05D9\u05EA\u05DB\u05DF \u05E9\u05E6\u05E8\u05D9\u05DA \u05EA\u05E8\u05D2\u05D5\u05DC \u05E0\u05D5\u05E1\u05E3",
                    // הילד חזר על אותה טעות מספר פעמים — ייתכן שצריך תרגול נוסף
                type = recurring ? InsightType.PracticeArea : InsightType.PracticeArea,
                confidence = recurring ? ConfidenceLevel.High : ConfidenceLevel.Medium,
                evidence = recurring
                    ? $"Target '{s.repeatedMistakeTarget}' confused across 3+ sessions"
                    : $"Target '{s.repeatedMistakeTarget}' mistaken {s.repeatedMistakeCount} times in session",
                priority = 9,
                scope = recurring ? PatternScope.Trend : PatternScope.Session
            });
        }

        // ── 10. Hint Dependence (Trend) ──
        if (multiSession && s.hintsUsed >= 2 && s.completed)
        {
            insights.Add(new BehavioralInsight
            {
                patternKey = "hint_dependence",
                insight = "\u05D4\u05D9\u05DC\u05D3 \u05E0\u05E2\u05D6\u05E8 \u05DC\u05E2\u05D9\u05EA\u05D9\u05DD \u05E7\u05E8\u05D5\u05D1\u05D5\u05EA \u05D1\u05E8\u05DE\u05D6\u05D9\u05DD \u05D1\u05E4\u05E2\u05D9\u05DC\u05D5\u05EA \u05D6\u05D5 \u05D5\u05E2\u05E9\u05D5\u05D9 \u05E2\u05D3\u05D9\u05D9\u05DF \u05DC\u05D1\u05E0\u05D5\u05EA \u05D1\u05D9\u05D8\u05D7\u05D5\u05DF",
                // הילד נעזר לעיתים קרובות ברמזים בפעילות זו ועשוי עדיין לבנות ביטחון
                type = InsightType.BehaviorPattern,
                confidence = s.hintsUsed >= 3 ? ConfidenceLevel.High : ConfidenceLevel.Medium,
                evidence = $"Avg {s.hintsUsed} hints/session across {s.sessionCount} sessions, still completes",
                priority = 5,
                scope = PatternScope.Trend
            });
        }

        // ── 11. Improvement Over Time (Trend) ──
        if (multiSession && s.improvesOverTime && !s.declinesOverTime)
        {
            insights.Add(new BehavioralInsight
            {
                patternKey = "trend_improvement",
                insight = "\u05E0\u05E8\u05D0\u05EA \u05DE\u05D2\u05DE\u05EA \u05E9\u05D9\u05E4\u05D5\u05E8 \u05D1\u05E4\u05E2\u05D9\u05DC\u05D5\u05EA \u05D4\u05D6\u05D5 \u05DC\u05D0\u05D5\u05E8\u05DA \u05D6\u05DE\u05DF",
                // נראת מגמת שיפור בפעילות הזו לאורך זמן
                type = InsightType.Improvement,
                confidence = ConfidenceLevel.High,
                evidence = $"Score improved across {s.sessionCount} sessions",
                priority = 9,
                scope = PatternScope.Trend
            });
        }

        // ── 12. Decline Over Time (Trend) ──
        if (multiSession && s.declinesOverTime && !s.improvesOverTime)
        {
            insights.Add(new BehavioralInsight
            {
                patternKey = "trend_decline",
                insight = "\u05D4\u05D1\u05D9\u05E6\u05D5\u05E2\u05D9\u05DD \u05D1\u05E4\u05E2\u05D9\u05DC\u05D5\u05EA \u05D4\u05D6\u05D5 \u05D4\u05E4\u05DB\u05D5 \u05E4\u05D7\u05D5\u05EA \u05E2\u05E7\u05D1\u05D9\u05D9\u05DD \u05DC\u05D0\u05D7\u05E8\u05D5\u05E0\u05D4 \u2014 \u05D9\u05D9\u05EA\u05DB\u05DF \u05E9\u05E8\u05DE\u05EA \u05D4\u05E7\u05D5\u05E9\u05D9 \u05E6\u05E8\u05D9\u05DB\u05D4 \u05D4\u05EA\u05D0\u05DE\u05D4",
                // הביצועים בפעילות הזו הפכו פחות עקביים לאחרונה — ייתכן שרמת הקושי צריכה התאמה
                type = InsightType.PracticeArea,
                confidence = ConfidenceLevel.Medium,
                evidence = $"Score declined across {s.sessionCount} sessions",
                priority = 7,
                scope = PatternScope.Trend
            });
        }

        // ── Abandoned without trying hints ──
        if (s.abandoned && s.mistakes > 3 && s.hintsUsed == 0)
        {
            insights.Add(new BehavioralInsight
            {
                patternKey = "quit_without_help",
                insight = "\u05D4\u05D9\u05DC\u05D3 \u05E2\u05E6\u05E8 \u05DC\u05E4\u05E0\u05D9 \u05E9\u05E1\u05D9\u05D9\u05DD \u2014 \u05D4\u05E4\u05E2\u05D9\u05DC\u05D5\u05EA \u05E2\u05DC\u05D5\u05DC\u05D4 \u05DC\u05D4\u05D9\u05D5\u05EA \u05DE\u05D0\u05EA\u05D2\u05E8\u05EA \u05DE\u05D3\u05D9",
                // הילד עצר לפני שסיים — הפעילות עלולה להיות מאתגרת מדי
                type = InsightType.PracticeArea,
                confidence = ConfidenceLevel.Medium,
                evidence = $"Abandoned after {s.mistakes} mistakes, 0 hints used",
                priority = 8,
                scope = PatternScope.Session
            });
        }

        // Sort by priority descending
        insights.Sort((a, b) => b.priority.CompareTo(a.priority));
        return insights;
    }

    // ═══════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Analyze a single session and return behavioral insights.
    /// </summary>
    public static List<BehavioralInsight> AnalyzeSession(GameSessionData session)
    {
        var summary = BuildSummary(session);
        return Analyze(summary);
    }

    /// <summary>
    /// Analyze a game's full history (trend + session patterns).
    /// </summary>
    public static List<BehavioralInsight> AnalyzeGameHistory(GamePerformanceProfile game)
    {
        if (game == null || game.recentSessions == null || game.recentSessions.Count < 2)
            return new List<BehavioralInsight>();

        var sessions = new List<GameSessionData>();
        foreach (var s in game.recentSessions)
            sessions.Add(s);

        var summary = BuildAggregatedSummary(sessions);
        return Analyze(summary);
    }
}
