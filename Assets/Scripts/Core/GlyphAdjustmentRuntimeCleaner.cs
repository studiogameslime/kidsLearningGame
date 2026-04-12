using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Disables TMP kerning (glyph pair adjustments) at runtime on all platforms.
///
/// ROOT CAUSE: TMP Dynamic fonts read kerning from the font's GPOS table
/// whenever new glyphs are added to the atlas. This can't be prevented.
/// The fix: disable kerning on every TMP_Text component so pair adjustments
/// are never applied, regardless of what's in the font feature table.
///
/// IMPORTANT: We must NOT modify TMP_Text properties inside TEXT_CHANGED_EVENT
/// because that fires during GenerateTextMesh() and causes re-entrant corruption.
/// Instead we collect components that need fixing and process them next frame.
/// </summary>
public static class GlyphAdjustmentRuntimeCleaner
{
    private static bool _initialized;
    private static readonly HashSet<TMP_Text> _pendingFix = new HashSet<TMP_Text>();
    private static bool _updateRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        DisableKerningOnAll();

        // Catch dynamically created TMP components — queue for next frame
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);

        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            DisableKerningOnAll();
        };
    }

    private static void OnTextChanged(Object obj)
    {
        // Do NOT modify TMP properties here — this fires inside GenerateTextMesh().
        // Instead, queue the component for processing next frame.
        if (obj is TMP_Text tmp)
        {
            #pragma warning disable 0618
            if (tmp.enableKerning)
            {
                _pendingFix.Add(tmp);
                if (!_updateRegistered)
                {
                    _updateRegistered = true;
                    // Use Camera.onPreRender for a one-shot next-frame callback
                    Application.onBeforeRender += ProcessPending;
                }
            }
            #pragma warning restore 0618
        }
    }

    private static void ProcessPending()
    {
        Application.onBeforeRender -= ProcessPending;
        _updateRegistered = false;

        foreach (var tmp in _pendingFix)
        {
            if (tmp == null) continue;
            #pragma warning disable 0618
            if (tmp.enableKerning)
                tmp.enableKerning = false;
            #pragma warning restore 0618
        }
        _pendingFix.Clear();
    }

    private static void DisableKerningOnAll()
    {
        int count = 0;
        var allText = Resources.FindObjectsOfTypeAll<TMP_Text>();
        foreach (var tmp in allText)
        {
            if (tmp == null) continue;

            #pragma warning disable 0618
            if (tmp.enableKerning)
            {
                tmp.enableKerning = false;
                count++;
            }
            #pragma warning restore 0618
        }

        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    }
}
