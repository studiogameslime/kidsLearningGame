using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Automatically attaches HebrewTMPText to every TMP_Text in every scene.
/// DontDestroyOnLoad singleton — created before any scene loads.
/// Hooks into sceneLoaded to process all text objects.
///
/// This ensures ALL Hebrew text renders correctly on mobile without
/// needing to manually add HebrewTMPText to each text object.
/// </summary>
public class HebrewTextAutoFixer : MonoBehaviour
{
    private static HebrewTextAutoFixer _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null) return;
        var go = new GameObject("HebrewTextAutoFixer");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<HebrewTextAutoFixer>();
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
        // Wait one frame for all Start() / setup to complete before fixing
        StartCoroutine(FixAllNextFrame());
    }

    private System.Collections.IEnumerator FixAllNextFrame()
    {
        // Wait 2 frames: setup scripts create objects in Start/Awake,
        // and some controllers set text in Start()
        yield return null;
        yield return null;
        FixAllTMPInScene();
    }

    private static void FixAllTMPInScene()
    {
        // Find ALL TMP_Text components including inactive objects
        var allTMP = Resources.FindObjectsOfTypeAll<TMP_Text>();
        int fixedCount = 0;

        foreach (var tmp in allTMP)
        {
            // Skip assets (prefabs, etc.) — only process scene objects
            if (tmp.gameObject.scene.name == null) continue;
            if (!tmp.gameObject.scene.isLoaded) continue;

            // Skip if already has HebrewTMPText
            if (tmp.GetComponent<HebrewTMPText>() != null) continue;

            // Add the auto-fixer component
            tmp.gameObject.AddComponent<HebrewTMPText>();
            fixedCount++;
        }

        if (fixedCount > 0)
            Debug.Log($"[HebrewAutoFix] Attached HebrewTMPText to {fixedCount} text objects in {SceneManager.GetActiveScene().name}");
    }
}
