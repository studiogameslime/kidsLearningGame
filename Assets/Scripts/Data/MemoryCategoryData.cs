using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a set of card faces for the memory game.
/// Create one per category (Animals, Numbers, etc.) via Assets > Create > Kids Learning Game > Memory Category.
/// </summary>
[CreateAssetMenu(fileName = "NewMemoryCategory", menuName = "Kids Learning Game/Memory Category")]
public class MemoryCategoryData : ScriptableObject
{
    [Tooltip("Must match the categoryKey used in the sub-item data.")]
    public string categoryKey;

    [Tooltip("Sprites used as card faces. The game picks random pairs from this list.")]
    public List<Sprite> cardFaces = new List<Sprite>();

    [Tooltip("Sprite shown on the back of every card.")]
    public Sprite cardBack;

    [Tooltip("How many pairs to play with (e.g. 10 means 20 cards total).")]
    public int pairCount = 10;
}
