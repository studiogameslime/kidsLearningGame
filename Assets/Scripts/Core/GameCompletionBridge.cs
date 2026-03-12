using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bridges the confetti event to JourneyManager. One-shot per scene.
/// Only fires during active journey. DontDestroyOnLoad singleton.
/// </summary>
public class GameCompletionBridge : MonoBehaviour
{
    public static GameCompletionBridge Instance { get; private set; }

    private bool _hasFiredThisScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("GameCompletionBridge");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<GameCompletionBridge>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _hasFiredThisScene = false;
    }

    public void OnConfettiPlayed()
    {
        if (_hasFiredThisScene) return;
        if (!JourneyManager.IsJourneyActive) return;

        // Only fire from actual game scenes, not DiscoveryReveal/Home/World
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "DiscoveryReveal" || sceneName == "HomeScene" || sceneName == "WorldScene")
            return;

        _hasFiredThisScene = true;

        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : null;
        StartCoroutine(DelayedComplete(gameId));
    }

    private System.Collections.IEnumerator DelayedComplete(string gameId)
    {
        yield return new WaitForSeconds(2f);
        JourneyManager.Instance?.OnCurrentGameFinished(gameId);
    }
}
