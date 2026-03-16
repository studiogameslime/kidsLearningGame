using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Sets isRightToLeftText = true on every TMP_Text in every scene.
/// This enables TMP's native RTL rendering for Hebrew — no manual
/// string reversal needed.
///
/// DontDestroyOnLoad singleton, created before any scene loads.
/// Runs after scene load + 1 frame (so setup scripts finish first).
/// </summary>
public class HebrewRTLEnforcer : MonoBehaviour
{
    private static HebrewRTLEnforcer _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null) return;
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
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this) _instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Wait for setup scripts to create all UI objects
        StartCoroutine(EnforceNextFrame());
    }

    private System.Collections.IEnumerator EnforceNextFrame()
    {
        yield return null;
        yield return null;
        EnforceRTL();
    }

    private static void EnforceRTL()
    {
        // Find ALL TMP_Text in the scene (including inactive)
        var allTMP = Resources.FindObjectsOfTypeAll<TMP_Text>();
        int count = 0;

        foreach (var tmp in allTMP)
        {
            // Skip prefabs / assets — only process loaded scene objects
            if (tmp.gameObject.scene.name == null) continue;
            if (!tmp.gameObject.scene.isLoaded) continue;

            if (!tmp.isRightToLeftText)
            {
                tmp.isRightToLeftText = true;
                count++;
            }
        }

        if (count > 0)
            Debug.Log($"[HebrewRTL] Set isRightToLeftText=true on {count} TMP objects in {SceneManager.GetActiveScene().name}");
    }
}
