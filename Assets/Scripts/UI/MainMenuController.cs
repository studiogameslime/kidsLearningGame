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

        PopulateGrid();
        UpdateProfileButton();
    }

    private void UpdateProfileButton()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        // Set avatar color
        if (profileButtonImage != null)
            profileButtonImage.color = profile.AvatarColor;

        // Try to load custom avatar photo
        if (!string.IsNullOrEmpty(profile.avatarImagePath) && profileButtonPhoto != null)
        {
            string fullPath = System.IO.Path.Combine(Application.persistentDataPath, profile.avatarImagePath);
            if (System.IO.File.Exists(fullPath))
            {
                var bytes = System.IO.File.ReadAllBytes(fullPath);
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes))
                {
                    profileButtonPhoto.sprite = Sprite.Create(tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f));
                    profileButtonPhoto.gameObject.SetActive(true);
                    if (profileButtonInitial != null)
                        profileButtonInitial.gameObject.SetActive(false);
                    // Hide color — photo fills the whole circle
                    if (profileButtonImage != null)
                        profileButtonImage.color = Color.white;
                    return;
                }
            }
        }

        // No photo — show initial
        if (profileButtonPhoto != null)
            profileButtonPhoto.gameObject.SetActive(false);

        if (profileButtonInitial != null)
        {
            profileButtonInitial.gameObject.SetActive(true);
            profileButtonInitial.text = profile.Initial;
        }
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
}
