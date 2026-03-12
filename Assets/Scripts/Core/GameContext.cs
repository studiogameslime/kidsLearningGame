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

    /// <summary>Runtime texture from gallery import (not an asset — created at runtime).</summary>
    public static Texture2D CustomTexture;

    /// <summary>Clears all selection state.</summary>
    public static void Clear()
    {
        CurrentGame = null;
        CurrentSelection = null;
        CustomTexture = null;
    }
}

/// <summary>
/// Simple static navigation helper.
/// Centralises scene-loading logic so callers don't need to know scene names.
/// </summary>
public static class NavigationManager
{
    private const string MainMenuScene = "MainMenu";
    private const string HomeSceneName = "HomeScene";
    private const string SelectionMenuScene = "SelectionMenu";
    private const string ProfileSelectionScene = "ProfileSelection";
    private const string ProfileCreationScene = "ProfileCreation";

    /// <summary>Go to the home screen (main entry point after profile selection).</summary>
    public static void GoToHome()
    {
        GameContext.Clear();
        SceneManager.LoadScene(HomeSceneName);
    }

    /// <summary>Go to the profile selection screen.</summary>
    public static void GoToProfileSelection()
    {
        GameContext.Clear();
        SceneManager.LoadScene(ProfileSelectionScene);
    }

    /// <summary>Go to the profile creation onboarding flow.</summary>
    public static void GoToProfileCreation()
    {
        SceneManager.LoadScene(ProfileCreationScene);
    }

    /// <summary>Go back to the main hub. If a journey is active, ends it and goes to HomeScene.</summary>
    public static void GoToMainMenu()
    {
        if (JourneyManager.IsJourneyActive)
        {
            JourneyManager.Instance?.EndJourney();
            return;
        }
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
