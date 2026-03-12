using UnityEngine;

/// <summary>
/// Provides the current theme color derived from the active profile's avatar color.
/// Also computes a matching text color for readability.
/// </summary>
public static class ThemeManager
{
    /// <summary>
    /// Returns the active profile's chosen color, or a default warm peach if no profile.
    /// </summary>
    public static Color HeaderColor
    {
        get
        {
            var profile = ProfileManager.ActiveProfile;
            if (profile != null)
                return profile.AvatarColor;
            return DefaultHeaderColor;
        }
    }

    /// <summary>
    /// Returns a text color that contrasts well against the header color.
    /// </summary>
    public static Color TextColor
    {
        get
        {
            return GetTextColor(HeaderColor);
        }
    }

    private static readonly Color DefaultHeaderColor = HexColor("#F8E8D8");

    /// <summary>
    /// Determines whether white or dark text is more readable on the given background.
    /// Uses relative luminance (WCAG formula).
    /// </summary>
    public static Color GetTextColor(Color bg)
    {
        float luminance = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
        return luminance > 0.6f
            ? new Color(0.2f, 0.2f, 0.2f, 1f)  // dark text on light bg
            : Color.white;                        // white text on dark bg
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
