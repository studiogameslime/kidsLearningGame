using UnityEngine;

/// <summary>
/// Manages in-app review prompts. Tracks whether the review was already shown
/// so it only appears once per profile. Falls back to opening the Play Store page.
/// </summary>
public static class StoreReviewManager
{
    private const string PlayStoreUrl = "https://play.google.com/store/apps/details?id=";

    /// <summary>
    /// Request a review if not already shown for this profile.
    /// Returns true if the review was triggered, false if already shown.
    /// </summary>
    public static bool TryRequestReview()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return false;

        // Check if already shown
        if (profile.hasShownStoreReview) return false;

        // Mark as shown
        profile.hasShownStoreReview = true;
        ProfileManager.Instance?.Save();

        // Open Play Store for rating
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // Try native Android in-app review first
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var manager = new AndroidJavaClass("com.google.android.play.core.review.ReviewManagerFactory"))
            {
                var reviewManager = manager.CallStatic<AndroidJavaObject>("create", activity);
                var request = reviewManager.Call<AndroidJavaObject>("requestReviewFlow");

                // Use a task listener to launch the flow when ready
                request.Call<AndroidJavaObject>("addOnCompleteListener",
                    new ReviewCompleteListener(reviewManager, activity));
            }
        }
        catch (System.Exception e)
        {
            // Don't open Play Store externally — it disrupts the user experience.
            // Reset flag so we can retry next time.
            Debug.Log($"[StoreReview] Native review failed, will retry later: {e.Message}");
            profile.hasShownStoreReview = false;
            ProfileManager.Instance?.Save();
        }
#else
        Debug.Log("[StoreReview] Review requested (editor/non-Android — skipped)");
#endif
        return true;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private class ReviewCompleteListener : AndroidJavaProxy
    {
        private readonly AndroidJavaObject _manager;
        private readonly AndroidJavaObject _activity;

        public ReviewCompleteListener(AndroidJavaObject manager, AndroidJavaObject activity)
            : base("com.google.android.gms.tasks.OnCompleteListener")
        {
            _manager = manager;
            _activity = activity;
        }

        public void onComplete(AndroidJavaObject task)
        {
            if (task.Call<bool>("isSuccessful"))
            {
                var reviewInfo = task.Call<AndroidJavaObject>("getResult");
                _manager.Call<AndroidJavaObject>("launchReviewFlow", _activity, reviewInfo);
            }
            else
            {
                // Don't redirect externally — just skip and allow retry
                Debug.Log("[StoreReview] Review task failed, will retry later");
            }
        }
    }
#endif
}
