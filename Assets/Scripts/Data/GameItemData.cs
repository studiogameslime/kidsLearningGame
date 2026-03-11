using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a single main-menu game entry.
/// Create one asset per game via Assets > Create > Kids Learning Game > Game Item.
/// </summary>
[CreateAssetMenu(fileName = "NewGame", menuName = "Kids Learning Game/Game Item")]
public class GameItemData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string title;

    [Header("Visuals")]
    [Tooltip("Leave empty to use a colored placeholder.")]
    public Sprite thumbnail;
    public Color cardColor = new Color(0.7f, 0.62f, 0.86f); // soft purple default

    [Header("Navigation")]
    [Tooltip("Scene to load when this game has no sub-items, or when sub-items don't override it.")]
    public string targetSceneName;

    [Header("Sub-Selection")]
    public bool hasSubItems;
    [Tooltip("Title shown at the top of the selection screen, e.g. 'Choose a Category'.")]
    public string selectionScreenTitle = "Choose";
    public List<SubItemData> subItems = new List<SubItemData>();
}

/// <summary>
/// A sub-option within a game (e.g. "Animals" inside Memory Game).
/// Serialized inline in GameItemData — not a separate ScriptableObject.
/// </summary>
[System.Serializable]
public class SubItemData
{
    public string id;
    public string title;

    [Tooltip("Leave empty to use a colored placeholder.")]
    public Sprite thumbnail;
    public Color cardColor = new Color(0.81f, 0.61f, 0.85f);

    [Tooltip("If empty, uses the parent game's targetSceneName.")]
    public string targetSceneName;

    [Tooltip("Key passed to the game scene so it knows what content to load.")]
    public string categoryKey;

    [Tooltip("Optional direct reference to a content asset (e.g. an image for coloring).")]
    public Sprite contentAsset;
}
