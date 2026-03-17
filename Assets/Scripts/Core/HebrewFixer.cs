using System.Text;
using System.Collections.Generic;

/// <summary>
/// Hebrew bidi text fixer for TextMeshPro. Converts logical-order Hebrew
/// to visual-order for correct display in TMP's LTR rendering mode.
///
/// ALWAYS use with isRightToLeftText = false.
///
/// Why: TMP's isRightToLeftText=true is broken on mobile — it reverses
/// numbers and corrupts Hebrew glyph spacing. This fixer implements
/// proper bidi reordering that works identically on all platforms.
///
/// Handles: pure Hebrew, Hebrew+numbers, punctuation, math expressions,
/// mixed Hebrew/English, percentage signs, ranges (3-5), bullets (•).
///
/// Call Fix() exactly once per text assignment. All existing call sites
/// (HebrewFixer.Fix() and H() wrappers) work unchanged.
/// </summary>
public static class HebrewFixer
{
    private enum SType { RTL, LTR, Neutral, WS }

    private struct Seg
    {
        public string text;
        public SType type;
    }

    public static string Fix(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Quick check: skip if no Hebrew
        bool hasHebrew = false;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if ((c >= '\u0590' && c <= '\u05FF') || (c >= '\uFB1D' && c <= '\uFB4F'))
            { hasHebrew = true; break; }
        }
        if (!hasHebrew) return input;

        // Build segments
        var segs = new List<Seg>();
        int pos = 0;
        while (pos < input.Length)
        {
            if (IsHeb(input[pos]))
            {
                int s = pos;
                while (pos < input.Length && IsHeb(input[pos])) pos++;
                segs.Add(new Seg { text = input.Substring(s, pos - s), type = SType.RTL });
            }
            else if (IsLat(input[pos]))
            {
                int s = pos;
                while (pos < input.Length)
                {
                    if (IsLat(input[pos])) { pos++; continue; }
                    if (input[pos] == ' ' || IsNeu(input[pos]))
                    {
                        int k = pos;
                        while (k < input.Length && (input[k] == ' ' || IsNeu(input[k]))) k++;
                        if (k < input.Length && IsLat(input[k])) { pos = k; continue; }
                    }
                    break;
                }
                segs.Add(new Seg { text = input.Substring(s, pos - s), type = SType.LTR });
            }
            else if (input[pos] == ' ')
            {
                int s = pos;
                while (pos < input.Length && input[pos] == ' ') pos++;
                segs.Add(new Seg { text = input.Substring(s, pos - s), type = SType.WS });
            }
            else
            {
                int s = pos;
                while (pos < input.Length && !IsHeb(input[pos]) && !IsLat(input[pos]) && input[pos] != ' ') pos++;
                segs.Add(new Seg { text = input.Substring(s, pos - s), type = SType.Neutral });
            }
        }

        // Resolve neutrals/whitespace
        for (int i = 0; i < segs.Count; i++)
        {
            if (segs[i].type == SType.Neutral || segs[i].type == SType.WS)
            {
                var p = Strong(segs, i, -1);
                var n = Strong(segs, i, +1);
                segs[i] = new Seg { text = segs[i].text, type = (p == n) ? p : SType.RTL };
            }
        }

        // Output: reverse segment order, reverse RTL content
        var sb = new StringBuilder(input.Length);
        for (int i = segs.Count - 1; i >= 0; i--)
        {
            if (segs[i].type == SType.LTR)
                sb.Append(segs[i].text);
            else
                for (int j = segs[i].text.Length - 1; j >= 0; j--)
                    sb.Append(segs[i].text[j]);
        }
        return sb.ToString();
    }

    static bool IsHeb(char c) => (c >= '\u0590' && c <= '\u05FF') || (c >= '\uFB1D' && c <= '\uFB4F');
    static bool IsLat(char c) => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
    static bool IsNeu(char c) => !IsHeb(c) && !IsLat(c) && c != ' ';

    static SType Strong(List<Seg> s, int from, int dir)
    {
        for (int i = from + dir; i >= 0 && i < s.Count; i += dir)
            if (s[i].type == SType.RTL || s[i].type == SType.LTR) return s[i].type;
        return SType.RTL;
    }
}
