using System;
using UnityEngine;
using GoogleMobileAds.Api;

/// <summary>
/// Manages an interstitial ad shown every time the parent dashboard is entered.
/// Pre-loads on init so it's ready instantly.
/// Call ShowAd(onComplete) — if ad is available it shows, otherwise skips.
/// </summary>
public class InterstitialAdManager : MonoBehaviour
{
#if UNITY_EDITOR
    private const string AdUnitId = "ca-app-pub-3940256099942544/1033173712"; // Google test interstitial
#elif UNITY_ANDROID
    private const string AdUnitId = "ca-app-pub-4452511612073107/1733662374";
#elif UNITY_IOS
    private const string AdUnitId = "ca-app-pub-4452511612073107/1733662374";
#else
    private const string AdUnitId = "unused";
#endif

    public static InterstitialAdManager Instance { get; private set; }

    private InterstitialAd _interstitialAd;
    private Action _onAdClosed;
    private bool _pendingCallback;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        LoadAd();
    }

    private void Update()
    {
        if (_pendingCallback)
        {
            _pendingCallback = false;
            var cb = _onAdClosed;
            _onAdClosed = null;
            cb?.Invoke();
            LoadAd(); // pre-load next ad
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        DestroyAd();
    }

    /// <summary>
    /// Show an interstitial ad. Calls onComplete when the ad is closed
    /// (or immediately if no ad is available).
    /// </summary>
    public void ShowAd(Action onComplete)
    {
        if (_interstitialAd != null && _interstitialAd.CanShowAd())
        {
            _onAdClosed = onComplete;
            _interstitialAd.Show();
        }
        else
        {
            Debug.Log("[AdMob] Interstitial not ready, skipping");
            onComplete?.Invoke();
            LoadAd();
        }
    }

    private void LoadAd()
    {
        if (!ConsentManager.CanShowAds)
        {
            Debug.Log("[AdMob] Interstitial skipped — consent not granted");
            return;
        }

        DestroyAd();

        var request = new AdRequest();
        InterstitialAd.Load(AdUnitId, request, (InterstitialAd ad, LoadAdError error) =>
        {
            if (error != null)
            {
                Debug.LogWarning($"[AdMob] Interstitial load failed: {error.GetMessage()}");
                return;
            }

            _interstitialAd = ad;
            Debug.Log("[AdMob] Interstitial ad loaded");

            _interstitialAd.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("[AdMob] Interstitial closed");
                _pendingCallback = true;
            };

            _interstitialAd.OnAdFullScreenContentFailed += (AdError adError) =>
            {
                Debug.LogWarning($"[AdMob] Interstitial show failed: {adError.GetMessage()}");
                _pendingCallback = true;
            };
        });
    }

    private void DestroyAd()
    {
        if (_interstitialAd != null)
        {
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }
    }
}
