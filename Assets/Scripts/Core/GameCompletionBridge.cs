using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bridges game completion to analytics and journey systems.
///
/// Flow:
/// 1. Game calls ConfettiController.Play()
/// 2. Confetti starts → OnConfettiPlayed() fires immediately → registers analytics
/// 3. Confetti animation finishes → OnCelebrationFinished() fires → triggers scene transition
///
/// This ensures celebrations complete fully before any scene change.
/// DontDestroyOnLoad singleton.
/// </summary>
public class GameCompletionBridge : MonoBehaviour
{
    public static GameCompletionBridge Instance { get; private set; }

    private bool _analyticsRegistered;
    private bool _celebrationComplete;
    private bool _navigationLocked;

    /// <summary>
    /// Active stats collector for the current game session.
    /// Games can set this to provide detailed metrics. If null, a minimal
    /// session is registered automatically on confetti.
    /// </summary>
    public GameStatsCollector ActiveCollector { get; set; }

    /// <summary>True while celebration is playing — used to block exit buttons.</summary>
    public static bool IsCelebrating => Instance != null && Instance._navigationLocked;

    /// <summary>
    /// True if the journey system will handle navigation after this game completes.
    /// Games should check this before auto-reloading rounds — if true, skip the reload
    /// and let the bridge navigate to the discovery scene or next journey step.
    /// </summary>
    public static bool WillJourneyNavigate => JourneyManager.IsJourneyActive;

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
        _analyticsRegistered = false;
        _celebrationComplete = false;
        _navigationLocked = false;
        ActiveCollector = null;
    }

    /// <summary>
    /// Called by ConfettiController.Play() at the START of the celebration.
    /// Registers analytics immediately (so data is captured) but does NOT trigger navigation.
    /// </summary>
    public void OnConfettiPlayed()
    {
        if (_analyticsRegistered) return;

        // Only fire from actual game scenes
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "DiscoveryReveal" || sceneName == "HomeScene" ||
            sceneName == "WorldScene" || sceneName == "DrawingGallery")
            return;

        _analyticsRegistered = true;
        _navigationLocked = true;

        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : null;

        // Register analytics immediately
        if (ActiveCollector != null)
        {
            ActiveCollector.Finalize(completed: true);
            ActiveCollector = null;
        }
        else if (!string.IsNullOrEmpty(gameId))
        {
            var minimal = new GameSessionData
            {
                gameId = gameId,
                completed = true,
                difficultyLevel = StatsManager.Instance != null
                    ? StatsManager.Instance.GetGameDifficulty(gameId) : 1,
                startTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                correctActions = 1,
                totalActions = 1,
                mistakes = 0,
                hintsUsed = 0
            };
            Debug.Log($"[Analytics] Fallback minimal session for {gameId}");
            StatsManager.Instance?.RegisterGameSession(minimal);
        }
    }

    /// <summary>
    /// Called by ConfettiController AFTER the celebration animation fully completes.
    /// This is where scene transitions are safe to trigger.
    /// </summary>
    public void OnCelebrationFinished()
    {
        if (_celebrationComplete) return;
        _celebrationComplete = true;
        _navigationLocked = false;

        // Journey chaining (only when journey is active)
        if (!JourneyManager.IsJourneyActive) return;

        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : null;
        StartCoroutine(DelayedComplete(gameId));
    }

    private System.Collections.IEnumerator DelayedComplete(string gameId)
    {
        // Short pause after celebration before transition
        yield return new WaitForSeconds(0.5f);
        JourneyManager.Instance?.OnCurrentGameFinished(gameId);
    }
}
