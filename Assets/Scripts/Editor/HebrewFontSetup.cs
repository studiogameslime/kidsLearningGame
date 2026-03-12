using UnityEngine;
using UnityEditor;
using TMPro;
using TMPro.EditorUtilities;
using System.IO;

/// <summary>
/// Generates TMP SDF Font Assets from Rubik Hebrew font and sets as default.
/// Run via Tools > Kids Learning Game > Setup Hebrew Font.
/// </summary>
public class HebrewFontSetup : EditorWindow
{
    // Latin Basic + Latin-1 Supplement + Hebrew block
    private const string CharacterSet =
        // ASCII printable (32-126)
        " !\"#$%&'()*+,-./0123456789:;<=>?@" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`" +
        "abcdefghijklmnopqrstuvwxyz{|}~" +
        // Common punctuation & symbols
        "\u00A0\u00AB\u00BB\u00BF\u2013\u2014\u2018\u2019\u201C\u201D\u2026\u20AA" + // ₪
        // Hebrew (U+0590–U+05FF) — all consonants, vowels, accents
        "\u05D0\u05D1\u05D2\u05D3\u05D4\u05D5\u05D6\u05D7\u05D8\u05D9\u05DA\u05DB\u05DC\u05DD\u05DE\u05DF" +
        "\u05E0\u05E1\u05E2\u05E3\u05E4\u05E5\u05E6\u05E7\u05E8\u05E9\u05EA" +
        // Hebrew vowels (nikud)
        "\u05B0\u05B1\u05B2\u05B3\u05B4\u05B5\u05B6\u05B7\u05B8\u05B9\u05BA\u05BB\u05BC\u05BD\u05BE\u05BF" +
        "\u05C0\u05C1\u05C2\u05C3\u05C4\u05C5\u05C6\u05C7" +
        // Hebrew punctuation
        "\u05F0\u05F1\u05F2\u05F3\u05F4" +
        // Common extras
        "?!.,;:'\"()-+";

    [MenuItem("Tools/Kids Learning Game/Setup Hebrew Font")]
    public static void RunSetup()
    {
        RunSetupSilent();
        EditorUtility.DisplayDialog("Done!", "Hebrew fonts generated and set as default TMP font.\n\nAll new TMP text will use Rubik.", "OK");
    }

    public static void RunSetupSilent()
    {
        EnsureFolder("Assets/Fonts");
        EnsureFolder("Assets/Fonts/TMP");

        var boldFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Rubik-Bold.ttf");
        var regularFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Rubik-Regular.ttf");

        if (boldFont == null || regularFont == null)
        {
            Debug.LogError("Rubik font files not found at Assets/Fonts/. Cannot generate TMP fonts.");
            return;
        }

        EditorUtility.DisplayProgressBar("Hebrew Font Setup", "Generating Rubik Bold SDF…", 0.3f);
        var boldTMP = GenerateFontAsset(boldFont, "Assets/Fonts/TMP/Rubik-Bold SDF.asset", 48);

        EditorUtility.DisplayProgressBar("Hebrew Font Setup", "Generating Rubik Regular SDF…", 0.6f);
        var regularTMP = GenerateFontAsset(regularFont, "Assets/Fonts/TMP/Rubik-Regular SDF.asset", 48);

        EditorUtility.DisplayProgressBar("Hebrew Font Setup", "Setting default font…", 0.9f);

        // Set bold as default TMP font
        if (boldTMP != null)
            SetDefaultTMPFont(boldTMP);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();

        Debug.Log("Hebrew font setup complete. Rubik-Bold set as default TMP font.");
    }

    private static TMP_FontAsset GenerateFontAsset(Font sourceFont, string outputPath, int samplingSize)
    {
        // Check if already exists
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath);
        if (existing != null)
        {
            Debug.Log($"Font asset already exists: {outputPath} — regenerating.");
            AssetDatabase.DeleteAsset(outputPath);
        }

        // Create the font asset using TMP's built-in generation
        var fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            samplingSize,
            9,  // padding
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
            1024,  // atlas width
            1024   // atlas height
        );

        if (fontAsset == null)
        {
            Debug.LogError($"Failed to create TMP font asset from {sourceFont.name}");
            return null;
        }

        // Try to add all Hebrew + Latin characters
        uint[] unicodeArray = GetUnicodeArray(CharacterSet);
        fontAsset.TryAddCharacters(unicodeArray, out uint[] missing);

        if (missing != null && missing.Length > 0)
            Debug.LogWarning($"Missing {missing.Length} glyphs in {sourceFont.name} (likely unused nikud/accents)");

        // Save
        AssetDatabase.CreateAsset(fontAsset, outputPath);

        // Save atlas texture as sub-asset
        if (fontAsset.atlasTextures != null)
        {
            foreach (var tex in fontAsset.atlasTextures)
            {
                if (tex != null)
                {
                    tex.name = fontAsset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(tex, fontAsset);
                }
            }
        }

        // Save material as sub-asset
        if (fontAsset.material != null)
        {
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        Debug.Log($"Created TMP font asset: {outputPath} ({fontAsset.characterTable.Count} characters)");
        return fontAsset;
    }

    private static void SetDefaultTMPFont(TMP_FontAsset fontAsset)
    {
        // Load TMP Settings
        var settings = Resources.Load<TMP_Settings>("TMP Settings");
        if (settings == null)
        {
            Debug.LogWarning("TMP Settings not found. Cannot set default font automatically.");
            return;
        }

        // Use SerializedObject to set the default font asset
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
        var set = new System.Collections.Generic.HashSet<uint>();
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
