using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orchestrates the discovery journey: chains games, triggers discoveries, manages session.
/// DontDestroyOnLoad singleton. Requires GameDatabase loaded to pick games.
/// </summary>
public class JourneyManager : MonoBehaviour
{
    public static JourneyManager Instance { get; private set; }
    public static bool IsJourneyActive { get; private set; }

    public DiscoveryEntry ActiveDiscovery { get; private set; }

    // Runtime session state (not persisted)
    private string lastPlayedGameId;
    private string secondLastPlayedGameId;
    private string pendingChainGameId;
    private string pendingChainAnimalId;
    private int sessionGamesPlayed;

    private GameDatabase _gameDb;

    private const string HomeScene = "WorldScene";
    private const string DiscoveryScene = "DiscoveryReveal";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("JourneyManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<JourneyManager>();
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
    }

    /// <summary>Set by HomeController or any scene that has a reference to the database.</summary>
    public void SetGameDatabase(GameDatabase db)
    {
        _gameDb = db;
    }

    private GameDatabase GetGameDb()
    {
        if (_gameDb != null) return _gameDb;

        // Try Resources folder
        _gameDb = Resources.Load<GameDatabase>("GameDatabase");
        if (_gameDb != null) return _gameDb;

        // Fallback: find any loaded GameDatabase
        var dbs = Resources.FindObjectsOfTypeAll<GameDatabase>();
        if (dbs.Length > 0) _gameDb = dbs[0];

        return _gameDb;
    }

