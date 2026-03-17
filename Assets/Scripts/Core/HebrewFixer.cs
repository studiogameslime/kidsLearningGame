using System.Text;

/// <summary>
/// Fixes Hebrew RTL text for TextMeshPro on ALL platforms.
///
/// TMP's isRightToLeftText has inconsistent behavior between Editor and mobile
/// builds. To guarantee identical rendering everywhere, we use a single pipeline:
///
///   1. HebrewFixer.Fix() reverses Hebrew text (ALWAYS, on all platforms)
///   2. isRightToLeftText = false (ALWAYS, on all platforms)
///   3. TMP renders the pre-reversed text in LTR mode
///
/// This produces correct visual output because reversed Hebrew characters
/// displayed left-to-right appear in the correct right-to-left reading order.
///
/// Mixed content (Hebrew + numbers/Latin) is handled by re-reversing LTR runs
/// so digits and Latin words remain in their natural left-to-right order.
///
/// RULES:
/// - Call Fix() exactly ONCE per string assignment
/// - Always set isRightToLeftText = false
/// - Never use TMP's native RTL mode
/// </summary>
public static class HebrewFixer
{
    /// <summary>
    /// Reverses Hebrew text for correct visual display in TMP's LTR mode.
    /// Handles mixed Hebrew/Latin/digits. Call exactly once per assignment.
    /// </summary>
    public static string Fix(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Skip if no Hebrew characters present
        bool hasHebrew = false;
        for (int h = 0; h < input.Length; h++)
        {
            if (input[h] >= '\u0590' && input[h] <= '\u05FF') { hasHebrew = true; break; }
        }
        if (!hasHebrew) return input;

        // Reverse the entire string
        var chars = input.ToCharArray();
        System.Array.Reverse(chars);

        // Re-reverse embedded LTR runs (digits, Latin, punctuation)
        // so they display in correct left-to-right order
        var sb = new StringBuilder(chars.Length);
        int i = 0;
        while (i < chars.Length)
        {
            if (IsLTR(chars[i]))
            {
                int start = i;
                // Collect contiguous LTR chars (including spaces between LTR chars)
                while (i < chars.Length && (IsLTR(chars[i]) || (chars[i] == ' ' && i + 1 < chars.Length && IsLTR(chars[i + 1]))))
                    i++;

                // Write this run in original (un-reversed) order
                for (int j = i - 1; j >= start; j--)
                    sb.Append(chars[j]);
            }
            else
            {
                sb.Append(chars[i]);
                i++;
            }
        }

        return sb.ToString();
    }

    private static bool IsLTR(char c)
    {
        if (c >= '0' && c <= '9') return true;
        if (c >= 'A' && c <= 'Z') return true;
        if (c >= 'a' && c <= 'z') return true;
        if (c == '.' || c == ',' || c == '!' || c == '?' || c == ':' || c == ';') return true;
        if (c == '/' || c == '-' || c == '+' || c == '=' || c == '%' || c == '#') return true;
        // Unicode arrows and bullets
        if (c >= '\u2190' && c <= '\u2199') return true; // arrows
        if (c == '\u2022') return true; // bullet •
        if (c == '\u2013' || c == '\u2014') return true; // en-dash, em-dash
        return false;
    }
}
