using UnityEngine;

/// <summary>
/// Google Play In-App Updates — shows native update dialog when a new version is available.
/// Uses FLEXIBLE update type: non-blocking download, then prompts user to install.
/// Falls back to IMMEDIATE if flexible is not available.
/// Auto-checks on app start via RuntimeInitializeOnLoadMethod.
/// Checks on resume at most once every 12 hours to avoid spamming.
/// </summary>
public class InAppUpdateManager : MonoBehaviour
{
    private static InAppUpdateManager _instance;

    // Cooldown: check at most once per 12 hours
    private const string LastCheckKey = "inapp_update_last_check";
    private const double CheckCooldownHours = 12.0;

    private volatile bool _downloadReady;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _appUpdateManager;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null) return;
        var go = new GameObject("InAppUpdateManager");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<InAppUpdateManager>();
    }

    private void Start()
    {
        CheckForUpdateIfCooldownElapsed();
    }

    /// <summary>Re-check on resume, but only if cooldown has elapsed.</summary>
    private void OnApplicationPause(bool paused)
    {
        if (!paused) CheckForUpdateIfCooldownElapsed();
    }

    private void Update()
    {
        // When flexible download completes, show a prompt instead of force-restarting
        if (_downloadReady)
        {
            _downloadReady = false;
            ShowUpdateReadyPrompt();
        }
    }

    private void CheckForUpdateIfCooldownElapsed()
    {
        string lastCheckStr = PlayerPrefs.GetString(LastCheckKey, "");
        if (!string.IsNullOrEmpty(lastCheckStr)
            && System.DateTime.TryParse(lastCheckStr, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var lastCheck))
        {
            double hoursSince = (System.DateTime.UtcNow - lastCheck).TotalHours;
            if (hoursSince < CheckCooldownHours)
            {
                Debug.Log($"[InAppUpdate] Skipping check — last check {hoursSince:F1}h ago");
                return;
            }
        }

        PlayerPrefs.SetString(LastCheckKey, System.DateTime.UtcNow.ToString("o"));
        PlayerPrefs.Save();

        CheckForUpdate();
    }

    private void CheckForUpdate()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaObject activity;
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            if (_appUpdateManager == null)
            {
                using (var factory = new AndroidJavaClass(
                    "com.google.android.play.core.appupdate.AppUpdateManagerFactory"))
                {
                    _appUpdateManager = factory.CallStatic<AndroidJavaObject>("create", activity);
                }
            }

            var appUpdateInfoTask = _appUpdateManager.Call<AndroidJavaObject>("getAppUpdateInfo");
            // activity is kept alive by the listener for the async callback
            appUpdateInfoTask.Call<AndroidJavaObject>("addOnSuccessListener",
                new UpdateInfoListener(this, activity));

            Debug.Log("[InAppUpdate] Checking for updates...");
        }
        catch (System.Exception e)
        {
            Debug.Log($"[InAppUpdate] Check failed: {e.Message}");
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void StartUpdate(AndroidJavaObject appUpdateInfo, AndroidJavaObject activity, bool immediate)
    {
        try
        {
            int updateType = immediate ? 1 : 0; // IMMEDIATE=1, FLEXIBLE=0
            using (var optionsBuilderClass = new AndroidJavaClass(
                "com.google.android.play.core.appupdate.AppUpdateOptions"))
            using (var optionsBuilder = optionsBuilderClass.CallStatic<AndroidJavaObject>(
                "newBuilder", updateType))
            using (var options = optionsBuilder.Call<AndroidJavaObject>("build"))
            {
                // Register install state listener for flexible updates
                if (!immediate)
                {
                    _appUpdateManager.Call("registerListener", new InstallStateListener(this));
                }

                _appUpdateManager.Call<AndroidJavaObject>(
                    "startUpdateFlowForResult", appUpdateInfo, activity, options);
            }

            Debug.Log($"[InAppUpdate] {(immediate ? "Immediate" : "Flexible")} update flow started");
        }
        catch (System.Exception e)
        {
            Debug.Log($"[InAppUpdate] Start update failed: {e.Message}");
        }
    }

    /// <summary>Install a downloaded flexible update.</summary>
    public void CompleteUpdate()
    {
        try
        {
            _appUpdateManager?.Call("completeUpdate");
            Debug.Log("[InAppUpdate] Completing update (app will restart)");
        }
        catch (System.Exception e)
        {
            Debug.Log($"[InAppUpdate] Complete update failed: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        if (_appUpdateManager != null)
        {
            _appUpdateManager.Dispose();
            _appUpdateManager = null;
        }
    }
#endif

    // ── Update-ready prompt ──────────────────────────────────────────

    /// <summary>
    /// Show a simple in-game prompt so the user can choose when to restart.
    /// Avoids force-restarting mid-gameplay.
    /// </summary>
    private void ShowUpdateReadyPrompt()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Build a minimal full-screen overlay with a "Restart to update" button
        var canvas = FindOrCreateOverlayCanvas();
        if (canvas == null) { CompleteUpdate(); return; } // fallback: just install

        var overlay = new GameObject("UpdatePromptOverlay");
        overlay.transform.SetParent(canvas.transform, false);
        var overlayRT = overlay.AddComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        // Semi-transparent backdrop
        var bg = overlay.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0, 0, 0, 0.5f);

        // Snackbar-like bar at bottom
        var bar = new GameObject("Bar");
        bar.transform.SetParent(overlay.transform, false);
        var barRT = bar.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 0);
        barRT.anchorMax = new Vector2(1, 0);
        barRT.pivot = new Vector2(0.5f, 0);
        barRT.sizeDelta = new Vector2(0, 120);
        var barImg = bar.AddComponent<UnityEngine.UI.Image>();
        barImg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        // Label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(bar.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.05f, 0);
        labelRT.anchorMax = new Vector2(0.55f, 1);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        var label = labelGO.AddComponent<UnityEngine.UI.Text>();
        label.text = "\u05E2\u05D3\u05DB\u05D5\u05DF \u05DE\u05D5\u05DB\u05DF! \u05DC\u05D7\u05E6\u05D5 \u05DC\u05D4\u05EA\u05E7\u05E0\u05D4.";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 28;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleRight;

        // "Restart" button
        var btnGO = new GameObject("RestartBtn");
        btnGO.transform.SetParent(bar.transform, false);
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.6f, 0.15f);
        btnRT.anchorMax = new Vector2(0.95f, 0.85f);
        btnRT.offsetMin = Vector2.zero;
        btnRT.offsetMax = Vector2.zero;
        var btnImg = btnGO.AddComponent<UnityEngine.UI.Image>();
        btnImg.color = new Color(0.2f, 0.7f, 0.3f, 1f);
        var btn = btnGO.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = btnImg;

        var btnLabelGO = new GameObject("BtnLabel");
        btnLabelGO.transform.SetParent(btnGO.transform, false);
        var btnLabelRT = btnLabelGO.AddComponent<RectTransform>();
        btnLabelRT.anchorMin = Vector2.zero;
        btnLabelRT.anchorMax = Vector2.one;
        btnLabelRT.offsetMin = Vector2.zero;
        btnLabelRT.offsetMax = Vector2.zero;
        var btnLabel = btnLabelGO.AddComponent<UnityEngine.UI.Text>();
        btnLabel.text = "\u05E2\u05D3\u05DB\u05DF \u05E2\u05DB\u05E9\u05D9\u05D5";
        btnLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnLabel.fontSize = 26;
        btnLabel.color = Color.white;
        btnLabel.alignment = TextAnchor.MiddleCenter;

        btn.onClick.AddListener(() =>
        {
            Destroy(overlay);
            CompleteUpdate();
        });

        // Dismiss on backdrop tap (close prompt, update installs next launch)
        var dismissBtn = overlay.AddComponent<UnityEngine.UI.Button>();
        dismissBtn.onClick.AddListener(() => Destroy(overlay));
