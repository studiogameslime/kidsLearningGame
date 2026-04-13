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

    /// <summary>Achievement sticker ID awarded this round, or null.</summary>
    public string LastAwardedAchievementId { get; private set; }

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
    /// Increment game count for a specific profile (used in 2-player mode
    /// to count games without awarding stars/stickers/discoveries).
    /// </summary>
    public static void IncrementGameCount(UserProfile profile)
    {
        if (profile == null) return;
        var jp = profile.journey;
        jp.totalGamesCompleted++;

        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : null;
        if (!string.IsNullOrEmpty(gameId))
        {
            var stat = jp.GetOrCreateStat(gameId);
            stat.timesPlayedInJourney++;
        }

        ProfileManager.Instance.Save();
    }

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

        // ── Check achievement milestone FIRST (10/30/50 rounds) ──
        TryAwardAchievement(gameId, jp);

        // ── Award sticker — skip if achievement was awarded this round ──
        if (LastAwardedAchievementId == null)
            TryPickSticker(gameId, jp);
        else
            LastAwardedStickerId = null;

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
    /// Award sticker + track stats on non-confetti rounds.
    /// Stars and discoveries are skipped, but sticker pacing, game stats,
    /// and achievement milestones still run every round.
    /// </summary>
    public void AwardStickerOnly()
    {
        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : null;
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        var jp = profile.journey;

        // Increment per-game stat (needed for achievement tracking)
        if (!string.IsNullOrEmpty(gameId))
        {
            var stat = jp.GetOrCreateStat(gameId);
            stat.timesPlayedInJourney++;
        }

        TryAwardAchievement(gameId, jp);

        if (LastAwardedAchievementId == null)
            TryPickSticker(gameId, jp);
        else
            LastAwardedStickerId = null;

        // Save stats (sticker/achievement collection is deferred to balloon pop)
        ProfileManager.Instance.Save();
    }

    /// <summary>
    /// Pick a sticker — NOT added to collection until balloon is popped.
    /// </summary>
    private void TryPickSticker(string gameId, JourneyProgress jp)
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
                LastAwardedStickerId = stickerId;
                jp.roundsUntilNextSticker = Random.Range(3, 6);
                Debug.Log($"[Sticker] Picked {stickerId} from {gameId} (pending pop)");
            }
            else
            {
                jp.roundsUntilNextSticker = 1;
            }
        }
    }

    /// <summary>Add sticker to collection. Called when balloon is popped.</summary>
    public static void CollectSticker(string stickerId)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null || string.IsNullOrEmpty(stickerId)) return;
        if (profile.journey.collectedStickerIds.Contains(stickerId)) return;
        profile.journey.collectedStickerIds.Add(stickerId);
        ProfileManager.Instance.Save();
        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : "unknown";
        FirebaseAnalyticsManager.LogStickerCollected(stickerId, $"game_{gameId}");
        Debug.Log($"[Sticker] Collected {stickerId} (balloon popped)");
    }

    private void TryAwardAchievement(string gameId, JourneyProgress jp)
    {
        LastAwardedAchievementId = null;
        if (string.IsNullOrEmpty(gameId)) return;

        var stat = jp.GetOrCreateStat(gameId);
        string achievementId = StickerCatalog.CheckAchievement(gameId, stat.timesPlayedInJourney, jp.collectedStickerIds);
        if (achievementId != null)
        {
            LastAwardedAchievementId = achievementId;
            Debug.Log($"[Achievement] Picked {achievementId} (played {stat.timesPlayedInJourney}x, pending pop)");
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
