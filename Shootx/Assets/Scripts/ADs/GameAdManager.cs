using GoogleMobileAds.Api;
using System;
using System.Collections;
using UnityEngine;

public enum RewardedAdState
{
    Idle,
    Loading,
    Ready,
    Showing,
    Closed,
    Failed
}

public class GameAdManager : MonoBehaviour
{
    public static GameAdManager Instance { get; private set; }

    [Header("=== Android Ad Unit IDs ===")]
    [SerializeField] private string bannerId = "ca-app-pub-8291738977037870/1235947238";
    [SerializeField] private string interstitialId = "ca-app-pub-8291738977037870/5833667794";
    [SerializeField] private string rewardedId = "ca-app-pub-8291738977037870/9932310465";

    [Header("=== iOS Ad Unit IDs (Optional Overrides) ===")]
    [SerializeField] private string bannerIdIOS = "";
    [SerializeField] private string interstitialIdIOS = "";
    [SerializeField] private string rewardedIdIOS = "";

    [Header("=== Interstitial Settings ===")]
    [SerializeField] private float autoInterstitialInterval = 120f;
    [SerializeField] private bool startAutoInterstitialOnStart = true;

    // Events for UI lifecycle synchronization
    public static event Action OnRewardedAdOpened;
    public static event Action OnRewardedAdClosed;

    private float _interstitialTimer = 0f;
    private bool _autoInterstitialRunning = false;
    private bool _bannerVisible = false;

    // Ad views & instances
    private BannerView _bannerView;
    private InterstitialAd _interstitialAd;

    // Rewarded Ad State Tracking
    private RewardedAd _rewardedAd;
    private RewardedAdState _rewardedAdState = RewardedAdState.Idle;
    private string _activeOperationId = null;
    private bool _currentRewardEarned = false;
    private Action<bool> _pendingRewardedCallback = null;
    private Coroutine _activeWatchdogCoroutine = null;
    private bool _isAdLoadingInProgress = false;

    public RewardedAdState CurrentRewardedAdState => _rewardedAdState;
    public bool IsRewardedAdReady => _rewardedAd != null && _rewardedAd.CanShowAd();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Debug.Log("[AdMob] Initializing Google Mobile Ads SDK (v11.2.0)...");
        MobileAds.Initialize(initStatus =>
        {
            Debug.Log("[AdMob] Google Mobile Ads SDK Initialized successfully.");
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

        if (_interstitialTimer >= autoInterstitialInterval &&
            UIManager.Instance != null && !UIManager.Instance.isgameactive())
        {
            _interstitialTimer = 0f;
            ShowInterstitialNow();
        }
    }

    public string GetBannerId()
    {
        if (Application.platform == RuntimePlatform.IPhonePlayer && !string.IsNullOrEmpty(bannerIdIOS))
            return bannerIdIOS;
        return bannerId;
    }

    public string GetInterstitialId()
    {
        if (Application.platform == RuntimePlatform.IPhonePlayer && !string.IsNullOrEmpty(interstitialIdIOS))
            return interstitialIdIOS;
        return interstitialId;
    }

    public string GetRewardedId()
    {
        if (Application.platform == RuntimePlatform.IPhonePlayer && !string.IsNullOrEmpty(rewardedIdIOS))
            return rewardedIdIOS;
        return rewardedId;
    }

    // ===================================================================
    #region Rewarded Ad Lifecycle & State Machine
    // ===================================================================

    public void LoadRewardedAd(Action<bool> onLoadedCallback = null)
    {
        if (_rewardedAdState == RewardedAdState.Showing)
        {
            Debug.LogWarning("[AdMob] Cannot load a new Rewarded Ad while an ad is currently showing.");
            onLoadedCallback?.Invoke(false);
            return;
        }

        if (_isAdLoadingInProgress)
        {
            Debug.Log("[AdMob] Rewarded Ad load already in progress.");
            return;
        }

        // Clean up previous unused instance safely
        if (_rewardedAd != null)
        {
            Debug.Log("[AdMob] Disposing previous unused Rewarded Ad instance.");
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }

        _isAdLoadingInProgress = true;
        _rewardedAdState = RewardedAdState.Loading;

        string adUnitId = GetRewardedId();
        Debug.Log($"[AdMob] Loading Rewarded Ad with Unit ID: {adUnitId}...");

        var adRequest = new AdRequest();
        RewardedAd.Load(adUnitId, adRequest, (RewardedAd ad, LoadAdError error) =>
        {
            _isAdLoadingInProgress = false;

            if (error != null || ad == null)
            {
                _rewardedAdState = RewardedAdState.Failed;
                Debug.LogError($"[AdMob] Rewarded Ad failed to load: {error?.GetMessage()} (Code: {error?.GetCode()})");
                onLoadedCallback?.Invoke(false);
                return;
            }

            _rewardedAd = ad;
            _rewardedAdState = RewardedAdState.Ready;
            Debug.Log("[AdMob] Rewarded Ad successfully loaded and ready to present.");

            // Register ALL full-screen lifecycle events
            RegisterRewardedAdEvents(_rewardedAd);

            onLoadedCallback?.Invoke(true);
        });
    }

    private void RegisterRewardedAdEvents(RewardedAd ad)
    {
        if (ad == null) return;

        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log($"[AdMob] Rewarded Ad full-screen content opened (Operation: {_activeOperationId}).");
            _rewardedAdState = RewardedAdState.Showing;
            OnRewardedAdOpened?.Invoke();
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log($"[AdMob] Rewarded Ad full-screen content closed. Reward Earned: {_currentRewardEarned} (Operation: {_activeOperationId}).");
            _rewardedAdState = RewardedAdState.Closed;
            OnRewardedAdClosed?.Invoke();

            string opId = _activeOperationId;
            bool earned = _currentRewardEarned;

            // Safe disposal only AFTER the ad has closed
            if (_rewardedAd != null)
            {
                _rewardedAd.Destroy();
                _rewardedAd = null;
            }

            FinalizeRewardedAdOperation(opId, earned);

            // Automatically preload the next rewarded ad
            LoadRewardedAd();
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError($"[AdMob] Rewarded Ad failed to show: {error?.GetMessage()} (Code: {error?.GetCode()}) (Operation: {_activeOperationId}).");
            _rewardedAdState = RewardedAdState.Failed;

            string opId = _activeOperationId;

            // Safe disposal on presentation failure
            if (_rewardedAd != null)
            {
                _rewardedAd.Destroy();
                _rewardedAd = null;
            }

            FinalizeRewardedAdOperation(opId, false);

            // Retry preloading
            LoadRewardedAd();
        };

        ad.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log($"[AdMob] Rewarded Ad paid event: {adValue.Value} {adValue.CurrencyCode} precision: {adValue.Precision}");
        };

