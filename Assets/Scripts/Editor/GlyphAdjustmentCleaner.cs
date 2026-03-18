#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using TMPro;

/// <summary>
/// Clears glyph pair adjustment records from all TMP font assets.
/// - Manual: Tools > Kids Learning Game > Clear Glyph Adjustments
/// - Automatic: runs before every build via IPreprocessBuildWithReport
/// </summary>
public static class GlyphAdjustmentCleaner
{
    [MenuItem("Tools/Kids Learning Game/Clear Glyph Adjustments")]
    public static void ClearAll()
    {
        int totalCleared = ClearAllFontAssets();
        AssetDatabase.SaveAssets();
        Debug.Log($"[GlyphCleaner] Done. Cleared {totalCleared} total glyph pair records.");
    }

    public static int ClearAllFontAssets()
    {
        var guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Fonts" });
        int totalCleared = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null) continue;

            int count = ClearFontRecords(font);
            if (count > 0)
            {
                EditorUtility.SetDirty(font);
                totalCleared += count;
                Debug.Log($"[GlyphCleaner] Cleared {count} glyph pairs in {path}");
            }
        }

        return totalCleared;
    }

    public static int ClearFontRecords(TMP_FontAsset font)
    {
        if (font.fontFeatureTable == null) return 0;

        int totalCleared = 0;

        var records = font.fontFeatureTable.glyphPairAdjustmentRecords;
        if (records != null && records.Count > 0)
        {
            totalCleared += records.Count;
            records.Clear();
        }

        return totalCleared;
    }
}

/// <summary>
/// Automatically clears glyph pair adjustments before every build.
/// TMP can regenerate pairs during atlas packing — this ensures they're
/// stripped right before the build packages the assets.
/// </summary>
public class GlyphAdjustmentPreBuild : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        int cleared = GlyphAdjustmentCleaner.ClearAllFontAssets();
        if (cleared > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[GlyphCleaner] Pre-build: cleared {cleared} glyph pairs before build.");
        }
    }
}
#endif
