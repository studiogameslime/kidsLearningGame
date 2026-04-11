using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Hint")]
    public TextMeshProUGUI gamesCountHint; // "X מתוך Y משחקים פעילים"

    [Header("Navigation")]
    public Button backToWorldButton;

    [Header("Profile Button")]
    public Image profileButtonImage;
    public Image profileButtonPhoto;
    public TextMeshProUGUI profileButtonInitial;

    private void Start()
    {
        // If no profile is selected, go back to profile selection
        if (ProfileManager.Instance != null && ProfileManager.ActiveProfile == null)
        {
            NavigationManager.GoToProfileSelection();
            return;
        }

        if (database == null)
        {
            Debug.LogError("MainMenuController: No GameDatabase assigned!");
            return;
        }

        if (backToWorldButton != null)
            backToWorldButton.onClick.AddListener(OnBackToWorldPressed);

        PopulateGrid();
        UpdateProfileButton();
    }

    private void UpdateProfileButton()
    {
        // Hide profile button from header
        if (profileButtonImage != null)
            profileButtonImage.gameObject.SetActive(false);
    }

    private void PopulateGrid()
    {
        var profile = ProfileManager.ActiveProfile;
        var visibleGames = profile != null
            ? GameVisibilityService.GetVisibleGames(profile, database.games)
            : database.games;

        int totalGames = database.games.Count;
        int activeGames = visibleGames.Count;

        // Show hint — always visible with parent area info
        if (gamesCountHint != null)
        {
            string countPart = activeGames < totalGames
                ? $"{activeGames} \u05DE\u05EA\u05D5\u05DA {totalGames} \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E4\u05E2\u05D9\u05DC\u05D9\u05DD"
                // X מתוך Y משחקים פעילים
                : "\u05DB\u05DC \u05D4\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E4\u05E2\u05D9\u05DC\u05D9\u05DD";
                // כל המשחקים פעילים
            string actionPart = "\u05D4\u05E4\u05E2\u05D9\u05DC\u05D5 \u05D0\u05D5 \u05D4\u05E1\u05EA\u05D9\u05E8\u05D5 \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D5\u05E9\u05E0\u05D5 \u05E8\u05DE\u05D5\u05EA \u05E7\u05D5\u05E9\u05D9 \u05D1\u05D0\u05D9\u05D6\u05D5\u05E8 \u05D4\u05D5\u05E8\u05D9\u05DD";
            // הפעילו או הסתירו משחקים ושנו רמות קושי באיזור הורים
            HebrewText.SetText(gamesCountHint, countPart + "  \u00B7  " + actionPart);
            gamesCountHint.gameObject.SetActive(true);
        }

        Color profileColor = profile != null ? profile.AvatarColor : new Color(0.56f, 0.79f, 0.98f);

        foreach (var game in visibleGames)
        {
            var card = Instantiate(cardPrefab, cardContainer);
            var capturedGame = game;

            card.Setup(
                game.title,
                game.thumbnail,
                game.cardColor,
                () => OnGameCardTapped(capturedGame)
            );

            // Add game name + difficulty + profile color
            string hebrewName = ParentDashboardViewModel.GetGameName(game.id);
            int difficulty = GameDifficultyConfig.GetLevel(game.id);
            card.SetupExtended(game.id, hebrewName, profileColor, difficulty);
        }
    }

    private bool _navigating;

    private void OnGameCardTapped(GameItemData game)
    {
        if (_navigating) return;
        _navigating = true;

        BackgroundMusicManager.PlayOneShot(game.nameClip);

        // Track game play in profile
        if (ProfileManager.Instance != null)
            ProfileManager.Instance.RecordGamePlayed(game.id);

        if (game.hasSubItems && game.subItems != null && game.subItems.Count > 0)
            NavigationManager.GoToSelectionMenu(game);
        else
            NavigationManager.GoToGame(game);
    }

    /// <summary>Called by the profile switch button to go back to profile selection.</summary>
    public void OnSwitchProfilePressed()
    {
        NavigationManager.GoToProfileSelection();
    }

    /// <summary>Called by the back button to return to the World scene.</summary>
    public void OnBackToWorldPressed()
    {
        NavigationManager.GoToWorld();
    }
}
