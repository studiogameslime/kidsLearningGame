using UnityEngine;

/// <summary>
/// Validation tests for HebrewFixer. Attach to any GameObject and check console.
/// Remove after verification.
/// </summary>
public class HebrewFixerTest : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("=== HebrewFixer Validation ===");

        // Pure Hebrew — should display right-to-left
        Test("אזור הורים", "םירוה רוזא");
        Test("קטגוריות התפתחות", "תוחתפתה תוירוגטק");

        // Hebrew + number — number should stay as "2", not reversed
        Test("יש לך 2 משחקים", "םיקחשמ 2 ךל שי");

        // Hebrew + punctuation — colon and text should be readable
        Test("בחר צבע: אדום", "םודא :עבצ רחב");

        // Math expression — "? = 5 + 4" should show "4 + 5 = ?" in visual order
        // Actually this is all LTR (digits, operators, ?) — stays as-is within RTL context
        var mathResult = HebrewFixer.Fix("? = 5 + 4");
        Debug.Log($"  Math: '? = 5 + 4' → '{mathResult}'");

        // Mixed Hebrew/English — "Play" should stay readable, Hebrew reversed
        var mixedResult = HebrewFixer.Fix("לחץ Play כדי להתחיל");
        Debug.Log($"  Mixed: 'לחץ Play כדי להתחיל' → '{mixedResult}'");
        // Visual order should show Hebrew reversed with "Play" intact in the middle

        // Number at boundary with bullet
        var bulletResult = HebrewFixer.Fix("מתן • גיל 4");
        Debug.Log($"  Bullet: 'מתן • גיל 4' → '{bulletResult}'");

        // Pure numbers — should pass through unchanged
        Test("12345", "12345");

        // Pure English — should pass through unchanged
        Test("Hello World", "Hello World");

        Debug.Log("=== HebrewFixer Validation Complete ===");
    }

    private void Test(string input, string expected)
    {
        string result = HebrewFixer.Fix(input);
        bool pass = result == expected;
        string status = pass ? "PASS" : "FAIL";
        Debug.Log($"  [{status}] '{input}' → '{result}'" + (pass ? "" : $" (expected '{expected}')"));
    }
}
