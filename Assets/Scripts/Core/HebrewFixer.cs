using System.Text;

/// <summary>
/// Fixes Hebrew RTL text for TextMeshPro on Android.
/// TMP's isRightToLeftText is buggy on Android — letters get scrambled.
/// Instead, we manually reverse the string and leave isRightToLeftText = false.
/// </summary>
public static class HebrewFixer
{
    /// <summary>
    /// Reverses a Hebrew string so TMP renders it in correct RTL order.
    /// Handles mixed Hebrew/Latin by reversing only the overall character order
    /// while keeping digit sequences and Latin words in correct LTR order.
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

        // Simple approach: reverse the entire string.
        // This works for pure Hebrew and Hebrew+spaces.
        // For mixed Hebrew/numbers, we reverse the whole thing
        // then re-reverse any embedded LTR runs (digits, Latin).
        var chars = input.ToCharArray();
        System.Array.Reverse(chars);

        // Re-reverse LTR runs (digits, basic Latin letters, punctuation like !)
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
