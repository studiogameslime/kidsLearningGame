using UnityEngine;

/// <summary>
/// Google Play In-App Updates — shows native update dialog when a new version is available.
/// Uses FLEXIBLE update type: shows a non-blocking banner, user can continue playing.
/// Auto-checks on app start via RuntimeInitializeOnLoadMethod.
/// </summary>
public class InAppUpdateManager : MonoBehaviour
{
    private static InAppUpdateManager _instance;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _appUpdateManager;
    private AndroidJavaObject _appUpdateInfo;
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

    private void CheckForUpdate()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var factory = new AndroidJavaClass("com.google.android.play.core.appupdate.AppUpdateManagerFactory");
            _appUpdateManager = factory.CallStatic<AndroidJavaObject>("create", activity);

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
    private void StartFlexibleUpdate(AndroidJavaObject appUpdateInfo, AndroidJavaObject activity)
    {
        try
        {
            // AppUpdateType.FLEXIBLE = 0
            int FLEXIBLE = 0;
            _appUpdateManager.Call<AndroidJavaObject>("startUpdateFlowForResult",
                appUpdateInfo, activity, FLEXIBLE, 100); // requestCode = 100
            Debug.Log("[InAppUpdate] Flexible update flow started");
        }
        catch (System.Exception e)
        {
            Debug.Log($"[InAppUpdate] Start update failed: {e.Message}");
        }
    }

    /// <summary>
    /// Called when a flexible update is downloaded. Triggers install.
    /// </summary>
    public void CompleteUpdate()
    {
        try
        {
            if (_appUpdateManager != null)
                _appUpdateManager.Call("completeUpdate");
        }
        catch (System.Exception e)
        {
            Debug.Log($"[InAppUpdate] Complete update failed: {e.Message}");
        }
    }

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
                // UpdateAvailability.UPDATE_AVAILABLE = 2
                int availability = appUpdateInfo.Call<int>("updateAvailability");
                Debug.Log($"[InAppUpdate] Availability: {availability}");

                if (availability == 2) // UPDATE_AVAILABLE
                {
                    // Check if flexible update is allowed
                    // AppUpdateType.FLEXIBLE = 0
                    bool isFlexibleAllowed = appUpdateInfo.Call<bool>("isUpdateTypeAllowed", 0);

                    if (isFlexibleAllowed)
                    {
                        Debug.Log("[InAppUpdate] Flexible update available — starting flow");
                        _manager.StartFlexibleUpdate(appUpdateInfo, _activity);
                    }
                    else
                    {
                        // Try immediate update (AppUpdateType.IMMEDIATE = 1)
                        bool isImmediateAllowed = appUpdateInfo.Call<bool>("isUpdateTypeAllowed", 1);
                        if (isImmediateAllowed)
                        {
                            Debug.Log("[InAppUpdate] Immediate update available — starting flow");
                            var factory = new AndroidJavaClass("com.google.android.play.core.appupdate.AppUpdateManagerFactory");
                            var mgr = factory.CallStatic<AndroidJavaObject>("create", _activity);
                            mgr.Call<AndroidJavaObject>("startUpdateFlowForResult",
                                appUpdateInfo, _activity, 1, 101);
                        }
                    }
                }
                else if (availability == 3) // DEVELOPER_TRIGGERED_UPDATE_IN_PROGRESS
                {
                    // Resume a previously started update
                    Debug.Log("[InAppUpdate] Resuming in-progress update");
                    _manager.StartFlexibleUpdate(appUpdateInfo, _activity);
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
#endif
}
