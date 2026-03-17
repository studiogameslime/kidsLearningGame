using System.Text;
using UnityEngine;

/// <summary>
/// Fixes Hebrew RTL text for TextMeshPro on mobile builds (Android/iOS).
///
/// WHY THIS EXISTS:
/// In the Unity Editor, TMP's isRightToLeftText=true works because TMP can access
/// the source font's OpenType tables for proper RTL shaping. On mobile builds,
/// TMP only uses the baked SDF atlas which lacks shaping data, causing Hebrew
/// characters to be spaced incorrectly or reordered.
///
/// SOLUTION:
/// Manually reverse Hebrew strings so they display correctly in LTR mode.
/// Always set isRightToLeftText = false when using this fixer.
///
/// USAGE:
/// All text is fixed at the source — setup scripts and controllers call Fix()
/// exactly once when assigning text. No auto-fixers, no runtime monitoring.
/// </summary>
public static class HebrewFixer
{
    /// <summary>
    /// Reverses a Hebrew string so TMP renders it in correct visual RTL order.
    /// Handles mixed Hebrew/Latin/digits by reversing Hebrew runs while keeping
    /// LTR content (numbers, Latin words) in correct reading order.
    ///
    /// Call EXACTLY ONCE per string assignment. Do not double-call.
    ///
    /// In Editor: returns input unchanged (TMP handles RTL natively).
    /// In Build: reverses Hebrew for correct mobile rendering.
    /// </summary>
    public static string Fix(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

#if UNITY_EDITOR && !HEBREW_FORCE_FIX
        // In editor, TMP handles RTL natively — no reversal needed
        return input;
#else
        // On device builds, manually reverse for correct rendering
        return ReverseHebrew(input);
#endif
    }

    /// <summary>
    /// Always reverses, regardless of platform. Use for testing in editor.
    /// </summary>
    public static string ForceReverse(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return ReverseHebrew(input);
    }

    private static string ReverseHebrew(string input)
    {
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

        // Re-reverse LTR runs (digits, Latin letters, punctuation)
        // so they stay in correct left-to-right reading order
        var sb = new StringBuilder(chars.Length);
        int i = 0;
        while (i < chars.Length)
        {
            if (IsLTR(chars[i]))
            {
                int start = i;
                while (i < chars.Length && (IsLTR(chars[i]) || chars[i] == ' ' && i + 1 < chars.Length && IsLTR(chars[i + 1])))
                    i++;

                // Reverse this LTR run back to correct order
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
        if (c == '.' || c == ',' || c == '!' || c == '?' || c == ':' || c == '/' || c == '-' || c == '%') return true;
        // Unicode arrows and math symbols should stay LTR
        if (c == '\u2190' || c == '\u2191' || c == '\u2192' || c == '\u2193' || c == '\u2194') return true;
        // Bullet
        if (c == '\u2022') return true;
        return false;
    }
}
