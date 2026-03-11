using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Holds the current navigation selection state and provides scene-loading helpers.
/// Set fields before loading a scene so the target scene knows what data to use.
/// </summary>
public static class GameContext
{
    /// <summary>The game the player selected from the main menu.</summary>
    public static GameItemData CurrentGame;

    /// <summary>The sub-item the player selected (null if the game launched directly).</summary>
    public static SubItemData CurrentSelection;

    /// <summary>Clears all selection state.</summary>
    public static void Clear()
    {
        CurrentGame = null;
        CurrentSelection = null;
    }
}

/// <summary>
/// Simple static navigation helper.
/// Centralises scene-loading logic so callers don't need to know scene names.
/// </summary>
public static class NavigationManager
{
    private const string MainMenuScene = "MainMenu";
    private const string SelectionMenuScene = "SelectionMenu";

    /// <summary>Go back to the main hub.</summary>
    public static void GoToMainMenu()
    {
        GameContext.Clear();
        SceneManager.LoadScene(MainMenuScene);
    }

    /// <summary>Open the reusable selection screen for a game that has sub-items.</summary>
    public static void GoToSelectionMenu(GameItemData game)
    {
        GameContext.CurrentGame = game;
        GameContext.CurrentSelection = null;
        SceneManager.LoadScene(SelectionMenuScene);
    }

    /// <summary>Open a game scene directly (no sub-selection).</summary>
    public static void GoToGame(GameItemData game)
    {
        GameContext.CurrentGame = game;
        GameContext.CurrentSelection = null;
        SceneManager.LoadScene(game.targetSceneName);
    }

    /// <summary>Open a game scene with a specific sub-item selected.</summary>
    public static void GoToGame(GameItemData game, SubItemData selection)
    {
        GameContext.CurrentGame = game;
        GameContext.CurrentSelection = selection;

        string scene = !string.IsNullOrEmpty(selection.targetSceneName)
            ? selection.targetSceneName
            : game.targetSceneName;

        SceneManager.LoadScene(scene);
    }
}
