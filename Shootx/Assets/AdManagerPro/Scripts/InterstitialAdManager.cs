#if ADS
using UnityEngine;
using GoogleMobileAds.Api;
using System.Collections;

namespace AdManagerPro
{
    public static class InterstitialAdManager
    {
        private static InterstitialAd interstitial;
        public static bool isAdmobInterstitialReady = false;

        private static MobileAdsSettings settings;
        private static string interstitialID;
        static InterstitialAdManager()
        {
            settings = Resources.Load<MobileAdsSettings>("MobileAdsSettings");

            if (settings == null)
            {
                Debug.LogError("MobileAdsSettings not found in Resources folder.");
                return;
            }

            MobileAds.Initialize(initStatus => { });
        }

        public static void RequestAdInterstitial()
        {
            if (settings == null)
            {
                Debug.LogError("MobileAdsSettings is null.");
                return;
            }

            if (interstitial != null)
            {
                interstitial.Destroy();
                interstitial = null;
            }
            CheckID();
            AdRequest request = new AdRequest();

            InterstitialAd.Load(interstitialID, request, (InterstitialAd ad, LoadAdError error) =>
            {
                if (error != null)
                {
                    Debug.LogError("Failed to load interstitial ad: " + error.GetMessage());
                    return;
                }
                else if (ad == null)
                {
                    Debug.LogError("Failed to load interstitial ad: ad is null");
                    return;
                }

                interstitial = ad;
                isAdmobInterstitialReady = true;
                RegisterInterstitialEventHandlers(ad);
            });
        }

        private static void RegisterInterstitialEventHandlers(InterstitialAd interstitialAd)
        {
            interstitialAd.OnAdFullScreenContentClosed += () =>
            {
                isAdmobInterstitialReady = false;
                RequestAdInterstitial();
            };

            interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                isAdmobInterstitialReady = false;
                RequestAdInterstitial();
            };
        }

        public static void ShowAdInterstitial()
        {
            if (interstitial != null && interstitial.CanShowAd())
            {
                interstitial.Show();
            }
            else
            {
                RequestAdInterstitial();
            }
        }

        private static void CheckID(){
            if(settings.testMode) interstitialID = "ca-app-pub-3940256099942544/1033173712";
            else interstitialID = settings.interstitialID;
        }
    }
}
#endif