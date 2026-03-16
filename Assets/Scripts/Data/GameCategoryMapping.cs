using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps each game to 1–3 skill categories with weights.
/// Created as a ScriptableObject asset in Assets/Data/Analytics/.
/// </summary>
[CreateAssetMenu(fileName = "GameCategoryMapping", menuName = "Kids Learning Game/Analytics/Game Category Mapping")]
public class GameCategoryMapping : ScriptableObject
{
    public List<GameCategoryEntry> entries = new List<GameCategoryEntry>();

    public List<CategoryWeight> GetWeights(string gameId)
    {
        foreach (var e in entries)
            if (e.gameId == gameId) return e.weights;
        return null;
    }

    /// <summary>Returns all game IDs that contribute to a given category.</summary>
    public List<string> GetGamesForCategory(SkillCategory category)
    {
        var result = new List<string>();
        foreach (var e in entries)
        {
            foreach (var w in e.weights)
            {
                if (w.category == category)
                {
                    result.Add(e.gameId);
                    break;
                }
            }
        }
        return result;
    }
}

[Serializable]
public class GameCategoryEntry
{
    public string gameId;
    public List<CategoryWeight> weights = new List<CategoryWeight>();
}

[Serializable]
public class CategoryWeight
{
    public SkillCategory category;
    [Range(0f, 1f)]
    public float weight;
}