#endif
    }

    private Canvas FindOrCreateOverlayCanvas()
    {
        // Try to find an existing screen-space overlay canvas
        foreach (var c in FindObjectsOfType<Canvas>())
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                return c;
        }

        // Create a temporary one
        var go = new GameObject("UpdateOverlayCanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        go.AddComponent<UnityEngine.UI.CanvasScaler>();
        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return canvas;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // ── Listeners ──

    private class UpdateInfoListener : AndroidJavaProxy
    {
        private readonly InAppUpdateManager _manager;
        private readonly AndroidJavaObject _activity;

        public UpdateInfoListener(InAppUpdateManager manager, AndroidJavaObject activity)
            : base("com.google.android.gms.tasks.OnSuccessListener")
        {
            _manager = manager;
            _activity = activity;
        }

        public void onSuccess(AndroidJavaObject appUpdateInfo)
        {
            try
            {
                int availability = appUpdateInfo.Call<int>("updateAvailability");
                Debug.Log($"[InAppUpdate] Availability: {availability}");

                if (availability == 2) // UPDATE_AVAILABLE
                {
                    bool isFlexibleAllowed = appUpdateInfo.Call<bool>("isUpdateTypeAllowed", 0);
                    bool isImmediateAllowed = appUpdateInfo.Call<bool>("isUpdateTypeAllowed", 1);

                    if (isFlexibleAllowed)
                    {
                        _manager.StartUpdate(appUpdateInfo, _activity, false);
                    }
                    else if (isImmediateAllowed)
                    {
                        _manager.StartUpdate(appUpdateInfo, _activity, true);
                    }
                    else
                    {
                        Debug.Log("[InAppUpdate] Update available but no allowed type");
                    }
                }
                else if (availability == 3) // DEVELOPER_TRIGGERED_UPDATE_IN_PROGRESS
                {
                    _manager.StartUpdate(appUpdateInfo, _activity, true);
                }
                else
                {
                    Debug.Log("[InAppUpdate] No update available");
                }
            }
            catch (System.Exception e)
            {
                Debug.Log($"[InAppUpdate] Info check error: {e.Message}");
            }
        }
    }

    /// <summary>Listens for flexible update download completion.</summary>
    private class InstallStateListener : AndroidJavaProxy
    {
        private readonly InAppUpdateManager _manager;

        public InstallStateListener(InAppUpdateManager manager)
            : base("com.google.android.play.core.install.InstallStateUpdatedListener")
        {
            _manager = manager;
        }

        public void onStateUpdate(AndroidJavaObject installState)
        {
            try
            {
                int status = installState.Call<int>("installStatus");
                Debug.Log($"[InAppUpdate] Install status: {status}");

                // InstallStatus.DOWNLOADED = 11
                if (status == 11)
                {
                    Debug.Log("[InAppUpdate] Download complete — prompting user to install");
                    // Signal main thread to show the prompt (callback may be off main thread)
                    _manager._downloadReady = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.Log($"[InAppUpdate] State update error: {e.Message}");
            }
        }
    }
#endif
}
