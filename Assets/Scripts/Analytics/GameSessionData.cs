using System;
using System.Collections.Generic;

/// <summary>
/// Raw data from a single game play session.
/// Created at game start, populated during play, finalized on completion.
///
/// All new fields default to 0/null/false for backward compatibility with old saved profiles.
/// </summary>
[Serializable]
public class GameSessionData
{
    public string gameId;
    public int difficultyLevel;
    public long startTime;
    public float durationSeconds;

    public bool completed;
    public bool abandoned;

    public int attempts;
    public int mistakes;
    public int hintsUsed;

    public float firstActionDelay;
    public float averageActionInterval;

    public int correctActions;
    public int totalActions;

    public int maxStreak;
    public float longestPause;

    /// <summary>
    /// Score calculated by the game's scoring strategy at registration time.
    /// 0-100 scale. Set by StatsManager, not by the game.
    /// </summary>
    public float sessionScore;

    // ── Content Context ──────────────────────────────────────────

    /// <summary>Stable ID of what was played: "Cat", "level_3", "Orange". Not localized.</summary>
    public string contentId;

    /// <summary>Content grouping: "animals", "colors", "shapes", "maze", etc.</summary>
    public string contentCategory;

    // ── Session Boundary ─────────────────────────────────────────

    /// <summary>How many rounds/goals were completed within this session. 0 if none.</summary>
    public int roundsCompleted;

    /// <summary>Expected total rounds. 0 = unknown or endless.</summary>
    public int totalRoundsPlanned;

    /// <summary>True for games with no natural end: Simon Says, Tower Stack, Flappy Bird.</summary>
    public bool isEndlessMode;

    // ── Termination ──────────────────────────────────────────────

    /// <summary>Why the session ended: "completed", "quit", "failed", "timeout", "interrupted".</summary>
    public string endReason;

    // ── Timing ───────────────────────────────────────────────────

    /// <summary>Active play time (same as durationSeconds for now; structured for future idle tracking).</summary>
    public float activePlayDuration;

    /// <summary>Seconds from session start to first interaction (any action, correct or not).</summary>
    public float timeToFirstAction;

    /// <summary>Seconds from session start to first correct action.</summary>
    public float timeToFirstCorrect;

    /// <summary>Seconds from first action to completion. 0 if not completed.</summary>
    public float timeToCompletion;

    // ── Interaction Detail ────────────────────────────────────────

    /// <summary>Times the child restarted within this session.</summary>
    public int retries;

    /// <summary>Auto-corrections or forced help events.</summary>
    public int autoAssists;

    // ── Action Log ───────────────────────────────────────────────

    /// <summary>Per-action log with timestamps and context. Capped at 200 entries.</summary>
    public List<ActionRecord> actions;

    /// <summary>True if the actions list was capped due to overflow.</summary>
    public bool actionLogTruncated;

    // ── Game-Specific Raw Metrics ─────────────────────────────────

    /// <summary>
    /// Game-specific raw metrics not covered by standard fields.
    /// Store concrete raw facts, not interpreted/derived values.
    /// Examples: "mismatchCount", "pairsTotal", "maxSequenceReached", "wallHits".
    /// </summary>
    public List<CustomMetric> customMetrics;

    // ── Initialization ───────────────────────────────────────────

    /// <summary>Ensures all list fields are initialized (safe after deserialization).</summary>
    public void EnsureInitialized()
    {
        if (customMetrics == null) customMetrics = new List<CustomMetric>();
        if (actions == null) actions = new List<ActionRecord>();
    }

    // ── Custom Metric Helpers ────────────────────────────────────

    public void SetCustom(string key, float value)
    {
        EnsureInitialized();
        for (int i = 0; i < customMetrics.Count; i++)
        {
            if (customMetrics[i].key == key)
            {
                customMetrics[i] = new CustomMetric { key = key, value = value };
                return;
            }
        }
        customMetrics.Add(new CustomMetric { key = key, value = value });
    }

    public float GetCustom(string key, float fallback = 0f)
    {
        if (customMetrics == null) return fallback;
        foreach (var m in customMetrics)
            if (m.key == key) return m.value;
        return fallback;
    }
}

/// <summary>
/// A single recorded action within a game session.
/// Lightweight struct for per-action timing and context.
/// </summary>
[Serializable]
public struct ActionRecord
{
    /// <summary>Seconds since session start.</summary>
    public float timestamp;

    /// <summary>Whether this action was correct.</summary>
    public bool correct;

    /// <summary>Action type tag: "match", "flip", "tap", "drop", "place", etc.</summary>
    public string tag;

    /// <summary>Optional: what was targeted (animal id, color id, slot id, etc.).</summary>
    public string targetId;

    /// <summary>Optional: numeric value if relevant (index, count, score delta, etc.).</summary>
    public float value;
}

[Serializable]
public struct CustomMetric
{
    public string key;
    public float value;
}
