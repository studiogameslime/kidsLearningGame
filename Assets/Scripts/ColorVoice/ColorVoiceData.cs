using UnityEngine;

/// <summary>
/// Data model for a single color prompt in the Color Voice game.
/// </summary>
[System.Serializable]
public class ColorPrompt
{
    public string id;
    public string hebrewName;
    public Color color;
    public string[] acceptedAnswers;
}

/// <summary>
/// Static registry of all color prompts for the Color Voice game.
/// </summary>
public static class ColorVoiceData
{
    public static readonly ColorPrompt[] Colors = {
        new ColorPrompt {
            id = "red",
            hebrewName = "\u05D0\u05D3\u05D5\u05DD", // אדום
            color = HexColor("#FF4444"),
            acceptedAnswers = new[] { "\u05D0\u05D3\u05D5\u05DD", "\u05D0\u05D3\u05D5\u05DD\u05D4" } // אדום, אדומה
        },
        new ColorPrompt {
            id = "blue",
            hebrewName = "\u05DB\u05D7\u05D5\u05DC", // כחול
            color = HexColor("#4488FF"),
            acceptedAnswers = new[] { "\u05DB\u05D7\u05D5\u05DC", "\u05DB\u05D7\u05D5\u05DC\u05D4" } // כחול, כחולה
        },
        new ColorPrompt {
            id = "green",
            hebrewName = "\u05D9\u05E8\u05D5\u05E7", // ירוק
            color = HexColor("#44BB44"),
            acceptedAnswers = new[] { "\u05D9\u05E8\u05D5\u05E7", "\u05D9\u05E8\u05D5\u05E7\u05D4" } // ירוק, ירוקה
        },
        new ColorPrompt {
            id = "yellow",
            hebrewName = "\u05E6\u05D4\u05D5\u05D1", // צהוב
            color = HexColor("#FFD600"),
            acceptedAnswers = new[] { "\u05E6\u05D4\u05D5\u05D1", "\u05E6\u05D4\u05D5\u05D1\u05D4" } // צהוב, צהובה
        },
        new ColorPrompt {
            id = "orange",
            hebrewName = "\u05DB\u05EA\u05D5\u05DD", // כתום
            color = HexColor("#FF8844"),
            acceptedAnswers = new[] { "\u05DB\u05EA\u05D5\u05DD", "\u05DB\u05EA\u05D5\u05DE\u05D4" } // כתום, כתומה
        },
        new ColorPrompt {
            id = "pink",
            hebrewName = "\u05D5\u05E8\u05D5\u05D3", // ורוד
            color = HexColor("#FF88BB"),
            acceptedAnswers = new[] { "\u05D5\u05E8\u05D5\u05D3", "\u05D5\u05E8\u05D5\u05D3\u05D4" } // ורוד, ורודה
        },
        new ColorPrompt {
            id = "purple",
            hebrewName = "\u05E1\u05D2\u05D5\u05DC", // סגול
            color = HexColor("#AA44FF"),
            acceptedAnswers = new[] { "\u05E1\u05D2\u05D5\u05DC", "\u05E1\u05D2\u05D5\u05DC\u05D4" } // סגול, סגולה
        },
    };

    /// <summary>
    /// Normalize Hebrew text for comparison: trim, remove nikud diacritics.
    /// </summary>
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char c in text.Trim())
        {
            // Skip Hebrew nikud range (U+0591–U+05C7)
            if (c >= '\u0591' && c <= '\u05C7') continue;
            // Skip Unicode directional marks and zero-width characters
            // that Android speech recognition may embed
            if (c == '\u200E' || c == '\u200F') continue; // LRM, RLM
            if (c == '\u200B' || c == '\u200C' || c == '\u200D') continue; // zero-width spaces/joiners
            if (c >= '\u202A' && c <= '\u202E') continue; // directional formatting
            if (c >= '\u2066' && c <= '\u2069') continue; // directional isolates
            if (c == '\uFEFF') continue; // BOM / zero-width no-break space
            if (c == '\u00A0') { sb.Append(' '); continue; } // non-breaking space → regular space
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Check if any of the recognized results match the target color.
    /// Returns true if any result contains an accepted answer.
    /// </summary>
    public static bool IsMatch(ColorPrompt target, string[] recognizedResults)
    {
        if (recognizedResults == null) return false;
        foreach (var result in recognizedResults)
        {
            string normalized = Normalize(result);
            foreach (var answer in target.acceptedAnswers)
            {
                if (normalized.Contains(Normalize(answer)))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Find which color was said (if any), useful for partial matching.
    /// Returns the matched ColorPrompt or null.
    /// </summary>
    public static ColorPrompt FindSpokenColor(string[] recognizedResults)
    {
        if (recognizedResults == null) return null;
        foreach (var color in Colors)
        {
            if (IsMatch(color, recognizedResults))
                return color;
        }
        return null;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
