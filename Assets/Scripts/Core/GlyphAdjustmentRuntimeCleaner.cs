using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Automatically clears glyph pair adjustment records at runtime.
/// TMP Dynamic fonts regenerate kerning from the GPOS table when new glyphs
/// are added to the atlas. This cleaner removes those records after every
/// scene load and whenever TMP updates a font, ensuring Hebrew text spacing
/// is never broken by incorrect kerning pairs.
/// </summary>
public static class GlyphAdjustmentRuntimeCleaner
{
    // Track fonts we've already processed this frame to avoid redundant work
    private static readonly HashSet<int> _cleanedThisFrame = new HashSet<int>();
    private static bool _frameCallbackRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        // Clear on first scene load
        int count = ClearAllLoadedFonts();
        Debug.Log($"[GlyphCleaner] Init: cleared glyph adjustments on {count} font(s)");

        // Subscribe to font property changes (fires when TMP modifies a font asset)
        TMPro_EventManager.FONT_PROPERTY_EVENT.Add(OnFontPropertyChanged);

        // Subscribe to text object changes (fires after TMP generates/renders text)
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);

        // Clear again on every scene load — dynamic fonts may regenerate pairs
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            int c = ClearAllLoadedFonts();
            if (c > 0)
                Debug.Log($"[GlyphCleaner] Scene '{scene.name}' loaded: cleared {c} font(s)");
        };
    }

    private static void OnFontPropertyChanged(bool isChanged, Object obj)
    {
        if (isChanged && obj is TMP_FontAsset font)
        {
            Debug.Log($"[GlyphCleaner] FONT_PROPERTY_EVENT triggered for \"{font.name}\"");
            ClearFontRecords(font);
        }
    }

    private static void OnTextChanged(Object obj)
    {
        // When any TMP text changes, clear its font (and fallbacks)
        if (obj is TMP_Text tmpText && tmpText.font != null)
        {
            Debug.Log($"[GlyphCleaner] TEXT_CHANGED_EVENT triggered by \"{tmpText.name}\" (font: \"{tmpText.font.name}\")");
            ClearFontAndFallbacks(tmpText.font);
        }
    }

    private static void ClearFontAndFallbacks(TMP_FontAsset font)
    {
        if (font == null) return;

        int id = font.GetInstanceID();
        if (_cleanedThisFrame.Contains(id)) return;

        ClearFontRecords(font);
        _cleanedThisFrame.Add(id);

        // Also clear fallback fonts
        if (font.fallbackFontAssetTable != null)
        {
            foreach (var fallback in font.fallbackFontAssetTable)
                ClearFontAndFallbacks(fallback);
        }

        // Register end-of-frame cleanup once per frame
        if (!_frameCallbackRegistered)
        {
            _frameCallbackRegistered = true;
            Application.onBeforeRender += ClearFrameCache;
        }
    }

    private static void ClearFrameCache()
    {
        _cleanedThisFrame.Clear();
        _frameCallbackRegistered = false;
        Application.onBeforeRender -= ClearFrameCache;
    }

    private static int ClearAllLoadedFonts()
    {
        var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        int count = 0;
        foreach (var font in fonts)
        {
            if (ClearFontRecords(font))
                count++;
        }
        return count;
    }

    private static bool ClearFontRecords(TMP_FontAsset font)
    {
        if (font == null || font.fontFeatureTable == null) return false;
        var records = font.fontFeatureTable.glyphPairAdjustmentRecords;
        if (records == null || records.Count == 0) return false;

        int count = records.Count;
        records.Clear();

        Debug.Log($"[GlyphCleaner] Removed {count} glyph pairs from \"{font.name}\"");
        return true;
    }
}
