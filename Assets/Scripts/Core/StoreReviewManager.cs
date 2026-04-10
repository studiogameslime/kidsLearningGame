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

            // Store references so GC doesn't collect them during async callback
            requestTask.Call<AndroidJavaObject>("addOnCompleteListener",
                new ReviewTaskListener(reviewManager, activity));

            Debug.Log("[StoreReview] Review flow requested");
        }
        catch (System.Exception e)
        {
            Debug.Log($"[StoreReview] Failed, will retry: {e.Message}");
            profile.hasShownStoreReview = false;
            ProfileManager.Instance?.Save();
        }
#else
        Debug.Log("[StoreReview] Review requested (editor — skipped)");
#endif
        return true;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private class ReviewTaskListener : AndroidJavaProxy
    {
        private AndroidJavaObject _manager;
        private AndroidJavaObject _activity;

        public ReviewTaskListener(AndroidJavaObject manager, AndroidJavaObject activity)
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
                    _manager.Call<AndroidJavaObject>("launchReviewFlow", _activity, reviewInfo);
                    Debug.Log("[StoreReview] Review flow launched");
                }
                else
                {
                    Debug.Log("[StoreReview] Review task failed, will retry");
                    ResetFlag();
                }
            }
            catch (System.Exception e)
            {
                Debug.Log($"[StoreReview] Callback error: {e.Message}");
                ResetFlag();
            }
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
    }
#endif
}
