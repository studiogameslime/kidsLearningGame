using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bridges game completion to star/discovery systems.
///
/// Flow:
/// 1. Game calls ConfettiController.Play()
/// 2. Confetti starts → OnConfettiPlayed() fires → registers analytics
/// 3. Confetti animation finishes → OnCelebrationFinished() fires
/// 4. Awards star, checks for discovery, returns to game selection
///
/// DontDestroyOnLoad singleton.
/// </summary>
public class GameCompletionBridge : MonoBehaviour
{
    public static GameCompletionBridge Instance { get; private set; }

    private bool _celebrationComplete;
    private bool _navigationLocked;

    public GameStatsCollector ActiveCollector { get; set; }

    /// <summary>True while celebration is playing — used to block exit buttons.</summary>
    public static bool IsCelebrating => Instance != null && Instance._navigationLocked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("GameCompletionBridge");
        Instance = go.AddComponent<GameCompletionBridge>();
        DontDestroyOnLoad(go);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Abandon any active stats collector that wasn't finalized
        if (ActiveCollector != null)
        {
            ActiveCollector.Abandon();
            ActiveCollector = null;
        }
        _navigationLocked = false;
        _celebrationComplete = false;
    }

    /// <summary>
    /// Called immediately when confetti starts playing.
    /// Blocks exit buttons and locks navigation.
    /// </summary>
    public void OnConfettiPlayed()
    {
        _navigationLocked = true;
        _celebrationComplete = false;
    }

    /// <summary>
    /// Called when the celebration animation finishes.
    /// Awards star, checks discoveries, then returns to game selection.
    /// </summary>
    public void OnCelebrationFinished()
    {
        if (_celebrationComplete) return;
        _celebrationComplete = true;
        _navigationLocked = false;

        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : null;
        StartCoroutine(OnGameCompleted(gameId));
    }

    private System.Collections.IEnumerator OnGameCompleted(string gameId)
    {
        yield return new WaitForSeconds(0.3f);

        var profile = ProfileManager.ActiveProfile;
        if (profile == null) yield break;

        var jp = profile.journey;

        // ── Award star ──
        jp.totalGamesCompleted++;
        jp.totalStars++;

        // Update per-game stat
        if (!string.IsNullOrEmpty(gameId))
        {
            var stat = jp.GetOrCreateStat(gameId);
            stat.timesPlayedInJourney++;
        }

        // ── Check for discovery ──
        if (DiscoveryCatalog.HasMore(jp))
        {
            jp.gamesUntilNextDiscovery--;

            if (jp.gamesUntilNextDiscovery <= 0)
            {
                // Try contextual discovery based on what was just played
                string animalKey = GameContext.CurrentSelection?.categoryKey;
                var discovery = DiscoveryCatalog.GetContextual(jp, animalKey, null);
                if (discovery == null)
                    discovery = DiscoveryCatalog.GetNext(jp);

                if (discovery != null)
                {
                    jp.discoveryQueue.Add(discovery);

                    // Queue as pending world reward (gift box)
                    bool alreadyPending = false;
                    foreach (var r in jp.pendingWorldRewards)
                        if (r.type == discovery.type && r.id == discovery.id) { alreadyPending = true; break; }
                    if (!alreadyPending)
                        jp.pendingWorldRewards.Add(new DiscoveryEntry { type = discovery.type, id = discovery.id });

                    jp.gamesUntilNextDiscovery = DiscoveryScheduler.CalcNextInterval(jp);
                    ProfileManager.Instance.Save();

                    // Show discovery reveal
                    BubbleTransition.LoadScene("DiscoveryReveal");
                    yield break;
                }
                else
                {
                    // No valid discovery — reschedule
                    jp.gamesUntilNextDiscovery = DiscoveryScheduler.CalcNextInterval(jp);
                }
            }
        }

        ProfileManager.Instance.Save();

        // No discovery — return to game selection (no auto-next-game)
        // The game's own OnAfterComplete handles what happens next
    }
}
