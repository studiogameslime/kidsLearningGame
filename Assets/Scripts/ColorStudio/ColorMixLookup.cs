using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RYB-based color mixing table for kids. Always mixes 2 colors.
/// Hand-tuned results that look good and intuitive.
/// Fallback: Color.Lerp with slight brightening to avoid muddy results.
/// </summary>
public static class ColorMixLookup
{
    // Base color hex values
    public const string Red    = "#EF4444";
    public const string Blue   = "#3B82F6";
    public const string Yellow = "#FACC15";
    public const string Black  = "#1E1E1E";
    public const string White  = "#FFFFFF";

    private static readonly Dictionary<(string, string), string> Table = new Dictionary<(string, string), string>
    {
        // Primary mixes
        { (Red, Yellow),    "#FF8C00" }, // orange
        { (Red, Blue),      "#8B45A6" }, // purple
        { (Yellow, Blue),   "#4CAF50" }, // green

        // With white (lighter)
        { (Red, White),     "#FF8FAB" }, // pink
        { (Blue, White),    "#87CEEB" }, // light blue
        { (Yellow, White),  "#FFFACD" }, // cream
        { ("#FF8C00", White), "#FFDAB9" }, // peach (orange+white)
        { ("#4CAF50", White), "#90EE90" }, // light green
        { ("#8B45A6", White), "#DDA0DD" }, // lilac

        // With black (darker)
        { (Red, Black),     "#8B0000" }, // dark red
        { (Blue, Black),    "#191970" }, // midnight blue
        { (Yellow, Black),  "#808000" }, // olive
        { ("#FF8C00", Black), "#CC5500" }, // burnt orange
        { ("#4CAF50", Black), "#2E7D32" }, // dark green
        { ("#8B45A6", Black), "#4A0E4E" }, // dark purple

        // Secondary mixes
        { ("#4CAF50", Yellow), "#ADFF2F" }, // lime
        { ("#4CAF50", Blue),   "#20B2AA" }, // teal/turquoise
        { ("#8B45A6", Red),    "#C71585" }, // magenta
        { ("#FF8C00", Red),    "#FF4500" }, // red-orange
        { ("#FF8C00", Yellow), "#FFD700" }, // gold
        { ("#8B45A6", Blue),   "#4B0082" }, // indigo
        { ("#FF8FAB", White),  "#FFE4E1" }, // light pink
        { ("#87CEEB", White),  "#E0F7FA" }, // ice blue
        { ("#FF8FAB", Red),    "#E91E63" }, // hot pink
    };

    /// <summary>
    /// Mix two colors. Uses lookup table first, then falls back to
    /// Color.Lerp with brightening to avoid muddy brown.
    /// </summary>
    public static Color Mix(Color colorA, Color colorB)
    {
        string hexA = ColorToHex(colorA);
        string hexB = ColorToHex(colorB);

        // Try both orderings
        if (Table.TryGetValue((hexA, hexB), out string resultHex))
            return HexToColor(resultHex);
        if (Table.TryGetValue((hexB, hexA), out resultHex))
            return HexToColor(resultHex);

        // Fallback: lerp with slight brightening
        Color mixed = Color.Lerp(colorA, colorB, 0.5f);
        // Brighten slightly to avoid muddy results
        float brightness = (mixed.r + mixed.g + mixed.b) / 3f;
        if (brightness < 0.4f)
            mixed = Color.Lerp(mixed, Color.white, 0.15f);
        return mixed;
    }

    /// <summary>Mix using hex strings directly.</summary>
    public static string MixHex(string hexA, string hexB)
    {
        // Try lookup
        if (Table.TryGetValue((hexA, hexB), out string result)) return result;
        if (Table.TryGetValue((hexB, hexA), out result)) return result;

        // Fallback
        Color a = HexToColor(hexA);
        Color b = HexToColor(hexB);
        return ColorToHex(Mix(a, b));
    }

    public static string ColorToHex(Color c)
    {
        return $"#{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}";
    }

    public static Color HexToColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    public static readonly string[] BaseColorHexes = { Red, Blue, Yellow, Black, White };

    public static bool IsBaseColor(string hex)
    {
        foreach (var b in BaseColorHexes)
            if (b == hex) return true;
        return false;
    }
}
