using System;
using System.Collections.Generic;

/// <summary>
/// Raw data from a single game play session.
/// Created at game start, populated during play, finalized on completion.
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
    /// Game-specific metrics not covered by standard fields.
    /// Examples: "cardsOpened", "pairsMatched", "maxSequence", "piecesPlaced".
    /// </summary>
    public List<CustomMetric> customMetrics = new List<CustomMetric>();

    public void SetCustom(string key, float value)
    {
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
        foreach (var m in customMetrics)
            if (m.key == key) return m.value;
        return fallback;
    }
}

[Serializable]
public struct CustomMetric
{
    public string key;
    public float value;
}
