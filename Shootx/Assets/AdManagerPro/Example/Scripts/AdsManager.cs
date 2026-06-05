#if ADS
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace AdManagerPro  
{
    [HelpURL("https://codeum-games.gitbook.io/admanager-pro-easy-setup")]
    public class AdsManager : MonoBehaviour
    {
    
    [Header("Button Rewarded ADS")]
    [Space(3)]
    [SerializeField] Button adsButtonRewarded;
    [Space(10)]
    [Header("Button Interstitial ADS")]
    [Space(3)]
    [SerializeField] Button adsButtomInterstitial;
    [Space(10)]
    [Header("Button Rewarded Interstitial ADS")]
    [Space(3)]
    [SerializeField] Button adsButtomRewardedInterstitial;

    [Space(10)]
    [Header("Elements for Native ADS")]
    [Space(3)]
    [SerializeField] Image imageNative;
    [SerializeField] TMP_Text headerNative, bodyNative;

    private void Start()
    {
        // Requests to download advertisements
        
        // BannerAdManager.RequestAdBanner(AdPosition.Top);  // Other position
        BannerAdManager.RequestAdBanner();
        InterstitialAdManager.RequestAdInterstitial();
        RewardedAdManager.RequestRewardedAd();
        RewardedInterstitialAdManager.RequestRewardedInterstitialAd();
        AppOpenAdManager.LoadAppOpenAd();

        // Check the availability/loading status of the ad.
        Debug.Log("Banner Ready: " + BannerAdManager.isAdmobBannerReady);
        Debug.Log("Interstitial Ready: " + InterstitialAdManager.isAdmobInterstitialReady);
        Debug.Log("Rewarded Ready: " + RewardedAdManager.isAdmobRewardedReady);
        Debug.Log("Rewarded Interstitial Ready: " + RewardedInterstitialAdManager.isAdmobRewardedInterstitialReady);
        Debug.Log("App Open Ready: " + AppOpenAdManager.isAdmobAppOpenReady);
        
        // Show banner ad
        BannerAdManager.ShowAdBanner();

#if NATIVE_ADS
        // Customize the NativeAdManager with specific UI elements to display native ads.
        NativeAdManager.adImage = imageNative;
        NativeAdManager.adBodyText = bodyNative;
        NativeAdManager.adHeadlineText = headerNative;

        // Request to download native ad
        NativeAdManager.RequestNativeAd();
        Debug.Log("Native Ready: " + NativeAdManager.isNativeAdReady);
#endif
        /*
            Register callback methods that will be executed when certain buttons are pressed and show the corresponding advertisement.
        */
        adsButtonRewarded.onClick.AddListener(() =>
        {
            RewardedAdManager.ShowRewardedAd(OnAdWatched);
        });
        adsButtomInterstitial.onClick.AddListener(() =>
        {
            InterstitialAdManager.ShowAdInterstitial();
        });
        adsButtomRewardedInterstitial.onClick.AddListener(() =>
        {
            RewardedInterstitialAdManager.ShowRewardedInterstitialAd(OnAdWatched);
        });
    }

    /*
        The `OnApplicationPause` method performs actions depending on the application's pause state. 
        It takes a single parameter `pause`, which is a boolean value (`true` or `false`).
        If `pause` is `false` (i.e., when the application resumes from a paused state), 
        the method calls `AppOpenAdManager.ShowAppOpenAd()` to display an app open ad.
    */
    private void OnApplicationPause(bool pause)
    {
        if (pause == false)
        {
            AppOpenAdManager.ShowAppOpenAd();
        }
    }

    /*
        The `OnAdWatched` method performs actions depending on whether the ad was watched or not. 
        It takes one parameter, `adWatched`, which is a boolean value (`true` or `false`).
    */
    private void OnAdWatched(bool adWatched)
    {
        if (adWatched)
        {
            Debug.Log("Ad watched, reward added!");
        }
        else
        {
            Debug.Log("Ad was not watched.");
        }
    }
    }
}
#endif