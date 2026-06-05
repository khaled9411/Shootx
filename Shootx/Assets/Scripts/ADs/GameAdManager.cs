using AdManagerPro;
using System;
using System.Collections;
using UnityEngine;

public class GameAdManager : MonoBehaviour
{
    public static GameAdManager Instance { get; private set; }
    #region Inspector Settings

    [Header("Interstitial")]
    [SerializeField] private float autoInterstitialInterval = 120f;
    [SerializeField] private bool startAutoInterstitialOnStart = true;

    #endregion
    #region Private State

    private float _interstitialTimer = 0f;
    private bool _autoInterstitialRunning = false;
    private bool _bannerVisible = false;
    private Action<bool> _pendingRewardedCallback;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        RewardedAdManager.RequestRewardedAd();
        InterstitialAdManager.RequestAdInterstitial();

        if (startAutoInterstitialOnStart)
            StartAutoInterstitial();
    }

    private void Update()
    {
        if (!_autoInterstitialRunning) return;

        _interstitialTimer += Time.unscaledDeltaTime;

        if (_interstitialTimer >= autoInterstitialInterval && !UIManager.Instance.isgameactive())
        {
            _interstitialTimer = 0f;
            ShowInterstitialNow();
        }
    }

    #endregion

    #region Rewarded Ad

    public void ShowRewardedAd(Action<bool> onResult)
    {
        if (!RewardedAdManager.isAdmobRewardedReady)
        {
            Debug.LogWarning("[GameAdManager] Rewarded Ad not ready, new upload request...");
            RewardedAdManager.RequestRewardedAd();
            onResult?.Invoke(false);
            return;
        }

        _pendingRewardedCallback = onResult;
        RewardedAdManager.ShowRewardedAd(OnRewardedAdWatched);
    }

    private void OnRewardedAdWatched(bool adWatched)
    {
        _pendingRewardedCallback?.Invoke(adWatched);
        _pendingRewardedCallback = null;
        RewardedAdManager.RequestRewardedAd();
    }

    #endregion

    #region Banner Ad

    public void ShowBanner()
    {
        if (_bannerVisible) return;
        _bannerVisible = true;
        BannerAdManager.ShowAdBanner();
        Debug.Log("[GameAdManager] Banner to Show");
    }
    public void HideBanner()
    {
        if (!_bannerVisible) return;
        _bannerVisible = false;
        BannerAdManager.HideAdBanner();
        Debug.Log("[GameAdManager] Banner to Hide");
    }

    public void ToggleBanner()
    {
        if (_bannerVisible) HideBanner();
        else ShowBanner();
    }

    public bool IsBannerVisible() => _bannerVisible;

    #endregion

    #region Interstitial Ad

    public void ShowInterstitialNow()
    {
        if (!InterstitialAdManager.isAdmobInterstitialReady)
        {
            Debug.LogWarning("[GameAdManager] Interstitial Not ready.");
            InterstitialAdManager.RequestAdInterstitial();
            return;
        }

        InterstitialAdManager.ShowAdInterstitial();

        InterstitialAdManager.RequestAdInterstitial();

        Debug.Log("[GameAdManager] Interstitial to Shown");
    }

    public void StartAutoInterstitial()
    {
        _autoInterstitialRunning = true;
        _interstitialTimer = 0f;
        Debug.Log($"[GameAdManager] Auto-Interstitial It started — all {autoInterstitialInterval}s");
    }

    public void StopAutoInterstitial()
    {
        _autoInterstitialRunning = false;
        Debug.Log("[GameAdManager] Auto-Interstitial arrested");
    }

    public void SetAutoInterstitialInterval(float seconds)
    {
        autoInterstitialInterval = Mathf.Max(10f, seconds);
        _interstitialTimer = 0f;
    }

    #endregion

    #region App Open

    private void OnApplicationPause(bool pause)
    {
        //if (!pause)
        //    AppOpenAdManager.ShowAppOpenAd();
    }

    #endregion
}