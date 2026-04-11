using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

/// <summary>
/// View component for a single card in the menu grid.
/// Used for both main-menu game cards and sub-selection cards.
/// Assign the internal references in the prefab; call Setup() at runtime.
/// </summary>
public class GameCardView : MonoBehaviour
{
    [Header("References (set in prefab)")]
    public Image backgroundImage;
    public Image thumbnailImage;
    public TextMeshProUGUI titleText;
    public Button button;

    [Header("Placeholder styling")]
    [Tooltip("Icon shown when no thumbnail is assigned.")]
    public Image placeholderIcon;

    // Runtime-created labels
    private TextMeshProUGUI _gameNameLabel;
    private TextMeshProUGUI _difficultyLabel;

    /// <summary>
    /// Configures the card for display. Called by the menu controller.
    /// </summary>
    public void Setup(string title, Sprite thumbnail, Color cardColor, UnityAction onClick)
    {
        // Title (legacy — hidden in main menu, visible in sub-selection)
        if (titleText != null)
            HebrewText.SetText(titleText, title);

        // Thumbnail
        if (thumbnail != null)
        {
            if (thumbnailImage != null)
            {
                thumbnailImage.sprite = thumbnail;
                thumbnailImage.gameObject.SetActive(true);
            }
            if (placeholderIcon != null)
                placeholderIcon.gameObject.SetActive(false);
        }
        else
        {
            if (thumbnailImage != null)
                thumbnailImage.gameObject.SetActive(false);
            if (placeholderIcon != null)
                placeholderIcon.gameObject.SetActive(true);
        }

        // Tap handler
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
        }
    }

    /// <summary>
    /// Extended setup for main menu — adds profile-colored border, game name, and difficulty.
    /// </summary>
    public void SetupExtended(string gameId, string hebrewName, Color profileColor, int difficulty)
    {
        // Tint the card border with profile color (soft tint)
        if (backgroundImage != null)
        {
            Color tint = Color.Lerp(Color.white, profileColor, 0.25f);
            backgroundImage.color = tint;
        }

        // Game name label at bottom
        if (_gameNameLabel == null && thumbnailImage != null)
        {
            var nameGO = new GameObject("GameName");
            nameGO.transform.SetParent(transform, false);
            var nameRT = nameGO.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0);
            nameRT.anchorMax = new Vector2(1, 0);
            nameRT.pivot = new Vector2(0.5f, 0);
            nameRT.anchoredPosition = new Vector2(0, 4);
            nameRT.sizeDelta = new Vector2(0, 28);

            // Semi-transparent background strip
            var stripGO = new GameObject("Strip");
            stripGO.transform.SetParent(nameGO.transform, false);
            var stripRT = stripGO.AddComponent<RectTransform>();
            stripRT.anchorMin = Vector2.zero;
            stripRT.anchorMax = Vector2.one;
            stripRT.offsetMin = Vector2.zero;
            stripRT.offsetMax = Vector2.zero;
            var stripImg = stripGO.AddComponent<Image>();
            stripImg.color = new Color(0, 0, 0, 0.45f);
            stripImg.raycastTarget = false;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(nameGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(6, 0);
            textRT.offsetMax = new Vector2(-6, 0);

            _gameNameLabel = textGO.AddComponent<TextMeshProUGUI>();
            _gameNameLabel.fontSize = 18;
            _gameNameLabel.color = Color.white;
            _gameNameLabel.alignment = TextAlignmentOptions.Center;
            _gameNameLabel.raycastTarget = false;
            _gameNameLabel.enableWordWrapping = false;
            _gameNameLabel.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (_gameNameLabel != null)
        {
            // Difficulty label inline
            string diffText = "";
            if (difficulty <= 3) diffText = " \u00B7 \u05E7\u05DC"; // · קל
            else if (difficulty <= 7) diffText = " \u00B7 \u05D1\u05D9\u05E0\u05D5\u05E0\u05D9"; // · בינוני
            else diffText = " \u00B7 \u05E7\u05E9\u05D4"; // · קשה

            HebrewText.SetText(_gameNameLabel, hebrewName + diffText);
        }
    }
}
