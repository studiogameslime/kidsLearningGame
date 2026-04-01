using UnityEngine;

/// <summary>
/// Centralized UI constants for game buttons, feedback colors, and styling.
/// All game controllers should reference these instead of hardcoding colors.
/// </summary>
public static class GameUIConstants
{
    // ── Button Background ─────────────────────────────────────
    public static readonly Color ButtonBackground = HexColor("#E3F2FD");      // light blue
    public static readonly Color ButtonBorder = HexColor("#90CAF9");          // medium blue
    public static readonly Color ButtonTextColor = HexColor("#1565C0");       // dark blue

    // ── Button States ─────────────────────────────────────────
    public static readonly Color ButtonHighlighted = HexColor("#BBDEFB");
    public static readonly Color ButtonPressed = HexColor("#90CAF9");

    // ── Card/Tile Background ──────────────────────────────────
    public static readonly Color CardBackground = Color.white;
    public static readonly Color CardBorder = HexColor("#E0E0E0");           // light gray

    // ── Feedback Colors ───────────────────────────────────────
    public static readonly Color CorrectColor = HexColor("#C8E6C9");         // soft green
    public static readonly Color WrongColor = HexColor("#FFCDD2");           // soft red

    // ── Font Sizes ────────────────────────────────────────────
    public const int ButtonFontSize = 72;

    // ── Border ────────────────────────────────────────────────
    public const float BorderThickness = 4f;

    // ── Hint Timing ───────────────────────────────────────────
    public const float HintDelay = 5f;

    // ── Helper ────────────────────────────────────────────────
    private static Color HexColor(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString(hex, out c);
        return c;
    }
}
