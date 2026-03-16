/// <summary>
/// Hebrew text helper for TextMeshPro.
///
/// Previous approach: manually reversed Hebrew strings because TMP's
/// isRightToLeftText was reportedly buggy on Android.
///
/// Current approach: use TMP's built-in RTL support (isRightToLeftText = true).
/// This class is now a passthrough — Fix() returns text unchanged.
/// HebrewRTLEnforcer singleton sets isRightToLeftText = true on all TMP objects.
///
/// Fix() is kept as a no-op so existing call sites don't need to change.
/// </summary>
public static class HebrewFixer
{
    /// <summary>
    /// Returns the input unchanged. TMP handles RTL natively via isRightToLeftText.
    /// Kept for backward compatibility — all existing call sites continue to work.
    /// </summary>
    public static string Fix(string input) => input;
}
