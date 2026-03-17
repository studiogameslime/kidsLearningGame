using System.Text;
using System.Collections.Generic;

/// <summary>
/// Production-safe Hebrew/RTL text fixer for TextMeshPro.
///
/// Implements a simplified Unicode Bidirectional Algorithm (UBiDi) that correctly
/// handles mixed Hebrew + Latin + numbers + punctuation + math operators.
///
/// Architecture:
///   1. Input: logical-order string (as typed/stored)
///   2. Fix() applies bidi reordering to produce visual-order string
///   3. TMP renders with isRightToLeftText = false
///   4. Result: correct visual display on ALL platforms
///
/// Why this exists:
///   - TMP's isRightToLeftText does naive reversal, breaks mixed content on mobile
///   - Full string reversal breaks numbers, English words, math expressions
///   - This implements proper bidi: Hebrew runs are reversed, LTR runs preserved
///
/// Handles correctly:
///   - Pure Hebrew: "אזור הורים"
///   - Hebrew + numbers: "יש לך 2 משחקים"
///   - Hebrew + punctuation: "בחר צבע: אדום"
///   - Math expressions: "? = 5 + 4"
///   - Mixed Hebrew/English: "לחץ Play כדי להתחיל"
///   - Numbers at boundaries: "גיל 4 • מתן"
///
/// Usage:
///   tmp.text = HebrewFixer.Fix("יש לך 2 משחקים");
///   tmp.isRightToLeftText = false;  // ALWAYS false
///
/// Call Fix() exactly once per text assignment. Never double-call.
/// </summary>
public static class HebrewFixer
{
    // Character type classification for bidi algorithm
    private enum CharType { RTL, LTR, Neutral, Whitespace }

    /// <summary>
    /// Converts logical-order Hebrew/mixed text to visual-order for TMP LTR rendering.
    /// Safe for all content types: pure Hebrew, mixed Hebrew/Latin, numbers, math, punctuation.
    /// </summary>
    public static string Fix(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Quick check: skip if no Hebrew characters
        bool hasHebrew = false;
        for (int i = 0; i < input.Length; i++)
        {
            if (IsHebrew(input[i])) { hasHebrew = true; break; }
        }
        if (!hasHebrew) return input;

        // Split into bidi runs
        var runs = BuildRuns(input);

        // Resolve neutral/whitespace runs based on surrounding context
        ResolveNeutrals(runs);

        // Reverse the order of runs (base direction is RTL)
        // Then reverse the content of each RTL run
        var sb = new StringBuilder(input.Length);

        // Process runs in reverse order (RTL base direction)
        for (int i = runs.Count - 1; i >= 0; i--)
        {
            var run = runs[i];
            if (run.type == CharType.RTL || run.type == CharType.Whitespace)
            {
                // RTL run: reverse characters
                for (int j = run.end; j >= run.start; j--)
                    sb.Append(input[j]);
            }
            else
            {
                // LTR run: keep original order
                for (int j = run.start; j <= run.end; j++)
                    sb.Append(input[j]);
            }
        }

        return sb.ToString();
    }

    // ── Run Building ──────────────────────────────────────────

    private struct BidiRun
    {
        public int start;
        public int end;
        public CharType type;
    }

