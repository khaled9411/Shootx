using System;
using UnityEngine;
using GoogleMobileAds.Api;

public class GameAdManager : MonoBehaviour
{
    public static GameAdManager Instance { get; private set; }

    [Header("Ad Unit IDs")]
    [SerializeField] private string bannerId = "ca-app-pub-8291738977037870/1235947238";
    [SerializeField] private string interstitialId = "ca-app-pub-8291738977037870/5833667794";
    [SerializeField] private string rewardedId = "ca-app-pub-8291738977037870/9932310465";

    [Header("Interstitial Settings")]
    [SerializeField] private float autoInterstitialInterval = 120f;
    [SerializeField] private bool startAutoInterstitialOnStart = true;

    private float _interstitialTimer = 0f;
    private bool _autoInterstitialRunning = false;
    private bool _bannerVisible = false;
    private Action<bool> _pendingRewardedCallback;

    private BannerView _bannerView;
    private InterstitialAd _interstitialAd;
    private RewardedAd _rewardedAd;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        MobileAds.Initialize(initStatus =>
        {
            LoadBannerAd();
            LoadInterstitialAd();
            LoadRewardedAd();
        });

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

    private string GetBannerId()
    {
        return bannerId;
    }

    private string GetInterstitialId()
    {
        return interstitialId;
    }

    private string GetRewardedId()
    {
        return rewardedId;
    }

    #region Rewarded Ad

    private void LoadRewardedAd()
    {
        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }

        var adRequest = new AdRequest();
        RewardedAd.Load(GetRewardedId(), adRequest, (RewardedAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null) return;
            _rewardedAd = ad;
        });
    }

    public void ShowRewardedAd(Action<bool> onResult)
    {
        if (_rewardedAd != null && _rewardedAd.CanShowAd())
        {
            _pendingRewardedCallback = onResult;
            _rewardedAd.Show((Reward reward) =>
            {
                _pendingRewardedCallback?.Invoke(true);
                _pendingRewardedCallback = null;
            });
            LoadRewardedAd();
        }
        else
        {
            Debug.LogWarning("Rewarded Ad not ready");
            onResult?.Invoke(false);
            LoadRewardedAd();
        }
    }

    #endregion

    #region Banner Ad

    private void LoadBannerAd()
    {
        if (_bannerView != null)
        {
            _bannerView.Destroy();
        }

        _bannerView = new BannerView(GetBannerId(), AdSize.Banner, AdPosition.Bottom);
        var adRequest = new AdRequest();
        _bannerView.LoadAd(adRequest);
        _bannerView.Hide();
        _bannerVisible = false;
    }

    public void ShowBanner()
    {
        if (_bannerView == null) LoadBannerAd();
        _bannerView.Show();
        _bannerVisible = true;
    }

    public void HideBanner()
    {
        if (_bannerView != null)
        {
            _bannerView.Hide();
        }
        _bannerVisible = false;
    }

    public void ToggleBanner()
    {
        if (_bannerVisible) HideBanner();
        else ShowBanner();
    }

    public bool IsBannerVisible() => _bannerVisible;

    #endregion

    #region Interstitial Ad

    private void LoadInterstitialAd()
    {
        if (_interstitialAd != null)
        {
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }

        var adRequest = new AdRequest();
        InterstitialAd.Load(GetInterstitialId(), adRequest, (InterstitialAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null) return;
            _interstitialAd = ad;
        });
    }

    public void ShowInterstitialNow()
    {
        if (_interstitialAd != null && _interstitialAd.CanShowAd())
        {
            _interstitialAd.Show();
            LoadInterstitialAd();
        }
        else
        {
            Debug.LogWarning("Interstitial Not ready");
            LoadInterstitialAd();
        }
    }

    public void StartAutoInterstitial()
    {
        _autoInterstitialRunning = true;
        _interstitialTimer = 0f;
    }

    public void StopAutoInterstitial()
    {
        _autoInterstitialRunning = false;
    }

    public void SetAutoInterstitialInterval(float seconds)
    {
        autoInterstitialInterval = Mathf.Max(10f, seconds);
        _interstitialTimer = 0f;
    }

    #endregion
}