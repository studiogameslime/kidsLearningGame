using System.Text;
using System.Collections.Generic;

/// <summary>
/// Production Hebrew/RTL text fixer for TextMeshPro.
///
/// TMP's isRightToLeftText = false is broken:
///   - Reverses numbers ("10" → "01")
///   - Breaks glyph spacing on Hebrew characters (ו, י get gaps)
///   - Does naive full reversal, not proper bidi
///
/// Solution: manual bidi reordering + isRightToLeftText = false ALWAYS.
/// Fix() converts logical-order Hebrew to visual-order for LTR rendering.
/// </summary>
public static class HebrewFixer
{
    public static string Fix(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        bool hasHebrew = false;
        for (int i = 0; i < input.Length; i++)
            if (IsHebrew(input[i])) { hasHebrew = true; break; }
        if (!hasHebrew) return input;

        // Split into segments: Hebrew words, LTR runs, whitespace, neutrals
        var segments = new List<Segment>();
        int pos = 0;

        while (pos < input.Length)
        {
            if (IsHebrew(input[pos]))
            {
                int start = pos;
                while (pos < input.Length && IsHebrew(input[pos])) pos++;
                segments.Add(new Segment(input.Substring(start, pos - start), SegType.RTL));
            }
            else if (IsLatinOrDigit(input[pos]))
            {
                // Absorb entire LTR run including spaces/operators between LTR chars
                int start = pos;
                while (pos < input.Length)
                {
                    if (IsLatinOrDigit(input[pos])) { pos++; continue; }
                    // Check if neutral/space is followed by more LTR
                    if (input[pos] == ' ' || IsNeutral(input[pos]))
                    {
                        int look = pos;
                        while (look < input.Length && (input[look] == ' ' || IsNeutral(input[look]))) look++;
                        if (look < input.Length && IsLatinOrDigit(input[look])) { pos = look; continue; }
                    }
                    break;
                }
                segments.Add(new Segment(input.Substring(start, pos - start), SegType.LTR));
            }
            else if (input[pos] == ' ')
            {
                int start = pos;
                while (pos < input.Length && input[pos] == ' ') pos++;
                segments.Add(new Segment(input.Substring(start, pos - start), SegType.WS));
            }
            else
            {
                // Neutral: punctuation, symbols
                int start = pos;
                while (pos < input.Length && !IsHebrew(input[pos]) && !IsLatinOrDigit(input[pos]) && input[pos] != ' ') pos++;
                segments.Add(new Segment(input.Substring(start, pos - start), SegType.Neutral));
            }
        }

        // Resolve neutrals and whitespace to RTL or LTR based on neighbors
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i].type == SegType.Neutral || segments[i].type == SegType.WS)
            {
                var prev = FindStrong(segments, i, -1);
                var next = FindStrong(segments, i, +1);
                segments[i] = new Segment(segments[i].text,
                    prev == next ? prev : SegType.RTL); // default RTL base
            }
        }

        // Build visual string: reverse segment order, reverse RTL segment contents
        var sb = new StringBuilder(input.Length);
        for (int i = segments.Count - 1; i >= 0; i--)
        {
            if (segments[i].type == SegType.LTR)
                sb.Append(segments[i].text); // keep LTR as-is
            else
                AppendReversed(sb, segments[i].text); // reverse RTL
        }

        return sb.ToString();
    }

    // ── Types ──

    private enum SegType { RTL, LTR, Neutral, WS }

    private struct Segment
    {
        public string text;
        public SegType type;
        public Segment(string t, SegType st) { text = t; type = st; }
    }

    // ── Helpers ──

    private static bool IsHebrew(char c) =>
        (c >= '\u0590' && c <= '\u05FF') || (c >= '\uFB1D' && c <= '\uFB4F');

    private static bool IsLatinOrDigit(char c) =>
        (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    private static bool IsNeutral(char c) =>
        !IsHebrew(c) && !IsLatinOrDigit(c) && c != ' ';

    private static SegType FindStrong(List<Segment> segs, int from, int dir)
    {
        for (int i = from + dir; i >= 0 && i < segs.Count; i += dir)
            if (segs[i].type == SegType.RTL || segs[i].type == SegType.LTR)
                return segs[i].type;
        return SegType.RTL;
    }

    private static void AppendReversed(StringBuilder sb, string s)
    {
        for (int i = s.Length - 1; i >= 0; i--)
            sb.Append(s[i]);
    }
}
