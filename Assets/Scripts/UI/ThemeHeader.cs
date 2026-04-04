using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to a TopBar/Header GameObject. On Start, applies the active profile's
/// theme color to the bar background, adjusts title text + icon colors, and
/// normalizes the TopBar layout for consistency across all scenes.
/// </summary>
public class ThemeHeader : MonoBehaviour
{
    private const float StandardHeight = 100f;
    private const float TitleWidthInset = -200f;

    private void Start()
    {
        NormalizeLayout();

        // Auto-add edge extender so header background extends beyond safe area
        if (GetComponent<SafeAreaEdgeExtender>() == null)
            gameObject.AddComponent<SafeAreaEdgeExtender>();

        ApplyThemeColors();
    }

    private void NormalizeLayout()
    {
        var rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            // Ensure standard height (100px) — anchored to top, stretched full width
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, StandardHeight);
        }

        // Background should not block raycasts on game area below
        var img = GetComponent<Image>();
        if (img != null)
            img.raycastTarget = false;

        // Normalize title text: centered, standard width constraint, white
        foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            // Skip button labels — only normalize the main title
            if (tmp.transform.parent != transform) continue;

            var titleRt = tmp.GetComponent<RectTransform>();
            if (titleRt != null)
            {
                titleRt.anchorMin = Vector2.zero;
                titleRt.anchorMax = Vector2.one;
                titleRt.anchoredPosition = Vector2.zero;
                titleRt.sizeDelta = new Vector2(TitleWidthInset, 0f);
                titleRt.pivot = new Vector2(0.5f, 0.5f);
            }
        }
    }

    private void ApplyThemeColors()
    {
        // Apply theme color to header background
        var img = GetComponent<Image>();
        if (img != null)
            img.color = ThemeManager.HeaderColor;

        // Propagate to edge extender (extends header background beyond safe area)
        var extender = GetComponent<SafeAreaEdgeExtender>();
        if (extender != null)
            extender.SetColor(ThemeManager.HeaderColor);

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
