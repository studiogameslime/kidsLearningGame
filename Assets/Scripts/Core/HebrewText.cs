using TMPro;

/// <summary>
/// Hebrew RTL text utility for TextMeshPro.
/// Uses TMP's native isRightToLeftText flag — no external bidi algorithm.
///
/// Usage: HebrewText.SetText(tmp, "שלום עולם");
/// </summary>
public static class HebrewText
{
    /// <summary>
    /// Assign text to a TMP component with proper RTL support.
    /// Hebrew text sets isRightToLeftText = true. No bidi transformation applied.
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

        bool hasHebrew = ContainsHebrew(text);
        tmp.isRightToLeftText = hasHebrew;
        tmp.text = text;
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
