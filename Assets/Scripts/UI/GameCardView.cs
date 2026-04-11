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
    /// Extended setup for main menu — adds profile-colored border, game name above, difficulty below.
    /// </summary>
    public void SetupExtended(string gameId, string hebrewName, Color profileColor, int difficulty)
    {
        // Tint the card border with profile color (soft tint)
        if (backgroundImage != null)
        {
            Color tint = Color.Lerp(Color.white, profileColor, 0.25f);
            backgroundImage.color = tint;
        }

        // ── Game name ABOVE the thumbnail ──
        if (_gameNameLabel == null)
        {
            var nameGO = new GameObject("GameNameTop");
            nameGO.transform.SetParent(transform, false);
            nameGO.transform.SetAsLastSibling();
            var nameRT = nameGO.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 1);
            nameRT.anchorMax = new Vector2(1, 1);
            nameRT.pivot = new Vector2(0.5f, 1);
            nameRT.anchoredPosition = new Vector2(0, -4);
            nameRT.sizeDelta = new Vector2(-20, 40);

            _gameNameLabel = nameGO.AddComponent<TextMeshProUGUI>();
            _gameNameLabel.fontSize = 28;
            _gameNameLabel.fontStyle = FontStyles.Bold;
            _gameNameLabel.color = new Color(0.25f, 0.25f, 0.3f);
            _gameNameLabel.alignment = TextAlignmentOptions.Center;
            _gameNameLabel.raycastTarget = false;
            _gameNameLabel.enableWordWrapping = false;
            _gameNameLabel.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (_gameNameLabel != null)
            HebrewText.SetText(_gameNameLabel, hebrewName);

        // ── Difficulty label BELOW the thumbnail ──
        if (_difficultyLabel == null)
        {
            var diffGO = new GameObject("DifficultyBottom");
            diffGO.transform.SetParent(transform, false);
            diffGO.transform.SetAsLastSibling();
            var diffRT = diffGO.AddComponent<RectTransform>();
            diffRT.anchorMin = new Vector2(0, 0);
            diffRT.anchorMax = new Vector2(1, 0);
            diffRT.pivot = new Vector2(0.5f, 0);
            diffRT.anchoredPosition = new Vector2(0, 4);
            diffRT.sizeDelta = new Vector2(0, 32);

            _difficultyLabel = diffGO.AddComponent<TextMeshProUGUI>();
            _difficultyLabel.fontSize = 22;
            _difficultyLabel.color = new Color(0.4f, 0.4f, 0.4f);
            _difficultyLabel.alignment = TextAlignmentOptions.Center;
            _difficultyLabel.raycastTarget = false;
        }

        if (_difficultyLabel != null)
        {
            string diffName;
            if (difficulty <= 3) diffName = "\u05E7\u05DC"; // קל
            else if (difficulty <= 7) diffName = "\u05D1\u05D9\u05E0\u05D5\u05E0\u05D9"; // בינוני
            else diffName = "\u05E7\u05E9\u05D4"; // קשה

            HebrewText.SetText(_difficultyLabel, "\u05E8\u05DE\u05D4 \u05E0\u05D5\u05DB\u05D7\u05D9\u05EA: " + diffName);
            // רמה נוכחית: קל/בינוני/קשה
        }
    }
}
