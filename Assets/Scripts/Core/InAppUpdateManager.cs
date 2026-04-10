using UnityEngine;

/// <summary>
/// Google Play In-App Updates — shows native update dialog when a new version is available.
/// Uses FLEXIBLE update type: non-blocking download, then prompts to install.
/// Falls back to IMMEDIATE if flexible is not available.
/// Auto-checks on app start via RuntimeInitializeOnLoadMethod.
/// </summary>
public class InAppUpdateManager : MonoBehaviour
{
    private static InAppUpdateManager _instance;

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
        CheckForUpdate();
    }

    /// <summary>Re-check on resume in case an update completed while backgrounded.</summary>
    private void OnApplicationPause(bool paused)
    {
        if (!paused) CheckForUpdate();
    }

    private void CheckForUpdate()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (_appUpdateManager == null)
            {
                var factory = new AndroidJavaClass("com.google.android.play.core.appupdate.AppUpdateManagerFactory");
                _appUpdateManager = factory.CallStatic<AndroidJavaObject>("create", activity);
            }

            var appUpdateInfoTask = _appUpdateManager.Call<AndroidJavaObject>("getAppUpdateInfo");
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
            // Build AppUpdateOptions
            int updateType = immediate ? 1 : 0; // IMMEDIATE=1, FLEXIBLE=0
            var optionsBuilder = new AndroidJavaClass("com.google.android.play.core.appupdate.AppUpdateOptions")
                .CallStatic<AndroidJavaObject>("newBuilder", updateType);
            var options = optionsBuilder.Call<AndroidJavaObject>("build");

            // Register install state listener for flexible updates
            if (!immediate)
            {
                _appUpdateManager.Call("registerListener", new InstallStateListener(this));
            }

            var updateTask = _appUpdateManager.Call<AndroidJavaObject>(
                "startUpdateFlowForResult", appUpdateInfo, activity, options);

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
                    Debug.Log("[InAppUpdate] Download complete — triggering install");
                    _manager.CompleteUpdate();
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
