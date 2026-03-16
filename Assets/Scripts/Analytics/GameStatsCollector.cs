using System;
using UnityEngine;

/// <summary>
/// Per-game-session helper. Create at game start, call Record* methods during play,
/// then call Finalize() on completion to build and register the session data.
///
/// Usage:
///   var collector = new GameStatsCollector("memory");
///   collector.RecordAction(correct: true);
///   collector.RecordMistake();
///   collector.SetCustom("pairsMatched", 4);
///   collector.Finalize(completed: true);
/// </summary>
public class GameStatsCollector
{
    private readonly GameSessionData _data;
    private readonly float _startRealtime;
    private float _lastActionTime;
    private float _totalIntervals;
    private int _intervalCount;
    private int _currentStreak;
    private float _longestPauseSince;
    private bool _finalized;

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
        _lastActionTime = -1f;
        _longestPauseSince = Time.realtimeSinceStartup;
    }

    /// <summary>Current difficulty level for this session.</summary>
    public int Difficulty => _data.difficultyLevel;

    /// <summary>Number of mistakes so far.</summary>
    public int Mistakes => _data.mistakes;

    /// <summary>Number of correct actions so far.</summary>
    public int CorrectActions => _data.correctActions;

    /// <summary>Record a correct or incorrect action.</summary>
    public void RecordAction(bool correct)
    {
        _data.totalActions++;
        _data.attempts++;

        float now = Time.realtimeSinceStartup;

        // First action delay
        if (_data.totalActions == 1)
            _data.firstActionDelay = now - (_longestPauseSince);

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
        }
        else
        {
            _data.mistakes++;
            _currentStreak = 0;
        }
    }

    /// <summary>Shorthand: record a correct action.</summary>
    public void RecordCorrect() => RecordAction(true);

    /// <summary>Shorthand: record a mistake (also counted as an action).</summary>
    public void RecordMistake() => RecordAction(false);

    /// <summary>Record that the child used a hint.</summary>
    public void RecordHint()
    {
        _data.hintsUsed++;
    }

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

    /// <summary>
    /// Mark the session as abandoned (child left without completing).
    /// Automatically finalizes.
    /// </summary>
    public void Abandon()
    {
        if (_finalized) return;
        _data.abandoned = true;
        _data.completed = false;
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
        FinalizeInternal();
    }

    private void FinalizeInternal()
    {
        _finalized = true;
        _data.durationSeconds = Time.realtimeSinceStartup - _startRealtime;

        if (_intervalCount > 0)
            _data.averageActionInterval = _totalIntervals / _intervalCount;

        StatsManager.Instance?.RegisterGameSession(_data);
    }
}
