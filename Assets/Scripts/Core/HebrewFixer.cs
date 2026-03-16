using System.Text;

/// <summary>
/// Fixes Hebrew RTL text for TextMeshPro on all platforms (especially Android/iOS).
/// TMP's isRightToLeftText is buggy on mobile — letters get scrambled or spaced.
/// Instead, we manually reverse the string and leave isRightToLeftText = false.
///
/// IMPORTANT: Always set tmp.isRightToLeftText = false when using this fixer.
/// Call Fix() exactly ONCE per string — do NOT double-fix.
/// </summary>
public static class HebrewFixer
{
    /// <summary>
    /// Reverses a Hebrew string so TMP renders it in correct RTL order.
    /// Handles mixed Hebrew/Latin by reversing only the overall character order
    /// while keeping digit sequences and Latin words in correct LTR order.
    ///
    /// WARNING: Call this exactly once per string. Calling it twice will
    /// double-reverse and produce broken output.
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

        // Reverse the entire string, then re-reverse embedded LTR runs.
        var chars = input.ToCharArray();
        System.Array.Reverse(chars);

        // Re-reverse LTR runs (digits, basic Latin letters, punctuation)
        var sb = new StringBuilder(chars.Length);
        int i = 0;
        while (i < chars.Length)
        {
            if (IsLTR(chars[i]))
            {
                // Collect the LTR run
                int start = i;
                while (i < chars.Length && (IsLTR(chars[i]) || chars[i] == ' ' && i + 1 < chars.Length && IsLTR(chars[i + 1])))
                    i++;

                // Reverse this run back to LTR order
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
        // Digits
        if (c >= '0' && c <= '9') return true;
        // Basic Latin letters
        if (c >= 'A' && c <= 'Z') return true;
        if (c >= 'a' && c <= 'z') return true;
        // Common punctuation that belongs with LTR runs
        if (c == '.' || c == ',' || c == '!' || c == '?' || c == ':' || c == '/' || c == '-') return true;
        return false;
    }
}
