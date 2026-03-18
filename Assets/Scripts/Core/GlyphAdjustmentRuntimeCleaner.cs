using UnityEngine;
using TMPro;

/// <summary>
/// Disables TMP kerning (glyph pair adjustments) at runtime on all platforms.
///
/// ROOT CAUSE: TMP Dynamic fonts call FontEngine.GetGlyphPairAdjustmentRecords()
/// whenever new glyphs are added to the atlas. This reads the GPOS kerning table
/// directly from the font binary and repopulates pair records — even after we clear them.
/// In builds (where fonts are Dynamic), this happens on every new character rendered.
///
/// Clearing records is a losing battle because TMP re-reads from the font file.
/// The only reliable fix: disable kerning at the component level so TMP never
/// applies pair adjustments, regardless of what's in the table.
///
/// This is controlled by m_enableKerning / fontFeatures on each TMP_Text.
/// We disable it on all existing components and on every scene load.
/// </summary>
public static class GlyphAdjustmentRuntimeCleaner
{
    private static bool _initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        DisableKerningOnAll();

        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            DisableKerningOnAll();
        };
    }

    private static void DisableKerningOnAll()
    {
        int count = 0;
        var allText = Resources.FindObjectsOfTypeAll<TMP_Text>();
        foreach (var tmp in allText)
        {
            if (tmp == null) continue;

            // Remove kern from active font features
            #pragma warning disable 0618
            if (tmp.enableKerning)
            {
                tmp.enableKerning = false;
                count++;
            }
            #pragma warning restore 0618
        }

        if (count > 0)
            Debug.Log($"[GlyphCleaner] Disabled kerning on {count} TMP component(s)");
    }
}
