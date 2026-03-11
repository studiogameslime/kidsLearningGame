using UnityEngine;

/// <summary>
/// Drives the main-menu scene: reads the GameDatabase and spawns a card for each game.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Data")]
    public GameDatabase database;

    [Header("UI References")]
    public Transform cardContainer; // the Content object inside the ScrollRect
    public GameCardView cardPrefab;

    private void Start()
    {
        if (database == null)
        {
            Debug.LogError("MainMenuController: No GameDatabase assigned!");
            return;
        }

        PopulateGrid();
    }

    private void PopulateGrid()
    {
        foreach (var game in database.games)
        {
            var card = Instantiate(cardPrefab, cardContainer);
            var capturedGame = game; // capture for closure

            card.Setup(
                game.title,
                game.thumbnail,
                game.cardColor,
                () => OnGameCardTapped(capturedGame)
            );
        }
    }

    private void OnGameCardTapped(GameItemData game)
    {
        if (game.hasSubItems && game.subItems != null && game.subItems.Count > 0)
            NavigationManager.GoToSelectionMenu(game);
        else
            NavigationManager.GoToGame(game);
    }
}
