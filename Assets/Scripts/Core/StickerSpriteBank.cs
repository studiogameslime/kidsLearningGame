using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static runtime cache of sticker sprites, populated once from WorldScene.
/// Stores sprites by name within each category prefix.
/// Sticker IDs are "{prefix}{spriteName}" — e.g. "animal_dog", "balloon_red", "letter_א".
/// </summary>
public static class StickerSpriteBank
{
    // prefix → (lowercased sprite name → sprite)
    private static readonly Dictionary<string, Dictionary<string, Sprite>> _categories
        = new Dictionary<string, Dictionary<string, Sprite>>();

    public static void Register(string prefix, Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0) return;
        var dict = new Dictionary<string, Sprite>();
        foreach (var spr in sprites)
            if (spr != null) dict[spr.name.ToLower()] = spr;
        _categories[prefix] = dict;
    }

    /// <summary>
    /// Look up a sprite by sticker ID (e.g. "animal_dog" → animalsStickers["dog"]).
    /// </summary>
    public static Sprite GetSprite(string stickerId)
    {
        if (string.IsNullOrEmpty(stickerId)) return null;

        int idx = stickerId.IndexOf('_');
        if (idx < 0) return null;

        string prefix = stickerId.Substring(0, idx + 1);
        string spriteName = stickerId.Substring(idx + 1);

        Dictionary<string, Sprite> dict;
        if (!_categories.TryGetValue(prefix, out dict)) return null;

        Sprite spr;
        return dict.TryGetValue(spriteName, out spr) ? spr : null;
    }

    /// <summary>
    /// Get all possible sticker IDs for a category (e.g. "animal_" → ["animal_dog", "animal_cat", ...]).
    /// </summary>
    public static List<string> GetAllIds(string prefix)
    {
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
