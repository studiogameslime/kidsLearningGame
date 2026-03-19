using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates 2 wrong Hebrew letter distractors for the first-letter game.
/// Scales visual similarity with difficulty.
/// </summary>
public static class LetterDistractorGenerator
{
    // Hebrew alphabet: 22 base letters (no final forms)
    private static readonly char[] Alphabet =
    {
        '\u05D0', // א Alef
        '\u05D1', // ב Bet
        '\u05D2', // ג Gimel
        '\u05D3', // ד Dalet
        '\u05D4', // ה He
        '\u05D5', // ו Vav
        '\u05D6', // ז Zayin
        '\u05D7', // ח Het
        '\u05D8', // ט Tet
        '\u05D9', // י Yod
        '\u05DB', // כ Kaf
        '\u05DC', // ל Lamed
        '\u05DE', // מ Mem
        '\u05E0', // נ Nun
        '\u05E1', // ס Samekh
        '\u05E2', // ע Ayin
        '\u05E4', // פ Pe
        '\u05E6', // צ Tsadi
        '\u05E7', // ק Qof
        '\u05E8', // ר Resh
        '\u05E9', // ש Shin
        '\u05EA', // ת Tav
    };

    // Visually similar pairs (bidirectional)
    private static readonly Dictionary<char, char[]> SimilarLetters = new Dictionary<char, char[]>
    {
        { '\u05D1', new[] { '\u05DB' } },          // ב ↔ כ
        { '\u05DB', new[] { '\u05D1' } },          // כ ↔ ב
        { '\u05D3', new[] { '\u05E8' } },          // ד ↔ ר
        { '\u05E8', new[] { '\u05D3' } },          // ר ↔ ד
        { '\u05D4', new[] { '\u05D7', '\u05EA' } }, // ה ↔ ח,ת
        { '\u05D7', new[] { '\u05D4', '\u05EA' } }, // ח ↔ ה,ת
        { '\u05EA', new[] { '\u05D4', '\u05D7' } }, // ת ↔ ה,ח
        { '\u05D5', new[] { '\u05D6' } },          // ו ↔ ז
        { '\u05D6', new[] { '\u05D5' } },          // ז ↔ ו
        { '\u05E1', new[] { '\u05E2' } },          // ס ↔ ע (closed vs open)
        { '\u05E2', new[] { '\u05E1' } },          // ע ↔ ס
    };

    /// <summary>
    /// Returns shuffled array of 3 letters: [correct, wrong1, wrong2] in random order.
    /// Difficulty 1-3: distractors visually different from correct.
    /// Difficulty 4-6: one similar allowed.
    /// Difficulty 7-10: prefer similar distractors.
    /// </summary>
    public static char[] Generate(char correct, int difficulty)
    {
        var candidates = new List<char>();
        var similar = new List<char>();

        // Get similar letters for the correct answer
        char[] simArr = null;
        SimilarLetters.TryGetValue(correct, out simArr);

        foreach (var c in Alphabet)
        {
            if (c == correct) continue;
            bool isSimilar = simArr != null && System.Array.IndexOf(simArr, c) >= 0;

            if (isSimilar)
                similar.Add(c);
            else
                candidates.Add(c);
        }

        var picked = new List<char>();

        if (difficulty <= 3)
        {
            // Easy: only pick from visually different letters
            Shuffle(candidates);
            for (int i = 0; i < Mathf.Min(2, candidates.Count); i++)
                picked.Add(candidates[i]);
        }
        else if (difficulty >= 7 && similar.Count > 0)
        {
            // Hard: prefer similar letters
            Shuffle(similar);
            for (int i = 0; i < Mathf.Min(2, similar.Count); i++)
                picked.Add(similar[i]);
            // Fill remaining from non-similar
            if (picked.Count < 2)
            {
                Shuffle(candidates);
                for (int i = 0; picked.Count < 2 && i < candidates.Count; i++)
                    picked.Add(candidates[i]);
            }
        }
        else
        {
            // Medium: mix similar and different
            var all = new List<char>();
            all.AddRange(candidates);
            all.AddRange(similar);
            Shuffle(all);
            for (int i = 0; i < Mathf.Min(2, all.Count); i++)
                picked.Add(all[i]);
        }

        // Build result: correct + 2 distractors, shuffled
        var result = new char[3];
        result[0] = correct;
        result[1] = picked.Count > 0 ? picked[0] : Alphabet[0];
        result[2] = picked.Count > 1 ? picked[1] : Alphabet[1];

        // Fisher-Yates shuffle
        for (int i = result.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            char tmp = result[i];
            result[i] = result[j];
            result[j] = tmp;
        }

        return result;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}
