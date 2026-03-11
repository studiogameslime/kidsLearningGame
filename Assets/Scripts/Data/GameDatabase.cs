using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry of all games shown in the main menu.
/// Create via Assets > Create > Kids Learning Game > Game Database.
/// Drag GameItemData assets into the list to define menu order.
/// </summary>
[CreateAssetMenu(fileName = "GameDatabase", menuName = "Kids Learning Game/Game Database")]
public class GameDatabase : ScriptableObject
{
    [Tooltip("Games appear in the main menu in this order.")]
    public List<GameItemData> games = new List<GameItemData>();
}
