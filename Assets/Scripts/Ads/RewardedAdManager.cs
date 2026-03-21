using System;
using UnityEngine;
using GoogleMobileAds.Api;

/// <summary>
/// Manages a single rewarded ad. Pre-loads on init so it's ready instantly.
/// Call ShowAd(onComplete) — if ad is available it shows, otherwise skips.
///
/// Used in Parent Dashboard after solving the math gate.
/// </summary>
public class RewardedAdManager : MonoBehaviour
{
#if UNITY_ANDROID
    private const string AdUnitId = "ca-app-pub-4452511612073107/3466930965";
#elif UNITY_IOS
    private const string AdUnitId = "ca-app-pub-4452511612073107/3466930965";
#else
    private const string AdUnitId = "unused";
#endif

    public static RewardedAdManager Instance { get; private set; }

    private const float ShowChance = 0.3f; // 30% chance to load/show

    private RewardedAd _rewardedAd;
    private Action _onAdClosed;
    private bool _shouldShow;
    private bool _pendingCallback;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        RollAndLoad();
    }

    private void Update()
    {
        if (_pendingCallback)
        {
            _pendingCallback = false;
            var cb = _onAdClosed;
            _onAdClosed = null;
            cb?.Invoke();
            RollAndLoad();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        DestroyAd();
    }

    /// <summary>
    /// Show a rewarded ad. Calls onComplete when the ad is closed (or immediately if no ad available).
    /// </summary>
    public void ShowAd(Action onComplete)
    {
        if (_shouldShow && _rewardedAd != null && _rewardedAd.CanShowAd())
        {
            _onAdClosed = onComplete;
            _rewardedAd.Show(reward =>
            {
                Debug.Log($"[AdMob] Rewarded: {reward.Amount} {reward.Type}");
            });
        }
        else
        {
            Debug.Log("[AdMob] Rewarded ad skipped (not loaded or lost roll)");
            onComplete?.Invoke();
            RollAndLoad();
        }
    }

    private void RollAndLoad()
    {
        _shouldShow = UnityEngine.Random.value < ShowChance;
        if (_shouldShow)
        {
            Debug.Log("[AdMob] Rewarded: won roll (30%), loading ad");
            LoadAd();
        }
        else
        {
            Debug.Log("[AdMob] Rewarded: lost roll (70%), no ad this time");
        }
    }

    private void LoadAd()
    {
        DestroyAd();

        var request = new AdRequest();
        RewardedAd.Load(AdUnitId, request, (RewardedAd ad, LoadAdError error) =>
        {
            if (error != null)
            {
                Debug.LogWarning($"[AdMob] Rewarded load failed: {error.GetMessage()}");
                return;
            }

            _rewardedAd = ad;
            Debug.Log("[AdMob] Rewarded ad loaded");

            // When ad is closed (watched or dismissed), flag for main-thread callback.
            // AdMob callbacks may fire on a background thread on Android,
            // so we defer to Update() to safely touch Unity objects.
            _rewardedAd.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("[AdMob] Rewarded ad closed");
                _pendingCallback = true;
            };

            _rewardedAd.OnAdFullScreenContentFailed += (AdError adError) =>
            {
                Debug.LogWarning($"[AdMob] Rewarded show failed: {adError.GetMessage()}");
                _pendingCallback = true;
            };
        });
    }

    private void DestroyAd()
    {
        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }
    }
}
