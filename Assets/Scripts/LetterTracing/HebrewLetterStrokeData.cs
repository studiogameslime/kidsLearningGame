using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stroke path definitions for all Hebrew letters in correct visual orientation.
/// Coordinates are normalized 0–1 where (0,0) = bottom-left, (1,1) = top-right.
/// Paths are designed to sit on top of standard printed Hebrew glyphs.
/// Stroke direction follows child-friendly simplified writing order.
/// </summary>
public static class HebrewLetterStrokeData
{
    public struct StrokeData
    {
        public List<Vector2> points;
    }

    public struct LetterData
    {
        public char letter;
        public string name;
        public List<StrokeData> strokes;
    }

    private static Dictionary<char, LetterData> _letters;

    public static readonly char[] AllLetters =
    {
        '\u05D0', '\u05D1', '\u05D2', '\u05D3', '\u05D4',
        '\u05D5', '\u05D6', '\u05D7', '\u05D8', '\u05D9',
        '\u05DB', '\u05DA', '\u05DC', '\u05DE', '\u05DD',
        '\u05E0', '\u05DF', '\u05E1', '\u05E2', '\u05E4',
        '\u05E3', '\u05E6', '\u05E5', '\u05E7', '\u05E8',
        '\u05E9', '\u05EA'
    };

    public static readonly char[] SimpleLetters =
    {
        '\u05D5', // ו
        '\u05D9', // י
        '\u05DA', // ך
        '\u05DF', // ן
        '\u05D3', // ד
        '\u05E8', // ר
        '\u05D2', // ג
    };

    public static readonly char[] MediumLetters =
    {
        '\u05D1', // ב
        '\u05D4', // ה
        '\u05D6', // ז
        '\u05DB', // כ
        '\u05DC', // ל
        '\u05E0', // נ
        '\u05EA', // ת
        '\u05E7', // ק
    };

    public static LetterData Get(char letter)
    {
        EnsureInit();
        if (_letters.TryGetValue(letter, out var data))
            return data;
        Debug.LogWarning($"HebrewLetterStrokeData: No data for '{letter}'");
        return _letters['\u05D5'];
    }

