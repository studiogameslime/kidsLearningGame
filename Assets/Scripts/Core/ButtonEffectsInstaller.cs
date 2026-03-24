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

    static readonly string[] NavButtonNames = {
        "HomeButton", "BackButton", "HomeBtn", "BackBtn",
        "GamesButton", "AlbumButton", "ParentDashboardButton",
        "TrophyButton", "GameCard", "ProfileCard"
    };

    static void AttachToAllButtons()
    {
        var buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (var btn in buttons)
        {
            if (btn.GetComponent<ButtonPressEffect>() != null) continue;

            // Only attach to navigation/menu buttons, not gameplay answer buttons
            string name = btn.gameObject.name;
            bool isNavButton = false;
            foreach (var nav in NavButtonNames)
            {
                if (name.Contains(nav))
                {
                    isNavButton = true;
                    break;
                }
            }

            // Also attach to all buttons in menu/selection scenes
            string scene = SceneManager.GetActiveScene().name;
            if (scene == "MainMenu" || scene == "SelectionMenu"
                || scene == "ProfileSelection" || scene == "ProfileCreation"
                || scene == "HomeScene" || scene == "WorldScene")
                isNavButton = true;

            if (isNavButton)
                btn.gameObject.AddComponent<ButtonPressEffect>();
        }
    }
}
