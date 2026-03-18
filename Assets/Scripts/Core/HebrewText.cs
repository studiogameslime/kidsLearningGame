using TMPro;

/// <summary>
/// Hebrew RTL text utility for TextMeshPro.
/// Uses TMP's native isRightToLeftText flag with pre-processing to keep
/// numbers, punctuation sequences, and Latin runs in correct LTR order.
///
/// TMP's isRightToLeftText reverses ALL characters. We pre-reverse LTR runs
/// (digits, Latin, punctuation groups) so they survive the reversal and
/// display correctly within RTL text.
///
/// Usage: HebrewText.SetText(tmp, "ברירת מחדל: 16 קלפים");
/// </summary>
public static class HebrewText
{
    /// <summary>
    /// Assign text to a TMP component with proper RTL + number support.
    /// </summary>
    public static void SetText(TMP_Text tmp, string text)
    {
        if (tmp == null) return;

        if (string.IsNullOrEmpty(text))
        {
            tmp.isRightToLeftText = false;
            tmp.text = text ?? "";
            return;
        }

        if (!ContainsHebrew(text))
        {
            tmp.isRightToLeftText = false;
            tmp.text = text;
            return;
        }

        tmp.isRightToLeftText = true;
        tmp.text = PreReverseLTRRuns(text);
    }

    /// <summary>
    /// Pre-reverses LTR runs (digits, Latin letters, and adjacent punctuation)
    /// so they display correctly after TMP's full RTL reversal.
    /// Example: "גיל: 16 קלפים" → "גיל: 61 םיפלק" → TMP reverses → "קלפים 16 :גיל"
    /// </summary>
    private static string PreReverseLTRRuns(string text)
    {
        char[] result = text.ToCharArray();
        int i = 0;

        while (i < result.Length)
        {
            if (IsLTRChar(result[i]))
            {
                // Find the full LTR run (digits/Latin + connecting punctuation like . , - : /)
                int start = i;
                while (i < result.Length && (IsLTRChar(result[i]) || IsLTRConnector(result[i], result, i)))
                    i++;

                // Trim trailing connectors (they belong to RTL context)
                while (i > start && !IsLTRChar(result[i - 1]))
                    i--;

                // Reverse this run in-place
                if (i - start > 1)
                    System.Array.Reverse(result, start, i - start);
            }
            else
            {
                i++;
            }
        }

        return new string(result);
    }

    /// <summary>Returns true for characters that should stay in LTR order (digits, Latin).</summary>
    private static bool IsLTRChar(char c)
    {
        return (c >= '0' && c <= '9')
            || (c >= 'A' && c <= 'Z')
            || (c >= 'a' && c <= 'z');
    }

    /// <summary>
    /// Returns true for punctuation that connects LTR tokens (e.g. "3.14", "10-20", "2:30").
    /// Only treated as connector if surrounded by LTR chars.
    /// </summary>
    private static bool IsLTRConnector(char c, char[] text, int index)
    {
        if (c != '.' && c != ',' && c != '-' && c != ':' && c != '/' && c != '%')
            return false;

        // Must have LTR char after it to be a connector
        return index + 1 < text.Length && IsLTRChar(text[index + 1]);
    }

    public static bool ContainsHebrew(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if ((c >= '\u0590' && c <= '\u05FF') || (c >= '\uFB1D' && c <= '\uFB4F'))
                return true;
        }
        return false;
    }
}
