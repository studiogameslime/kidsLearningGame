using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static runtime cache of sticker sprites.
/// Auto-loads from Resources/Stickers/ if not already populated by WorldScene.
/// Sticker IDs are "{prefix}{spriteName}" — e.g. "animal_dog", "balloon_red".
/// </summary>
public static class StickerSpriteBank
{
    private static readonly Dictionary<string, Dictionary<string, Sprite>> _categories
        = new Dictionary<string, Dictionary<string, Sprite>>();

    private static bool _autoLoaded;

    // Mapping: prefix → Resources sticker sheet path
    private static readonly (string prefix, string resourcePath)[] StickerSheets =
    {
        ("animal_",  "Stickers/animalsStickers"),
        ("letter_",  "Stickers/lettersStickers"),
        ("number_",  "Stickers/numbersStickers"),
        ("balloon_", "Stickers/ballonsStickers"),
        ("ocean_",   "Stickers/aquatiumStickers"),
        ("vehicle_", "Stickers/carsStickers"),
        ("food_",    "Stickers/foodStickers"),
        ("art_",     "Stickers/artStickers"),
        ("nature_",  "Stickers/natureStickers"),
    };

    public static void Register(string prefix, Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0) return;
        var dict = new Dictionary<string, Sprite>();
        foreach (var spr in sprites)
            if (spr != null) dict[spr.name.ToLower()] = spr;
        _categories[prefix] = dict;
    }

    /// <summary>
    /// Ensure all sticker sheets are loaded. Called automatically on first GetSprite/GetAllIds.
    /// </summary>
    private static void EnsureLoaded()
    {
        if (_autoLoaded) return;
        _autoLoaded = true;

        foreach (var (prefix, path) in StickerSheets)
        {
            if (_categories.ContainsKey(prefix)) continue; // already registered by WorldScene
            var sprites = Resources.LoadAll<Sprite>(path);
            if (sprites != null && sprites.Length > 0)
                Register(prefix, sprites);
        }
    }

    public static Sprite GetSprite(string stickerId)
    {
        if (string.IsNullOrEmpty(stickerId)) return null;

        EnsureLoaded();

        int idx = stickerId.IndexOf('_');
        if (idx < 0) return null;

        string prefix = stickerId.Substring(0, idx + 1);
        string spriteName = stickerId.Substring(idx + 1);

        Dictionary<string, Sprite> dict;
        if (!_categories.TryGetValue(prefix, out dict)) return null;

        Sprite spr;
        return dict.TryGetValue(spriteName, out spr) ? spr : null;
    }

    public static List<string> GetAllIds(string prefix)
    {
        EnsureLoaded();

        var result = new List<string>();
        Dictionary<string, Sprite> dict;
        if (_categories.TryGetValue(prefix, out dict))
        {
            foreach (var name in dict.Keys)
                result.Add(prefix + name);
        }
        return result;
    }
}
