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
    private const string WorldSceneName = "WorldScene";
    private const string DrawingGalleryScene = "DrawingGallery";

    /// <summary>Go to the World scene (main hub after profile selection).</summary>
    public static void GoToHome()
    {
        GameContext.Clear();
        BubbleTransition.LoadScene(WorldSceneName);
    }

    /// <summary>Go to the profile selection screen.</summary>
    public static void GoToProfileSelection()
    {
        GameContext.Clear();
        BubbleTransition.LoadScene(ProfileSelectionScene);
    }

    /// <summary>Go to the profile creation onboarding flow.</summary>
    public static void GoToProfileCreation()
    {
        BubbleTransition.LoadScene(ProfileCreationScene);
    }

    /// <summary>Go back to the World scene (main hub).
    /// Blocked while celebration is playing to prevent premature exit.</summary>
    public static void GoToMainMenu()
    {
        if (GameCompletionBridge.IsCelebrating) return;
        GameContext.Clear();
        BubbleTransition.LoadScene(WorldSceneName);
    }

    /// <summary>Go to the World scene (main hub).</summary>
    public static void GoToWorld()
    {
        GameContext.Clear();
        BubbleTransition.LoadScene(WorldSceneName);
    }

    /// <summary>Go to the games collection (MainMenu scene).</summary>
    public static void GoToGamesCollection()
    {
        GameContext.Clear();
        SceneManager.LoadScene(MainMenuScene);
    }

    /// <summary>Go to the parent dashboard (analytics).</summary>
    public static void GoToParentDashboard()
    {
        BubbleTransition.LoadScene("ParentDashboard");
    }

    /// <summary>Go to the Color Studio sandbox.</summary>
    public static void GoToColorStudio()
    {
        GameContext.Clear();
        BubbleTransition.LoadScene("ColorStudioScene");
    }

    /// <summary>Go to the Aquarium collectible scene.</summary>
    public static void GoToAquarium()
    {
        GameContext.Clear();
        BubbleTransition.LoadScene("AquariumScene");
    }

    /// <summary>Go to the drawing gallery.</summary>
    public static void GoToDrawingGallery()
    {
        BubbleTransition.LoadScene(DrawingGalleryScene);
    }

    /// <summary>Open the reusable selection screen for a game that has sub-items.</summary>
    public static void GoToSelectionMenu(GameItemData game)
    {
        if (game == null) return;
        if (!game.hasSubItems || game.subItems == null || game.subItems.Count == 0)
        {
            GoToGame(game);
            return;
        }
        GameContext.CurrentGame = game;
        GameContext.CurrentSelection = null;
        SceneManager.LoadScene(SelectionMenuScene);
    }

    /// <summary>Open a game scene directly (no sub-selection).</summary>
    public static void GoToGame(GameItemData game)
    {
        if (game == null) return;
        GameContext.CurrentGame = game;
        GameContext.CurrentSelection = null;
        BubbleTransition.LoadScene(game.targetSceneName);
    }

    /// <summary>Open a game scene with a specific sub-item selected.</summary>
    public static void GoToGame(GameItemData game, SubItemData selection)
    {
        if (game == null) return;
        GameContext.CurrentGame = game;
        GameContext.CurrentSelection = selection;

        string scene = (selection != null && !string.IsNullOrEmpty(selection.targetSceneName))
            ? selection.targetSceneName
            : game.targetSceneName;

        BubbleTransition.LoadScene(scene);
    }
}
