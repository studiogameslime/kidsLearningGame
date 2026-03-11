using UnityEngine;
using TMPro;

/// <summary>
/// Placeholder controller for mini-game scenes.
/// Displays which game and sub-selection was chosen so you can verify the data flow.
/// Replace this with real gameplay logic later.
/// </summary>
public class PlaceholderGameController : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI infoText;

    private void Start()
    {
        var game = GameContext.CurrentGame;
        var selection = GameContext.CurrentSelection;

        // Title
        if (titleText != null)
            titleText.text = game != null ? game.title : "Unknown Game";

        // Info panel — shows what was selected for debugging / verification
        if (infoText != null)
        {
            string info = "";

            if (game != null)
                info += $"Game: {game.title}\nGame ID: {game.id}\n";

            if (selection != null)
            {
                info += $"\nCategory: {selection.title}";
                info += $"\nCategory Key: {selection.categoryKey}";

                if (selection.contentAsset != null)
                    info += $"\nContent Asset: {selection.contentAsset.name}";
            }
            else
            {
                info += "\n(No sub-selection — launched directly)";
            }

            infoText.text = info;
        }
    }

    /// <summary>Called by the Home button.</summary>
    public void OnHomePressed()
    {
        NavigationManager.GoToMainMenu();
    }

    /// <summary>Called by the Restart button.</summary>
    public void OnRestartPressed()
    {
        // Reload the current scene with the same context
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }
}
