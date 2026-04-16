using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the main-menu scene: reads the GameDatabase and spawns a card for each game.
/// Detects new games added in app updates and shows them at the top with a "New!" badge.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    private const string KnownGamesKey = "known_game_ids";
    private const string NewGamePrefix = "new_game_discovered_";
    private const int NewBadgeDays = 7;
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

        DetectNewGames();
        PopulateGrid();
        UpdateProfileButton();
    }

    // ── New game detection ──

    private HashSet<string> _newGameIds = new HashSet<string>();

    private void DetectNewGames()
    {
        if (database == null) return;

        string knownRaw = PlayerPrefs.GetString(KnownGamesKey, "");

        if (string.IsNullOrEmpty(knownRaw))
        {
            // First time — save all current IDs as known, EXCEPT halfpuzzle (treat it as new)
            var ids = new List<string>();
            foreach (var game in database.games)
            {
                if (game == null) continue;
                if (game.id == "halfpuzzle") continue; // intentionally excluded so it shows as new
                ids.Add(game.id);
            }
            PlayerPrefs.SetString(KnownGamesKey, string.Join(",", ids));

            // Mark halfpuzzle as discovered now
            if (database.games.Exists(g => g != null && g.id == "halfpuzzle"))
            {
                PlayerPrefs.SetString(NewGamePrefix + "halfpuzzle", DateTime.UtcNow.ToString("o"));
                _newGameIds.Add("halfpuzzle");
            }

            PlayerPrefs.Save();
            return;
        }

        // Parse known IDs
        var knownSet = new HashSet<string>(knownRaw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        bool changed = false;

        foreach (var game in database.games)
        {
            if (game == null) continue;

            if (!knownSet.Contains(game.id))
            {
                // New game found in this update
                knownSet.Add(game.id);
                PlayerPrefs.SetString(NewGamePrefix + game.id, DateTime.UtcNow.ToString("o"));
                changed = true;
            }

            // Check if this game has an active "new" badge (discovered < 7 days ago)
            string discoveredStr = PlayerPrefs.GetString(NewGamePrefix + game.id, "");
            if (!string.IsNullOrEmpty(discoveredStr)
                && DateTime.TryParse(discoveredStr, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime discovered))
            {
                if ((DateTime.UtcNow - discovered).TotalDays < NewBadgeDays)
                    _newGameIds.Add(game.id);
            }
        }

        if (changed)
        {
            PlayerPrefs.SetString(KnownGamesKey, string.Join(",", knownSet));
            PlayerPrefs.Save();
        }
    }

    private bool IsNewGame(string gameId)
    {
        return _newGameIds.Contains(gameId);
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

        // New-in-app games first, then the rest — both groups keep their original order
        var newGames = new List<GameItemData>();
        var regularGames = new List<GameItemData>();
        foreach (var g in visibleGames)
            (IsNewGame(g.id) ? newGames : regularGames).Add(g);
        var ordered = new List<GameItemData>(newGames.Count + regularGames.Count);
        ordered.AddRange(newGames);
        ordered.AddRange(regularGames);

        foreach (var game in ordered)
        {
            var card = Instantiate(cardPrefab, cardContainer);
            var capturedGame = game;

            card.Setup(
                game.title,
                game.thumbnail,
                game.cardColor,
                () => OnGameCardTapped(capturedGame)
            );

            // Add game name + difficulty + profile color + new badge
            string hebrewName = ParentDashboardViewModel.GetGameName(game.id);
            int difficulty = GameDifficultyConfig.GetLevel(game.id);
            bool isNew = IsNewGame(game.id);
            card.SetupExtended(game.id, hebrewName, profileColor, difficulty, isNew);
        }
    }

    private bool _navigating;

    private void OnGameCardTapped(GameItemData game)
    {
        if (_navigating) return;

        BackgroundMusicManager.PlayOneShot(game.nameClip);

        // Track game play in profile
        if (ProfileManager.Instance != null)
            ProfileManager.Instance.RecordGamePlayed(game.id);

        // 2-player games show partner selection first
        if (TwoPlayerManager.SupportsMultiplayer(game.id))
        {
            TwoPlayerSetupFlow.Start(this, game, (g) =>
            {
                _navigating = true;
                if (g.hasSubItems && g.subItems != null && g.subItems.Count > 0)
                    NavigationManager.GoToSelectionMenu(g);
                else
                    NavigationManager.GoToGame(g);
            });
            return;
        }

        _navigating = true;
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