    public void StartJourney()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null)
        {
            Debug.LogError("JourneyManager: No active profile.");
            return;
        }

        var jp = profile.journey;

        // Seed starter animals/colors on first journey
        // WorldController seeds starter GIFTS (pendingWorldRewards) on first world visit.
        // Here we only seed directly to unlocked lists if gifts weren't queued.
        if (jp.discoveryQueue.Count == 0 && jp.unlockedAnimalIds.Count == 0 && jp.pendingWorldRewards.Count == 0)
        {
            string favAnimal = profile.favoriteAnimalId;
            if (string.IsNullOrEmpty(favAnimal)) favAnimal = "Cat";
            if (!jp.unlockedAnimalIds.Contains(favAnimal))
                jp.unlockedAnimalIds.Add(favAnimal);

            foreach (var id in DiscoveryCatalog.StarterColors)
                if (!jp.unlockedColorIds.Contains(id)) jp.unlockedColorIds.Add(id);

            jp.gamesUntilNextDiscovery = 1;
            ProfileManager.Instance.Save();
        }
        else if (jp.gamesUntilNextDiscovery <= 0)
        {
            jp.gamesUntilNextDiscovery = 1;
            ProfileManager.Instance.Save();
        }

        // Reset runtime session
        lastPlayedGameId = null;
        secondLastPlayedGameId = null;
        pendingChainGameId = null;
        pendingChainAnimalId = null;
        sessionGamesPlayed = 0;
        ActiveDiscovery = null;

        IsJourneyActive = true;

        // First game uses the favorite animal — pick a game that actually has it as sub-item
        if (sessionGamesPlayed == 0)
        {
            string favAnimal = profile.favoriteAnimalId;
            if (string.IsNullOrEmpty(favAnimal)) favAnimal = "Cat";
            var allGameIds = GetAllVisibleGameIds(profile);
            string bestGame = FindGameWithAnimal(favAnimal, allGameIds);
            if (bestGame == null && allGameIds.Count > 0)
                bestGame = allGameIds[Random.Range(0, allGameIds.Count)];
            if (bestGame == null)
            {
                Debug.LogError("JourneyManager: No visible games. Ending journey.");
                EndJourney();
                return;
            }
            LoadGame(bestGame, favAnimal);
            return;
        }

        PickNextGame();
    }

    public void OnCurrentGameFinished(string completedGameId)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        var jp = profile.journey;
        jp.totalGamesCompleted++;
        sessionGamesPlayed++;

        // Update per-game stat
        if (!string.IsNullOrEmpty(completedGameId))
        {
            var stat = jp.GetOrCreateStat(completedGameId);
            stat.timesPlayedInJourney++;
        }

        if (DiscoveryCatalog.HasMore(jp))
            jp.gamesUntilNextDiscovery--;

        // Check for puzzle → coloring chain
        if (completedGameId == "puzzle" && GameContext.CurrentSelection != null)
        {
            pendingChainGameId = "coloring";
            pendingChainAnimalId = GameContext.CurrentSelection.categoryKey;
        }

        // Check for discovery
        if (jp.gamesUntilNextDiscovery <= 0 && DiscoveryCatalog.HasMore(jp))
        {
            // Try contextual discovery based on what was just played
            string animalKey = GameContext.CurrentSelection?.categoryKey;
            string colorKey = null; // TODO: populate from color-based games if needed

            ActiveDiscovery = DiscoveryCatalog.GetContextual(jp, animalKey, colorKey);
            if (ActiveDiscovery == null)
                ActiveDiscovery = DiscoveryCatalog.GetNext(jp);

            // Extra safety: skip if this exact item is already in the queue
            if (ActiveDiscovery != null)
            {
                bool alreadyQueued = false;
                foreach (var entry in jp.discoveryQueue)
                {
                    if (entry.type == ActiveDiscovery.type && entry.id == ActiveDiscovery.id)
                    {
                        alreadyQueued = true;
                        break;
                    }
                }
                if (alreadyQueued)
                {
                    // Try the next catalog item instead
                    ActiveDiscovery = DiscoveryCatalog.GetNext(jp);
                }
            }

            // If no valid discovery after duplicate check, skip and continue playing
            if (ActiveDiscovery == null)
            {
                jp.gamesUntilNextDiscovery = DiscoveryScheduler.CalcNextInterval(jp);
                ProfileManager.Instance.Save();
                PickNextGame();
                return;
            }

            jp.discoveryQueue.Add(ActiveDiscovery);
            jp.gamesUntilNextDiscovery = DiscoveryScheduler.CalcNextInterval(jp);
            ProfileManager.Instance.Save();
            BubbleTransition.LoadScene(DiscoveryScene);
            return;
        }

        // Check for pending chain
        if (!string.IsNullOrEmpty(pendingChainGameId))
        {
            LoadChainedGame();
            return;
        }

        ProfileManager.Instance.Save();
        PickNextGame();
    }

    public void ContinueAfterDiscovery()
    {
        // Queue as pending reward — do NOT add to unlocked lists yet.
        // The world will add to unlocked lists when the child opens the gift box.
        // This prevents the new animal/color from appearing before the gift is opened.
        var profile = ProfileManager.ActiveProfile;
        if (profile != null && ActiveDiscovery != null)
        {
            var jp = profile.journey;
            if (ActiveDiscovery.type == "animal" || ActiveDiscovery.type == "color")
            {
                // Skip if already unlocked or already pending
                bool alreadyUnlocked = (ActiveDiscovery.type == "animal" && jp.unlockedAnimalIds.Contains(ActiveDiscovery.id))
                                    || (ActiveDiscovery.type == "color" && jp.unlockedColorIds.Contains(ActiveDiscovery.id));
                bool alreadyPending = false;
                foreach (var e in jp.pendingWorldRewards)
                    if (e.type == ActiveDiscovery.type && e.id == ActiveDiscovery.id) { alreadyPending = true; break; }

                if (!alreadyUnlocked && !alreadyPending)
                {
                    jp.pendingWorldRewards.Add(new DiscoveryEntry
                        { type = ActiveDiscovery.type, id = ActiveDiscovery.id });
                }
            }
            ProfileManager.Instance.Save();
        }

        ActiveDiscovery = null;

        // Check for pending chain
        if (!string.IsNullOrEmpty(pendingChainGameId))
        {
            LoadChainedGame();
            return;
        }

        PickNextGame();
    }

    public void EndJourney()
    {
        IsJourneyActive = false;
        lastPlayedGameId = null;
        secondLastPlayedGameId = null;
        pendingChainGameId = null;
        pendingChainAnimalId = null;
        ActiveDiscovery = null;
        ProfileManager.Instance?.Save();
        BubbleTransition.LoadScene(HomeScene);
    }

    private void PickNextGame()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        var jp = profile.journey;
        var allGameIds = GetAllVisibleGameIds(profile);

        if (allGameIds.Count == 0)
        {
            Debug.LogError("JourneyManager: No visible games. Ending journey.");
            EndJourney();
            return;
        }

        // Filter out recently played
        var candidates = new List<string>();
        foreach (var id in allGameIds)
        {
            if (allGameIds.Count >= 3 && (id == lastPlayedGameId || id == secondLastPlayedGameId))
                continue;
            candidates.Add(id);
        }
        if (candidates.Count == 0)
            candidates.AddRange(allGameIds);

        // Weighted random: less-played games get higher weight
        int maxPlayed = 0;
        foreach (var id in candidates)
        {
            int played = jp.GetOrCreateStat(id).timesPlayedInJourney;
            if (played > maxPlayed) maxPlayed = played;
        }

        float totalWeight = 0f;
        var weights = new float[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            int played = jp.GetOrCreateStat(candidates[i]).timesPlayedInJourney;
            weights[i] = maxPlayed - played + 1;
            totalWeight += weights[i];
        }

        float roll = Random.Range(0f, totalWeight);
        string picked = candidates[0];
        float cumulative = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                picked = candidates[i];
                break;
            }
        }

        secondLastPlayedGameId = lastPlayedGameId;
        lastPlayedGameId = picked;

        LoadGame(picked, null);
    }

    /// <summary>Returns all game IDs visible to the profile (age-filtered from GameDatabase).</summary>
    private List<string> GetAllVisibleGameIds(UserProfile profile)
    {
        var db = GetGameDb();
        if (db == null) return new List<string>();

        var visible = GameVisibilityService.GetVisibleGames(profile, db.games);
        var ids = new List<string>();
        foreach (var g in visible)
            ids.Add(g.id);
        return ids;
    }

    private string FindGameWithAnimal(string animalId, List<string> gameIds)
    {
        var db = GetGameDb();
        if (db == null) return null;

        string lowerAnimal = animalId.ToLower();
        var matches = new List<string>();

        foreach (var gameId in gameIds)
        {
            foreach (var g in db.games)
            {
                if (g.id != gameId || !g.hasSubItems || g.subItems == null) continue;
                foreach (var sub in g.subItems)
                {
                    if (sub.categoryKey != null && sub.categoryKey.ToLower() == lowerAnimal)
                    {
                        matches.Add(gameId);
                        break;
                    }
                }
                break;
            }
        }

        return matches.Count > 0 ? matches[Random.Range(0, matches.Count)] : null;
    }

    private void LoadChainedGame()
    {
        string gameId = pendingChainGameId;
        string animalId = pendingChainAnimalId;
        pendingChainGameId = null;
        pendingChainAnimalId = null;

        LoadGame(gameId, animalId);
    }

    private void LoadGame(string gameId, string animalCategoryKey)
    {
        var db = GetGameDb();
        if (db == null)
        {
            Debug.LogError("JourneyManager: No GameDatabase found.");
            return;
        }

        GameItemData gameData = null;
        foreach (var g in db.games)
        {
            if (g.id == gameId)
            {
                gameData = g;
                break;
            }
        }

        if (gameData == null)
        {
            Debug.LogError($"JourneyManager: Game '{gameId}' not found in database.");
            return;
        }

        GameContext.CurrentGame = gameData;
        GameContext.CurrentSelection = null;
        GameContext.CustomTexture = null;

        // If game has sub-items and we have a specific animal, find the matching sub-item
        if (!string.IsNullOrEmpty(animalCategoryKey) && gameData.subItems != null)
        {
            foreach (var sub in gameData.subItems)
            {
                if (sub.categoryKey == animalCategoryKey)
                {
                    GameContext.CurrentSelection = sub;
                    break;
                }
            }
        }

        // If game has sub-items but no specific selection, pick a random one from unlocked animals
        if (GameContext.CurrentSelection == null && gameData.hasSubItems && gameData.subItems != null && gameData.subItems.Count > 0)
        {
            var profile = ProfileManager.ActiveProfile;
            if (profile != null)
            {
                var jp = profile.journey;
                var validSubs = new List<SubItemData>();
                foreach (var sub in gameData.subItems)
                {
                    // Match by categoryKey against unlocked animals
                    string key = sub.categoryKey;
                    if (!string.IsNullOrEmpty(key))
                    {
                        // Check if the animal name matches (case-insensitive)
                        foreach (var animalId in jp.unlockedAnimalIds)
                        {
                            if (animalId.ToLower() == key.ToLower())
                            {
                                validSubs.Add(sub);
                                break;
                            }
                        }
                    }
                }
                if (validSubs.Count > 0)
                    GameContext.CurrentSelection = validSubs[Random.Range(0, validSubs.Count)];
                else if (gameData.subItems.Count > 0)
                    GameContext.CurrentSelection = gameData.subItems[Random.Range(0, gameData.subItems.Count)];
            }
        }

        string scene = GameContext.CurrentSelection != null && !string.IsNullOrEmpty(GameContext.CurrentSelection.targetSceneName)
            ? GameContext.CurrentSelection.targetSceneName
            : gameData.targetSceneName;

        BubbleTransition.LoadScene(scene);
    }
}
