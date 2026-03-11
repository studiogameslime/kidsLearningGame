using UnityEngine;
using TMPro;

/// <summary>
/// Drives the reusable selection-menu scene.
/// Reads the current GameContext.CurrentGame and displays its sub-items.
/// </summary>
public class SelectionMenuController : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public Transform cardContainer;
    public GameCardView cardPrefab;

    private void Start()
    {
        var game = GameContext.CurrentGame;
        if (game == null)
        {
            Debug.LogError("SelectionMenuController: No game in GameContext! Did you navigate here correctly?");
            return;
        }

        // Set the screen title
        if (titleText != null)
            titleText.text = game.selectionScreenTitle;

        PopulateGrid(game);
    }

    private void PopulateGrid(GameItemData game)
    {
        foreach (var item in game.subItems)
        {
            var card = Instantiate(cardPrefab, cardContainer);
            var capturedItem = item;
            var capturedGame = game;

            card.Setup(
                item.title,
                item.thumbnail,
                item.cardColor,
                () => NavigationManager.GoToGame(capturedGame, capturedItem)
            );
        }
    }

    /// <summary>Called by the Back button in the scene.</summary>
    public void OnBackPressed()
    {
        NavigationManager.GoToMainMenu();
    }
}
