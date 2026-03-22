using UnityEngine;
using GoogleMobileAds.Api;

/// <summary>
/// Initializes the Google Mobile Ads SDK once at app launch.
/// Sets child-directed treatment flag (COPPA compliance).
/// </summary>
public static class AdMobInitializer
{
    public static bool IsInitialized { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        // COPPA: mark all ad requests as child-directed
        var requestConfig = new RequestConfiguration
        {
            TagForChildDirectedTreatment = TagForChildDirectedTreatment.True,
            MaxAdContentRating = MaxAdContentRating.G
        };
        MobileAds.SetRequestConfiguration(requestConfig);

        MobileAds.Initialize(status =>
        {
            IsInitialized = true;
            Debug.Log("[AdMob] SDK initialized (child-directed, max rating G)");
        });
    }
}
