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

        // TEST: notification 5 minutes after app closes
        NotificationService.Instance?.ScheduleOneShot(
            "test_5min",
            "\u05D4\u05D9\u05D9 \u05D6\u05D4 \u05D8\u05E1\u05D8!", // היי זה טסט!
            "\u05D4\u05D4\u05EA\u05E8\u05D0\u05D4 \u05E2\u05D5\u05D1\u05D3\u05EA!", // ההתראה עובדת!
            delaySeconds: 300 // 5 minutes
        );
    }
}
