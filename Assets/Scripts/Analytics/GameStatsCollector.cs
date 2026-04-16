using System;
using UnityEngine;

/// <summary>
/// Per-game-session helper. Create at game start, call Record* methods during play,
/// then call Finalize() on completion to build and register the session data.
///
/// Usage:
///   var collector = new GameStatsCollector("memory", "Cat", SessionContent.Animals);
///   collector.RecordCorrect("match", "Cat");
///   collector.RecordMistake("mismatch");
///   collector.SetCustom("pairsMatched", 4);
///   collector.Finalize(completed: true);
/// </summary>
public class GameStatsCollector
{
    /// <summary>Maximum number of ActionRecord entries stored per session.</summary>
    public const int MaxActionLogSize = 200;

    private readonly GameSessionData _data;
    private readonly float _startRealtime;
    private float _lastActionTime;
    private float _totalIntervals;
    private int _intervalCount;
    private int _currentStreak;
    private float _longestPauseSince;
    private bool _finalized;
    private bool _hasFirstAction;
    private bool _hasFirstCorrect;

    // ── Constructors ─────────────────────────────────────────────

    /// <summary>Basic constructor. Content context can be set later via SetContentContext.</summary>
    public GameStatsCollector(string gameId)
    {
        _startRealtime = Time.realtimeSinceStartup;
        _data = new GameSessionData
        {
            gameId = gameId,
            startTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            difficultyLevel = StatsManager.Instance != null
                ? StatsManager.Instance.GetGameDifficulty(gameId)
                : 1
        };
        _data.EnsureInitialized();
        _lastActionTime = -1f;
        _longestPauseSince = Time.realtimeSinceStartup;
    }

    /// <summary>Full constructor with content context and session type.</summary>
    public GameStatsCollector(string gameId, string contentId, string contentCategory, bool isEndless = false)
        : this(gameId)
    {
        _data.contentId = contentId;
        _data.contentCategory = contentCategory;
        _data.isEndlessMode = isEndless;
    }

    // ── Read-only Properties ─────────────────────────────────────

    /// <summary>Current difficulty level for this session.</summary>
    public int Difficulty => _data.difficultyLevel;

    /// <summary>Number of mistakes so far.</summary>
    public int Mistakes => _data.mistakes;

    /// <summary>Number of correct actions so far.</summary>
    public int CorrectActions => _data.correctActions;

    /// <summary>Rounds completed so far.</summary>
    public int RoundsCompleted => _data.roundsCompleted;

    /// <summary>Session score (0-100) computed by scoring strategy after Finalize().</summary>
    public float SessionScore => _data.sessionScore;

    // ── Content Context ──────────────────────────────────────────

    /// <summary>Set or update content context after construction.</summary>
    public void SetContentContext(string contentId, string contentCategory)
    {
        _data.contentId = contentId;
        _data.contentCategory = contentCategory;
    }

    // ── Action Recording ─────────────────────────────────────────

    /// <summary>Record a correct or incorrect action (basic, no tag).</summary>
    public void RecordAction(bool correct)
    {
        RecordActionInternal(correct, null, null, 0f);
    }

    /// <summary>Record an action with tag and optional context.</summary>
    public void RecordAction(bool correct, string tag, string targetId = null, float value = 0f)
    {
        RecordActionInternal(correct, tag, targetId, value);
    }

    /// <summary>Shorthand: record a correct action.</summary>
    public void RecordCorrect() => RecordActionInternal(true, null, null, 0f);

    /// <summary>Record a correct action with tag and optional target.</summary>
    public void RecordCorrect(string tag, string targetId = null)
    {
        RecordActionInternal(true, tag, targetId, 0f);
    }

    /// <summary>Shorthand: record a mistake (also counted as an action).</summary>
    public void RecordMistake() => RecordActionInternal(false, null, null, 0f);

    /// <summary>Record a mistake with tag and optional target.</summary>
    public void RecordMistake(string tag, string targetId = null)
    {
        RecordActionInternal(false, tag, targetId, 0f);
    }

