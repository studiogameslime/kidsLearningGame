using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bridges the confetti event to JourneyManager and StatsManager. One-shot per scene.
/// Always registers analytics. Only chains journey when journey is active.
/// DontDestroyOnLoad singleton.
/// </summary>
public class GameCompletionBridge : MonoBehaviour
{
    public static GameCompletionBridge Instance { get; private set; }

    private bool _hasFiredThisScene;

    /// <summary>
    /// Active stats collector for the current game session.
    /// Games can set this to provide detailed metrics. If null, a minimal
    /// session is registered automatically on confetti.
    /// </summary>
    public GameStatsCollector ActiveCollector { get; set; }

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
        ActiveCollector = null;
    }

    public void OnConfettiPlayed()
    {
        if (_hasFiredThisScene) return;

        // Only fire from actual game scenes, not DiscoveryReveal/Home/World
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "DiscoveryReveal" || sceneName == "HomeScene" || sceneName == "WorldScene" || sceneName == "DrawingGallery")
            return;

        _hasFiredThisScene = true;

        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : null;

        // Register analytics (always, regardless of journey)
        if (ActiveCollector != null)
        {
            ActiveCollector.Finalize(completed: true);
            ActiveCollector = null;
        }
        else if (!string.IsNullOrEmpty(gameId))
        {
            // Minimal session when no collector was provided
            var minimal = new GameSessionData
            {
                gameId = gameId,
                completed = true,
                difficultyLevel = StatsManager.Instance != null
                    ? StatsManager.Instance.GetGameDifficulty(gameId) : 1,
                startTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                correctActions = 1,
                totalActions = 1
            };
            StatsManager.Instance?.RegisterGameSession(minimal);
        }

        // Journey chaining
        if (!JourneyManager.IsJourneyActive) return;
        StartCoroutine(DelayedComplete(gameId));
    }

    private System.Collections.IEnumerator DelayedComplete(string gameId)
    {
        yield return new WaitForSeconds(2f);
        JourneyManager.Instance?.OnCurrentGameFinished(gameId);
    }
}
