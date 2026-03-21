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

    private bool _celebrationComplete;
    private bool _navigationLocked;

    /// <summary>
    /// Active stats collector for the current game session.
    /// Games set this so the bridge can abandon it on scene change.
    /// Analytics registration is handled directly by BaseMiniGame.
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
        _celebrationComplete = false;
        _navigationLocked = false;
        ActiveCollector = null;
    }

    /// <summary>
    /// Called when confetti plays. Analytics are now registered directly by
    /// BaseMiniGame on each round completion, so this only manages navigation state.
    /// </summary>
    public void OnConfettiPlayed()
    {
        _navigationLocked = true;
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
