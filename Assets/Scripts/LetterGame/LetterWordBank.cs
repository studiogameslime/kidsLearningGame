using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Static word data for the Hebrew First Letter game.
/// Flat pool mixing animals + colors with cached word lengths for difficulty tiers.
/// </summary>
public static class LetterWordBank
{
    public struct WordEntry
    {
        public string id;           // "Cat", "red"
        public string hebrewWord;   // "חתול"
        public string soundId;      // "Cat" or "red" for SoundLibrary
        public string soundType;    // "animal" or "color"
        public bool hasAnim;        // true if AnimalAnimData exists
        public int wordLength;      // cached hebrewWord.Length
        public Color swatchColor;   // only used for colors
    }

    private static WordEntry[] _pool;

    public static WordEntry[] Pool
    {
        get
        {
            if (_pool == null) BuildPool();
            return _pool;
        }
    }

    private static void BuildPool()
    {
        var list = new List<WordEntry>();

        // ── Animals (19) ──
        AddAnimal(list, "Cat",      "\u05D7\u05EA\u05D5\u05DC");    // חתול
        AddAnimal(list, "Dog",      "\u05DB\u05DC\u05D1");          // כלב
        AddAnimal(list, "Bear",     "\u05D3\u05D5\u05D1");          // דוב
        AddAnimal(list, "Duck",     "\u05D1\u05E8\u05D5\u05D6");    // ברוז
        AddAnimal(list, "Fish",     "\u05D3\u05D2");                // דג
        AddAnimal(list, "Frog",     "\u05E6\u05E4\u05E8\u05D3\u05E2"); // צפרדע
        AddAnimal(list, "Bird",     "\u05E6\u05D9\u05E4\u05D5\u05E8"); // ציפור
        AddAnimal(list, "Cow",      "\u05E4\u05E8\u05D4");          // פרה
        AddAnimal(list, "Horse",    "\u05E1\u05D5\u05E1");          // סוס
        AddAnimal(list, "Lion",     "\u05D0\u05E8\u05D9\u05D4");    // אריה
        AddAnimal(list, "Monkey",   "\u05E7\u05D5\u05E3");          // קוף
        AddAnimal(list, "Elephant", "\u05E4\u05D9\u05DC");          // פיל
        AddAnimal(list, "Giraffe",  "\u05D2\u05F3\u05D9\u05E8\u05E4\u05D4"); // ג׳ירפה
        AddAnimal(list, "Zebra",    "\u05D6\u05D1\u05E8\u05D4");    // זברה
        AddAnimal(list, "Turtle",   "\u05E6\u05D1");                // צב
        AddAnimal(list, "Snake",    "\u05E0\u05D7\u05E9");          // נחש
        AddAnimal(list, "Sheep",    "\u05DB\u05D1\u05E9\u05D4");    // כבשה
        AddAnimal(list, "Chicken",  "\u05EA\u05E8\u05E0\u05D2\u05D5\u05DC"); // תרנגול
        AddAnimal(list, "Donkey",   "\u05D7\u05DE\u05D5\u05E8");    // חמור

        // ── Colors (7) ──
        AddColor(list, "red",    "\u05D0\u05D3\u05D5\u05DD", HexColor("#FF4444")); // אדום
        AddColor(list, "blue",   "\u05DB\u05D7\u05D5\u05DC", HexColor("#4488FF")); // כחול
        AddColor(list, "green",  "\u05D9\u05E8\u05D5\u05E7", HexColor("#44BB44")); // ירוק
        AddColor(list, "yellow", "\u05E6\u05D4\u05D5\u05D1", HexColor("#FFD600")); // צהוב
        AddColor(list, "orange", "\u05DB\u05EA\u05D5\u05DD", HexColor("#FF8844")); // כתום
        AddColor(list, "pink",   "\u05D5\u05E8\u05D5\u05D3", HexColor("#FF88BB")); // ורוד
        AddColor(list, "purple", "\u05E1\u05D2\u05D5\u05DC", HexColor("#AA44FF")); // סגול

        _pool = list.ToArray();
    }

    private static void AddAnimal(List<WordEntry> list, string id, string hebrew)
    {
        list.Add(new WordEntry
        {
            id = id,
            hebrewWord = hebrew,
            soundId = id,
            soundType = "animal",
            hasAnim = true,
            wordLength = hebrew.Length,
            swatchColor = Color.clear
        });
    }

    private static void AddColor(List<WordEntry> list, string id, string hebrew, Color color)
    {
        list.Add(new WordEntry
        {
            id = id,
            hebrewWord = hebrew,
            soundId = id,
            soundType = "color",
            hasAnim = false,
            wordLength = hebrew.Length,
            swatchColor = color
        });
    }

    /// <summary>
    /// Returns max word length allowed for a difficulty level.
    /// </summary>
    public static int MaxWordLength(int difficulty)
    {
        if (difficulty <= 3) return 3;  // 2-3 letter words
        if (difficulty <= 6) return 4;  // up to 4 letters
        return 99;                       // all words
    }

    /// <summary>
    /// Returns filtered pool based on difficulty.
    /// </summary>
    public static List<WordEntry> GetFilteredPool(int difficulty)
    {
        int maxLen = MaxWordLength(difficulty);
        var filtered = new List<WordEntry>();
        foreach (var w in Pool)
        {
            if (w.wordLength <= maxLen)
                filtered.Add(w);
        }
        return filtered;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
