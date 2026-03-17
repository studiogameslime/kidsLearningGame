/// <summary>
/// Hebrew text pipeline using RTLTMPro.
///
/// RTLTMPro implements the Unicode Bidirectional Algorithm properly,
/// handling Hebrew, numbers, punctuation, math, and mixed content
/// identically on Editor, Android, and iOS.
///
/// This class wraps RTLTMPro.RTLSupport.Fix() behind the existing
/// HebrewFixer.Fix() API so all 70+ call sites continue to work
/// without any changes.
///
/// Pipeline:
///   1. Text is stored in logical order (as typed)
///   2. Fix() converts to visual order using RTLTMPro's bidi algorithm
///   3. TMP renders with isRightToLeftText = false
///   4. Correct display on all platforms
///
/// All existing HebrewFixer.Fix() and H() call sites work unchanged.
/// </summary>
public static class HebrewFixer
{
    /// <summary>
    /// Converts logical-order Hebrew text to visual-order for TMP rendering.
    /// Uses RTLTMPro's Unicode Bidirectional Algorithm.
    /// Handles Hebrew, numbers, punctuation, math, mixed Hebrew/English.
    /// Call exactly once per text assignment.
    /// </summary>
    public static string Fix(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Quick check: skip if no Hebrew characters
        bool hasHebrew = false;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if ((c >= '\u0590' && c <= '\u05FF') || (c >= '\uFB1D' && c <= '\uFB4F'))
            {
                hasHebrew = true;
                break;
            }
        }
        if (!hasHebrew) return input;

        // RTLTMPro handles the full Unicode Bidirectional Algorithm:
        // - Hebrew characters reversed into visual order
        // - Numbers kept in LTR order
        // - Punctuation placed according to bidi rules
        // - Mixed Hebrew/English handled correctly
        // - Bracket/parenthesis mirroring
        return RTLTMPro.RTLSupport.Fix(input, false, true);
    }
}
