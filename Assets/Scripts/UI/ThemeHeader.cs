using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to a TopBar/Header GameObject. On Start, applies the active profile's
/// theme color to the bar background and adjusts title text + icon colors.
/// </summary>
public class ThemeHeader : MonoBehaviour
{
    private void Start()
    {
        // Apply theme color to header background
        var img = GetComponent<Image>();
        if (img != null)
            img.color = ThemeManager.HeaderColor;

        Color textColor = ThemeManager.TextColor;

        // Tint all text in the header
        foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
            tmp.color = textColor;

        // Icon-only buttons always stay white (icons are designed for colored header backgrounds)
        foreach (Transform child in transform)
        {
            var btn = child.GetComponent<Button>();
            if (btn == null) continue;

            var btnImg = child.GetComponent<Image>();
            if (btnImg == null) continue;

            var label = child.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null)
                btnImg.color = Color.white;
        }
    }
}
