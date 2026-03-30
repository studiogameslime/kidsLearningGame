using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared helper for loading sub-sprites from UI sprite sheets in editor setup scripts.
/// </summary>
public static class UISheetHelper
{
    private const string UI1Sheet = "Assets/Art/UI/UI_1.png";

    public static Sprite LoadSpriteFromSheet(string sheetPath, string spriteName)
    {
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
        if (allAssets != null)
            foreach (var asset in allAssets)
                if (asset is Sprite spr && spr.name == spriteName) return spr;
        return null;
    }

    /// <summary>Home icon (UI_1_2).</summary>
    public static Sprite HomeIcon => LoadSpriteFromSheet(UI1Sheet, "UI_1_2");

    /// <summary>Gear/settings icon (UI_1_4).</summary>
    public static Sprite GearIcon => LoadSpriteFromSheet(UI1Sheet, "UI_1_4");

    /// <summary>Collection/album icon (UI_1_1).</summary>
    public static Sprite AlbumIcon => LoadSpriteFromSheet(UI1Sheet, "UI_1_1");
}
