using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Generates TMP SDF Font Assets from Rubik Hebrew + Latin subsets
/// and configures Latin as a fallback font on the Hebrew primary.
/// Run via Tools > Kids Learning Game > Setup Hebrew Font.
/// </summary>
public class HebrewFontSetup : EditorWindow
{
    [MenuItem("Tools/Kids Learning Game/Setup Hebrew Font")]
    private static void MenuSetup()
    {
        RunSetupSilent();
    }

    private const string HebrewChars =
        // Hebrew consonants (U+05D0–U+05EA)
        "\u05D0\u05D1\u05D2\u05D3\u05D4\u05D5\u05D6\u05D7\u05D8\u05D9\u05DA\u05DB\u05DC\u05DD\u05DE\u05DF" +
        "\u05E0\u05E1\u05E2\u05E3\u05E4\u05E5\u05E6\u05E7\u05E8\u05E9\u05EA" +
        // Hebrew vowels (nikud)
        "\u05B0\u05B1\u05B2\u05B3\u05B4\u05B5\u05B6\u05B7\u05B8\u05B9\u05BA\u05BB\u05BC\u05BD\u05BE\u05BF" +
        "\u05C0\u05C1\u05C2\u05C3\u05C4\u05C5\u05C6\u05C7" +
        // Hebrew punctuation
        "\u05F0\u05F1\u05F2\u05F3\u05F4" +
        // Shekel sign
        "\u20AA";

