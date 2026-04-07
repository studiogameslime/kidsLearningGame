using UnityEngine;

/// <summary>
/// Tracks app session duration and days since install.
/// Auto-creates via RuntimeInitializeOnLoadMethod.
/// Logs app_session_duration on app pause/quit.
/// Sets days_since_install user property on init.
/// </summary>
public class AppSessionTracker : MonoBehaviour
{
    private static AppSessionTracker _instance;
    private float _sessionStartTime;
    private bool _logged;

    private const string InstallDateKey = "app_install_date";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null) return;
        var go = new GameObject("[AppSessionTracker]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AppSessionTracker>();
    }

    private void Awake()
    {
        _sessionStartTime = Time.realtimeSinceStartup;

        // Track install date
        if (!PlayerPrefs.HasKey(InstallDateKey))
        {
            PlayerPrefs.SetString(InstallDateKey, System.DateTime.UtcNow.ToString("o"));
            PlayerPrefs.Save();
        }

        // Set days_since_install user property
        string installDateStr = PlayerPrefs.GetString(InstallDateKey, "");
        if (System.DateTime.TryParse(installDateStr, out System.DateTime installDate))
        {
            int days = (System.DateTime.UtcNow - installDate).Days;
            FirebaseAnalyticsManager.LogDaysSinceInstall(days);
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && !_logged)
        {
            LogSession();
        }
        else if (!paused)
        {
            // Resumed — reset session timer
            _sessionStartTime = Time.realtimeSinceStartup;
            _logged = false;
        }
    }

    private void OnApplicationQuit()
    {
        if (!_logged) LogSession();
    }

    private void LogSession()
    {
        _logged = true;
        float duration = Time.realtimeSinceStartup - _sessionStartTime;
        FirebaseAnalyticsManager.LogAppSessionDuration(duration);

        // Schedule parent dashboard notification 1 hour after leaving
        ScheduleParentDashboardNotification();
    }

    private static readonly int[] NotificationMilestones = { 5, 10, 20, 35, 50, 75, 100, 150, 200 };

    private void ScheduleParentDashboardNotification()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        int games = profile.journey?.totalGamesCompleted ?? 0;
        if (games < 5) return;

        // Find the highest milestone reached
        int milestone = 0;
        foreach (int m in NotificationMilestones)
            if (games >= m) milestone = m;

        if (milestone == 0) return;

        // Check if we already sent for this milestone
        string key = $"notif_milestone_{profile.id}";
        int lastSent = PlayerPrefs.GetInt(key, 0);
        if (lastSent >= milestone) return;

        // Mark as sent
        PlayerPrefs.SetInt(key, milestone);
        PlayerPrefs.Save();

        string childName = profile.displayName ?? "";
        string title = $"{childName} \u05DB\u05D1\u05E8 \u05E9\u05D9\u05D7\u05E7 \u05D1-{milestone} \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD!"; // X כבר שיחק ב-Y משחקים!
        string body = "\u05D4\u05D9\u05DB\u05E0\u05E1\u05D5 \u05DC\u05D0\u05D6\u05D5\u05E8 \u05D4\u05D5\u05E8\u05D9\u05DD \u05DB\u05D3\u05D9 \u05DC\u05E8\u05D0\u05D5\u05EA \u05D0\u05EA \u05D4\u05D4\u05EA\u05E7\u05D3\u05DE\u05D5\u05EA"; // היכנסו לאזור הורים כדי לראות את ההתקדמות

        // ScheduleOneShot with unique tag per milestone (its internal guard won't block since tag is unique)
        NotificationService.Instance?.ScheduleOneShot(
            $"parent_dashboard_{milestone}_games",
            title, body, delaySeconds: 3600.0 // 1 hour
        );
    }
}
