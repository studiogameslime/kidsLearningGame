/// <summary>
/// Hebrew text pipeline for TextMeshPro.
///
/// Strategy: use TMP's native isRightToLeftText = true with Dynamic font atlas.
///
/// Why this works:
///   - Dynamic atlas mode makes TMP generate glyphs from the source TTF at runtime
///   - Same font shaping behavior on Editor, Android, and iOS
///   - TMP's isRightToLeftText handles Hebrew/LTR mixing natively
///   - No manual string reversal needed
///
/// Previous issue: Static font atlas had baked metrics that differed from
/// editor's runtime rendering, causing spacing issues on mobile.
///
/// Fix() is a passthrough — kept for backward compatibility with all call sites.
/// </summary>
public static class HebrewFixer
{
    /// <summary>
    /// Returns input unchanged. TMP handles RTL natively with Dynamic font atlas.
    /// Kept so existing call sites don't need modification.
    /// </summary>
    public static string Fix(string input) => input;
}