    private const string LatinChars =
        // ASCII printable (32-126)
        " !\"#$%&'()*+,-./0123456789:;<=>?@" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`" +
        "abcdefghijklmnopqrstuvwxyz{|}~" +
        // Common punctuation & symbols
        "\u00A0\u00AB\u00BB\u00BF\u2013\u2014\u2018\u2019\u201C\u201D\u2026" +
        "?!.,;:'\"()-+";

    /// <summary>All characters needed — Hebrew + Latin + digits + punctuation + symbols.</summary>
    private static readonly string AllChars = HebrewChars + LatinChars;

    public static void RunSetupSilent()
    {
        EnsureFolder("Assets/Fonts");
        EnsureFolder("Assets/Fonts/TMP");

        // Prefer Noto Sans Hebrew (Google's purpose-built Hebrew font — reliable TMP metrics)
        // Falls back to Rubik if not found
        var boldFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansHebrew-Bold.ttf");
        var regularFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansHebrew-Regular.ttf");

        string fontFamily = "NotoSansHebrew";
        if (boldFont == null || regularFont == null)
        {
            Debug.Log("Noto Sans Hebrew not found, trying Rubik...");
            boldFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Rubik-Bold.ttf");
            regularFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Rubik-Regular.ttf");
            fontFamily = "Rubik";
        }

        if (boldFont == null || regularFont == null)
        {
            Debug.LogError("No Hebrew font files found at Assets/Fonts/. Need NotoSansHebrew-Bold.ttf + NotoSansHebrew-Regular.ttf (or Rubik).");
            return;
        }

        try
        {
            // Heebo includes Hebrew + Latin + digits in one font — no fallback needed
            EditorUtility.DisplayProgressBar("Hebrew Font Setup", $"Generating {fontFamily} Bold SDF...", 0.2f);
            var boldTMP = GenerateFontAsset(boldFont, "Assets/Fonts/TMP/Rubik-Hebrew-Bold SDF.asset", AllChars);

            EditorUtility.DisplayProgressBar("Hebrew Font Setup", $"Generating {fontFamily} Regular SDF...", 0.5f);
            var regularTMP = GenerateFontAsset(regularFont, "Assets/Fonts/TMP/Rubik-Hebrew-Regular SDF.asset", AllChars);

            // Keep old Latin SDF as fallback for any missing glyphs
            var latinBold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Rubik-Latin-Bold.ttf");
            var latinRegular = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Rubik-Latin-Regular.ttf");

            if (latinBold != null)
            {
                EditorUtility.DisplayProgressBar("Hebrew Font Setup", "Generating Latin fallback Bold...", 0.7f);
                var latinBoldTMP = GenerateFontAsset(latinBold, "Assets/Fonts/TMP/Rubik-Latin-Bold SDF.asset", LatinChars);
                if (boldTMP != null && latinBoldTMP != null)
                    AddFallbackFont(boldTMP, latinBoldTMP);
            }
            if (latinRegular != null)
            {
                EditorUtility.DisplayProgressBar("Hebrew Font Setup", "Generating Latin fallback Regular...", 0.8f);
                var latinRegularTMP = GenerateFontAsset(latinRegular, "Assets/Fonts/TMP/Rubik-Latin-Regular SDF.asset", LatinChars);
                if (regularTMP != null && latinRegularTMP != null)
                    AddFallbackFont(regularTMP, latinRegularTMP);
            }

            // Set Bold as default TMP font
            EditorUtility.DisplayProgressBar("Hebrew Font Setup", "Setting default font...", 0.9f);
            if (boldTMP != null)
                SetDefaultTMPFont(boldTMP);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Hebrew font setup complete. {fontFamily} set as default TMP font (2048x2048, sampling 64). Latin fallback from Rubik.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static TMP_FontAsset GenerateFontAsset(Font sourceFont, string outputPath, string characterSet)
    {
        // Delete existing
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath);
        if (existing != null)
        {
            Debug.Log($"Font asset already exists: {outputPath} - regenerating.");
            AssetDatabase.DeleteAsset(outputPath);
        }

        // Try the full overload first, fall back to simple overload
        TMP_FontAsset fontAsset = null;

        try
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                64,   // sampling size — higher = better glyph metrics & kerning
                9,    // padding — larger padding prevents SDF bleed between glyphs
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                2048, // atlas width — more space = cleaner glyphs, less compression
                2048  // atlas height
            );
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Full CreateFontAsset failed for {sourceFont.name}: {e.Message}. Trying simple overload...");
        }

        // Fallback: simple single-parameter overload
        if (fontAsset == null)
        {
            try
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to create TMP font asset from {sourceFont.name}: {e.Message}");
                return null;
            }
        }

        if (fontAsset == null)
        {
            Debug.LogError($"Failed to create TMP font asset from {sourceFont.name}");
            return null;
        }

        // Set to Dynamic mode — TMP generates glyphs from source font at RUNTIME
        // This ensures identical rendering on Editor, Android, and iOS
        // (Static mode bakes metrics once, which can differ from runtime rendering)
        fontAsset.atlasPopulationMode = (TMPro.AtlasPopulationMode)UnityEngine.TextCore.Text.AtlasPopulationMode.Dynamic;

        // Pre-populate atlas with common characters for faster first render
        uint[] unicodeArray = GetUnicodeArray(characterSet);
        bool addResult = fontAsset.TryAddCharacters(unicodeArray, out uint[] missing);

        int added = unicodeArray.Length - (missing != null ? missing.Length : 0);
        Debug.Log($"[{sourceFont.name}] Added {added}/{unicodeArray.Length} characters (Dynamic mode)" +
                  (missing != null && missing.Length > 0 ? $" ({missing.Length} missing)" : ""));

        // Save as asset
        AssetDatabase.CreateAsset(fontAsset, outputPath);

        // Save atlas textures as sub-assets
        if (fontAsset.atlasTextures != null)
        {
            foreach (var tex in fontAsset.atlasTextures)
            {
                if (tex != null && !AssetDatabase.Contains(tex))
                {
                    tex.name = fontAsset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(tex, fontAsset);
                }
            }
        }

        // Save material as sub-asset
        if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
        {
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        Debug.Log($"Created TMP font asset: {outputPath} ({fontAsset.characterTable.Count} characters)");
        return fontAsset;
    }

    private static void AddFallbackFont(TMP_FontAsset primary, TMP_FontAsset fallback)
    {
        if (primary.fallbackFontAssetTable == null)
            primary.fallbackFontAssetTable = new List<TMP_FontAsset>();

        // Remove existing entry if present
        primary.fallbackFontAssetTable.RemoveAll(f => f == fallback);

        // Add as first fallback
        primary.fallbackFontAssetTable.Insert(0, fallback);
        EditorUtility.SetDirty(primary);
        Debug.Log($"Added {fallback.name} as fallback on {primary.name}");
    }

    private static void SetDefaultTMPFont(TMP_FontAsset fontAsset)
    {
        var settings = Resources.Load<TMP_Settings>("TMP Settings");
        if (settings == null)
        {
            Debug.LogWarning("TMP Settings not found. Import TMP Essentials first, then re-run this setup.");
            return;
        }

        var so = new SerializedObject(settings);
        var prop = so.FindProperty("m_defaultFontAsset");
        if (prop != null)
        {
            prop.objectReferenceValue = fontAsset;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            Debug.Log($"Set default TMP font to: {fontAsset.name}");
        }
        else
        {
            Debug.LogWarning("Could not find m_defaultFontAsset property in TMP Settings.");
        }
    }

    private static uint[] GetUnicodeArray(string chars)
    {
        var set = new HashSet<uint>();
        foreach (char c in chars)
            set.Add(c);
        var arr = new uint[set.Count];
        set.CopyTo(arr);
        return arr;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
