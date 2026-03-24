using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Automatically attaches ButtonPressEffect to every Button in every scene.
/// Uses [RuntimeInitializeOnLoadMethod] so no manual setup is needed.
/// </summary>
public class ButtonEffectsInstaller : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var go = new GameObject("ButtonEffectsInstaller");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<ButtonEffectsInstaller>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Process the current scene immediately
        AttachToAllButtons();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Delay one frame so dynamically-created UI has time to initialize
        StartCoroutine(DelayedAttach());
    }

    IEnumerator DelayedAttach()
    {
        yield return null;
        AttachToAllButtons();
    }

    static void AttachToAllButtons()
    {
        var buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (var btn in buttons)
        {
            if (btn.GetComponent<ButtonPressEffect>() == null)
                btn.gameObject.AddComponent<ButtonPressEffect>();
        }
    }
}
