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

        // Seed starters on first journey
        if (jp.unlockedGameIds.Count == 0)
        {
            foreach (var id in DiscoveryCatalog.StarterGameIds)
                if (!jp.unlockedGameIds.Contains(id)) jp.unlockedGameIds.Add(id);

            // Only unlock the favorite animal (not all 3 starters)
            string favAnimal = profile.favoriteAnimalId;
            if (string.IsNullOrEmpty(favAnimal)) favAnimal = "Cat";
            if (!jp.unlockedAnimalIds.Contains(favAnimal))
                jp.unlockedAnimalIds.Add(favAnimal);

            foreach (var id in DiscoveryCatalog.StarterColors)
                if (!jp.unlockedColorIds.Contains(id)) jp.unlockedColorIds.Add(id);
            jp.gamesUntilNextDiscovery = 1; // First discovery after game 1
            ProfileManager.Instance.Save();
        }

        // Purge any game IDs that no longer exist in the database
        var db = GetGameDb();
        if (db != null)
        {
            var validIds = new System.Collections.Generic.HashSet<string>();
            foreach (var g in db.games) validIds.Add(g.id);
            jp.unlockedGameIds.RemoveAll(id => !validIds.Contains(id));
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
            string bestGame = FindGameWithAnimal(favAnimal, jp.unlockedGameIds);
            if (bestGame == null)
                bestGame = jp.unlockedGameIds[Random.Range(0, jp.unlockedGameIds.Count)];
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
        // Apply the unlock
        var profile = ProfileManager.ActiveProfile;
        if (profile != null && ActiveDiscovery != null)
        {
            var jp = profile.journey;
            switch (ActiveDiscovery.type)
            {
                case "animal":
                    if (!jp.unlockedAnimalIds.Contains(ActiveDiscovery.id))
                        jp.unlockedAnimalIds.Add(ActiveDiscovery.id);
                    // Save as pending reward for gift box in World
                    jp.pendingWorldReward = new DiscoveryEntry
                        { type = ActiveDiscovery.type, id = ActiveDiscovery.id };
                    break;
                case "color":
                    if (!jp.unlockedColorIds.Contains(ActiveDiscovery.id))
                        jp.unlockedColorIds.Add(ActiveDiscovery.id);
                    jp.pendingWorldReward = new DiscoveryEntry
                        { type = ActiveDiscovery.type, id = ActiveDiscovery.id };
                    break;
                case "game":
                    if (!jp.unlockedGameIds.Contains(ActiveDiscovery.id))
                        jp.unlockedGameIds.Add(ActiveDiscovery.id);
                    break;
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
        var unlocked = jp.unlockedGameIds;

        if (unlocked.Count == 0)
        {
            Debug.LogError("JourneyManager: No unlocked games.");
            return;
        }

        // Filter out recently played and games not in the database
        var db = GetGameDb();
        var validIds = new System.Collections.Generic.HashSet<string>();
        if (db != null)
            foreach (var g in db.games)
                validIds.Add(g.id);

        var candidates = new List<string>();
        foreach (var id in unlocked)
        {
            if (!validIds.Contains(id)) continue; // skip removed games
            if (unlocked.Count >= 3 && (id == lastPlayedGameId || id == secondLastPlayedGameId))
                continue;
            candidates.Add(id);
        }
        if (candidates.Count == 0)
            candidates.AddRange(unlocked);

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