        ad.OnAdClicked += () =>
        {
            Debug.Log("[AdMob] Rewarded Ad clicked by user.");
        };

        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("[AdMob] Rewarded Ad impression recorded.");
        };
    }

    /// <summary>
    /// Displays a rewarded ad with strict session tracking and guaranteed callback finalization.
    /// </summary>
    public void ShowRewardedAd(Action<bool> onResult)
    {
        if (_activeOperationId != null)
        {
            Debug.LogWarning("[AdMob] An ad operation is already currently active. Ignoring duplicate request.");
            return;
        }

        string operationId = Guid.NewGuid().ToString("N");
        _activeOperationId = operationId;
        _currentRewardEarned = false;
        _pendingRewardedCallback = onResult;

        Debug.Log($"[AdMob] Starting Rewarded Ad show operation: {operationId}");

        if (IsRewardedAdReady)
        {
            PresentLoadedAd(operationId);
        }
        else
        {
            Debug.Log("[AdMob] Rewarded Ad is not ready yet. Attempting on-demand load with timeout...");
            if (_activeWatchdogCoroutine != null) StopCoroutine(_activeWatchdogCoroutine);
            _activeWatchdogCoroutine = StartCoroutine(LoadAndShowWatchdog(operationId, 10f));
        }
    }

    private void PresentLoadedAd(string operationId)
    {
        if (_rewardedAd == null || !_rewardedAd.CanShowAd())
        {
            Debug.LogError($"[AdMob] Cannot present ad for operation {operationId}: ad is null or cannot show.");
            FinalizeRewardedAdOperation(operationId, false);
            LoadRewardedAd();
            return;
        }

        _rewardedAdState = RewardedAdState.Showing;

        // Start fallback safety watchdog (e.g. 120s max for full ad presentation in case of system freeze)
        if (_activeWatchdogCoroutine != null) StopCoroutine(_activeWatchdogCoroutine);
        _activeWatchdogCoroutine = StartCoroutine(ShowSafetyWatchdog(operationId, 120f));

        Debug.Log($"[AdMob] Presenting Rewarded Ad to user for operation: {operationId}...");
        _rewardedAd.Show((Reward reward) =>
        {
            Debug.Log($"[AdMob] User earned reward callback invoked: {reward.Type} - {reward.Amount} (Operation: {operationId})");
            if (_activeOperationId == operationId)
            {
                _currentRewardEarned = true;
            }
        });
        // Note: We deliberately DO NOT destroy or reload the ad here!
        // Destruction occurs strictly in OnAdFullScreenContentClosed or OnAdFullScreenContentFailed.
    }

    private IEnumerator LoadAndShowWatchdog(string operationId, float timeoutSeconds)
    {
        bool loadComplete = false;
        bool loadSucceeded = false;

        LoadRewardedAd(success =>
        {
            loadComplete = true;
            loadSucceeded = success;
        });

        float timer = 0f;
        while (!loadComplete && timer < timeoutSeconds)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (_activeOperationId != operationId)
        {
            yield break;
        }

        if (loadComplete && loadSucceeded && IsRewardedAdReady)
        {
            PresentLoadedAd(operationId);
        }
        else
        {
            Debug.LogError($"[AdMob] Rewarded Ad on-demand load failed or timed out after {timeoutSeconds}s (Operation: {operationId}).");
            FinalizeRewardedAdOperation(operationId, false);
        }
    }

    private IEnumerator ShowSafetyWatchdog(string operationId, float maxShowDuration)
    {
        yield return new WaitForSecondsRealtime(maxShowDuration);

        if (_activeOperationId == operationId)
        {
            Debug.LogWarning($"[AdMob] Rewarded Ad show operation {operationId} exceeded safety timeout ({maxShowDuration}s). Forcing terminal cleanup.");
            if (_rewardedAd != null)
            {
                _rewardedAd.Destroy();
                _rewardedAd = null;
            }
            FinalizeRewardedAdOperation(operationId, _currentRewardEarned);
            LoadRewardedAd();
        }
    }

    /// <summary>
    /// Centralized, idempotent method to terminate a rewarded ad operation and guarantee UI release.
    /// </summary>
    private void FinalizeRewardedAdOperation(string operationId, bool rewardGranted)
    {
        if (_activeOperationId == null || _activeOperationId != operationId)
        {
            Debug.Log($"[AdMob] Finalize ignored: operation {operationId} is not active (current active: {_activeOperationId}).");
            return;
        }

        Debug.Log($"[AdMob] Finalizing Rewarded Ad operation: {operationId}. Result granted: {rewardGranted}");

        if (_activeWatchdogCoroutine != null)
        {
            StopCoroutine(_activeWatchdogCoroutine);
            _activeWatchdogCoroutine = null;
        }

        _activeOperationId = null;
        _rewardedAdState = RewardedAdState.Idle;

        var callback = _pendingRewardedCallback;
        _pendingRewardedCallback = null;

        callback?.Invoke(rewardGranted);
    }

    #endregion

    // ===================================================================
    #region Banner Ad
    // ===================================================================

    private void LoadBannerAd()
    {
        if (_bannerView != null)
        {
            _bannerView.Destroy();
            _bannerView = null;
        }

        string adUnitId = GetBannerId();
        Debug.Log($"[AdMob] Loading Banner Ad with Unit ID: {adUnitId}...");

        _bannerView = new BannerView(adUnitId, AdSize.Banner, AdPosition.Bottom);
        var adRequest = new AdRequest();
        _bannerView.LoadAd(adRequest);
        _bannerView.Hide();
        _bannerVisible = false;
    }

    public void ShowBanner()
    {
        if (GameDataManager.Instance != null && GameDataManager.Instance.AdsRemoved)
        {
            return;
        }

        if (_bannerView == null) LoadBannerAd();
        _bannerView?.Show();
        _bannerVisible = true;
    }

    public void HideBanner()
    {
        _bannerView?.Hide();
        _bannerVisible = false;
    }

    public void ToggleBanner()
    {
        if (_bannerVisible) HideBanner();
        else ShowBanner();
    }

    public bool IsBannerVisible() => _bannerVisible;

    #endregion

    // ===================================================================
    #region Interstitial Ad
    // ===================================================================

    private void LoadInterstitialAd()
    {
        if (_interstitialAd != null)
        {
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }

        string adUnitId = GetInterstitialId();
        Debug.Log($"[AdMob] Loading Interstitial Ad with Unit ID: {adUnitId}...");

        var adRequest = new AdRequest();
        InterstitialAd.Load(adUnitId, adRequest, (InterstitialAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogError($"[AdMob] Failed to load interstitial ad: {error?.GetMessage()}");
                return;
            }

            _interstitialAd = ad;
            Debug.Log("[AdMob] Interstitial Ad loaded and ready.");
        });
    }

    public void ShowInterstitialNow()
    {
        if (GameDataManager.Instance != null && GameDataManager.Instance.AdsRemoved)
        {
            return;
        }

        if (_interstitialAd != null && _interstitialAd.CanShowAd())
        {
            _interstitialAd.Show();
            LoadInterstitialAd();
        }
        else
        {
            Debug.LogWarning("[AdMob] Interstitial ad not ready. Triggering reload.");
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
