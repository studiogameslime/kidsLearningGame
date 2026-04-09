using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bridges game completion to star/discovery systems.
///
/// Flow (controlled by BaseMiniGame.CompletionSequence):
/// 1. Game round completes → confetti + sounds play
/// 2. BaseMiniGame waits for confetti to finish
/// 3. BaseMiniGame calls AwardAndCheckDiscovery()
/// 4. Awards star, checks for discovery
/// 5. If discovery: loads DiscoveryReveal (returns true)
/// 6. If no discovery: returns false → game advances to next round
///
/// DontDestroyOnLoad singleton.
/// </summary>
public class GameCompletionBridge : MonoBehaviour
{
    public static GameCompletionBridge Instance { get; private set; }

    private bool _navigationLocked;

    public GameStatsCollector ActiveCollector { get; set; }

    /// <summary>Sticker ID awarded in the most recent round, or null if none.</summary>
    public string LastAwardedStickerId { get; private set; }

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
    }

    /// <summary>
    /// Lock navigation during celebration (called by BaseMiniGame when confetti starts).
    /// </summary>
    public void LockNavigation() => _navigationLocked = true;

    /// <summary>
    /// Unlock navigation (called after celebration completes).
    /// </summary>
    public void UnlockNavigation() => _navigationLocked = false;

    /// <summary>
    /// Award star and check for discovery. Called by BaseMiniGame AFTER confetti
    /// and sounds have finished, BEFORE advancing to the next round.
    /// Returns true if DiscoveryReveal was loaded (caller should yield break).
    /// </summary>
    public bool AwardAndCheckDiscovery()
    {
        _navigationLocked = false;

        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : null;

        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return false;

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

        // ── Award sticker (every 3-5 rounds, from game's category) ──
        TryAwardSticker(gameId, jp);

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
                    FirebaseAnalyticsManager.LogDiscovery(discovery.type, discovery.id);

                    // Show discovery reveal
                    BubbleTransition.LoadScene("DiscoveryReveal");
                    return true; // Discovery loaded — caller should NOT advance
                }
                else
                {
                    // No valid discovery — reschedule
                    jp.gamesUntilNextDiscovery = DiscoveryScheduler.CalcNextInterval(jp);
                }
            }
        }

        ProfileManager.Instance.Save();
        return false; // No discovery — caller may advance to next round
    }

    /// <summary>
    /// Award sticker only (no star, no discovery). Used on non-confetti rounds
    /// so sticker pacing works independently of confetti/star logic.
    /// </summary>
    public void AwardStickerOnly()
    {
        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : null;
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;
        TryAwardSticker(gameId, profile.journey);
        if (LastAwardedStickerId != null)
            ProfileManager.Instance.Save();
    }

    private void TryAwardSticker(string gameId, JourneyProgress jp)
    {
        LastAwardedStickerId = null;
        if (string.IsNullOrEmpty(gameId)) return;

        string prefix = StickerCatalog.GetCategoryForGame(gameId);
        if (prefix == null) return;

        jp.roundsUntilNextSticker--;
        if (jp.roundsUntilNextSticker <= 0)
        {
            string stickerId = StickerCatalog.PickRandomSticker(prefix, jp.collectedStickerIds);
            if (stickerId != null)
            {
                jp.collectedStickerIds.Add(stickerId);
                LastAwardedStickerId = stickerId;
                Debug.Log($"[Sticker] Awarded {stickerId} from game {gameId}");
            }
            jp.roundsUntilNextSticker = Random.Range(3, 6);
        }
    }

    /// <summary>
    /// Legacy callback — kept for backward compatibility but no longer triggers discovery.
    /// Discovery is now handled synchronously by BaseMiniGame via AwardAndCheckDiscovery().
    /// </summary>
    public void OnCelebrationFinished()
    {
        _navigationLocked = false;
    }
}
