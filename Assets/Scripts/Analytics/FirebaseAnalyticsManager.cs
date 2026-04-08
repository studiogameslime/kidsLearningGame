using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Analytics;

/// <summary>
/// Firebase Analytics wrapper — singleton, auto-initializes, COPPA compliant.
/// Call static methods from anywhere to log events.
/// </summary>
public class FirebaseAnalyticsManager : MonoBehaviour
{
    public static FirebaseAnalyticsManager Instance { get; private set; }
    private static bool _initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("[FirebaseAnalytics]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<FirebaseAnalyticsManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        try { InitFirebase(); }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Firebase] Init failed (non-fatal): {e.Message}");
        }
    }

    private void InitFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            try
            {
            if (task.Result == DependencyStatus.Available)
            {
                // COPPA compliance — no personalized ads, child-directed
                FirebaseAnalytics.SetUserProperty("allow_personalized_ads", "false");
                FirebaseAnalytics.SetConsent(
                    new Dictionary<ConsentType, ConsentStatus>
                    {
                        { ConsentType.AnalyticsStorage, ConsentStatus.Granted },
                        { ConsentType.AdStorage, ConsentStatus.Denied },
                        { ConsentType.AdPersonalization, ConsentStatus.Denied },
                        { ConsentType.AdUserData, ConsentStatus.Denied },
                    });

                // Set session timeout to 30 minutes
                FirebaseAnalytics.SetSessionTimeoutDuration(new System.TimeSpan(0, 30, 0));

                _initialized = true;
                Debug.Log("[Firebase] Analytics initialized (COPPA mode)");

                // Set initial user properties
                UpdateUserProperties();
            }
            else
            {
                Debug.LogWarning($"[Firebase] Could not resolve dependencies: {task.Result}");
            }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Firebase] Init callback failed (non-fatal): {e.Message}");
            }
        });
    }

    // ══════════════════════════════════
    //  USER PROPERTIES
    // ══════════════════════════════════

    public static void UpdateUserProperties()
    {
        if (!_initialized) return;

        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        // Age bucket (not exact age — COPPA safe)
        string ageBucket;
        if (profile.age <= 3) ageBucket = "2-3";
        else if (profile.age <= 5) ageBucket = "4-5";
        else ageBucket = "6+";

        FirebaseAnalytics.SetUserProperty("child_age_bucket", ageBucket);
        FirebaseAnalytics.SetUserProperty("total_stars",
            (profile.journey?.totalStars ?? 0).ToString());
        FirebaseAnalytics.SetUserProperty("games_played",
            (profile.journey?.totalGamesCompleted ?? 0).ToString());
    }

    // ══════════════════════════════════
    //  GAME EVENTS
    // ══════════════════════════════════

    public static void LogGameStarted(string gameId, int difficulty)
    {
        if (!_initialized) return;
        string gameName = ParentDashboardViewModel.GetGameName(gameId);
        FirebaseAnalytics.LogEvent("game_started",
            new Parameter("game_id", gameId),
            new Parameter("game_name", gameName),
            new Parameter("difficulty", difficulty));
    }

    public static void LogGameCompleted(string gameId, int difficulty,
        float score, int mistakes, float duration)
    {
        if (!_initialized) return;
        string gameName = ParentDashboardViewModel.GetGameName(gameId);
        FirebaseAnalytics.LogEvent("game_completed",
            new Parameter("game_id", gameId),
            new Parameter("game_name", gameName),
            new Parameter("difficulty", difficulty),
            new Parameter("score", (double)score),
            new Parameter("mistakes", mistakes),
            new Parameter("duration_seconds", (double)duration));
    }

    public static void LogGameExited(string gameId)
    {
        if (!_initialized) return;
        string gameName = ParentDashboardViewModel.GetGameName(gameId);
        FirebaseAnalytics.LogEvent("game_exited",
            new Parameter("game_id", gameId),
            new Parameter("game_name", gameName));
    }

    // ══════════════════════════════════
    //  SCREEN EVENTS
    // ══════════════════════════════════

    public static void LogScreenView(string screenName)
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventScreenView,
            new Parameter(FirebaseAnalytics.ParameterScreenName, screenName));
    }

    // ══════════════════════════════════
    //  PROFILE EVENTS
    // ══════════════════════════════════

    public static void LogProfileCreated(int age, string favoriteAnimal)
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("profile_created",
            new Parameter("age", age),
            new Parameter("favorite_animal", favoriteAnimal ?? "none"));
        UpdateUserProperties();
    }

    // ══════════════════════════════════
    //  AQUARIUM EVENTS
    // ══════════════════════════════════

    public static void LogAquariumFeed()
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("aquarium_feed");
    }

    public static void LogAquariumGiftOpened(string itemId)
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("aquarium_gift_opened",
            new Parameter("item_id", itemId));
    }

    // ══════════════════════════════════
    //  COLOR STUDIO EVENTS
    // ══════════════════════════════════

    public static void LogColorMixed(string colorA, string colorB, string result)
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("color_mixed",
            new Parameter("color_a", colorA),
            new Parameter("color_b", colorB),
            new Parameter("result", result));
    }

    // ══════════════════════════════════
    //  DISCOVERY EVENTS
    // ══════════════════════════════════

    public static void LogDiscovery(string type, string id)
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("discovery_unlocked",
            new Parameter("type", type),
            new Parameter("item_id", id));
    }

    // ══════════════════════════════════
    //  PARENT DASHBOARD
    // ══════════════════════════════════

    public static void LogParentDashboardOpened()
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("parent_dashboard_opened");
    }

    public static void LogParentTabSwitched(string tabName)
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("parent_tab_switched",
            new Parameter("tab_name", tabName));
    }

    public static void LogGameVisibilityChanged(string gameId, string gameName, bool visible)
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("game_visibility_changed",
            new Parameter("game_id", gameId),
            new Parameter("game_name", gameName),
            new Parameter("visible", visible ? "on" : "off"));
    }

    public static void LogDifficultyChanged(string gameId, string gameName, int newDifficulty, bool isManual)
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("difficulty_changed",
            new Parameter("game_id", gameId),
            new Parameter("game_name", gameName),
            new Parameter("difficulty", newDifficulty),
            new Parameter("manual", isManual ? "yes" : "no"));
    }

    // ══════════════════════════════════
    //  WORLD INTERACTIONS (throttled)
    // ══════════════════════════════════

    private static float _lastBalloonPop;
    private static float _lastAnimalTap;
    private static float _lastAnimalDrag;
    private static float _lastBubblePop;
    private static float _lastDayNight;
    private const float ThrottleSeconds = 2f; // max 1 event per 2 seconds

    public static void LogBalloonPopped()
    {
        if (!_initialized || Time.realtimeSinceStartup - _lastBalloonPop < ThrottleSeconds) return;
        _lastBalloonPop = Time.realtimeSinceStartup;
        FirebaseAnalytics.LogEvent("balloon_popped");
    }

    public static void LogAnimalTapped(string animalId)
    {
        if (!_initialized || Time.realtimeSinceStartup - _lastAnimalTap < ThrottleSeconds) return;
        _lastAnimalTap = Time.realtimeSinceStartup;
        FirebaseAnalytics.LogEvent("animal_tapped",
            new Parameter("animal_id", animalId));
    }

    public static void LogAnimalDragged(string animalId)
    {
        if (!_initialized || Time.realtimeSinceStartup - _lastAnimalDrag < ThrottleSeconds) return;
        _lastAnimalDrag = Time.realtimeSinceStartup;
        FirebaseAnalytics.LogEvent("animal_dragged",
            new Parameter("animal_id", animalId));
    }

    public static void LogAquariumBubblePopped()
    {
        if (!_initialized || Time.realtimeSinceStartup - _lastBubblePop < ThrottleSeconds) return;
        _lastBubblePop = Time.realtimeSinceStartup;
        FirebaseAnalytics.LogEvent("aquarium_bubble_popped");
    }

    public static void LogDayNightToggled(string target)
    {
        if (!_initialized || Time.realtimeSinceStartup - _lastDayNight < ThrottleSeconds) return;
        _lastDayNight = Time.realtimeSinceStartup;
        FirebaseAnalytics.LogEvent("day_night_toggled",
            new Parameter("target", target));
    }

    // ══════════════════════════════════
    //  COLLECTIBLE ALBUM
    // ══════════════════════════════════

    public static void LogAlbumOpened()
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("album_opened");
    }

    public static void LogAlbumPageViewed(int pageIndex)
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("album_page_viewed",
            new Parameter("page", pageIndex));
    }

    // ══════════════════════════════════
    //  SHARING
    // ══════════════════════════════════

    public static void LogParentImageUploaded()
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("parent_image_uploaded");
    }

    public static void LogCertificateShared()
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("certificate_shared");
    }

    public static void LogDrawingShared()
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("drawing_shared");
    }

    // ══════════════════════════════════
    //  SANDBOX SESSIONS
    // ══════════════════════════════════

    public static void LogSandboxSession(string sandboxId, float durationSeconds)
    {
        if (!_initialized || durationSeconds < 2f) return; // ignore very short sessions
        FirebaseAnalytics.LogEvent("sandbox_session",
            new Parameter("sandbox_id", sandboxId),
            new Parameter("duration_seconds", (double)durationSeconds));
    }

    // ══════════════════════════════════
    //  APP SESSION
    // ══════════════════════════════════

    public static void LogSceneDuration(string sceneName, float durationSeconds)
    {
        if (!_initialized || durationSeconds < 2f) return;
        FirebaseAnalytics.LogEvent("scene_duration",
            new Parameter("scene_name", sceneName),
            new Parameter("duration_seconds", (double)durationSeconds));
    }

    public static void LogGameSessionDuration(string gameId, float durationSeconds)
    {
        if (!_initialized || durationSeconds < 3f) return;
        FirebaseAnalytics.LogEvent("game_session_duration",
            new Parameter("game_id", gameId),
            new Parameter("game_name", ParentDashboardViewModel.GetGameName(gameId)),
            new Parameter("duration_seconds", (double)durationSeconds));
    }

    public static void LogAppSessionDuration(float durationSeconds)
    {
        if (!_initialized || durationSeconds < 5f) return;
        FirebaseAnalytics.LogEvent("app_session_duration",
            new Parameter("duration_seconds", (double)durationSeconds));
    }

    public static void LogFirstGamePlayed(string gameId)
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("first_game_played",
            new Parameter("game_id", gameId),
            new Parameter("game_name", ParentDashboardViewModel.GetGameName(gameId)));
    }

    // ══════════════════════════════════
    //  MILESTONES
    // ══════════════════════════════════

    public static void LogMilestoneReached(string type, int value)
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("milestone_reached",
            new Parameter("milestone_type", type),
            new Parameter("milestone_value", value));
    }

    // ══════════════════════════════════
    //  PARENT DASHBOARD
    // ══════════════════════════════════

    public static void LogParentSessionDuration(float durationSeconds)
    {
        if (!_initialized || durationSeconds < 3f) return;
        FirebaseAnalytics.LogEvent("parent_session_duration",
            new Parameter("duration_seconds", (double)durationSeconds));
    }

    public static void LogParentChangedSetting(string setting, string value)
    {
        if (!_initialized) return;
        FirebaseAnalytics.LogEvent("parent_changed_setting",
            new Parameter("setting", setting),
            new Parameter("value", value));
    }

    // ══════════════════════════════════
    //  RETENTION
    // ══════════════════════════════════

    public static void LogDaysSinceInstall(int days)
    {
        if (!_initialized) return;
        FirebaseAnalytics.SetUserProperty("days_since_install", days.ToString());
    }
}
