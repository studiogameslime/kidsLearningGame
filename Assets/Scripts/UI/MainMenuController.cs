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

        foreach (var game in visibleGames)
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
