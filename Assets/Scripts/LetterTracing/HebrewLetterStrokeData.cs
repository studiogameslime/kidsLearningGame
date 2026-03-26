using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static stroke path definitions for all Hebrew letters.
/// Coordinates are normalized 0–1 where (0,0) = bottom-left, (1,1) = top-right.
/// Each letter has ordered strokes; each stroke is a list of waypoints defining the path.
/// The first waypoint is the start point (where the child begins tracing).
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

    // Simple letters for low difficulty (1–2 strokes)
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

    // Medium letters (2–3 strokes)
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
        return _letters['\u05D5']; // fallback to ו
    }

    private static void EnsureInit()
    {
        if (_letters != null) return;
        _letters = new Dictionary<char, LetterData>();

        // ── א (Alef) — 3 strokes ──
        Add('\u05D0', "\u05D0\u05DC\u05E3",
            S(V(0.75f,0.85f), V(0.55f,0.65f), V(0.35f,0.45f), V(0.25f,0.15f)),       // diagonal: top-right → bottom-left
            S(V(0.25f,0.85f), V(0.35f,0.70f), V(0.45f,0.55f)),                         // short: top-left → center
            S(V(0.55f,0.50f), V(0.65f,0.35f), V(0.75f,0.15f))                          // short: center → bottom-right
        );

        // ── ב (Bet) — 3 strokes ──
        Add('\u05D1', "\u05D1\u05D9\u05EA",
            S(V(0.80f,0.80f), V(0.60f,0.80f), V(0.40f,0.80f), V(0.20f,0.80f)),        // top: right → left
            S(V(0.20f,0.80f), V(0.20f,0.60f), V(0.20f,0.40f), V(0.20f,0.20f)),        // left down
            S(V(0.20f,0.20f), V(0.40f,0.20f), V(0.60f,0.20f), V(0.80f,0.20f))         // bottom: left → right
        );

        // ── ג (Gimel) — 2 strokes ──
        Add('\u05D2', "\u05D2\u05D9\u05DE\u05DC",
            S(V(0.50f,0.85f), V(0.50f,0.65f), V(0.50f,0.45f), V(0.50f,0.25f)),        // vertical down
            S(V(0.50f,0.25f), V(0.60f,0.20f), V(0.70f,0.18f))                          // small foot right
        );

        // ── ד (Dalet) — 2 strokes ──
        Add('\u05D3', "\u05D3\u05DC\u05EA",
            S(V(0.80f,0.80f), V(0.60f,0.80f), V(0.40f,0.80f), V(0.20f,0.80f)),        // top: right → left
            S(V(0.20f,0.80f), V(0.20f,0.60f), V(0.20f,0.40f), V(0.20f,0.20f))         // left down
        );

        // ── ה (He) — 3 strokes ──
        Add('\u05D4', "\u05D4\u05D0",
            S(V(0.80f,0.80f), V(0.60f,0.80f), V(0.40f,0.80f), V(0.20f,0.80f)),        // top: right → left
            S(V(0.20f,0.80f), V(0.20f,0.60f), V(0.20f,0.40f), V(0.20f,0.20f)),        // left vertical down
            S(V(0.65f,0.75f), V(0.65f,0.60f), V(0.65f,0.48f))                          // right short vertical
        );

        // ── ו (Vav) — 1 stroke ──
        Add('\u05D5', "\u05D5\u05D0\u05D5",
            S(V(0.50f,0.85f), V(0.50f,0.70f), V(0.50f,0.55f), V(0.50f,0.40f), V(0.50f,0.20f))
        );

        // ── ז (Zayin) — 2 strokes ──
        Add('\u05D6', "\u05D6\u05D9\u05DF",
            S(V(0.65f,0.82f), V(0.50f,0.82f), V(0.35f,0.82f)),                        // top: right → left
            S(V(0.50f,0.82f), V(0.48f,0.60f), V(0.45f,0.40f), V(0.42f,0.20f))         // down-left
        );

        // ── ח (Chet) — 3 strokes ──
        Add('\u05D7', "\u05D7\u05D9\u05EA",
            S(V(0.25f,0.80f), V(0.25f,0.60f), V(0.25f,0.40f), V(0.25f,0.20f)),        // left vertical
            S(V(0.75f,0.80f), V(0.75f,0.60f), V(0.75f,0.40f), V(0.75f,0.20f)),        // right vertical
            S(V(0.75f,0.80f), V(0.60f,0.83f), V(0.40f,0.83f), V(0.25f,0.80f))         // top bridge: right → left
        );

        // ── ט (Tet) — 2 strokes ──
        Add('\u05D8', "\u05D8\u05D9\u05EA",
            S(V(0.25f,0.50f), V(0.25f,0.30f), V(0.35f,0.20f), V(0.55f,0.20f),
              V(0.75f,0.25f), V(0.78f,0.45f), V(0.75f,0.65f), V(0.60f,0.80f),
              V(0.40f,0.82f), V(0.25f,0.75f)),                                          // curved loop clockwise
            S(V(0.55f,0.75f), V(0.55f,0.55f), V(0.55f,0.35f))                          // inner vertical
        );

        // ── י (Yod) — 1 stroke ──
        Add('\u05D9', "\u05D9\u05D5\u05D3",
            S(V(0.55f,0.70f), V(0.50f,0.60f), V(0.45f,0.50f))
        );

        // ── כ (Kaf) — 1 stroke ──
        Add('\u05DB', "\u05DB\u05E3",
            S(V(0.75f,0.80f), V(0.60f,0.82f), V(0.40f,0.78f), V(0.25f,0.65f),
              V(0.22f,0.45f), V(0.30f,0.25f), V(0.45f,0.20f))
        );

        // ── ך (Kaf sofit) — 1 stroke ──
        Add('\u05DA', "\u05DA\u05E3 \u05E1\u05D5\u05E4\u05D9\u05EA",
            S(V(0.50f,0.85f), V(0.50f,0.70f), V(0.50f,0.50f), V(0.50f,0.30f), V(0.50f,0.10f))
        );

        // ── ל (Lamed) — 2 strokes ──
        Add('\u05DC', "\u05DC\u05DE\u05D3",
            S(V(0.50f,0.20f), V(0.48f,0.40f), V(0.45f,0.60f), V(0.42f,0.80f), V(0.40f,0.92f)),  // bottom → top
            S(V(0.40f,0.92f), V(0.50f,0.95f), V(0.58f,0.90f))                                    // hook at top
        );

        // ── מ (Mem) — 4 strokes ──
        Add('\u05DE', "\u05DE\u05DD",
            S(V(0.25f,0.80f), V(0.25f,0.60f), V(0.25f,0.40f), V(0.25f,0.20f)),        // left vertical down
            S(V(0.25f,0.20f), V(0.45f,0.20f), V(0.65f,0.20f)),                         // bottom: left → right
            S(V(0.65f,0.20f), V(0.65f,0.40f), V(0.65f,0.60f), V(0.65f,0.80f)),        // right vertical up
            S(V(0.65f,0.80f), V(0.50f,0.82f), V(0.35f,0.80f))                          // top: right → left (partial)
        );

        // ── ם (Mem sofit) — 1 stroke (rectangle) ──
        Add('\u05DD', "\u05DD\u05DD \u05E1\u05D5\u05E4\u05D9\u05EA",
            S(V(0.75f,0.80f), V(0.75f,0.60f), V(0.75f,0.40f), V(0.75f,0.20f),
              V(0.55f,0.20f), V(0.30f,0.20f),
              V(0.30f,0.40f), V(0.30f,0.60f), V(0.30f,0.80f),
              V(0.50f,0.80f), V(0.75f,0.80f))
        );

        // ── נ (Nun) — 2 strokes ──
        Add('\u05E0', "\u05E0\u05D5\u05DF",
            S(V(0.45f,0.80f), V(0.45f,0.60f), V(0.45f,0.40f), V(0.45f,0.22f)),        // vertical down
            S(V(0.45f,0.22f), V(0.55f,0.20f), V(0.65f,0.20f))                          // small foot right
        );

        // ── ן (Nun sofit) — 1 stroke ──
        Add('\u05DF', "\u05DF\u05D5\u05DF \u05E1\u05D5\u05E4\u05D9\u05EA",
            S(V(0.50f,0.85f), V(0.50f,0.65f), V(0.50f,0.45f), V(0.50f,0.25f), V(0.50f,0.10f))
        );

        // ── ס (Samekh) — 1 stroke (closed loop) ──
        Add('\u05E1', "\u05E1\u05DE\u05DA",
            S(V(0.50f,0.82f), V(0.70f,0.78f), V(0.78f,0.60f), V(0.75f,0.38f),
              V(0.60f,0.22f), V(0.40f,0.22f), V(0.25f,0.38f), V(0.22f,0.60f),
              V(0.30f,0.78f), V(0.50f,0.82f))
        );

        // ── ע (Ayin) — 2 strokes ──
        Add('\u05E2', "\u05E2\u05D9\u05DF",
            S(V(0.65f,0.82f), V(0.55f,0.65f), V(0.45f,0.45f), V(0.30f,0.20f)),        // right stroke: top → bottom-left
            S(V(0.35f,0.82f), V(0.45f,0.65f), V(0.55f,0.45f), V(0.70f,0.20f))         // left stroke: top → bottom-right
        );

        // ── פ (Pe) — 3 strokes ──
        Add('\u05E4', "\u05E4\u05D0",
            S(V(0.80f,0.80f), V(0.60f,0.80f), V(0.40f,0.80f), V(0.20f,0.80f)),        // top: right → left
            S(V(0.20f,0.80f), V(0.20f,0.60f), V(0.20f,0.40f), V(0.20f,0.20f)),        // left down
            S(V(0.65f,0.70f), V(0.70f,0.55f), V(0.65f,0.40f), V(0.50f,0.35f))         // inner curve
        );

        // ── ף (Pe sofit) — 2 strokes ──
        Add('\u05E3', "\u05E3\u05D0 \u05E1\u05D5\u05E4\u05D9\u05EA",
            S(V(0.50f,0.85f), V(0.50f,0.65f), V(0.50f,0.45f), V(0.50f,0.25f), V(0.50f,0.10f)),  // long vertical
            S(V(0.50f,0.85f), V(0.60f,0.88f), V(0.65f,0.82f))                                    // small hook
        );

        // ── צ (Tsadi) — 3 strokes ──
        Add('\u05E6', "\u05E6\u05D3\u05D9",
            S(V(0.55f,0.82f), V(0.45f,0.65f), V(0.35f,0.45f), V(0.25f,0.22f)),        // diagonal: top → bottom-left
            S(V(0.45f,0.82f), V(0.45f,0.60f), V(0.45f,0.40f), V(0.45f,0.22f)),        // vertical down
            S(V(0.45f,0.22f), V(0.55f,0.20f), V(0.65f,0.22f))                          // small right extension
        );

        // ── ץ (Tsadi sofit) — 2 strokes ──
        Add('\u05E5', "\u05E5\u05D3\u05D9 \u05E1\u05D5\u05E4\u05D9\u05EA",
            S(V(0.50f,0.85f), V(0.50f,0.65f), V(0.50f,0.45f), V(0.50f,0.25f), V(0.50f,0.10f)),  // long vertical
            S(V(0.50f,0.10f), V(0.60f,0.08f), V(0.65f,0.12f))                                    // angled foot
        );

        // ── ק (Qof) — 2 strokes ──
        Add('\u05E7', "\u05E7\u05D5\u05E3",
            S(V(0.45f,0.80f), V(0.60f,0.78f), V(0.70f,0.65f), V(0.68f,0.45f),
              V(0.55f,0.35f), V(0.40f,0.38f), V(0.35f,0.55f), V(0.40f,0.72f),
              V(0.45f,0.80f)),                                                           // circle clockwise
            S(V(0.70f,0.50f), V(0.70f,0.35f), V(0.70f,0.20f))                          // vertical line right
        );

        // ── ר (Resh) — 1 stroke ──
        Add('\u05E8', "\u05E8\u05D9\u05E9",
            S(V(0.75f,0.80f), V(0.55f,0.82f), V(0.35f,0.80f), V(0.25f,0.70f),
              V(0.22f,0.55f), V(0.22f,0.40f), V(0.22f,0.20f))
        );

        // ── ש (Shin) — 4 strokes ──
        Add('\u05E9', "\u05E9\u05D9\u05DF",
            S(V(0.75f,0.78f), V(0.75f,0.55f), V(0.75f,0.35f)),                         // right vertical
            S(V(0.50f,0.82f), V(0.50f,0.60f), V(0.50f,0.40f)),                         // middle vertical
            S(V(0.25f,0.78f), V(0.25f,0.55f), V(0.25f,0.35f)),                         // left vertical
            S(V(0.25f,0.35f), V(0.40f,0.22f), V(0.60f,0.22f), V(0.75f,0.35f))         // connecting base
        );

        // ── ת (Tav) — 2 strokes ──
        Add('\u05EA', "\u05EA\u05D0\u05D5",
            S(V(0.80f,0.80f), V(0.60f,0.80f), V(0.40f,0.80f), V(0.20f,0.80f)),        // top: right → left
            S(V(0.20f,0.80f), V(0.20f,0.60f), V(0.20f,0.40f), V(0.20f,0.20f))         // left vertical down
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
