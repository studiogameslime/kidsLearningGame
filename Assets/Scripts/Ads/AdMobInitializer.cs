using UnityEngine;
using GoogleMobileAds.Api;

/// <summary>
/// Initializes the Google Mobile Ads SDK once at app launch.
/// Place on a persistent GameObject or use RuntimeInitializeOnLoadMethod.
/// </summary>
public static class AdMobInitializer
{
    public static bool IsInitialized { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        MobileAds.Initialize(status =>
        {
            IsInitialized = true;
            Debug.Log("[AdMob] SDK initialized");
        });
    }
}