    private static void EnsureInit()
    {
        if (_letters != null) return;
        _letters = new Dictionary<char, LetterData>();

        // All coordinates use the standard printed Hebrew glyph as source of truth.
        // Letters are centered in a ~0.20–0.80 box within the 0–1 canvas.
        // Right side of letters faces RIGHT on screen (correct Hebrew orientation).

        // ── א (Alef) — 3 strokes ──
        Add('\u05D0', "\u05D0\u05DC\u05E3",
            S(V(0.65f,0.80f), V(0.55f,0.65f), V(0.45f,0.50f), V(0.35f,0.35f)),       // diagonal: upper-right → lower-left
            S(V(0.30f,0.80f), V(0.38f,0.68f), V(0.45f,0.55f)),                         // upper-left → center
            S(V(0.55f,0.48f), V(0.62f,0.35f), V(0.70f,0.22f))                          // center → lower-right
        );

        // ── ב (Bet) — 3 strokes ──
        // Standard printed ב: horizontal top, left vertical, horizontal bottom. Right side open.
        Add('\u05D1', "\u05D1\u05D9\u05EA",
            S(V(0.75f,0.78f), V(0.60f,0.78f), V(0.45f,0.78f), V(0.30f,0.78f)),        // top: right → left
            S(V(0.30f,0.78f), V(0.30f,0.60f), V(0.30f,0.42f), V(0.30f,0.22f)),        // left vertical: down
            S(V(0.30f,0.22f), V(0.45f,0.22f), V(0.60f,0.22f), V(0.75f,0.22f))         // bottom: left → right
        );

        // ── ג (Gimel) — 3 strokes ──
        // Printed ג: top horizontal, descending left stroke, short foot
        Add('\u05D2', "\u05D2\u05D9\u05DE\u05DC",
            S(V(0.65f,0.78f), V(0.50f,0.78f), V(0.38f,0.78f)),                        // top: right → left
            S(V(0.38f,0.78f), V(0.36f,0.60f), V(0.34f,0.42f), V(0.32f,0.24f)),        // descending: upper-left → lower-left
            S(V(0.32f,0.24f), V(0.42f,0.22f), V(0.52f,0.22f))                          // foot: left → right
        );

        // ── ד (Dalet) — 2 strokes ──
        // Printed ד: horizontal top extending left, left vertical down
        Add('\u05D3', "\u05D3\u05DC\u05EA",
            S(V(0.72f,0.78f), V(0.58f,0.78f), V(0.42f,0.78f), V(0.30f,0.78f)),        // top: right → left
            S(V(0.30f,0.78f), V(0.30f,0.60f), V(0.30f,0.42f), V(0.30f,0.22f))         // left vertical: down
        );

        // ── ה (He) — 3 strokes ──
        // Printed ה: top bar, left vertical, separate short right vertical
        Add('\u05D4', "\u05D4\u05D0",
            S(V(0.75f,0.78f), V(0.58f,0.78f), V(0.42f,0.78f), V(0.28f,0.78f)),        // top: right → left
            S(V(0.28f,0.78f), V(0.28f,0.60f), V(0.28f,0.42f), V(0.28f,0.22f)),        // left vertical: down
            S(V(0.62f,0.72f), V(0.62f,0.58f), V(0.62f,0.45f))                          // right short vertical: down (separate)
        );

        // ── ו (Vav) — 1 stroke ──
        // Printed ו: single vertical line
        Add('\u05D5', "\u05D5\u05D0\u05D5",
            S(V(0.50f,0.82f), V(0.50f,0.68f), V(0.50f,0.52f), V(0.50f,0.38f), V(0.50f,0.22f))
        );

        // ── ז (Zayin) — 2 strokes ──
        // Printed ז: top horizontal, vertical/diagonal down
        Add('\u05D6', "\u05D6\u05D9\u05DF",
            S(V(0.60f,0.80f), V(0.48f,0.80f), V(0.38f,0.80f)),                        // top: right → left
            S(V(0.48f,0.80f), V(0.47f,0.62f), V(0.46f,0.42f), V(0.45f,0.22f))         // descending from center
        );

        // ── ח (Chet) — 3 strokes ──
        // Printed ח: right vertical, left vertical, top bridge connecting them
        Add('\u05D7', "\u05D7\u05D9\u05EA",
            S(V(0.70f,0.78f), V(0.70f,0.60f), V(0.70f,0.42f), V(0.70f,0.22f)),        // right vertical: down
            S(V(0.30f,0.78f), V(0.30f,0.60f), V(0.30f,0.42f), V(0.30f,0.22f)),        // left vertical: down
            S(V(0.70f,0.78f), V(0.55f,0.80f), V(0.42f,0.80f), V(0.30f,0.78f))         // top bridge: right → left
        );

        // ── ט (Tet) — 2 strokes ──
        // Simplified printed ט: outer curve loop, inner vertical
        Add('\u05D8', "\u05D8\u05D9\u05EA",
            S(V(0.70f,0.55f), V(0.70f,0.35f), V(0.60f,0.22f), V(0.42f,0.22f),
              V(0.28f,0.30f), V(0.25f,0.48f), V(0.30f,0.65f), V(0.42f,0.78f),
              V(0.55f,0.80f), V(0.68f,0.75f)),                                          // outer curve clockwise from right
            S(V(0.50f,0.72f), V(0.50f,0.55f), V(0.50f,0.38f))                          // inner vertical
        );

        // ── י (Yod) — 1 stroke ──
        // Printed י: short mark, upper-right to lower-left
        Add('\u05D9', "\u05D9\u05D5\u05D3",
            S(V(0.55f,0.68f), V(0.50f,0.58f), V(0.45f,0.48f))
        );

        // ── כ (Kaf) — 2 strokes ──
        // Printed כ: upper curve from right going left-down, then short bottom
        Add('\u05DB', "\u05DB\u05E3",
            S(V(0.70f,0.78f), V(0.55f,0.80f), V(0.38f,0.74f), V(0.28f,0.60f),
              V(0.28f,0.42f), V(0.32f,0.28f)),                                          // curve: upper-right → lower-left
            S(V(0.32f,0.28f), V(0.45f,0.22f), V(0.58f,0.22f))                          // bottom foot: left → right
        );

        // ── ך (Kaf sofit) — 1 stroke ──
        // Printed ך: tall vertical
        Add('\u05DA', "\u05DA\u05E3 \u05E1\u05D5\u05E4\u05D9\u05EA",
            S(V(0.50f,0.85f), V(0.50f,0.68f), V(0.50f,0.48f), V(0.50f,0.28f), V(0.50f,0.10f))
        );

        // ── ל (Lamed) — 2 strokes ──
        // Printed ל: tall ascending stroke (bottom→top), then top hook
        Add('\u05DC', "\u05DC\u05DE\u05D3",
            S(V(0.55f,0.22f), V(0.52f,0.40f), V(0.48f,0.58f), V(0.45f,0.75f), V(0.43f,0.88f)),  // ascending: bottom-right → top
            S(V(0.43f,0.88f), V(0.52f,0.92f), V(0.58f,0.88f))                                    // top hook
        );

        // ── מ (Mem) — 4 strokes ──
        // Printed מ: right vertical, bottom horizontal, left vertical up, top partial
        Add('\u05DE', "\u05DE\u05DD",
            S(V(0.70f,0.78f), V(0.70f,0.60f), V(0.70f,0.42f), V(0.70f,0.22f)),        // right vertical: down
            S(V(0.70f,0.22f), V(0.55f,0.22f), V(0.40f,0.22f), V(0.30f,0.22f)),        // bottom: right → left
            S(V(0.30f,0.22f), V(0.30f,0.40f), V(0.30f,0.58f), V(0.30f,0.78f)),        // left vertical: up
            S(V(0.30f,0.78f), V(0.45f,0.80f), V(0.55f,0.78f))                          // top partial: left → right
        );

        // ── ם (Mem sofit) — 1 stroke (rectangle) ──
        // Printed ם: closed rectangular loop starting upper-right
        Add('\u05DD', "\u05DD\u05DD \u05E1\u05D5\u05E4\u05D9\u05EA",
            S(V(0.70f,0.78f), V(0.55f,0.80f), V(0.40f,0.80f), V(0.30f,0.78f),
              V(0.30f,0.60f), V(0.30f,0.40f), V(0.30f,0.22f),
              V(0.45f,0.22f), V(0.60f,0.22f), V(0.70f,0.22f),
              V(0.70f,0.40f), V(0.70f,0.60f), V(0.70f,0.78f))
        );

        // ── נ (Nun) — 2 strokes ──
        // Printed נ: right vertical down, short foot left
        Add('\u05E0', "\u05E0\u05D5\u05DF",
            S(V(0.58f,0.78f), V(0.58f,0.60f), V(0.58f,0.42f), V(0.58f,0.24f)),        // right vertical: down
            S(V(0.58f,0.24f), V(0.48f,0.22f), V(0.38f,0.22f))                          // foot: right → left
        );

        // ── ן (Nun sofit) — 1 stroke ──
        // Printed ן: long vertical
        Add('\u05DF', "\u05DF\u05D5\u05DF \u05E1\u05D5\u05E4\u05D9\u05EA",
            S(V(0.50f,0.85f), V(0.50f,0.68f), V(0.50f,0.48f), V(0.50f,0.28f), V(0.50f,0.10f))
        );

        // ── ס (Samekh) — 1 stroke (closed loop) ──
        // Printed ס: closed rounded rectangle, starting upper-right going left
        Add('\u05E1', "\u05E1\u05DE\u05DA",
            S(V(0.68f,0.78f), V(0.52f,0.80f), V(0.38f,0.78f), V(0.28f,0.65f),
              V(0.28f,0.48f), V(0.28f,0.35f), V(0.38f,0.22f),
              V(0.52f,0.22f), V(0.65f,0.28f), V(0.70f,0.42f),
              V(0.70f,0.58f), V(0.68f,0.72f), V(0.68f,0.78f))
        );

        // ── ע (Ayin) — 2 strokes ──
        // Printed ע: right descending stroke first, then left descending stroke
        Add('\u05E2', "\u05E2\u05D9\u05DF",
            S(V(0.68f,0.78f), V(0.60f,0.62f), V(0.52f,0.45f), V(0.48f,0.25f)),        // right stroke: upper-right → lower-center
            S(V(0.32f,0.78f), V(0.38f,0.62f), V(0.44f,0.45f), V(0.48f,0.25f))         // left stroke: upper-left → lower-center
        );

        // ── פ (Pe) — 3 strokes ──
        // Printed פ: top bar, left vertical, inner curved return on right
        Add('\u05E4', "\u05E4\u05D0",
            S(V(0.72f,0.78f), V(0.58f,0.78f), V(0.42f,0.78f), V(0.28f,0.78f)),        // top: right → left
            S(V(0.28f,0.78f), V(0.28f,0.60f), V(0.28f,0.42f), V(0.28f,0.22f)),        // left vertical: down
            S(V(0.65f,0.70f), V(0.68f,0.55f), V(0.62f,0.40f), V(0.48f,0.35f))         // inner curve
        );

        // ── ף (Pe sofit) — 2 strokes ──
        // Printed ף: long descending stroke, short upper hook
        Add('\u05E3', "\u05E3\u05D0 \u05E1\u05D5\u05E4\u05D9\u05EA",
            S(V(0.52f,0.82f), V(0.52f,0.65f), V(0.52f,0.45f), V(0.52f,0.25f), V(0.52f,0.10f)),  // main vertical: down
            S(V(0.52f,0.82f), V(0.42f,0.85f), V(0.35f,0.80f))                                    // upper hook: right → left
        );

        // ── צ (Tsadi) — 3 strokes ──
        // Printed צ: right descending diagonal, left vertical, short lower extension
        Add('\u05E6', "\u05E6\u05D3\u05D9",
            S(V(0.65f,0.78f), V(0.58f,0.62f), V(0.50f,0.45f), V(0.45f,0.28f)),        // right diagonal: upper-right → lower-center
            S(V(0.35f,0.78f), V(0.35f,0.60f), V(0.35f,0.42f), V(0.35f,0.28f)),        // left vertical: down
            S(V(0.45f,0.28f), V(0.55f,0.24f), V(0.62f,0.22f))                          // lower extension: center → right
        );

        // ── ץ (Tsadi sofit) — 2 strokes ──
        // Printed ץ: main descending stroke, short angled lower extension
        Add('\u05E5', "\u05E5\u05D3\u05D9 \u05E1\u05D5\u05E4\u05D9\u05EA",
            S(V(0.52f,0.82f), V(0.52f,0.65f), V(0.52f,0.45f), V(0.52f,0.25f), V(0.52f,0.10f)),  // main vertical: down
            S(V(0.52f,0.10f), V(0.42f,0.08f), V(0.35f,0.12f))                                    // angled foot: right → left
        );

        // ── ק (Qof) — 2 strokes ──
        // Printed ק: upper loop, descending right leg below baseline
        Add('\u05E7', "\u05E7\u05D5\u05E3",
            S(V(0.68f,0.78f), V(0.52f,0.80f), V(0.38f,0.75f), V(0.32f,0.62f),
              V(0.35f,0.48f), V(0.45f,0.40f), V(0.58f,0.42f), V(0.65f,0.52f),
              V(0.65f,0.65f), V(0.60f,0.75f)),                                          // upper loop
            S(V(0.65f,0.48f), V(0.65f,0.32f), V(0.65f,0.18f))                          // right descending leg
        );

        // ── ר (Resh) — 2 strokes ──
        // Printed ר: top horizontal, left vertical/curve down. CORRECT orientation.
        Add('\u05E8', "\u05E8\u05D9\u05E9",
            S(V(0.72f,0.78f), V(0.58f,0.78f), V(0.42f,0.78f), V(0.30f,0.78f)),        // top: right → left
            S(V(0.30f,0.78f), V(0.30f,0.62f), V(0.30f,0.45f), V(0.30f,0.28f))         // left vertical: down
        );

        // ── ש (Shin) — 4 strokes ──
        // Printed ש: three vertical heads, connecting base. Right vertical first.
        Add('\u05E9', "\u05E9\u05D9\u05DF",
            S(V(0.72f,0.78f), V(0.72f,0.60f), V(0.72f,0.42f)),                         // right vertical: down
            S(V(0.50f,0.82f), V(0.50f,0.64f), V(0.50f,0.46f)),                         // middle vertical: down
            S(V(0.28f,0.78f), V(0.28f,0.60f), V(0.28f,0.42f)),                         // left vertical: down
            S(V(0.72f,0.42f), V(0.58f,0.25f), V(0.42f,0.25f), V(0.28f,0.42f))         // connecting base: right → left
        );

        // ── ת (Tav) — 2 strokes ──
        // Printed ת: top horizontal, left vertical
        Add('\u05EA', "\u05EA\u05D0\u05D5",
            S(V(0.75f,0.78f), V(0.58f,0.78f), V(0.42f,0.78f), V(0.28f,0.78f)),        // top: right → left
            S(V(0.28f,0.78f), V(0.28f,0.60f), V(0.28f,0.42f), V(0.28f,0.22f))         // left vertical: down
        );
    }

    // ── Helpers ──

    private static Vector2 V(float x, float y) => new Vector2(x, y);

    private static StrokeData S(params Vector2[] pts) =>
        new StrokeData { points = new List<Vector2>(pts) };

    private static void Add(char letter, string name, params StrokeData[] strokes)
    {
        _letters[letter] = new LetterData
        {
            letter = letter,
            name = name,
            strokes = new List<StrokeData>(strokes)
        };
    }
}
