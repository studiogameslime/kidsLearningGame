using UnityEngine;
using UnityEngine.UI;
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

    private const string GallerySubItemId = "puzzle_gallery";

    private void Start()
    {
        var game = GameContext.CurrentGame;
        if (game == null)
        {
            Debug.LogError("SelectionMenuController: No game in GameContext! Did you navigate here correctly?");
            return;
        }

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

            if (item.id == GallerySubItemId)
            {
                card.Setup(
                    item.title,
                    null,
                    item.cardColor,
                    () => OpenGalleryForPuzzle(capturedGame)
                );
                StyleAsOutlinedCard(card, item.cardColor);
            }
            else
            {
                card.Setup(
                    item.title,
                    item.thumbnail,
                    item.cardColor,
                    () => NavigationManager.GoToGame(capturedGame, capturedItem)
                );
            }
        }
    }

    private void StyleAsOutlinedCard(GameCardView card, Color accentColor)
    {
        // Make frame border use the accent color (purple-ish) instead of white
        if (card.backgroundImage != null)
        {
            card.backgroundImage.color = accentColor;
            card.backgroundImage.fillCenter = false; // outline only
            card.backgroundImage.pixelsPerUnitMultiplier = 0.4f; // thicker border
        }

        // Hide the thumbnail, show placeholder area
        if (card.thumbnailImage != null)
            card.thumbnailImage.gameObject.SetActive(false);

        // Load import icon for the placeholder
        var importIcon = Resources.Load<Sprite>("Icons/import");
        if (importIcon == null)
        {
            // Try loading from Art/Icons
            var iconGO = new GameObject("GalleryIcon");
            iconGO.transform.SetParent(card.transform, false);
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.25f, 0.25f);
            iconRT.anchorMax = new Vector2(0.75f, 0.7f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.7f);
            iconImg.raycastTarget = false;

            // Create a simple "+" symbol as text
            var plusGO = new GameObject("PlusText");
            plusGO.transform.SetParent(iconGO.transform, false);
            var plusRT = plusGO.AddComponent<RectTransform>();
            plusRT.anchorMin = Vector2.zero;
            plusRT.anchorMax = Vector2.one;
            plusRT.offsetMin = Vector2.zero;
            plusRT.offsetMax = Vector2.zero;
            var plusTMP = plusGO.AddComponent<TextMeshProUGUI>();
            plusTMP.text = "+";
            plusTMP.fontSize = 120;
            plusTMP.fontStyle = FontStyles.Bold;
            plusTMP.color = Color.white;
            plusTMP.alignment = TextAlignmentOptions.Center;
            plusTMP.raycastTarget = false;

            // Hide the default placeholder icon
            if (card.placeholderIcon != null)
                card.placeholderIcon.gameObject.SetActive(false);
        }
        else
        {
            if (card.placeholderIcon != null)
            {
                card.placeholderIcon.sprite = importIcon;
                card.placeholderIcon.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.7f);
                card.placeholderIcon.gameObject.SetActive(true);
            }
        }

        // Show title for gallery card
        if (card.titleText != null)
        {
            card.titleText.gameObject.SetActive(true);
            card.titleText.isRightToLeftText = true;
            card.titleText.color = new Color(accentColor.r * 0.7f, accentColor.g * 0.7f, accentColor.b * 0.7f, 1f);
        }
    }

    private void OpenGalleryForPuzzle(GameItemData game)
    {
#if UNITY_ANDROID || UNITY_IOS
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (string.IsNullOrEmpty(path)) return;
            var tex = NativeGallery.LoadImageAtPath(path, 1024, false);
            if (tex == null) return;
            LaunchPuzzleWithTexture(game, tex);
        });
#else
        DesktopGalleryPick(game);
#endif
    }

#if !UNITY_ANDROID && !UNITY_IOS
    private void DesktopGalleryPick(GameItemData game)
    {
        // NativeGallery supports standalone builds too
        try
        {
            NativeGallery.GetImageFromGallery((path) =>
            {
                if (string.IsNullOrEmpty(path)) return;
                var tex = NativeGallery.LoadImageAtPath(path, 1024, false);
                if (tex == null) return;
                LaunchPuzzleWithTexture(game, tex);
            });
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Gallery pick failed: {e.Message}");
        }
    }
#endif

    private void LaunchPuzzleWithTexture(GameItemData game, Texture2D tex)
    {
        GameContext.CurrentGame = game;
        GameContext.CurrentSelection = null;
        GameContext.CustomTexture = tex;
        UnityEngine.SceneManagement.SceneManager.LoadScene("PuzzleGame");
    }

    /// <summary>Called by the Back button in the scene.</summary>
    public void OnBackPressed()
    {
        NavigationManager.GoToMainMenu();
    }
}
