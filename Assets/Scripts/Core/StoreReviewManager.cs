using UnityEngine;

/// <summary>
/// Manages in-app review prompts using Google Play In-App Review API.
/// Shows once per profile, after 3+ parent dashboard visits.
/// </summary>
public static class StoreReviewManager
{
    public static bool TryRequestReview()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return false;
        if (profile.hasShownStoreReview) return false;

        profile.hasShownStoreReview = true;
        ProfileManager.Instance?.Save();

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var managerClass = new AndroidJavaClass("com.google.android.play.core.review.ReviewManagerFactory");
            var reviewManager = managerClass.CallStatic<AndroidJavaObject>("create", activity);
            var requestTask = reviewManager.Call<AndroidJavaObject>("requestReviewFlow");

            requestTask.Call<AndroidJavaObject>("addOnCompleteListener",
                new ReviewRequestListener(reviewManager, activity));

            Debug.Log("[StoreReview] Review flow requested");
        }
        catch (System.Exception e)
        {
            Debug.Log($"[StoreReview] Failed, will retry: {e.Message}");
            ResetFlag();
        }
#else
        Debug.Log("[StoreReview] Review requested (editor — skipped)");
#endif
        return true;
    }

    private static void ResetFlag()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
        {
            profile.hasShownStoreReview = false;
            ProfileManager.Instance?.Save();
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>Handles the requestReviewFlow Task completion.</summary>
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
                    var reviewInfo = task.Call<AndroidJavaObject>("getResult");
                    // launchReviewFlow returns Task<Void> — listen for completion
                    var launchTask = _manager.Call<AndroidJavaObject>("launchReviewFlow", _activity, reviewInfo);
                    launchTask.Call<AndroidJavaObject>("addOnCompleteListener",
                        new ReviewLaunchListener());
                    Debug.Log("[StoreReview] Review flow launched");
                }
                else
                {
                    Debug.Log("[StoreReview] Request failed, will retry");
                    ResetFlag();
                }
            }
            catch (System.Exception e)
            {
                Debug.Log($"[StoreReview] Callback error: {e.Message}");
                ResetFlag();
            }
        }
    }

    /// <summary>Handles the launchReviewFlow Task completion (user finished/dismissed review).</summary>
    private class ReviewLaunchListener : AndroidJavaProxy
    {
        public ReviewLaunchListener()
            : base("com.google.android.gms.tasks.OnCompleteListener") { }

        public void onComplete(AndroidJavaObject task)
        {
            // Review flow finished (user may or may not have reviewed)
            // Google doesn't tell us whether they actually reviewed — by design
            Debug.Log($"[StoreReview] Review flow completed (success={task.Call<bool>("isSuccessful")})");
        }
    }
#endif
}