    private void RecordActionInternal(bool correct, string tag, string targetId, float value)
    {
        _data.totalActions++;
        _data.attempts++;

        float now = Time.realtimeSinceStartup;
        float elapsed = now - _startRealtime;

        // First action timing
        if (!_hasFirstAction)
        {
            _hasFirstAction = true;
            _data.timeToFirstAction = elapsed;
            _data.firstActionDelay = elapsed;
        }

        // Action interval tracking
        if (_lastActionTime >= 0f)
        {
            float interval = now - _lastActionTime;
            _totalIntervals += interval;
            _intervalCount++;
        }
        _lastActionTime = now;

        // Pause tracking
        float pauseDuration = now - _longestPauseSince;
        if (pauseDuration > _data.longestPause)
            _data.longestPause = pauseDuration;
        _longestPauseSince = now;

        if (correct)
        {
            _data.correctActions++;
            _currentStreak++;
            if (_currentStreak > _data.maxStreak)
                _data.maxStreak = _currentStreak;

            // First correct timing
            if (!_hasFirstCorrect)
            {
                _hasFirstCorrect = true;
                _data.timeToFirstCorrect = elapsed;
            }
        }
        else
        {
            _data.mistakes++;
            _currentStreak = 0;
        }

        // Append to action log (capped)
        if (_data.actions.Count < MaxActionLogSize)
        {
            _data.actions.Add(new ActionRecord
            {
                timestamp = elapsed,
                correct = correct,
                tag = tag,
                targetId = targetId,
                value = value
            });
        }
        else
        {
            _data.actionLogTruncated = true;
        }
    }

    // ── Hints & Assists ──────────────────────────────────────────

    /// <summary>Record that the child used a hint.</summary>
    public void RecordHint()
    {
        _data.hintsUsed++;
    }

    /// <summary>Record an auto-correction or forced help event.</summary>
    public void RecordAutoAssist()
    {
        _data.autoAssists++;
    }

    // ── Rounds ───────────────────────────────────────────────────

    /// <summary>Increment rounds completed (call when a round/level/sequence finishes).</summary>
    public void RecordRoundComplete()
    {
        _data.roundsCompleted++;
    }

    /// <summary>Set the expected total rounds for this session.</summary>
    public void SetTotalRoundsPlanned(int n)
    {
        _data.totalRoundsPlanned = n;
    }

    // ── Retries ──────────────────────────────────────────────────

    /// <summary>Record a restart within this session.</summary>
    public void RecordRetry()
    {
        _data.retries++;
    }

    // ── Custom Metrics ───────────────────────────────────────────

    /// <summary>Set a game-specific custom metric.</summary>
    public void SetCustom(string key, float value)
    {
        _data.SetCustom(key, value);
    }

    /// <summary>Increment a game-specific custom metric.</summary>
    public void IncrementCustom(string key, float amount = 1f)
    {
        float current = _data.GetCustom(key);
        _data.SetCustom(key, current + amount);
    }

    // ── Termination ──────────────────────────────────────────────

    /// <summary>Set the end reason explicitly. Use SessionEndReason constants.</summary>
    public void SetEndReason(string reason)
    {
        _data.endReason = reason;
    }

    /// <summary>
    /// Mark the session as abandoned (child left without completing).
    /// Automatically finalizes.
    /// </summary>
    public void Abandon()
    {
        if (_finalized) return;
        _data.abandoned = true;
        _data.completed = false;
        if (string.IsNullOrEmpty(_data.endReason))
            _data.endReason = SessionEndReason.Quit;
        FinalizeInternal();
    }

    /// <summary>
    /// Finalize and register the session with StatsManager.
    /// Call once when the game round ends.
    /// </summary>
    public void Finalize(bool completed)
    {
        if (_finalized) return;
        _data.completed = completed;
        _data.abandoned = !completed;
        if (string.IsNullOrEmpty(_data.endReason))
            _data.endReason = completed ? SessionEndReason.Completed : SessionEndReason.Quit;
        FinalizeInternal();
    }

    private void FinalizeInternal()
    {
        _finalized = true;
        _data.durationSeconds = Time.realtimeSinceStartup - _startRealtime;

        // Active play duration: same as total for now.
        // Placeholder — future: subtract detected idle/paused time.
        _data.activePlayDuration = _data.durationSeconds;

        // Time to completion: elapsed from first action to finalize
        if (_data.completed && _hasFirstAction)
            _data.timeToCompletion = _data.durationSeconds - _data.timeToFirstAction;

        if (_intervalCount > 0)
            _data.averageActionInterval = _totalIntervals / _intervalCount;

        StatsManager.Instance?.RegisterGameSession(_data);
    }
}

/// <summary>
/// Standardized end reason values. Use these constants instead of ad-hoc strings.
/// </summary>
public static class SessionEndReason
{
    public const string Completed = "completed";
    public const string Quit = "quit";
    public const string Failed = "failed";
    public const string Timeout = "timeout";
    public const string Interrupted = "interrupted";
}

/// <summary>
/// Standardized content category values. Use these constants for contentCategory field.
/// </summary>
public static class SessionContent
{
    public const string Animals = "animals";
    public const string Colors = "colors";
    public const string Shapes = "shapes";
    public const string Drawing = "drawing";
    public const string Maze = "maze";
    public const string Sequence = "sequence";
    public const string Stacking = "stacking";
    public const string Building = "building";
    public const string Matching = "matching";
    public const string Arcade = "arcade";
}