    /// <summary>
    /// Splits input into contiguous runs of the same bidi type.
    /// LTR runs include: Latin letters, digits, and their adjacent operators/punctuation.
    /// RTL runs include: Hebrew letters.
    /// Neutral runs: punctuation not adjacent to LTR content.
    /// Whitespace runs: spaces between runs.
    /// </summary>
    private static List<BidiRun> BuildRuns(string input)
    {
        var runs = new List<BidiRun>();
        int i = 0;

        while (i < input.Length)
        {
            CharType ct = Classify(input[i]);

            if (ct == CharType.RTL)
            {
                int start = i;
                while (i < input.Length && Classify(input[i]) == CharType.RTL)
                    i++;
                runs.Add(new BidiRun { start = start, end = i - 1, type = CharType.RTL });
            }
            else if (ct == CharType.LTR)
            {
                // LTR run: include digits, Latin letters, and adjacent operators/punctuation
                int start = i;
                while (i < input.Length)
                {
                    CharType c = Classify(input[i]);
                    if (c == CharType.LTR) { i++; continue; }

                    // Include neutral chars (operators, punctuation) if followed by more LTR
                    if (c == CharType.Neutral || c == CharType.Whitespace)
                    {
                        int lookahead = i;
                        while (lookahead < input.Length &&
                               (Classify(input[lookahead]) == CharType.Neutral ||
                                Classify(input[lookahead]) == CharType.Whitespace))
                            lookahead++;

                        if (lookahead < input.Length && Classify(input[lookahead]) == CharType.LTR)
                        {
                            i = lookahead; // skip neutrals, continue LTR run
                            continue;
                        }
                    }
                    break;
                }
                runs.Add(new BidiRun { start = start, end = i - 1, type = CharType.LTR });
            }
            else if (ct == CharType.Whitespace)
            {
                int start = i;
                while (i < input.Length && Classify(input[i]) == CharType.Whitespace)
                    i++;
                runs.Add(new BidiRun { start = start, end = i - 1, type = CharType.Whitespace });
            }
            else // Neutral
            {
                int start = i;
                while (i < input.Length && Classify(input[i]) == CharType.Neutral)
                    i++;
                runs.Add(new BidiRun { start = start, end = i - 1, type = CharType.Neutral });
            }
        }

        return runs;
    }

    /// <summary>
    /// Resolves neutral and whitespace runs based on surrounding strong types.
    /// In RTL base direction:
    ///   - Neutral between two RTL → becomes RTL
    ///   - Neutral between two LTR → becomes LTR
    ///   - Neutral at edges or between mixed → becomes RTL (base direction)
    /// </summary>
    private static void ResolveNeutrals(List<BidiRun> runs)
    {
        for (int i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (run.type != CharType.Neutral && run.type != CharType.Whitespace)
                continue;

            CharType prev = GetStrongType(runs, i, -1);
            CharType next = GetStrongType(runs, i, +1);

            CharType resolved;
            if (prev == next)
                resolved = prev; // surrounded by same type
            else
                resolved = CharType.RTL; // default to base direction

            run.type = resolved;
            runs[i] = run;
        }
    }

    /// <summary>Find the nearest strong (RTL/LTR) type in the given direction.</summary>
    private static CharType GetStrongType(List<BidiRun> runs, int from, int dir)
    {
        int i = from + dir;
        while (i >= 0 && i < runs.Count)
        {
            if (runs[i].type == CharType.RTL || runs[i].type == CharType.LTR)
                return runs[i].type;
            i += dir;
        }
        return CharType.RTL; // base direction
    }

    // ── Character Classification ──────────────────────────────

    private static CharType Classify(char c)
    {
        // Hebrew block (U+0590–U+05FF) + Hebrew presentation forms (U+FB1D–U+FB4F)
        if (IsHebrew(c)) return CharType.RTL;

        // Latin letters
        if (c >= 'A' && c <= 'Z') return CharType.LTR;
        if (c >= 'a' && c <= 'z') return CharType.LTR;

        // Digits — classified as LTR (numbers always read left-to-right)
        if (c >= '0' && c <= '9') return CharType.LTR;

        // Whitespace
        if (c == ' ' || c == '\t' || c == '\n' || c == '\r') return CharType.Whitespace;
        if (c == '\u00A0') return CharType.Whitespace; // non-breaking space

        // Everything else is neutral (punctuation, operators, symbols)
        return CharType.Neutral;
    }

    private static bool IsHebrew(char c)
    {
        return (c >= '\u0590' && c <= '\u05FF') || (c >= '\uFB1D' && c <= '\uFB4F');
    }
}
