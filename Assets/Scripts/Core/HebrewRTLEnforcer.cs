using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Ensures all TMP_Text objects have the correct RTL setting per platform.
///
/// Editor: isRightToLeftText = true (TMP handles RTL natively with font access)
/// Build:  isRightToLeftText = false (HebrewFixer.Fix() reverses strings manually)
///
/// This split approach guarantees Hebrew looks correct everywhere:
/// - Editor: text appears correct because TMP has full font shaping
/// - Mobile: text appears correct because Fix() pre-reverses the string
/// </summary>
public class HebrewRTLEnforcer : MonoBehaviour
{
    private static HebrewRTLEnforcer _instance;
    private static bool _useNativeRTL;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null) return;

#if UNITY_EDITOR && !HEBREW_FORCE_FIX
        _useNativeRTL = true;
#else
        _useNativeRTL = false;
#endif

        var go = new GameObject("HebrewRTLEnforcer");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<HebrewRTLEnforcer>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        Debug.Log($"[HebrewRTL] Platform={Application.platform}, NativeRTL={_useNativeRTL}");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this) _instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(EnforceNextFrame());
    }

    private System.Collections.IEnumerator EnforceNextFrame()
    {
        yield return null;
        yield return null;
        EnforceAll();
    }

    private static void EnforceAll()
    {
        var allTMP = Resources.FindObjectsOfTypeAll<TMP_Text>();
        int count = 0;

        foreach (var tmp in allTMP)
        {
            if (tmp.gameObject.scene.name == null) continue;
            if (!tmp.gameObject.scene.isLoaded) continue;

            if (tmp.isRightToLeftText != _useNativeRTL)
            {
                tmp.isRightToLeftText = _useNativeRTL;
                count++;
            }
        }

        if (count > 0)
            Debug.Log($"[HebrewRTL] Enforced isRightToLeftText={_useNativeRTL} on {count} TMP objects in {SceneManager.GetActiveScene().name}");
    }
}
