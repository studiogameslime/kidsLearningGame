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

    /// <summary>
    /// Configures the card for display. Called by the menu controller.
    /// </summary>
    public void Setup(string title, Sprite thumbnail, Color cardColor, UnityAction onClick)
    {
        // Title
        if (titleText != null)
            HebrewText.SetText(titleText, title);

        // Background color — no longer used (frame is always black)

        // Thumbnail — show image if available, otherwise show placeholder icon area
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
}
