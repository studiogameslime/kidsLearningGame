using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines starting difficulty per game based on child's age.
/// Created as a ScriptableObject asset in Assets/Data/Analytics/.
/// </summary>
[CreateAssetMenu(fileName = "AgeDifficultyBaseline", menuName = "Kids Learning Game/Analytics/Age Difficulty Baseline")]
public class AgeDifficultyBaseline : ScriptableObject
{
    public List<AgeRange> ageRanges = new List<AgeRange>();

    /// <summary>
    /// Returns the starting difficulty for a game given the child's age in months.
    /// Falls back to 1 if no matching entry is found.
    /// </summary>
    public int GetStartingDifficulty(string gameId, int ageMonths)
    {
        // Find the best matching age range (last one whose minMonths <= age)
        AgeRange best = null;
        foreach (var range in ageRanges)
        {
            if (ageMonths >= range.minMonths)
                best = range;
        }

        if (best != null)
        {
            foreach (var entry in best.games)
            {
                if (entry.gameId == gameId)
                    return entry.startingDifficulty;
            }
        }

        return 1; // default
    }
}

[Serializable]
public class AgeRange
{
    [Tooltip("Minimum age in months for this range (inclusive).")]
    public int minMonths;
    public string label; // e.g. "2.5–3 years"
    public List<GameDifficultyEntry> games = new List<GameDifficultyEntry>();
}

[Serializable]
public class GameDifficultyEntry
{
    public string gameId;
    [Range(1, 10)]
    public int startingDifficulty = 1;
}
