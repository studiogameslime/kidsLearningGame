using UnityEngine;

/// <summary>
/// Manages in-app review prompts using Google Play In-App Review API.
/// Device-level guard: shows at most once across all profiles.
/// Triggered after 3+ parent dashboard visits.
/// </summary>
public static class StoreReviewManager
{
    private const string DeviceReviewShownKey = "store_review_shown";

    public static bool TryRequestReview()
    {
        // Device-level guard — Google recommends infrequent prompts
        if (PlayerPrefs.GetInt(DeviceReviewShownKey, 0) == 1)
            return false;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // activity and reviewManager are kept alive by the listener (async callback).
            // Only dispose the short-lived lookup classes.
            AndroidJavaObject activity;
            AndroidJavaObject reviewManager;
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }
            using (var managerClass = new AndroidJavaClass("com.google.android.play.core.review.ReviewManagerFactory"))
            {
                reviewManager = managerClass.CallStatic<AndroidJavaObject>("create", activity);
            }

            var requestTask = reviewManager.Call<AndroidJavaObject>("requestReviewFlow");
            // Listener owns activity + reviewManager and will Dispose them after the flow
            requestTask.Call<AndroidJavaObject>("addOnCompleteListener",
                new ReviewRequestListener(reviewManager, activity));

            Debug.Log("[StoreReview] Review flow requested");
        }
        catch (System.Exception e)
        {
            Debug.Log($"[StoreReview] Failed: {e.Message}");
            return false;
        }
#else
        Debug.Log("[StoreReview] Review requested (editor — skipped)");
#endif

        // Mark as shown only after successful request (no exception)
        PlayerPrefs.SetInt(DeviceReviewShownKey, 1);
        PlayerPrefs.Save();
        return true;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>Handles the requestReviewFlow Task completion.
    /// Owns the reviewManager + activity JNI references and disposes them when done.</summary>
    private class ReviewRequestListener : AndroidJavaProxy
    {
        private AndroidJavaObject _manager;
        private AndroidJavaObject _activity;

        public ReviewRequestListener(AndroidJavaObject manager, AndroidJavaObject activity)
            : base("com.google.android.gms.tasks.OnCompleteListener")
        {
            _manager = manager;
            _activity = activity;
        }

        public void onComplete(AndroidJavaObject task)
        {
            try
            {
                if (task.Call<bool>("isSuccessful"))
                {
                    using (var reviewInfo = task.Call<AndroidJavaObject>("getResult"))
                    {
                        var launchTask = _manager.Call<AndroidJavaObject>(
                            "launchReviewFlow", _activity, reviewInfo);
                        // Pass ownership to launch listener for final cleanup
                        launchTask.Call<AndroidJavaObject>("addOnCompleteListener",
                            new ReviewLaunchListener(_manager, _activity));
                        _manager = null;
                        _activity = null;
                    }
                    Debug.Log("[StoreReview] Review flow launched");
                }
                else
                {
                    Debug.Log("[StoreReview] Request task failed");
                    Cleanup();
                }
            }
            catch (System.Exception e)
            {
                Debug.Log($"[StoreReview] Callback error: {e.Message}");
                Cleanup();
            }
        }

        private void Cleanup()
        {
            _manager?.Dispose(); _manager = null;
            _activity?.Dispose(); _activity = null;
        }
    }

    /// <summary>Handles the launchReviewFlow Task completion and disposes JNI references.</summary>
    private class ReviewLaunchListener : AndroidJavaProxy
    {
        private AndroidJavaObject _manager;
        private AndroidJavaObject _activity;

        public ReviewLaunchListener(AndroidJavaObject manager, AndroidJavaObject activity)
            : base("com.google.android.gms.tasks.OnCompleteListener")
        {
            _manager = manager;
            _activity = activity;
        }

        public void onComplete(AndroidJavaObject task)
        {
            Debug.Log($"[StoreReview] Review flow completed (success={task.Call<bool>("isSuccessful")})");
            _manager?.Dispose(); _manager = null;
            _activity?.Dispose(); _activity = null;
        }
    }
#endif
}
