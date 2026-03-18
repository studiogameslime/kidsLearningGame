using System;
using UnityEngine;
using GoogleMobileAds.Api;

/// <summary>
/// Manages a banner ad shown ONLY in the Parent Dashboard.
/// Loads on Enable, destroys on Disable — ensures no ads in kids' screens.
///
/// Uses test ad unit IDs by default. Replace with real IDs before release.
/// </summary>
public class BannerAdManager : MonoBehaviour
{
    // Test ad unit IDs from Google — safe for development
#if UNITY_ANDROID
    private const string BannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";
#elif UNITY_IOS
    private const string BannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
#else
    private const string BannerAdUnitId = "unused";
#endif

    public static BannerAdManager Instance { get; private set; }
    private BannerView _bannerView;

    private void Awake()
    {
        Instance = this;
        enabled = false; // Start disabled — call ShowBanner() after parental gate
    }

    private void OnEnable()
    {
        LoadBanner();
    }

    /// <summary>Call after parental gate to show the banner ad.</summary>
    public void ShowBanner()
    {
        enabled = true;
    }

    private void OnDisable()
    {
        DestroyBanner();
    }

    private void OnDestroy()
    {
        DestroyBanner();
    }

    private void LoadBanner()
    {
        // Clean up any existing banner
        DestroyBanner();

        // Adaptive banner width based on screen
        var adSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(
            AdSize.FullWidth);

        _bannerView = new BannerView(BannerAdUnitId, adSize, AdPosition.Bottom);

        // Event listeners
        _bannerView.OnBannerAdLoaded += () =>
            Debug.Log("[AdMob] Banner loaded");
        _bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
            Debug.LogWarning($"[AdMob] Banner failed: {error.GetMessage()}");

        // Load the ad
        var request = new AdRequest();
        _bannerView.LoadAd(request);

        Debug.Log("[AdMob] Banner requested");
    }

    private void DestroyBanner()
    {
        if (_bannerView != null)
        {
            _bannerView.Destroy();
            _bannerView = null;
            Debug.Log("[AdMob] Banner destroyed");
        }
    }
}
